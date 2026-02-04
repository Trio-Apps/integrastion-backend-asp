using System;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Domain.Versioning;
using Volo.Abp;
using Volo.Abp.EntityFrameworkCore.Modeling;

namespace OrderXChange.EntityFrameworkCore;

/// <summary>
/// EF Core model configuration for Menu Versioning entities
/// </summary>
public static partial class OrderXChangeDbContextModelCreatingExtensions
{
    public static void ConfigureMenuVersioning(this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        // MenuSnapshot configuration
        builder.Entity<MenuSnapshot>(b =>
        {
            b.ToTable("MenuSnapshots");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes for efficient querying
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.Version })
                .HasDatabaseName("IX_MenuSnapshots_Account_Branch_Version");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.SnapshotDate })
                .HasDatabaseName("IX_MenuSnapshots_Account_Branch_Date");
            
            b.HasIndex(x => x.SnapshotHash)
                .HasDatabaseName("IX_MenuSnapshots_Hash");
            
            b.HasIndex(x => new { x.TalabatVendorCode, x.IsSyncedToTalabat })
                .HasDatabaseName("IX_MenuSnapshots_Vendor_Synced");
            
            // Foreign key to FoodicsAccount
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.SnapshotHash)
                .IsRequired()
                .HasMaxLength(64);
            
            b.Property(x => x.BranchId)
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatImportId)
                .HasMaxLength(200);
            
            b.Property(x => x.TalabatVendorCode)
                .HasMaxLength(100);
            
            b.Property(x => x.ChangelogJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.CompressedSnapshotData)
                .HasColumnType("LONGBLOB");
        });

        // MenuChangeLog configuration
        builder.Entity<MenuChangeLog>(b =>
        {
            b.ToTable("MenuChangeLogs");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes for efficient querying
            b.HasIndex(x => x.MenuSnapshotId)
                .HasDatabaseName("IX_MenuChangeLogs_SnapshotId");
            
            b.HasIndex(x => new { x.EntityType, x.EntityId })
                .HasDatabaseName("IX_MenuChangeLogs_Entity");
            
            b.HasIndex(x => new { x.ChangeType, x.EntityType })
                .HasDatabaseName("IX_MenuChangeLogs_ChangeType_EntityType");
            
            b.HasIndex(x => new { x.CurrentVersion, x.PreviousVersion })
                .HasDatabaseName("IX_MenuChangeLogs_Versions");
            
            // Foreign key to MenuSnapshot
            b.HasOne(x => x.MenuSnapshot)
                .WithMany()
                .HasForeignKey(x => x.MenuSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.ChangeType)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.EntityType)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.EntityId)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.EntityName)
                .HasMaxLength(500);
            
            b.Property(x => x.ChangedFields)
                .HasMaxLength(1000);
            
            b.Property(x => x.OldValueJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.NewValueJson)
                .HasColumnType("TEXT");
        });

        // MenuSyncRun configuration
        builder.Entity<MenuSyncRun>(b =>
        {
            b.ToTable("MenuSyncRuns");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes for efficient querying
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_MenuSyncRuns_FoodicsAccountId");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId })
                .HasDatabaseName("IX_MenuSyncRuns_Account_Branch");
            
            b.HasIndex(x => x.CorrelationId)
                .HasDatabaseName("IX_MenuSyncRuns_CorrelationId");
            
            b.HasIndex(x => x.Status)
                .HasDatabaseName("IX_MenuSyncRuns_Status");
            
            b.HasIndex(x => x.StartedAt)
                .HasDatabaseName("IX_MenuSyncRuns_StartedAt");
            
            b.HasIndex(x => new { x.Status, x.StartedAt })
                .HasDatabaseName("IX_MenuSyncRuns_Status_StartedAt");
            
            b.HasIndex(x => new { x.CanRetry, x.RetryCount, x.CompletedAt })
                .HasDatabaseName("IX_MenuSyncRuns_Retry");
            
            b.HasIndex(x => x.ParentSyncRunId)
                .HasDatabaseName("IX_MenuSyncRuns_ParentId");
            
            b.HasIndex(x => x.TalabatVendorCode)
                .HasDatabaseName("IX_MenuSyncRuns_TalabatVendorCode");
            
            // Foreign key to FoodicsAccount
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Self-referencing relationship for parent/child sync runs
            b.HasOne(x => x.ParentSyncRun)
                .WithMany(x => x.ChildSyncRuns)
                .HasForeignKey(x => x.ParentSyncRunId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Properties
            b.Property(x => x.CorrelationId)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.SyncType)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.TriggerSource)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.InitiatedBy)
                .HasMaxLength(200);
            
            b.Property(x => x.Status)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.Result)
                .HasMaxLength(50);
            
            b.Property(x => x.CurrentPhase)
                .HasMaxLength(100);
            
            b.Property(x => x.BranchId)
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatVendorCode)
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatImportId)
                .HasMaxLength(200);
            
            b.Property(x => x.TalabatSyncStatus)
                .HasMaxLength(50);
            
            b.Property(x => x.Tags)
                .HasMaxLength(500);
            
            b.Property(x => x.ErrorsJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.WarningsJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.MetricsJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.ConfigurationJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.CompressedTraceData)
                .HasColumnType("LONGBLOB");
        });

        // MenuSyncRunStep configuration
        builder.Entity<MenuSyncRunStep>(b =>
        {
            b.ToTable("MenuSyncRunSteps");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes for efficient querying
            b.HasIndex(x => x.MenuSyncRunId)
                .HasDatabaseName("IX_MenuSyncRunSteps_SyncRunId");
            
            b.HasIndex(x => new { x.MenuSyncRunId, x.SequenceNumber })
                .HasDatabaseName("IX_MenuSyncRunSteps_SyncRun_Sequence");
            
            b.HasIndex(x => x.StepType)
                .HasDatabaseName("IX_MenuSyncRunSteps_StepType");
            
            b.HasIndex(x => x.Timestamp)
                .HasDatabaseName("IX_MenuSyncRunSteps_Timestamp");
            
            // Foreign key to MenuSyncRun
            b.HasOne(x => x.MenuSyncRun)
                .WithMany(x => x.Steps)
                .HasForeignKey(x => x.MenuSyncRunId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.StepType)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.Message)
                .IsRequired()
                .HasMaxLength(2000);
            
            b.Property(x => x.Phase)
                .HasMaxLength(100);
            
            b.Property(x => x.DataJson)
                .HasColumnType("TEXT");
        });

        // MenuItemMapping configuration
        builder.Entity<MenuItemMapping>(b =>
        {
            b.ToTable("MenuItemMappings");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Unique constraint: One mapping per FoodicsAccount + BranchId + EntityType + FoodicsId
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.EntityType, x.FoodicsId })
                .IsUnique()
                .HasDatabaseName("IX_MenuItemMappings_Account_Branch_Entity_FoodicsId");
            
            // Unique constraint: One TalabatRemoteCode per FoodicsAccount + BranchId
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.TalabatRemoteCode })
                .IsUnique()
                .HasDatabaseName("IX_MenuItemMappings_Account_Branch_TalabatCode");
            
            // Indexes for efficient querying
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_MenuItemMappings_FoodicsAccountId");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId })
                .HasDatabaseName("IX_MenuItemMappings_Account_Branch");
            
            b.HasIndex(x => x.EntityType)
                .HasDatabaseName("IX_MenuItemMappings_EntityType");
            
            b.HasIndex(x => x.FoodicsId)
                .HasDatabaseName("IX_MenuItemMappings_FoodicsId");
            
            b.HasIndex(x => x.TalabatRemoteCode)
                .HasDatabaseName("IX_MenuItemMappings_TalabatRemoteCode");
            
            b.HasIndex(x => x.TalabatInternalId)
                .HasDatabaseName("IX_MenuItemMappings_TalabatInternalId");
            
            b.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_MenuItemMappings_IsActive");
            
            b.HasIndex(x => x.FirstSyncedAt)
                .HasDatabaseName("IX_MenuItemMappings_FirstSyncedAt");
            
            b.HasIndex(x => x.LastVerifiedAt)
                .HasDatabaseName("IX_MenuItemMappings_LastVerifiedAt");
            
            b.HasIndex(x => x.ParentMappingId)
                .HasDatabaseName("IX_MenuItemMappings_ParentMappingId");
            
            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_MenuItemMappings_TenantId");
            
            // Composite indexes for common queries
            b.HasIndex(x => new { x.IsActive, x.EntityType })
                .HasDatabaseName("IX_MenuItemMappings_Active_EntityType");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.IsActive })
                .HasDatabaseName("IX_MenuItemMappings_Account_Active");
            
            // Foreign key to FoodicsAccount
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Self-referencing relationship for parent/child mappings
            b.HasOne(x => x.ParentMapping)
                .WithMany(x => x.ChildMappings)
                .HasForeignKey(x => x.ParentMappingId)
                .OnDelete(DeleteBehavior.Restrict);
            
            // Properties
            b.Property(x => x.EntityType)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.FoodicsId)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatRemoteCode)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatInternalId)
                .HasMaxLength(100);
            
            b.Property(x => x.BranchId)
                .HasMaxLength(100);
            
            b.Property(x => x.CurrentFoodicsName)
                .HasMaxLength(500);
            
            b.Property(x => x.CurrentTalabatName)
                .HasMaxLength(500);
            
            b.Property(x => x.StructureHash)
                .HasMaxLength(64);
            
            b.Property(x => x.MetadataJson)
                .HasColumnType("TEXT");
        });

        // Add relationships between MenuSyncRun and other versioning entities
        builder.Entity<MenuSnapshot>(b =>
        {
            // Add MenuSyncRunId foreign key
            b.Property<Guid?>("MenuSyncRunId");
            
            b.HasIndex("MenuSyncRunId")
                .HasDatabaseName("IX_MenuSnapshots_SyncRunId");
            
            // Relationship to MenuSyncRun
            b.HasOne<MenuSyncRun>()
                .WithMany(sr => sr.Snapshots)
                .HasForeignKey("MenuSyncRunId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // MenuDelta configuration
        builder.Entity<MenuDelta>(b =>
        {
            b.ToTable("MenuDeltas");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes for efficient querying
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_MenuDeltas_FoodicsAccountId");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId })
                .HasDatabaseName("IX_MenuDeltas_Account_Branch");
            
            b.HasIndex(x => x.MenuGroupId)
                .HasDatabaseName("IX_MenuDeltas_MenuGroupId");
            
            b.HasIndex(x => x.SourceSnapshotId)
                .HasDatabaseName("IX_MenuDeltas_SourceSnapshotId");
            
            b.HasIndex(x => x.TargetSnapshotId)
                .HasDatabaseName("IX_MenuDeltas_TargetSnapshotId");
            
            b.HasIndex(x => x.SyncStatus)
                .HasDatabaseName("IX_MenuDeltas_SyncStatus");
            
            b.HasIndex(x => x.DeltaType)
                .HasDatabaseName("IX_MenuDeltas_DeltaType");
            
            b.HasIndex(x => x.TalabatVendorCode)
                .HasDatabaseName("IX_MenuDeltas_TalabatVendorCode");
            
            b.HasIndex(x => x.TalabatImportId)
                .HasDatabaseName("IX_MenuDeltas_TalabatImportId");
            
            b.HasIndex(x => x.CreationTime)
                .HasDatabaseName("IX_MenuDeltas_CreationTime");
            
            // Composite indexes for common queries
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId })
                .HasDatabaseName("IX_MenuDeltas_Account_Branch_Group");
            
            b.HasIndex(x => new { x.SyncStatus, x.CreationTime })
                .HasDatabaseName("IX_MenuDeltas_Status_CreationTime");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.SyncStatus })
                .HasDatabaseName("IX_MenuDeltas_Account_Status");
            
            // Foreign key to FoodicsAccount
            b.HasOne<Foodics.FoodicsAccount>()
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Foreign key to MenuGroup (optional)
            b.HasOne(x => x.MenuGroup)
                .WithMany()
                .HasForeignKey(x => x.MenuGroupId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Foreign key to SourceSnapshot (optional)
            b.HasOne(x => x.SourceSnapshot)
                .WithMany()
                .HasForeignKey(x => x.SourceSnapshotId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Foreign key to TargetSnapshot
            b.HasOne(x => x.TargetSnapshot)
                .WithMany()
                .HasForeignKey(x => x.TargetSnapshotId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.BranchId)
                .HasMaxLength(100);
            
            b.Property(x => x.DeltaType)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.SyncStatus)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.TalabatVendorCode)
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatImportId)
                .HasMaxLength(200);
            
            b.Property(x => x.DeltaSummaryJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.CompressedDeltaPayload)
                .HasColumnType("LONGBLOB");
            
            b.Property(x => x.SyncErrorDetails)
                .HasColumnType("TEXT");
            
            // Add MenuSyncRunId foreign key for tracking
            b.Property<Guid?>("MenuSyncRunId");
            
            b.HasIndex("MenuSyncRunId")
                .HasDatabaseName("IX_MenuDeltas_SyncRunId");
            
            // Relationship to MenuSyncRun
            b.HasOne<MenuSyncRun>()
                .WithMany(sr => sr.Deltas)
                .HasForeignKey("MenuSyncRunId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MenuItemDeletion>(b =>
        {
            // Add MenuSyncRunId foreign key
            b.Property<Guid?>("MenuSyncRunId");
            
            b.HasIndex("MenuSyncRunId")
                .HasDatabaseName("IX_MenuItemDeletions_SyncRunId");
            
            // Relationship to MenuSyncRun
            b.HasOne<MenuSyncRun>()
                .WithMany(sr => sr.Deletions)
                .HasForeignKey("MenuSyncRunId")
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // FoodicsMenuGroup configuration
        builder.Entity<FoodicsMenuGroup>(b =>
        {
            b.ToTable("FoodicsMenuGroups");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Unique constraint: Name must be unique within FoodicsAccount + BranchId + TenantId
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.Name, x.TenantId })
                .IsUnique()
                .HasDatabaseName("IX_FoodicsMenuGroups_Account_Branch_Name_Tenant");
            
            // Indexes for efficient querying
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_FoodicsMenuGroups_FoodicsAccountId");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId })
                .HasDatabaseName("IX_FoodicsMenuGroups_Account_Branch");
            
            b.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_FoodicsMenuGroups_IsActive");
            
            b.HasIndex(x => x.SortOrder)
                .HasDatabaseName("IX_FoodicsMenuGroups_SortOrder");
            
            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_FoodicsMenuGroups_TenantId");
            
            // Composite indexes for common queries
            b.HasIndex(x => new { x.FoodicsAccountId, x.IsActive })
                .HasDatabaseName("IX_FoodicsMenuGroups_Account_Active");
            
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.IsActive })
                .HasDatabaseName("IX_FoodicsMenuGroups_Account_Branch_Active");
            
            // Foreign key to FoodicsAccount
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);
            
            b.Property(x => x.Description)
                .HasMaxLength(1000);
            
            b.Property(x => x.BranchId)
                .HasMaxLength(100);
            
            b.Property(x => x.MetadataJson)
                .HasColumnType("TEXT");
        });

        // MenuGroupCategory configuration
        builder.Entity<MenuGroupCategory>(b =>
        {
            b.ToTable("MenuGroupCategories");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Unique constraint: One category per Menu Group (MenuGroupId + CategoryId + TenantId)
            b.HasIndex(x => new { x.MenuGroupId, x.CategoryId, x.TenantId })
                .IsUnique()
                .HasDatabaseName("IX_MenuGroupCategories_Group_Category_Tenant");
            
            // Indexes for efficient querying
            b.HasIndex(x => x.MenuGroupId)
                .HasDatabaseName("IX_MenuGroupCategories_MenuGroupId");
            
            b.HasIndex(x => x.CategoryId)
                .HasDatabaseName("IX_MenuGroupCategories_CategoryId");
            
            b.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_MenuGroupCategories_IsActive");
            
            b.HasIndex(x => x.AssignedAt)
                .HasDatabaseName("IX_MenuGroupCategories_AssignedAt");
            
            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_MenuGroupCategories_TenantId");
            
            // Composite indexes for common queries
            b.HasIndex(x => new { x.MenuGroupId, x.IsActive })
                .HasDatabaseName("IX_MenuGroupCategories_Group_Active");
            
            b.HasIndex(x => new { x.MenuGroupId, x.SortOrder })
                .HasDatabaseName("IX_MenuGroupCategories_Group_SortOrder");
            
            b.HasIndex(x => new { x.CategoryId, x.IsActive })
                .HasDatabaseName("IX_MenuGroupCategories_Category_Active");
            
            // Foreign key to FoodicsMenuGroup
            b.HasOne(x => x.MenuGroup)
                .WithMany(x => x.Categories)
                .HasForeignKey(x => x.MenuGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.CategoryId)
                .IsRequired()
                .HasMaxLength(100);
        });

        // Update MenuSnapshot configuration to include MenuGroup relationship
        builder.Entity<MenuSnapshot>(b =>
        {
            // Add MenuGroupId foreign key relationship
            b.HasOne(x => x.MenuGroup)
                .WithMany(x => x.Snapshots)
                .HasForeignKey(x => x.MenuGroupId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Add index for MenuGroupId
            b.HasIndex(x => x.MenuGroupId)
                .HasDatabaseName("IX_MenuSnapshots_MenuGroupId");
            
            // Update composite indexes to include MenuGroupId
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId, x.Version })
                .HasDatabaseName("IX_MenuSnapshots_Account_Branch_Group_Version");
        });

        // Update MenuSyncRun configuration to include MenuGroup relationship
        builder.Entity<MenuSyncRun>(b =>
        {
            // Add MenuGroupId foreign key relationship
            b.HasOne(x => x.MenuGroup)
                .WithMany(x => x.SyncRuns)
                .HasForeignKey(x => x.MenuGroupId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Add index for MenuGroupId
            b.HasIndex(x => x.MenuGroupId)
                .HasDatabaseName("IX_MenuSyncRuns_MenuGroupId");
            
            // Update composite indexes to include MenuGroupId
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId })
                .HasDatabaseName("IX_MenuSyncRuns_Account_Branch_Group");
        });

        // Update MenuItemMapping configuration to include MenuGroup relationship
        builder.Entity<MenuItemMapping>(b =>
        {
            // Add MenuGroupId foreign key relationship
            b.HasOne(x => x.MenuGroup)
                .WithMany(x => x.ItemMappings)
                .HasForeignKey(x => x.MenuGroupId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Add index for MenuGroupId
            b.HasIndex(x => x.MenuGroupId)
                .HasDatabaseName("IX_MenuItemMappings_MenuGroupId");
            
            // Update unique constraint to include MenuGroupId for scoped mappings
            // Remove old unique constraint first (this will be handled in migration)
            // Add new unique constraint that includes MenuGroupId
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId, x.EntityType, x.FoodicsId })
                .IsUnique()
                .HasDatabaseName("IX_MenuItemMappings_Account_Branch_Group_Entity_FoodicsId");
            
            // Update TalabatRemoteCode unique constraint to include MenuGroupId
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId, x.TalabatRemoteCode })
                .IsUnique()
                .HasDatabaseName("IX_MenuItemMappings_Account_Branch_Group_TalabatCode");
        });

        // MenuGroupTalabatMapping configuration
        builder.Entity<MenuGroupTalabatMapping>(b =>
        {
            b.ToTable("MenuGroupTalabatMappings");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Unique constraint: One mapping per MenuGroup
            b.HasIndex(x => x.MenuGroupId)
                .IsUnique()
                .HasDatabaseName("IX_MenuGroupTalabatMappings_MenuGroupId");
            
            // Unique constraint: One TalabatMenuId per FoodicsAccount + TalabatVendorCode
            b.HasIndex(x => new { x.FoodicsAccountId, x.TalabatVendorCode, x.TalabatMenuId })
                .IsUnique()
                .HasDatabaseName("IX_MenuGroupTalabatMappings_Account_Vendor_MenuId");
            
            // Indexes for efficient querying
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_FoodicsAccountId");
            
            b.HasIndex(x => x.TalabatVendorCode)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_TalabatVendorCode");
            
            b.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_IsActive");
            
            b.HasIndex(x => x.MappingStrategy)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_MappingStrategy");
            
            b.HasIndex(x => x.SyncStatus)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_SyncStatus");
            
            b.HasIndex(x => x.Priority)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_Priority");
            
            b.HasIndex(x => x.MappingEstablishedAt)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_MappingEstablishedAt");
            
            b.HasIndex(x => x.LastVerifiedAt)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_LastVerifiedAt");
            
            b.HasIndex(x => x.IsTalabatValidated)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_IsTalabatValidated");
            
            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_MenuGroupTalabatMappings_TenantId");
            
            // Composite indexes for common queries
            b.HasIndex(x => new { x.FoodicsAccountId, x.IsActive })
                .HasDatabaseName("IX_MenuGroupTalabatMappings_Account_Active");
            
            b.HasIndex(x => new { x.TalabatVendorCode, x.IsActive })
                .HasDatabaseName("IX_MenuGroupTalabatMappings_Vendor_Active");
            
            b.HasIndex(x => new { x.SyncStatus, x.LastVerifiedAt })
                .HasDatabaseName("IX_MenuGroupTalabatMappings_Status_LastVerified");
            
            b.HasIndex(x => new { x.IsActive, x.Priority })
                .HasDatabaseName("IX_MenuGroupTalabatMappings_Active_Priority");
            
            // Foreign key to FoodicsAccount
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Foreign key to FoodicsMenuGroup
            b.HasOne(x => x.MenuGroup)
                .WithMany()
                .HasForeignKey(x => x.MenuGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.TalabatVendorCode)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatMenuId)
                .IsRequired()
                .HasMaxLength(200);
            
            b.Property(x => x.TalabatMenuName)
                .IsRequired()
                .HasMaxLength(500);
            
            b.Property(x => x.TalabatMenuDescription)
                .HasMaxLength(2000);
            
            b.Property(x => x.MappingStrategy)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.ConfigurationJson)
                .HasColumnType("TEXT");
            
            b.Property(x => x.TalabatInternalMenuId)
                .HasMaxLength(200);
            
            b.Property(x => x.SyncStatus)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.LastSyncError)
                .HasColumnType("TEXT");
        });

        // ModifierGroup configuration
        builder.Entity<ModifierGroup>(b =>
        {
            b.ToTable("ModifierGroups");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId })
                .HasDatabaseName("IX_ModifierGroups_Account_Branch_MenuGroup");
            
            b.HasIndex(x => x.FoodicsModifierGroupId)
                .HasDatabaseName("IX_ModifierGroups_FoodicsId");
            
            b.HasIndex(x => new { x.IsActive, x.IsSyncedToTalabat })
                .HasDatabaseName("IX_ModifierGroups_Active_Synced");
            
            // Foreign keys
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            b.HasOne(x => x.MenuGroup)
                .WithMany()
                .HasForeignKey(x => x.MenuGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            
            // Properties
            b.Property(x => x.FoodicsModifierGroupId)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(500);
            
            b.Property(x => x.NameLocalized)
                .HasMaxLength(500);
            
            b.Property(x => x.StructureHash)
                .IsRequired()
                .HasMaxLength(64);
            
            b.Property(x => x.TalabatVendorCode)
                .HasMaxLength(100);
        });

        // ModifierOption configuration
        builder.Entity<ModifierOption>(b =>
        {
            b.ToTable("ModifierOptions");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes
            b.HasIndex(x => x.ModifierGroupId)
                .HasDatabaseName("IX_ModifierOptions_ModifierGroup");
            
            b.HasIndex(x => x.FoodicsModifierOptionId)
                .HasDatabaseName("IX_ModifierOptions_FoodicsId");
            
            b.HasIndex(x => new { x.IsActive, x.IsSyncedToTalabat })
                .HasDatabaseName("IX_ModifierOptions_Active_Synced");
            
            // Foreign key
            b.HasOne(x => x.ModifierGroup)
                .WithMany(x => x.Options)
                .HasForeignKey(x => x.ModifierGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.FoodicsModifierOptionId)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(500);
            
            b.Property(x => x.NameLocalized)
                .HasMaxLength(500);
            
            b.Property(x => x.Price)
                .HasColumnType("decimal(18,4)");
            
            b.Property(x => x.PreviousPrice)
                .HasColumnType("decimal(18,4)");
            
            b.Property(x => x.ImageUrl)
                .HasMaxLength(1000);
            
            b.Property(x => x.PropertyHash)
                .IsRequired()
                .HasMaxLength(64);
        });

        // ModifierOptionPriceHistory configuration
        builder.Entity<ModifierOptionPriceHistory>(b =>
        {
            b.ToTable("ModifierOptionPriceHistory");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes
            b.HasIndex(x => x.ModifierOptionId)
                .HasDatabaseName("IX_ModifierPriceHistory_Option");
            
            b.HasIndex(x => x.ChangedAt)
                .HasDatabaseName("IX_ModifierPriceHistory_Date");
            
            b.HasIndex(x => new { x.ChangeType, x.ChangedAt })
                .HasDatabaseName("IX_ModifierPriceHistory_Type_Date");
            
            // Foreign key
            b.HasOne(x => x.ModifierOption)
                .WithMany(x => x.PriceHistory)
                .HasForeignKey(x => x.ModifierOptionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.OldPrice)
                .HasColumnType("decimal(18,4)");
            
            b.Property(x => x.NewPrice)
                .HasColumnType("decimal(18,4)");
            
            b.Property(x => x.ChangePercentage)
                .HasColumnType("decimal(10,4)");
            
            b.Property(x => x.ChangeAmount)
                .HasColumnType("decimal(18,4)");
            
            b.Property(x => x.ChangeType)
                .IsRequired()
                .HasMaxLength(50);
            
            b.Property(x => x.Reason)
                .HasMaxLength(1000);
            
            b.Property(x => x.ChangeSource)
                .HasMaxLength(100);
            
            b.Property(x => x.ChangedBy)
                .HasMaxLength(200);
        });

        // ModifierGroupVersion configuration
        builder.Entity<ModifierGroupVersion>(b =>
        {
            b.ToTable("ModifierGroupVersions");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes
            b.HasIndex(x => new { x.ModifierGroupId, x.Version })
                .HasDatabaseName("IX_ModifierGroupVersions_Group_Version");
            
            b.HasIndex(x => x.SnapshotDate)
                .HasDatabaseName("IX_ModifierGroupVersions_Date");
            
            // Foreign key
            b.HasOne(x => x.ModifierGroup)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.ModifierGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(500);
            
            b.Property(x => x.NameLocalized)
                .HasMaxLength(500);
            
            b.Property(x => x.StructureHash)
                .IsRequired()
                .HasMaxLength(64);
            
            b.Property(x => x.ChangeReason)
                .HasMaxLength(1000);
            
            b.Property(x => x.ChangedBy)
                .HasMaxLength(200);
        });

        // ModifierOptionVersion configuration
        builder.Entity<ModifierOptionVersion>(b =>
        {
            b.ToTable("ModifierOptionVersions");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes
            b.HasIndex(x => new { x.ModifierOptionId, x.Version })
                .HasDatabaseName("IX_ModifierOptionVersions_Option_Version");
            
            b.HasIndex(x => x.SnapshotDate)
                .HasDatabaseName("IX_ModifierOptionVersions_Date");
            
            // Foreign key
            b.HasOne(x => x.ModifierOption)
                .WithMany(x => x.Versions)
                .HasForeignKey(x => x.ModifierOptionId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(500);
            
            b.Property(x => x.NameLocalized)
                .HasMaxLength(500);
            
            b.Property(x => x.Price)
                .HasColumnType("decimal(18,4)");
            
            b.Property(x => x.ImageUrl)
                .HasMaxLength(1000);
            
            b.Property(x => x.PropertyHash)
                .IsRequired()
                .HasMaxLength(64);
            
            b.Property(x => x.ChangeReason)
                .HasMaxLength(1000);
            
            b.Property(x => x.ChangedBy)
                .HasMaxLength(200);
        });

        // ProductModifierAssignment configuration
        builder.Entity<ProductModifierAssignment>(b =>
        {
            b.ToTable("ProductModifierAssignments");
            
            b.ConfigureByConvention();
            
            // Primary key
            b.HasKey(x => x.Id);
            
            // Indexes
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId, x.FoodicsProductId })
                .HasDatabaseName("IX_ProductModifierAssignments_Context_Product");
            
            b.HasIndex(x => x.ModifierGroupId)
                .HasDatabaseName("IX_ProductModifierAssignments_ModifierGroup");
            
            b.HasIndex(x => new { x.IsActive, x.IsSyncedToTalabat })
                .HasDatabaseName("IX_ProductModifierAssignments_Active_Synced");
            
            // Foreign keys
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Cascade);
            
            b.HasOne(x => x.MenuGroup)
                .WithMany()
                .HasForeignKey(x => x.MenuGroupId)
                .OnDelete(DeleteBehavior.SetNull);
            
            b.HasOne(x => x.ModifierGroup)
                .WithMany(x => x.ProductAssignments)
                .HasForeignKey(x => x.ModifierGroupId)
                .OnDelete(DeleteBehavior.Cascade);
            
            // Properties
            b.Property(x => x.FoodicsProductId)
                .IsRequired()
                .HasMaxLength(100);
            
            b.Property(x => x.TalabatVendorCode)
                .HasMaxLength(100);
            
            // Unique constraint for active assignments
            b.HasIndex(x => new { x.FoodicsAccountId, x.BranchId, x.MenuGroupId, x.FoodicsProductId, x.ModifierGroupId })
                .IsUnique()
                .HasDatabaseName("IX_ProductModifierAssignments_Unique");
        });
    }
}
