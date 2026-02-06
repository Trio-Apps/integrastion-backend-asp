using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Aggregate Root that tracks a complete menu synchronization run
/// Provides centralized tracking, logging, and coordination of all sync operations
/// </summary>
public class MenuSyncRun : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to FoodicsAccount
    /// </summary>
    [Required]
    public Guid FoodicsAccountId { get; set; }

    /// <summary>
    /// Branch ID being synced (null for all branches)
    /// </summary>
    [MaxLength(100)]
    public string? BranchId { get; set; }

    /// <summary>
    /// Menu Group ID being synced (null for branch-level sync)
    /// Enables Menu Group-specific sync tracking and isolation
    /// </summary>
    public Guid? MenuGroupId { get; set; }

    /// <summary>
    /// Unique correlation ID for tracing across services
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string CorrelationId { get; set; } = string.Empty;

    /// <summary>
    /// Type of sync run: Manual, Scheduled, Webhook, Retry
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string SyncType { get; set; } = string.Empty;

    /// <summary>
    /// Sync trigger source: User, Scheduler, WebhookEvent, RetryJob
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string TriggerSource { get; set; } = string.Empty;

    /// <summary>
    /// User or system that initiated the sync
    /// </summary>
    [MaxLength(200)]
    public string? InitiatedBy { get; set; }

    /// <summary>
    /// When the sync run started
    /// </summary>
    public DateTime StartedAt { get; set; }

    /// <summary>
    /// When the sync run completed (null if still running)
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Current status of the sync run
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = MenuSyncRunStatus.Pending;

    /// <summary>
    /// Overall result of the sync run
    /// </summary>
    [MaxLength(50)]
    public string? Result { get; set; }

    /// <summary>
    /// Total duration of the sync run
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Current phase of the sync process
    /// </summary>
    [MaxLength(100)]
    public string? CurrentPhase { get; set; }

    /// <summary>
    /// Progress percentage (0-100)
    /// </summary>
    public int ProgressPercentage { get; set; }

    /// <summary>
    /// Total number of products processed
    /// </summary>
    public int TotalProductsProcessed { get; set; }

    /// <summary>
    /// Number of products successfully synced
    /// </summary>
    public int ProductsSucceeded { get; set; }

    /// <summary>
    /// Number of products that failed to sync
    /// </summary>
    public int ProductsFailed { get; set; }

    /// <summary>
    /// Number of products that were skipped
    /// </summary>
    public int ProductsSkipped { get; set; }

    /// <summary>
    /// Number of products added in this sync
    /// </summary>
    public int ProductsAdded { get; set; }

    /// <summary>
    /// Number of products updated in this sync
    /// </summary>
    public int ProductsUpdated { get; set; }

    /// <summary>
    /// Number of products soft deleted in this sync
    /// </summary>
    public int ProductsDeleted { get; set; }

    /// <summary>
    /// Number of categories processed
    /// </summary>
    public int CategoriesProcessed { get; set; }

    /// <summary>
    /// Number of modifiers processed
    /// </summary>
    public int ModifiersProcessed { get; set; }

    /// <summary>
    /// Talabat vendor code being synced to
    /// </summary>
    [MaxLength(100)]
    public string? TalabatVendorCode { get; set; }

    /// <summary>
    /// Talabat import ID returned from submission
    /// </summary>
    [MaxLength(200)]
    public string? TalabatImportId { get; set; }

    /// <summary>
    /// When the data was submitted to Talabat
    /// </summary>
    public DateTime? TalabatSubmittedAt { get; set; }

    /// <summary>
    /// When Talabat confirmed the sync completion
    /// </summary>
    public DateTime? TalabatCompletedAt { get; set; }

    /// <summary>
    /// Talabat sync status
    /// </summary>
    [MaxLength(50)]
    public string? TalabatSyncStatus { get; set; }

    /// <summary>
    /// JSON array of error messages and details
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? ErrorsJson { get; set; }

    /// <summary>
    /// JSON array of warning messages
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? WarningsJson { get; set; }

    /// <summary>
    /// JSON object containing detailed sync metrics and statistics
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? MetricsJson { get; set; }

    /// <summary>
    /// JSON object containing sync configuration and parameters
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? ConfigurationJson { get; set; }

    /// <summary>
    /// Compressed execution trace for debugging
    /// </summary>
    [Column(TypeName = "LONGBLOB")]
    public byte[]? CompressedTraceData { get; set; }

    /// <summary>
    /// Parent sync run ID (for retry scenarios)
    /// </summary>
    public Guid? ParentSyncRunId { get; set; }

    /// <summary>
    /// Number of retry attempts
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Whether this sync run can be retried
    /// </summary>
    public bool CanRetry { get; set; } = true;

    /// <summary>
    /// Priority level for processing (1-10, 10 being highest)
    /// </summary>
    public int Priority { get; set; } = 5;

    /// <summary>
    /// Tags for categorization and filtering
    /// </summary>
    [MaxLength(500)]
    public string? Tags { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual Foodics.FoodicsAccount FoodicsAccount { get; set; } = null!;
    public virtual FoodicsMenuGroup? MenuGroup { get; set; }
    public virtual MenuSyncRun? ParentSyncRun { get; set; }
    public virtual ICollection<MenuSyncRun> ChildSyncRuns { get; set; } = new List<MenuSyncRun>();
    public virtual ICollection<MenuSyncRunStep> Steps { get; set; } = new List<MenuSyncRunStep>();
    public virtual ICollection<MenuSnapshot> Snapshots { get; set; } = new List<MenuSnapshot>();
    public virtual ICollection<MenuDelta> Deltas { get; set; } = new List<MenuDelta>();
    public virtual ICollection<MenuItemDeletion> Deletions { get; set; } = new List<MenuItemDeletion>();

    #region Business Methods

    /// <summary>
    /// Starts the sync run
    /// </summary>
    public void Start(string initiatedBy, string currentPhase = "Initialization")
    {
        if (Status != MenuSyncRunStatus.Pending)
        {
            throw new InvalidOperationException($"Cannot start sync run in status: {Status}");
        }

        StartedAt = DateTime.UtcNow;
        Status = MenuSyncRunStatus.Running;
        InitiatedBy = initiatedBy;
        CurrentPhase = currentPhase;
        ProgressPercentage = 0;

        AddStep(MenuSyncRunStepType.Started, "Sync run started", currentPhase);
    }

    /// <summary>
    /// Updates the current phase and progress
    /// </summary>
    public void UpdateProgress(string phase, int progressPercentage, string? details = null)
    {
        if (Status != MenuSyncRunStatus.Running)
        {
            throw new InvalidOperationException($"Cannot update progress for sync run in status: {Status}");
        }

        CurrentPhase = phase;
        ProgressPercentage = Math.Max(0, Math.Min(100, progressPercentage));

        AddStep(MenuSyncRunStepType.Progress, details ?? $"Progress: {progressPercentage}%", phase);
    }

    /// <summary>
    /// Completes the sync run successfully
    /// </summary>
    public void Complete(string result = "Success")
    {
        if (Status != MenuSyncRunStatus.Running)
        {
            throw new InvalidOperationException($"Cannot complete sync run in status: {Status}");
        }

        CompletedAt = DateTime.UtcNow;
        Duration = CompletedAt - StartedAt;
        Status = MenuSyncRunStatus.Completed;
        Result = result;
        ProgressPercentage = 100;

        AddStep(MenuSyncRunStepType.Completed, $"Sync run completed: {result}");
    }

    /// <summary>
    /// Fails the sync run with error details
    /// </summary>
    public void Fail(string error, Exception? exception = null)
    {
        CompletedAt = DateTime.UtcNow;
        Duration = CompletedAt - StartedAt;
        Status = MenuSyncRunStatus.Failed;
        Result = "Failed";

        var errorDetails = exception != null 
            ? $"{error}: {exception.Message}" 
            : error;

        AddStep(MenuSyncRunStepType.Error, errorDetails);
        AddError(errorDetails, exception);
    }

    /// <summary>
    /// Cancels the sync run
    /// </summary>
    public void Cancel(string reason = "Cancelled by user")
    {
        CompletedAt = DateTime.UtcNow;
        Duration = CompletedAt - StartedAt;
        Status = MenuSyncRunStatus.Cancelled;
        Result = "Cancelled";

        AddStep(MenuSyncRunStepType.Cancelled, reason);
    }

    /// <summary>
    /// Adds a step to the sync run
    /// </summary>
    public void AddStep(string stepType, string message, string? phase = null)
    {
        var step = new MenuSyncRunStep(Guid.NewGuid())
        {
            MenuSyncRunId = Id,
            StepType = stepType,
            Message = message,
            Phase = phase ?? CurrentPhase,
            Timestamp = DateTime.UtcNow,
            SequenceNumber = Steps.Count + 1
        };

        Steps.Add(step);
    }

    /// <summary>
    /// Adds an error to the sync run
    /// </summary>
    public void AddError(string error, Exception? exception = null)
    {
        var errors = GetErrors().ToList();
        
        var errorEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Message = error,
            Exception = exception?.ToString(),
            Phase = CurrentPhase
        };

        errors.Add(errorEntry);
        ErrorsJson = System.Text.Json.JsonSerializer.Serialize(errors);

        AddStep(MenuSyncRunStepType.Error, error);
    }

    /// <summary>
    /// Adds a warning to the sync run
    /// </summary>
    public void AddWarning(string warning)
    {
        var warnings = GetWarnings().ToList();
        
        var warningEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Message = warning,
            Phase = CurrentPhase
        };

        warnings.Add(warningEntry);
        WarningsJson = System.Text.Json.JsonSerializer.Serialize(warnings);

        AddStep(MenuSyncRunStepType.Warning, warning);
    }

    /// <summary>
    /// Updates sync statistics
    /// </summary>
    public void UpdateStatistics(
        int totalProcessed = 0,
        int succeeded = 0,
        int failed = 0,
        int skipped = 0,
        int added = 0,
        int updated = 0,
        int deleted = 0)
    {
        TotalProductsProcessed = totalProcessed;
        ProductsSucceeded = succeeded;
        ProductsFailed = failed;
        ProductsSkipped = skipped;
        ProductsAdded = added;
        ProductsUpdated = updated;
        ProductsDeleted = deleted;

        AddStep(MenuSyncRunStepType.Statistics, 
            $"Stats: {succeeded} succeeded, {failed} failed, {skipped} skipped");
    }

    /// <summary>
    /// Sets Talabat sync information
    /// </summary>
    public void SetTalabatSyncInfo(string vendorCode, string? importId = null, string? status = null)
    {
        TalabatVendorCode = vendorCode;
        
        if (!string.IsNullOrEmpty(importId))
        {
            TalabatImportId = importId;
            TalabatSubmittedAt = DateTime.UtcNow;
        }

        if (!string.IsNullOrEmpty(status))
        {
            TalabatSyncStatus = status;
            
            if (status == "Completed")
            {
                TalabatCompletedAt = DateTime.UtcNow;
            }
        }

        AddStep(MenuSyncRunStepType.TalabatSync, 
            $"Talabat sync: {status ?? "Submitted"} (ImportId: {importId})");
    }

    /// <summary>
    /// Gets all errors as objects
    /// </summary>
    public IEnumerable<dynamic> GetErrors()
    {
        if (string.IsNullOrEmpty(ErrorsJson))
            return Enumerable.Empty<dynamic>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<dynamic[]>(ErrorsJson) ?? 
                   Enumerable.Empty<dynamic>();
        }
        catch
        {
            return Enumerable.Empty<dynamic>();
        }
    }

    /// <summary>
    /// Gets all warnings as objects
    /// </summary>
    public IEnumerable<dynamic> GetWarnings()
    {
        if (string.IsNullOrEmpty(WarningsJson))
            return Enumerable.Empty<dynamic>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<dynamic[]>(WarningsJson) ?? 
                   Enumerable.Empty<dynamic>();
        }
        catch
        {
            return Enumerable.Empty<dynamic>();
        }
    }

    /// <summary>
    /// Checks if the sync run is in a terminal state
    /// </summary>
    public bool IsCompleted => Status == MenuSyncRunStatus.Completed || 
                              Status == MenuSyncRunStatus.Failed || 
                              Status == MenuSyncRunStatus.Cancelled;

    /// <summary>
    /// Checks if the sync run is currently active
    /// </summary>
    public bool IsActive => Status == MenuSyncRunStatus.Running;

    /// <summary>
    /// Gets the success rate as a percentage
    /// </summary>
    public double SuccessRate => TotalProductsProcessed > 0 
        ? (double)ProductsSucceeded / TotalProductsProcessed * 100 
        : 0;

    #endregion
}

/// <summary>
/// Individual step within a menu sync run
/// Provides detailed tracking of each operation
/// </summary>
public class MenuSyncRunStep : CreationAuditedEntity<Guid>, IMultiTenant
{
    public MenuSyncRunStep()
    {
    }

    public MenuSyncRunStep(Guid id)
    {
        Id = id;
    }
    /// <summary>
    /// Foreign key to MenuSyncRun
    /// </summary>
    [Required]
    public Guid MenuSyncRunId { get; set; }

    /// <summary>
    /// Type of step
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string StepType { get; set; } = string.Empty;

    /// <summary>
    /// Step message or description
    /// </summary>
    [Required]
    [MaxLength(2000)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Phase this step belongs to
    /// </summary>
    [MaxLength(100)]
    public string? Phase { get; set; }

    /// <summary>
    /// When this step occurred
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Sequence number within the sync run
    /// </summary>
    public int SequenceNumber { get; set; }

    /// <summary>
    /// Additional data as JSON
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? DataJson { get; set; }

    /// <summary>
    /// Duration of this step (if applicable)
    /// </summary>
    public TimeSpan? Duration { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual MenuSyncRun MenuSyncRun { get; set; } = null!;
}

/// <summary>
/// Constants for sync run status
/// </summary>
public static class MenuSyncRunStatus
{
    public const string Pending = "Pending";
    public const string Running = "Running";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Retrying = "Retrying";
}

/// <summary>
/// Constants for sync run types
/// </summary>
public static class MenuSyncRunType
{
    public const string Manual = "Manual";
    public const string Scheduled = "Scheduled";
    public const string Webhook = "Webhook";
    public const string Retry = "Retry";
    public const string Emergency = "Emergency";
}

/// <summary>
/// Constants for sync trigger sources
/// </summary>
public static class MenuSyncTriggerSource
{
    public const string User = "User";
    public const string Scheduler = "Scheduler";
    public const string WebhookEvent = "WebhookEvent";
    public const string RetryJob = "RetryJob";
    public const string HealthCheck = "HealthCheck";
    public const string AdminAction = "AdminAction";
}

/// <summary>
/// Constants for sync run step types
/// </summary>
public static class MenuSyncRunStepType
{
    public const string Started = "Started";
    public const string Progress = "Progress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string Cancelled = "Cancelled";
    public const string Error = "Error";
    public const string Warning = "Warning";
    public const string Statistics = "Statistics";
    public const string TalabatSync = "TalabatSync";
    public const string FoodicsSync = "FoodicsSync";
    public const string DataProcessing = "DataProcessing";
    public const string Validation = "Validation";
    public const string Transformation = "Transformation";
}

/// <summary>
/// Constants for sync phases
/// </summary>
public static class MenuSyncPhase
{
    public const string Initialization = "Initialization";
    public const string FoodicsDataFetch = "FoodicsDataFetch";
    public const string DataValidation = "DataValidation";
    public const string ChangeDetection = "ChangeDetection";
    public const string DeltaGeneration = "DeltaGeneration";
    public const string SoftDeleteProcessing = "SoftDeleteProcessing";
    public const string TalabatSubmission = "TalabatSubmission";
    public const string StatusMonitoring = "StatusMonitoring";
    public const string Finalization = "Finalization";
    public const string Cleanup = "Cleanup";
}
