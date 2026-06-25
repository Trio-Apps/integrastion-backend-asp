using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace OrderXChange.Application.Resilience;

/// <summary>
/// DelegatingHandler attached to every Foodics and Talabat HTTP client. It:
///   1. Throttles proactively per access token to stay under the provider's published limit.
///   2. Honors the Retry-After header on 429 and pauses the whole partition for that period.
///   3. Retries transient failures (5xx / 408 / timeouts) with exponential backoff + jitter.
///   4. Surfaces throttling via structured logs for monitoring/alerting.
/// Unknown hosts are passed straight through.
/// </summary>
public class RateLimitingHandler : DelegatingHandler
{
    private readonly OutboundRateLimiterStore _store;
    private readonly OutboundRateLimitOptions _options;
    private readonly ILogger<RateLimitingHandler> _logger;

    public RateLimitingHandler(
        OutboundRateLimiterStore store,
        IOptions<OutboundRateLimitOptions> options,
        ILogger<RateLimitingHandler> logger)
    {
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var partition = _store.Resolve(request);
        if (partition is null)
        {
            return await base.SendAsync(request, cancellationToken);
        }

        // Buffer the body once so the request can be safely re-sent on each attempt.
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var maxAttempts = Math.Max(1, _options.MaxRetryAttempts);

        for (var attempt = 1; ; attempt++)
        {
            await partition.WaitForCooldownAsync(cancellationToken);

            using var lease = await partition.AcquireAsync(cancellationToken);
            if (!lease.IsAcquired)
            {
                _logger.LogWarning(
                    "Throttle queue full for {Provider} partition (attempt {Attempt}); backing off",
                    partition.ProviderName, attempt);
                if (attempt >= maxAttempts)
                {
                    return TooManyRequests();
                }
                await Task.Delay(Backoff(attempt), cancellationToken);
                continue;
            }

            using var attemptRequest = Clone(request, body);
            HttpResponseMessage response;
            try
            {
                response = await base.SendAsync(attemptRequest, cancellationToken);
            }
            catch (Exception ex) when (IsTransient(ex, cancellationToken))
            {
                if (attempt >= maxAttempts)
                {
                    throw;
                }
                _logger.LogWarning(ex,
                    "Transient error calling {Provider} (attempt {Attempt}/{Max}); retrying",
                    partition.ProviderName, attempt, maxAttempts);
                await Task.Delay(Backoff(attempt), cancellationToken);
                continue;
            }

            ObserveRateLimitHeaders(response, partition);

            if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                var retryAfter = ReadRetryAfter(response)
                                 ?? TimeSpan.FromSeconds(_options.DefaultRetryAfterSeconds);
                retryAfter = Cap(retryAfter);
                partition.Pause(retryAfter);

                _logger.LogWarning(
                    "429 from {Provider} (attempt {Attempt}/{Max}); pausing partition {RetryAfterMs}ms. " +
                    "Total 429s on this token: {Count}",
                    partition.ProviderName, attempt, maxAttempts, retryAfter.TotalMilliseconds,
                    partition.Throttled429Count);

                if (attempt >= maxAttempts)
                {
                    return response;
                }
                response.Dispose();
                await Task.Delay(retryAfter, cancellationToken);
                continue;
            }

            if (IsTransientStatus(response.StatusCode) && attempt < maxAttempts)
            {
                _logger.LogWarning(
                    "Transient HTTP {Status} from {Provider} (attempt {Attempt}/{Max}); retrying",
                    (int)response.StatusCode, partition.ProviderName, attempt, maxAttempts);
                response.Dispose();
                await Task.Delay(Backoff(attempt), cancellationToken);
                continue;
            }

            return response;
        }
    }

    private void ObserveRateLimitHeaders(HttpResponseMessage response, OutboundRateLimiterStore.Partition partition)
    {
        if (!response.Headers.TryGetValues("X-RateLimit-Remaining", out var values)
            || !int.TryParse(values.FirstOrDefault(), out var remaining))
        {
            return;
        }

        // Use the authoritative server-reported budget as cross-instance back-pressure:
        // when it reaches the reserve, pause the token's partition before a 429 can happen.
        var applied = partition.ObserveRemaining(
            remaining,
            _options.LowRemainingReserve,
            TimeSpan.FromMilliseconds(_options.ReserveBackoffMs));

        if (applied)
        {
            _logger.LogWarning(
                "{Provider} server budget hit reserve ({Remaining} ≤ {Reserve}); pausing token {Ms}ms",
                partition.ProviderName, remaining, _options.LowRemainingReserve, _options.ReserveBackoffMs);
        }
        else if (remaining <= _store.LowRemainingWarningThreshold)
        {
            _logger.LogWarning(
                "{Provider} rate-limit budget low: {Remaining} requests remaining on this token",
                partition.ProviderName, remaining);
        }
    }

    private static TimeSpan? ReadRetryAfter(HttpResponseMessage response)
    {
        var retryAfter = response.Headers.RetryAfter;
        if (retryAfter is null)
        {
            return null;
        }
        if (retryAfter.Delta is { } delta)
        {
            return delta;
        }
        if (retryAfter.Date is { } date)
        {
            var wait = date - DateTimeOffset.UtcNow;
            return wait > TimeSpan.Zero ? wait : TimeSpan.Zero;
        }
        return null;
    }

    private TimeSpan Backoff(int attempt)
    {
        var baseMs = _options.BaseDelayMs * Math.Pow(_options.BackoffMultiplier, attempt - 1);
        var jitter = baseMs * _options.JitterFactor * Random.Shared.NextDouble();
        return Cap(TimeSpan.FromMilliseconds(baseMs + jitter));
    }

    private TimeSpan Cap(TimeSpan value)
    {
        var max = TimeSpan.FromMilliseconds(_options.MaxDelayMs);
        return value > max ? max : value;
    }

    private static bool IsTransientStatus(HttpStatusCode status)
        => (int)status >= 500 || status == HttpStatusCode.RequestTimeout;

    private static bool IsTransient(Exception ex, CancellationToken cancellationToken)
    {
        // A genuine caller cancellation must not be retried.
        if (cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        return ex is HttpRequestException
            // HttpClient.Timeout surfaces as TaskCanceledException without caller cancellation.
            || ex is TaskCanceledException
            || ex is OperationCanceledException;
    }

    private static HttpResponseMessage TooManyRequests()
        => new(HttpStatusCode.TooManyRequests)
        {
            ReasonPhrase = "Rate limit queue exhausted"
        };

    private static HttpRequestMessage Clone(HttpRequestMessage request, byte[]? body)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy
        };

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in request.Content!.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        foreach (var header in request.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in (IDictionary<string, object?>)request.Options)
        {
            ((IDictionary<string, object?>)clone.Options)[option.Key] = option.Value;
        }

        return clone;
    }
}
