using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrderXChange.Application.Resilience;
using Xunit;

namespace OrderXChange.Resilience;

/// <summary>
/// Tests for the centralized outbound rate-limit / 429 handler.
/// The first three are pure unit tests (stubbed transport). The last is a live integration
/// check against the real Foodics API; it only runs when FOODICS_TEST_TOKEN is set, so no
/// secret is ever committed and CI stays offline by default.
/// </summary>
public class RateLimitingHandlerTests
{
    private static OutboundRateLimitOptions DefaultOptions() => new()
    {
        Enabled = true,
        MaxRetryAttempts = 3,
        BaseDelayMs = 10,
        MaxDelayMs = 100,        // caps any Retry-After wait so the tests stay fast
        BackoffMultiplier = 2,
        JitterFactor = 0,
        DefaultRetryAfterSeconds = 1,
        LowRemainingReserve = 5,
        ReserveBackoffMs = 50,
        Providers =
        {
            new ProviderRateLimit
            {
                Name = "Foodics",
                Hosts = { "foodics.com" },
                PermitLimit = 1000,  // high, so the proactive throttle never blocks these tests
                WindowSeconds = 60,
                SegmentsPerWindow = 6,
                QueueLimit = 1000
            }
        }
    };

    private static (RateLimitingHandler handler, OutboundRateLimiterStore store) Build(
        StubHandler inner, OutboundRateLimitOptions? options = null)
    {
        var opts = Options.Create(options ?? DefaultOptions());
        var store = new OutboundRateLimiterStore(opts, NullLogger<OutboundRateLimiterStore>.Instance);
        var handler = new RateLimitingHandler(store, opts, NullLogger<RateLimitingHandler>.Instance)
        {
            InnerHandler = inner
        };
        return (handler, store);
    }

    private static OutboundRateLimiterStore.Partition Probe(OutboundRateLimiterStore store)
        => store.Resolve(new HttpRequestMessage(HttpMethod.Get, "https://api.foodics.com/v5/whoami"))!;

    [Fact]
    public async Task Retries_on_429_then_succeeds_and_counts_the_throttle()
    {
        var inner = new StubHandler(
            () =>
            {
                var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
                r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
                return r;
            },
            () => new HttpResponseMessage(HttpStatusCode.OK));

        var (handler, store) = Build(inner);
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://api.foodics.com/v5/whoami");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(2, inner.Calls);                       // first 429, retry succeeds
        Assert.Equal(1L, Probe(store).Throttled429Count);   // the 429 was recorded
    }

    [Fact]
    public async Task Gives_up_after_max_attempts_and_returns_the_last_429()
    {
        var inner = new StubHandler(() =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            r.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(1));
            return r;
        });

        var (handler, _) = Build(inner);
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://api.foodics.com/v5/whoami");

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(3, inner.Calls);  // MaxRetryAttempts
    }

    [Fact]
    public async Task Tracks_server_reported_remaining_budget_for_backpressure()
    {
        var inner = new StubHandler(() =>
        {
            var r = new HttpResponseMessage(HttpStatusCode.OK);
            r.Headers.Add("X-RateLimit-Limit", "90");
            r.Headers.Add("X-RateLimit-Remaining", "3"); // below the reserve of 5
            return r;
        });

        var (handler, store) = Build(inner);
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://api.foodics.com/v5/whoami");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3L, Probe(store).LastRemaining); // authoritative budget captured
    }

    [Fact]
    public async Task Unknown_host_passes_through_untouched()
    {
        var inner = new StubHandler(() => new HttpResponseMessage(HttpStatusCode.OK));
        var (handler, store) = Build(inner);
        using var client = new HttpClient(handler);

        var response = await client.GetAsync("https://example.com/anything");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, inner.Calls);
        Assert.Null(store.Resolve(new HttpRequestMessage(HttpMethod.Get, "https://example.com/anything")));
    }

    [Fact]
    public async Task Live_Foodics_whoami_exposes_rate_limit_headers()
    {
        var token = Environment.GetEnvironmentVariable("FOODICS_TEST_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            return; // integration check disabled unless a token is supplied
        }

        var opts = Options.Create(DefaultOptions());
        var store = new OutboundRateLimiterStore(opts, NullLogger<OutboundRateLimiterStore>.Instance);
        var handler = new RateLimitingHandler(store, opts, NullLogger<RateLimitingHandler>.Instance)
        {
            InnerHandler = new HttpClientHandler()
        };
        using var client = new HttpClient(handler);

        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.foodics.com/v5/whoami");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var probe = new HttpRequestMessage(HttpMethod.Get, "https://api.foodics.com/v5/whoami");
        probe.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var remaining = store.Resolve(probe)!.LastRemaining;

        // Foodics returns X-RateLimit-Remaining out of a 90/min budget.
        Assert.InRange(remaining, 0, 90);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpResponseMessage>> _responses;
        private readonly Func<HttpResponseMessage> _default;

        public StubHandler(params Func<HttpResponseMessage>[] responses)
        {
            _responses = new Queue<Func<HttpResponseMessage>>(responses);
            _default = responses.Length > 0 ? responses[^1] : () => new HttpResponseMessage(HttpStatusCode.OK);
        }

        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var factory = _responses.Count > 0 ? _responses.Dequeue() : _default;
            return Task.FromResult(factory());
        }
    }
}
