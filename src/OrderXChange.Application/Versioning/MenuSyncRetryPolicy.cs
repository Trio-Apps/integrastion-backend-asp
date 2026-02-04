using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Extensions.Http;
using Polly.Timeout;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Polly-based retry policy configuration for Menu Sync operations
/// Implements exponential backoff, circuit breaker, and timeout policies
/// </summary>
public class MenuSyncRetryPolicy : ITransientDependency
{
    private readonly ILogger<MenuSyncRetryPolicy> _logger;
    private readonly MenuSyncRetryOptions _options;

    public MenuSyncRetryPolicy(
        ILogger<MenuSyncRetryPolicy> logger,
        IOptions<MenuSyncRetryOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    /// <summary>
    /// Creates a comprehensive retry policy for Menu Sync operations
    /// Combines retry, circuit breaker, and timeout policies
    /// </summary>
    public IAsyncPolicy<T> CreatePolicy<T>(string operationName)
    {
        var retryPolicy = Policy<T>
            .Handle<HttpRequestException>()
            .Or<TaskCanceledException>()
            .Or<TimeoutRejectedException>()
            .Or<Exception>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => CalculateDelay(retryAttempt),
                onRetry: (DelegateResult<T> outcome, TimeSpan timespan, int retryCount, Context context) =>
                {
                    var errorMessage = outcome.Exception?.Message ?? "Unknown error";
                    _logger.LogWarning(
                        "Retry attempt {RetryCount} for {Operation} after {Delay}ms. Error: {Error}",
                        retryCount, operationName, timespan.TotalMilliseconds, errorMessage);
                });

        var circuitBreakerPolicy = Policy<T>
            .Handle<Exception>()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: _options.CircuitBreakerFailureThreshold,
                durationOfBreak: _options.CircuitBreakerDuration,
                onBreak: (DelegateResult<T> delegateResult, TimeSpan duration) =>
                {
                    var errorMessage = delegateResult.Exception?.Message ?? "Unknown error";
                    _logger.LogError(
                        "Circuit breaker opened for {Operation} for {Duration}s. Error: {Error}",
                        operationName, duration.TotalSeconds, errorMessage);
                },
                onReset: () =>
                {
                    _logger.LogInformation("Circuit breaker reset for {Operation}", operationName);
                },
                onHalfOpen: () =>
                {
                    _logger.LogInformation("Circuit breaker half-open for {Operation}", operationName);
                });

        var timeoutPolicy = Policy.TimeoutAsync<T>(_options.OperationTimeout);

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
    }

    /// <summary>
    /// Creates an HTTP-specific retry policy for Talabat API calls
    /// </summary>
    public IAsyncPolicy<HttpResponseMessage> CreateHttpPolicy(string operationName)
    {
        var retryPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .Or<TimeoutRejectedException>()
            .WaitAndRetryAsync(
                retryCount: _options.MaxRetryAttempts,
                sleepDurationProvider: retryAttempt => CalculateDelay(retryAttempt),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode.ToString() ?? "N/A";
                    var error = outcome.Exception?.Message ?? $"HTTP {statusCode}";
                    
                    _logger.LogWarning(
                        "HTTP retry attempt {RetryCount} for {Operation} after {Delay}ms. Error: {Error}",
                        retryCount, operationName, timespan.TotalMilliseconds, error);
                });

        var circuitBreakerPolicy = HttpPolicyExtensions
            .HandleTransientHttpError()
            .AdvancedCircuitBreakerAsync(
                failureThreshold: 0.5,
                samplingDuration: TimeSpan.FromSeconds(10),
                minimumThroughput: _options.CircuitBreakerFailureThreshold,
                durationOfBreak: _options.CircuitBreakerDuration,
                onBreak: (result, duration) =>
                {
                    var error = result.Exception?.Message ?? $"HTTP {result.Result?.StatusCode}";
                    _logger.LogError(
                        "HTTP circuit breaker opened for {Operation} for {Duration}s. Error: {Error}",
                        operationName, duration.TotalSeconds, error);
                },
                onReset: () =>
                {
                    _logger.LogInformation("HTTP circuit breaker reset for {Operation}", operationName);
                });

        var timeoutPolicy = Policy.TimeoutAsync<HttpResponseMessage>(_options.HttpTimeout);

        return Policy.WrapAsync(retryPolicy, circuitBreakerPolicy, timeoutPolicy);
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried
    /// </summary>
    private bool IsTransientError(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException => true,
            TimeoutException => true,
            SocketException => true,
            InvalidOperationException ex when ex.Message.Contains("timeout") => true,
            InvalidOperationException ex when ex.Message.Contains("connection") => true,
            _ => false
        };
    }

    /// <summary>
    /// Calculates exponential backoff delay with jitter
    /// </summary>
    private TimeSpan CalculateDelay(int retryAttempt)
    {
        var baseDelay = _options.BaseDelay;
        var exponentialDelay = TimeSpan.FromMilliseconds(
            baseDelay.TotalMilliseconds * Math.Pow(_options.BackoffMultiplier, retryAttempt - 1));

        // Add jitter to prevent thundering herd
        var jitter = TimeSpan.FromMilliseconds(
            Random.Shared.Next(0, (int)(exponentialDelay.TotalMilliseconds * _options.JitterFactor)));

        var totalDelay = exponentialDelay + jitter;

        // Cap at maximum delay
        return totalDelay > _options.MaxDelay ? _options.MaxDelay : totalDelay;
    }
}

/// <summary>
/// Configuration options for Menu Sync retry policies
/// </summary>
public class MenuSyncRetryOptions
{
    public const string SectionName = "MenuSync:Retry";

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Base delay between retries
    /// </summary>
    public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Exponential backoff multiplier
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Jitter factor to add randomness (0.0 to 1.0)
    /// </summary>
    public double JitterFactor { get; set; } = 0.1;

    /// <summary>
    /// Circuit breaker failure threshold
    /// </summary>
    public int CircuitBreakerFailureThreshold { get; set; } = 5;

    /// <summary>
    /// Circuit breaker open duration
    /// </summary>
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// General operation timeout
    /// </summary>
    public TimeSpan OperationTimeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// HTTP request timeout
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromMinutes(5);
}