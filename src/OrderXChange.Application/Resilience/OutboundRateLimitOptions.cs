using System.Collections.Generic;

namespace OrderXChange.Application.Resilience;

/// <summary>
/// Configuration for the centralized outbound rate-limit / 429 handler that
/// sits in front of all Foodics and Talabat HTTP clients.
///
/// Bound from the "OutboundRateLimit" section of appsettings.
/// </summary>
public class OutboundRateLimitOptions
{
    public const string SectionName = "OutboundRateLimit";

    /// <summary>Master switch. When false the handler passes every request through untouched.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Maximum send attempts per request, including the first (SLA target: 5).</summary>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>Base backoff delay (ms) for transient errors when no Retry-After is supplied.</summary>
    public int BaseDelayMs { get; set; } = 1000;

    /// <summary>Upper bound (ms) on any single backoff/Retry-After wait.</summary>
    public int MaxDelayMs { get; set; } = 60000;

    /// <summary>Exponential backoff multiplier for transient (non-429) errors.</summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>Jitter fraction (0..1) added on top of the computed backoff to avoid thundering herd.</summary>
    public double JitterFactor { get; set; } = 0.2;

    /// <summary>
    /// When a provider returns 429 without a Retry-After header, pause that partition
    /// for this many seconds before retrying.
    /// </summary>
    public int DefaultRetryAfterSeconds { get; set; } = 10;

    /// <summary>
    /// Log a warning when the provider's reported X-RateLimit-Remaining drops to or below
    /// this value, so we can see throttling building up before a 429 actually hits.
    /// </summary>
    public int LowRemainingWarningThreshold { get; set; } = 10;

    /// <summary>
    /// When the provider's reported X-RateLimit-Remaining drops to or below this reserve,
    /// proactively pause the token's partition (back-pressure) to avoid hitting a 429 at all.
    /// This is the authoritative, server-reported budget — essential when multiple app
    /// instances share one access token and their client-side counters cannot see each other.
    /// </summary>
    public int LowRemainingReserve { get; set; } = 5;

    /// <summary>How long (ms) to pause a partition when the server-reported budget hits the reserve.</summary>
    public int ReserveBackoffMs { get; set; } = 2000;

    /// <summary>Per-provider throttle definitions, matched by request host.</summary>
    public List<ProviderRateLimit> Providers { get; set; } = new();
}

/// <summary>
/// Throttle definition for a single upstream provider. The proactive limiter keeps
/// outbound traffic below the provider's published ceiling (e.g. Foodics = 90 req/min
/// per access token per IP — we stay under it with headroom).
/// </summary>
public class ProviderRateLimit
{
    /// <summary>Friendly name used in logs and metrics (e.g. "Foodics", "Talabat").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Host fragments that map a request to this provider (case-insensitive "contains").</summary>
    public List<string> Hosts { get; set; } = new();

    /// <summary>Maximum permits granted per window per partition (token). Set below the real limit.</summary>
    public int PermitLimit { get; set; } = 80;

    /// <summary>Length of the sliding window in seconds (Foodics enforces per minute).</summary>
    public int WindowSeconds { get; set; } = 60;

    /// <summary>Sliding-window segments; more segments = smoother throttling.</summary>
    public int SegmentsPerWindow { get; set; } = 6;

    /// <summary>How many requests may wait for a permit before we fail fast and back off.</summary>
    public int QueueLimit { get; set; } = 2000;
}
