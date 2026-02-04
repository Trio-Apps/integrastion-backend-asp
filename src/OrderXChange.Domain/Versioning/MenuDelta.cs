using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

public class MenuDelta : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? SourceSnapshotId { get; set; }
    [Required]
    public Guid TargetSnapshotId { get; set; }
    [Required]
    public Guid FoodicsAccountId { get; set; }
    [MaxLength(100)]
    public string? BranchId { get; set; }
    public Guid? MenuGroupId { get; set; }
    public int? SourceVersion { get; set; }
    public int TargetVersion { get; set; }
    [Required]
    [MaxLength(50)]
    public string DeltaType { get; set; } = string.Empty;
    public int TotalChanges { get; set; }
    public int AddedCount { get; set; }
    public int UpdatedCount { get; set; }
    public int RemovedCount { get; set; }
    [Column(TypeName = "TEXT")]
    public string DeltaSummaryJson { get; set; } = string.Empty;
    [Column(TypeName = "LONGBLOB")]
    public byte[]? CompressedDeltaPayload { get; set; }
    public bool IsSyncedToTalabat { get; set; }
    [MaxLength(200)]
    public string? TalabatImportId { get; set; }
    public DateTime? TalabatSyncedAt { get; set; }
    [MaxLength(100)]
    public string? TalabatVendorCode { get; set; }
    [MaxLength(50)]
    public string SyncStatus { get; set; } = MenuDeltaSyncStatus.Pending;
    [Column(TypeName = "TEXT")]
    public string? SyncErrorDetails { get; set; }
    public int RetryCount { get; set; }
    public Guid? TenantId { get; set; }
    public virtual MenuSnapshot? SourceSnapshot { get; set; }
    public virtual MenuSnapshot TargetSnapshot { get; set; } = null!;
    public virtual FoodicsMenuGroup? MenuGroup { get; set; }
}

public static class MenuDeltaType
{
    public const string FirstSync = "FirstSync";
    public const string Incremental = "Incremental";
    public const string FullResync = "FullResync";
}

public static class MenuDeltaSyncStatus
{
    public const string Pending = "Pending";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
    public const string PartiallyFailed = "PartiallyFailed";
}
