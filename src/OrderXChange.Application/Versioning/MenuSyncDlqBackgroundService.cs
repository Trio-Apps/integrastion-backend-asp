using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Background service for automatic retry of transient DLQ failures
/// Runs periodically to process failed menu sync operations
/// </summary>
public class MenuSyncDlqBackgroundService : BackgroundService, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<MenuSyncDlqBackgroundService> _logger;
    private readonly MenuSyncDlqBackgroundOptions _options;

    public MenuSyncDlqBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<MenuSyncDlqBackgroundService> logger,
        IOptions<MenuSyncDlqBackgroundOptions> options)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Menu Sync DLQ Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDlqMessagesAsync(stoppingToken);
                await Task.Delay(_options.ProcessingInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Menu Sync DLQ Background Service");
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken); // Wait before retrying
            }
        }

        _logger.LogInformation("Menu Sync DLQ Background Service stopped");
    }

    private async Task ProcessDlqMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dlqService = scope.ServiceProvider.GetRequiredService<IMenuSyncDlqService>();

        try
        {
            // Auto-retry transient failures
            if (_options.EnableAutoRetry)
            {
                _logger.LogDebug("Starting auto-retry of transient DLQ failures");
                
                var autoRetryResult = await dlqService.AutoRetryTransientFailuresAsync(
                    _options.MaxRetryAge, cancellationToken);

                if (autoRetryResult.EligibleMessages > 0)
                {
                    _logger.LogInformation(
                        "Auto-retry completed. Eligible={Eligible}, Success={Success}, Failed={Failed}, Rate={Rate:F1}%",
                        autoRetryResult.EligibleMessages, autoRetryResult.SuccessfulRetries, 
                        autoRetryResult.FailedRetries, autoRetryResult.SuccessRate);
                }
            }

            // Cleanup old messages
            if (_options.EnableCleanup && ShouldRunCleanup())
            {
                _logger.LogDebug("Starting DLQ cleanup");
                
                var cleanupResult = await dlqService.CleanupOldMessagesAsync(
                    _options.CleanupRetentionDays, cancellationToken);

                if (cleanupResult.DeletedMessages > 0)
                {
                    _logger.LogInformation(
                        "DLQ cleanup completed. Deleted={Count}, FreedKB={Size}",
                        cleanupResult.DeletedMessages, cleanupResult.FreedStorageBytes / 1024);
                }

                _lastCleanupTime = DateTime.UtcNow;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process DLQ messages");
        }
    }

    private DateTime _lastCleanupTime = DateTime.MinValue;

    private bool ShouldRunCleanup()
    {
        return DateTime.UtcNow - _lastCleanupTime >= _options.CleanupInterval;
    }
}

/// <summary>
/// Configuration options for Menu Sync DLQ background service
/// </summary>
public class MenuSyncDlqBackgroundOptions
{
    public const string SectionName = "MenuSync:DlqBackground";

    /// <summary>
    /// Interval between DLQ processing runs
    /// </summary>
    public TimeSpan ProcessingInterval { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to enable automatic retry of transient failures
    /// </summary>
    public bool EnableAutoRetry { get; set; } = true;

    /// <summary>
    /// Maximum age of messages eligible for auto-retry
    /// </summary>
    public TimeSpan MaxRetryAge { get; set; } = TimeSpan.FromHours(24);

    /// <summary>
    /// Whether to enable automatic cleanup of old messages
    /// </summary>
    public bool EnableCleanup { get; set; } = true;

    /// <summary>
    /// Interval between cleanup runs
    /// </summary>
    public TimeSpan CleanupInterval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Number of days to retain acknowledged/resolved messages
    /// </summary>
    public int CleanupRetentionDays { get; set; } = 30;
}