using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Resilience;

/// <summary>
/// Singleton store of per-(provider + access token) rate limiters and 429 cool-down state.
///
/// The Foodics limit is "90 requests / minute / access token / IP", and more than 10
/// consecutive 429s in a 10s window gets the IP blocked for 60s. We therefore partition by
/// access token (so one tenant cannot exhaust another's quota), throttle proactively below
/// the ceiling, and on a 429 pause the whole partition until Retry-After elapses.
/// </summary>
public class OutboundRateLimiterStore : ISingletonDependency, IDisposable
{
    private readonly OutboundRateLimitOptions _options;
    private readonly ILogger<OutboundRateLimiterStore> _logger;
    private readonly List<ProviderRateLimit> _providers;
    private readonly ConcurrentDictionary<string, Partition> _partitions = new();

    public OutboundRateLimiterStore(
        IOptions<OutboundRateLimitOptions> options,
        ILogger<OutboundRateLimiterStore> logger)
    {
        _options = options.Value;
        _logger = logger;
        _providers = _options.Providers ?? new List<ProviderRateLimit>();
    }

    /// <summary>
    /// Resolve the throttling partition for a request, or null when the host is not a
    /// configured provider (in which case the handler passes the request through untouched).
    /// </summary>
    public Partition? Resolve(HttpRequestMessage request)
    {
        if (!_options.Enabled || request.RequestUri is null)
        {
            return null;
        }

        var host = request.RequestUri.Host;
        var provider = _providers.FirstOrDefault(p =>
            p.Hosts.Any(h => host.Contains(h, StringComparison.OrdinalIgnoreCase)));

        if (provider is null)
        {
            return null;
        }

        // Partition per access token so each tenant/account gets its own quota.
        var tokenKey = TokenFingerprint(request);
        var key = $"{provider.Name}|{host}|{tokenKey}";

        return _partitions.GetOrAdd(key, k => new Partition(k, provider, _logger));
    }

    public int LowRemainingWarningThreshold => _options.LowRemainingWarningThreshold;

    private static string TokenFingerprint(HttpRequestMessage request)
    {
        var token = request.Headers.Authorization?.Parameter;
        if (string.IsNullOrEmpty(token))
        {
            return "anon";
        }

        // Short, non-reversible fingerprint of the token — never log the token itself.
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes, 0, 6);
    }

    public void Dispose()
    {
        foreach (var partition in _partitions.Values)
        {
            partition.Dispose();
        }
        _partitions.Clear();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// One provider+token partition: a sliding-window limiter plus a 429 cool-down gate.
    /// </summary>
    public sealed class Partition : IDisposable
    {
        private readonly RateLimiter _limiter;
        private readonly ILogger _logger;
        private long _pausedUntilTicks;
        private long _throttled429Count;
        private long _lastRemaining = -1;

        public Partition(string key, ProviderRateLimit provider, ILogger logger)
        {
            Key = key;
            ProviderName = provider.Name;
            _logger = logger;
            _limiter = new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                PermitLimit = Math.Max(1, provider.PermitLimit),
                Window = TimeSpan.FromSeconds(Math.Max(1, provider.WindowSeconds)),
                SegmentsPerWindow = Math.Max(1, provider.SegmentsPerWindow),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = Math.Max(0, provider.QueueLimit),
                AutoReplenishment = true
            });
        }

        public string Key { get; }
        public string ProviderName { get; }
        public long Throttled429Count => Interlocked.Read(ref _throttled429Count);

        /// <summary>Latest server-reported X-RateLimit-Remaining for this token (-1 if unseen).</summary>
        public long LastRemaining => Interlocked.Read(ref _lastRemaining);

        /// <summary>Block until any active 429 cool-down for this partition has elapsed.</summary>
        public async Task WaitForCooldownAsync(CancellationToken cancellationToken)
        {
            var pausedUntil = Interlocked.Read(ref _pausedUntilTicks);
            if (pausedUntil == 0)
            {
                return;
            }

            var wait = new DateTimeOffset(pausedUntil, TimeSpan.Zero) - DateTimeOffset.UtcNow;
            if (wait > TimeSpan.Zero)
            {
                _logger.LogDebug("Rate-limit cool-down active for {Key}; waiting {Ms}ms",
                    Key, wait.TotalMilliseconds);
                await Task.Delay(wait, cancellationToken);
            }
        }

        /// <summary>Acquire a proactive throttle permit, queuing until one is available.</summary>
        public ValueTask<RateLimitLease> AcquireAsync(CancellationToken cancellationToken)
            => _limiter.AcquireAsync(permitCount: 1, cancellationToken);

        /// <summary>
        /// Record a 429 and pause the whole partition until <paramref name="retryAfter"/> elapses,
        /// implementing Foodics' "pause ALL requests until Retry-After ends" guidance.
        /// </summary>
        public void Pause(TimeSpan retryAfter)
        {
            Interlocked.Increment(ref _throttled429Count);
            ExtendPause(retryAfter);
        }

        /// <summary>
        /// Record the server-reported remaining budget and, when it is at/below the reserve,
        /// pause the partition so every caller on this token backs off before a 429 occurs.
        /// Returns true if back-pressure was applied.
        /// </summary>
        public bool ObserveRemaining(int remaining, int reserve, TimeSpan backoff)
        {
            Interlocked.Exchange(ref _lastRemaining, remaining);
            if (remaining > reserve)
            {
                return false;
            }
            ExtendPause(backoff);
            return true;
        }

        private void ExtendPause(TimeSpan duration)
        {
            var until = DateTimeOffset.UtcNow.Add(duration).UtcTicks;
            // Extend, never shorten, an existing cool-down.
            long current;
            do
            {
                current = Interlocked.Read(ref _pausedUntilTicks);
                if (until <= current)
                {
                    return;
                }
            } while (Interlocked.CompareExchange(ref _pausedUntilTicks, until, current) != current);
        }

        public void Dispose() => _limiter.Dispose();
    }
}
