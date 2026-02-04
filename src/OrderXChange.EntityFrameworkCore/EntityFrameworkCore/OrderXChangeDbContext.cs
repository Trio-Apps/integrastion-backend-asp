using Microsoft.EntityFrameworkCore;
using Volo.Abp.AuditLogging.EntityFrameworkCore;
using Volo.Abp.BackgroundJobs.EntityFrameworkCore;
using Volo.Abp.BlobStoring.Database.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.FeatureManagement.EntityFrameworkCore;
using Volo.Abp.Identity;
using Volo.Abp.Identity.EntityFrameworkCore;
using Volo.Abp.PermissionManagement.EntityFrameworkCore;
using Volo.Abp.SettingManagement.EntityFrameworkCore;
using Volo.Abp.OpenIddict.EntityFrameworkCore;
using Volo.Abp.TenantManagement.EntityFrameworkCore;
using Volo.Abp.TenantManagement;
using Foodics;
using Volo.Abp.TenantManagement.Talabat;
using OrderXChange.Domain.Staging;
using OrderXChange.Domain.Versioning;
using OrderXChange.Idempotency;

namespace OrderXChange.EntityFrameworkCore;

[ReplaceDbContext(typeof(IIdentityDbContext))]
[ReplaceDbContext(typeof(ITenantManagementDbContext))]
[ConnectionStringName("Default")]
public class OrderXChangeDbContext :
    AbpDbContext<OrderXChangeDbContext>,
    ITenantManagementDbContext,
    IIdentityDbContext
{
    /* Add DbSet properties for your Aggregate Roots / Entities here. */


    #region Entities from the modules

    /* Notice: We only implemented IIdentityProDbContext and ISaasDbContext
     * and replaced them for this DbContext. This allows you to perform JOIN
     * queries for the entities of these modules over the repositories easily. You
     * typically don't need that for other modules. But, if you need, you can
     * implement the DbContext interface of the needed module and use ReplaceDbContext
     * attribute just like IIdentityProDbContext and ISaasDbContext.
     *
     * More info: Replacing a DbContext of a module ensures that the related module
     * uses this DbContext on runtime. Otherwise, it will use its own DbContext class.
     */

    // Identity
    public DbSet<IdentityUser> Users { get; set; }
    public DbSet<IdentityRole> Roles { get; set; }
    public DbSet<IdentityClaimType> ClaimTypes { get; set; }
    public DbSet<OrganizationUnit> OrganizationUnits { get; set; }
    public DbSet<IdentitySecurityLog> SecurityLogs { get; set; }
    public DbSet<IdentityLinkUser> LinkUsers { get; set; }
    public DbSet<IdentityUserDelegation> UserDelegations { get; set; }
    public DbSet<IdentitySession> Sessions { get; set; }

    public DbSet<Tenant> Tenants { get; set; }

    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }

    public DbSet<FoodicsAccount> FoodicsAccounts { get; set; }
    public DbSet<TalabatAccount> TalabatAccounts { get; set; }

    // Staging tables
    public DbSet<FoodicsProductStaging> FoodicsProductStaging { get; set; }
    
    // Talabat sync tracking
    public DbSet<TalabatCatalogSyncLog> TalabatCatalogSyncLogs { get; set; }
    
    // Dead Letter Queue
    public DbSet<DlqMessage> DlqMessages { get; set; }
    
    // Idempotency
    public DbSet<IdempotencyRecord> IdempotencyRecords { get; set; }
    
    // Menu Versioning
    public DbSet<MenuSnapshot> MenuSnapshots { get; set; }
    public DbSet<MenuChangeLog> MenuChangeLogs { get; set; }
    public DbSet<MenuDelta> MenuDeltas { get; set; }
    public DbSet<MenuSyncRun> MenuSyncRuns { get; set; }
    public DbSet<MenuSyncRunStep> MenuSyncRunSteps { get; set; }
    public DbSet<MenuItemMapping> MenuItemMappings { get; set; }
    public DbSet<FoodicsMenuGroup> FoodicsMenuGroups { get; set; }
    public DbSet<MenuGroupCategory> MenuGroupCategories { get; set; }
    public DbSet<MenuGroupTalabatMapping> MenuGroupTalabatMappings { get; set; }
    
    // Modifier Lifecycle
    public DbSet<ModifierGroup> ModifierGroups { get; set; }
    public DbSet<ModifierOption> ModifierOptions { get; set; }
    public DbSet<ModifierOptionPriceHistory> ModifierOptionPriceHistory { get; set; }
    public DbSet<ModifierGroupVersion> ModifierGroupVersions { get; set; }
    public DbSet<ModifierOptionVersion> ModifierOptionVersions { get; set; }
    public DbSet<ProductModifierAssignment> ProductModifierAssignments { get; set; }
    #endregion

    public OrderXChangeDbContext(DbContextOptions<OrderXChangeDbContext> options)
        : base(options)
    {

    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        /* Include modules to your migration db context */

        builder.ConfigurePermissionManagement();
        builder.ConfigureSettingManagement();
        builder.ConfigureBackgroundJobs();
        builder.ConfigureAuditLogging();
        builder.ConfigureFeatureManagement();
        builder.ConfigureIdentity();
        builder.ConfigureOpenIddict();
        builder.ConfigureBlobStoring();

        builder.ConfigureTenantManagement();
        
        /* Configure your own tables/entities inside here */
        
        // Configure Menu Versioning entities
        builder.ConfigureMenuVersioning();

        // Configure TalabatAccount entity
        // UPDATED: Added FoodicsAccount relationship
        builder.Entity<TalabatAccount>(b =>
        {
            b.ToTable("TalabatAccounts");
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_TalabatAccounts_TenantId");

            // Enforce uniqueness per tenant (a tenant can't have two Talabat vendors with same VendorCode)
            b.HasIndex(x => new { x.TenantId, x.VendorCode })
                .IsUnique()
                .HasDatabaseName("IX_TalabatAccounts_TenantId_VendorCode");

            // Keep a non-unique index on VendorCode for quick lookups (optional but useful)
            b.HasIndex(x => x.VendorCode)
                .HasDatabaseName("IX_TalabatAccounts_VendorCode");

            b.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_TalabatAccounts_IsActive");
            
            // NEW: Index for FoodicsAccountId foreign key
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_TalabatAccounts_FoodicsAccountId");

            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.VendorCode).IsRequired().HasMaxLength(50);
            b.Property(x => x.ChainCode).HasMaxLength(50);
            b.Property(x => x.ApiKey).HasMaxLength(200);
            b.Property(x => x.ApiSecret).HasMaxLength(200);
            b.Property(x => x.UserName).HasMaxLength(100);
            b.Property(x => x.PlatformKey).HasMaxLength(50);
            b.Property(x => x.PlatformRestaurantId).IsRequired().HasMaxLength(100);

            // Relationship to Tenant
            b.HasOne<Tenant>()
                .WithMany(t => t.TalabatAccounts)
                .HasForeignKey(x => x.TenantId);
            
            // NEW: Relationship to FoodicsAccount (optional link)
            // This establishes which Foodics data should be synced to this Talabat vendor
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.SetNull); // If FoodicsAccount deleted, set FK to null (don't cascade)
        });

        // Configure FoodicsProductStaging entity
        builder.Entity<FoodicsProductStaging>(b =>
        {
            b.ToTable(OrderXChangeConsts.DbTablePrefix + "FoodicsProductStaging", OrderXChangeConsts.DbSchema);
            b.ConfigureByConvention(); //auto configure for the base class props

            // Unique index: One product per FoodicsAccount (FoodicsAccountId + FoodicsProductId)
            b.HasIndex(x => new { x.FoodicsAccountId, x.FoodicsProductId })
                .IsUnique()
                .HasDatabaseName("IX_FoodicsProductStaging_Account_Product");

            // Indexes for common queries
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_FoodicsProductStaging_FoodicsAccountId");

            b.HasIndex(x => x.FoodicsProductId)
                .HasDatabaseName("IX_FoodicsProductStaging_FoodicsProductId");

            b.HasIndex(x => x.SyncDate)
                .HasDatabaseName("IX_FoodicsProductStaging_SyncDate");

            b.HasIndex(x => x.BranchId)
                .HasDatabaseName("IX_FoodicsProductStaging_BranchId");

            b.HasIndex(x => x.CategoryId)
                .HasDatabaseName("IX_FoodicsProductStaging_CategoryId");

            b.HasIndex(x => x.IsActive)
                .HasDatabaseName("IX_FoodicsProductStaging_IsActive");

            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_FoodicsProductStaging_TenantId");

            // Talabat sync status indexes
            b.HasIndex(x => x.TalabatSyncStatus)
                .HasDatabaseName("IX_FoodicsProductStaging_TalabatSyncStatus");

            b.HasIndex(x => x.TalabatImportId)
                .HasDatabaseName("IX_FoodicsProductStaging_TalabatImportId");

            b.HasIndex(x => x.TalabatSubmittedAt)
                .HasDatabaseName("IX_FoodicsProductStaging_TalabatSubmittedAt");

            b.HasIndex(x => x.TalabatVendorCode)
                .HasDatabaseName("IX_FoodicsProductStaging_TalabatVendorCode");

            // Foreign key relationship to FoodicsAccount
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Restrict); // Prevent cascade delete to preserve staging data

            // Property configurations
            b.Property(x => x.Name).IsRequired();
            b.Property(x => x.FoodicsProductId).IsRequired().HasMaxLength(100);
            b.Property(x => x.FoodicsAccountId).IsRequired();
        });

        // Configure IdempotencyRecord entity
        builder.Entity<IdempotencyRecord>(b =>
        {
            b.ToTable(OrderXChangeConsts.DbTablePrefix + "IdempotencyRecords", OrderXChangeConsts.DbSchema);
            
            // Composite primary key
            b.HasKey(x => new { x.AccountId, x.IdempotencyKey });
            
            // Indexes for common queries
            b.HasIndex(x => x.AccountId)
                .HasDatabaseName("IX_IdempotencyRecords_AccountId");
            
            b.HasIndex(x => x.LastProcessedUtc)
                .HasDatabaseName("IX_IdempotencyRecords_LastProcessedUtc");
            
            b.HasIndex(x => x.ExpiresAt)
                .HasDatabaseName("IX_IdempotencyRecords_ExpiresAt");
            
            b.HasIndex(x => x.Status)
                .HasDatabaseName("IX_IdempotencyRecords_Status");
            
            // Property configurations
            b.Property(x => x.AccountId).IsRequired();
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(500);
            b.Property(x => x.Status).IsRequired();
            b.Property(x => x.FirstSeenUtc).IsRequired();
            b.Property(x => x.LastProcessedUtc).IsRequired();
            b.Property(x => x.ResultHash).HasMaxLength(100);
        });

        // Configure TalabatCatalogSyncLog entity
        builder.Entity<TalabatCatalogSyncLog>(b =>
        {
            b.ToTable(OrderXChangeConsts.DbTablePrefix + "TalabatCatalogSyncLogs", OrderXChangeConsts.DbSchema);
            b.ConfigureByConvention();

            // Indexes for common queries
            b.HasIndex(x => x.FoodicsAccountId)
                .HasDatabaseName("IX_TalabatCatalogSyncLogs_FoodicsAccountId");

            b.HasIndex(x => x.VendorCode)
                .HasDatabaseName("IX_TalabatCatalogSyncLogs_VendorCode");

            b.HasIndex(x => x.ImportId)
                .HasDatabaseName("IX_TalabatCatalogSyncLogs_ImportId");

            b.HasIndex(x => x.Status)
                .HasDatabaseName("IX_TalabatCatalogSyncLogs_Status");

            b.HasIndex(x => x.SubmittedAt)
                .HasDatabaseName("IX_TalabatCatalogSyncLogs_SubmittedAt");

            b.HasIndex(x => x.CorrelationId)
                .HasDatabaseName("IX_TalabatCatalogSyncLogs_CorrelationId");

            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_TalabatCatalogSyncLogs_TenantId");

            // Foreign key relationship to FoodicsAccount
            b.HasOne(x => x.FoodicsAccount)
                .WithMany()
                .HasForeignKey(x => x.FoodicsAccountId)
                .OnDelete(DeleteBehavior.Restrict);

            // Property configurations
            b.Property(x => x.VendorCode).IsRequired().HasMaxLength(100);
            b.Property(x => x.Status).IsRequired().HasMaxLength(50);
        });

        // Configure DlqMessage entity
        builder.Entity<DlqMessage>(b =>
        {
            b.ToTable(OrderXChangeConsts.DbTablePrefix + "DlqMessages", OrderXChangeConsts.DbSchema);
            b.ConfigureByConvention();

            // Indexes for common queries
            b.HasIndex(x => x.EventType)
                .HasDatabaseName("IX_DlqMessages_EventType");

            b.HasIndex(x => x.CorrelationId)
                .HasDatabaseName("IX_DlqMessages_CorrelationId");

            b.HasIndex(x => x.AccountId)
                .HasDatabaseName("IX_DlqMessages_AccountId");

            b.HasIndex(x => x.FailureType)
                .HasDatabaseName("IX_DlqMessages_FailureType");

            b.HasIndex(x => x.IsReplayed)
                .HasDatabaseName("IX_DlqMessages_IsReplayed");

            b.HasIndex(x => x.IsAcknowledged)
                .HasDatabaseName("IX_DlqMessages_IsAcknowledged");

            b.HasIndex(x => x.LastAttemptUtc)
                .HasDatabaseName("IX_DlqMessages_LastAttemptUtc");

            b.HasIndex(x => x.Priority)
                .HasDatabaseName("IX_DlqMessages_Priority");

            b.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_DlqMessages_TenantId");

            // Composite index for filtering pending messages
            b.HasIndex(x => new { x.IsReplayed, x.IsAcknowledged, x.Priority })
                .HasDatabaseName("IX_DlqMessages_Pending");

            // Property configurations
            b.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            b.Property(x => x.CorrelationId).IsRequired().HasMaxLength(100);
            b.Property(x => x.ErrorCode).IsRequired().HasMaxLength(200);
            b.Property(x => x.FailureType).IsRequired().HasMaxLength(50);
            b.Property(x => x.Priority).IsRequired().HasMaxLength(20);
        });
    }
}
