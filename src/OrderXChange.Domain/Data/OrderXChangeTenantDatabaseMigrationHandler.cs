using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Identity;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Uow;

namespace OrderXChange.Data;

public class OrderXChangeTenantDatabaseMigrationHandler :
    IDistributedEventHandler<TenantCreatedEto>,
    IDistributedEventHandler<TenantConnectionStringUpdatedEto>,
    IDistributedEventHandler<ApplyDatabaseMigrationsEto>,
    ITransientDependency
{
    private readonly IEnumerable<IOrderXChangeDbSchemaMigrator> _dbSchemaMigrators;
    private readonly ICurrentTenant _currentTenant;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly IDataSeeder _dataSeeder;
    private readonly ITenantStore _tenantStore;
    private readonly IdentityUserManager _identityUserManager;
    private readonly ILogger<OrderXChangeTenantDatabaseMigrationHandler> _logger;

    public OrderXChangeTenantDatabaseMigrationHandler(
        IEnumerable<IOrderXChangeDbSchemaMigrator> dbSchemaMigrators,
        ICurrentTenant currentTenant,
        IUnitOfWorkManager unitOfWorkManager,
        IDataSeeder dataSeeder,
        ITenantStore tenantStore,
        IdentityUserManager identityUserManager,
        ILogger<OrderXChangeTenantDatabaseMigrationHandler> logger)
    {
        _dbSchemaMigrators = dbSchemaMigrators;
        _currentTenant = currentTenant;
        _unitOfWorkManager = unitOfWorkManager;
        _dataSeeder = dataSeeder;
        _tenantStore = tenantStore;
        _identityUserManager = identityUserManager;
        _logger = logger;
    }

    public async Task HandleEventAsync(TenantCreatedEto eventData)
    {
        await MigrateAndSeedForTenantAsync(
            eventData.Id,
            eventData.Properties.GetOrDefault("AdminEmail") ?? OrderXChangeConsts.AdminEmailDefaultValue,
            eventData.Properties.GetOrDefault("AdminPassword") ?? OrderXChangeConsts.AdminPasswordDefaultValue,
            shouldSeedData: true
        );
    }

    public async Task HandleEventAsync(TenantConnectionStringUpdatedEto eventData)
    {
        if (eventData.ConnectionStringName != ConnectionStrings.DefaultConnectionStringName ||
            eventData.NewValue.IsNullOrWhiteSpace())
        {
            return;
        }

        await MigrateAndSeedForTenantAsync(
            eventData.Id,
            shouldSeedData: false
        );

        /* You may want to move your data from the old database to the new database!
         * It is up to you. If you don't make it, new database will be empty
         * (and tenant's admin password is reset to 1q2w3E*).
         */
    }

    public async Task HandleEventAsync(ApplyDatabaseMigrationsEto eventData)
    {
        if (eventData.TenantId == null)
        {
            return;
        }

        await MigrateAndSeedForTenantAsync(
            eventData.TenantId.Value,
            shouldSeedData: false
        );
    }

    private async Task MigrateAndSeedForTenantAsync(
        Guid tenantId,
        string? adminEmail = null,
        string? adminPassword = null,
        bool shouldSeedData = true)
    {
        try
        {
            using (_currentTenant.Change(tenantId))
            {
                _logger.LogInformation("🔄 Starting database migration for tenant {TenantId}", tenantId);

                // Create database tables if needed
                using (var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: false))
                {
                    var tenantConfiguration = await _tenantStore.FindAsync(tenantId);
                    if (tenantConfiguration?.ConnectionStrings != null &&
                        !tenantConfiguration.ConnectionStrings.Default.IsNullOrWhiteSpace())
                    {
                        foreach (var migrator in _dbSchemaMigrators)
                        {
                            _logger.LogInformation("Running migrator: {MigratorType}", migrator.GetType().Name);
                            await migrator.MigrateAsync();
                        }

                        _logger.LogInformation("✅ Database migration completed for tenant {TenantId}", tenantId);
                    }
                    else
                    {
                        _logger.LogWarning("⚠️ No connection string found for tenant {TenantId}", tenantId);
                    }

                    await uow.CompleteAsync();
                }

                if (shouldSeedData)
                {
                    // Seed data only during TenantCreated flow to avoid resetting credentials on migration events.
                    _logger.LogInformation("🌱 Starting data seeding for tenant {TenantId}", tenantId);
                    
                    try
                    {
                        var seedContext = new DataSeedContext(tenantId);

                        if (!adminEmail.IsNullOrWhiteSpace())
                        {
                            seedContext.WithProperty(IdentityDataSeedContributor.AdminEmailPropertyName, adminEmail);
                        }

                        if (!adminPassword.IsNullOrWhiteSpace())
                        {
                            seedContext.WithProperty(IdentityDataSeedContributor.AdminPasswordPropertyName, adminPassword);
                        }

                        await _dataSeeder.SeedAsync(
                            seedContext
                        );

                        if (!adminEmail.IsNullOrWhiteSpace() && !adminPassword.IsNullOrWhiteSpace())
                        {
                            await EnsureAdminCredentialsAsync(tenantId, adminEmail, adminPassword);
                        }
                        
                        _logger.LogInformation("✅ Data seeding completed for tenant {TenantId}", tenantId);
                    }
                    catch (Exception seedEx)
                    {
                        _logger.LogError(seedEx, 
                            "❌ Error during data seeding for tenant {TenantId}. This may result in missing admin user.", 
                            tenantId);
                        // Don't throw - seeding failure should not block tenant creation
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, 
                "❌ Critical error during tenant migration/seeding for {TenantId}. " +
                "Tenant was created but may need manual setup.",
                tenantId);
            // Don't throw - this is handled asynchronously, API already returned
        }
    }

    private async Task EnsureAdminCredentialsAsync(
        Guid tenantId,
        string adminEmail,
        string adminPassword)
    {
        var user = _identityUserManager.Users
            .FirstOrDefault(u => u.TenantId == tenantId && u.Email == adminEmail);

        user ??= _identityUserManager.Users
            .FirstOrDefault(u => u.TenantId == tenantId && u.UserName == "admin");

        if (user == null)
        {
            _logger.LogWarning(
                "Admin user for tenant {TenantId} was not found after seeding; password enforcement skipped. AdminEmail={AdminEmail}",
                tenantId,
                adminEmail);
            return;
        }

        if (!string.Equals(user.UserName, adminEmail, StringComparison.OrdinalIgnoreCase))
        {
            var setUserNameResult = await _identityUserManager.SetUserNameAsync(user, adminEmail);
            if (!setUserNameResult.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to set tenant admin username to email for tenant {TenantId}: {Errors}",
                    tenantId,
                    string.Join("; ", setUserNameResult.Errors.Select(e => e.Description))
                );
            }
        }

        var resetToken = await _identityUserManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _identityUserManager.ResetPasswordAsync(user, resetToken, adminPassword);
        if (!resetResult.Succeeded)
        {
            _logger.LogWarning(
                "Failed to reset admin password for {AdminEmail} in tenant {TenantId}: {Errors}",
                adminEmail,
                tenantId,
                string.Join("; ", resetResult.Errors.Select(e => e.Description))
            );
        }
        else if (user.ShouldChangePasswordOnNextLogin)
        {
            user.SetShouldChangePasswordOnNextLogin(false);
            var updateResult = await _identityUserManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning(
                    "Failed to clear password-change flag for admin {AdminEmail} in tenant {TenantId}: {Errors}",
                    adminEmail,
                    tenantId,
                    string.Join("; ", updateResult.Errors.Select(e => e.Description))
                );
            }
        }

        // NOTE:
        // We intentionally do NOT set ShouldChangePasswordOnNextLogin here,
        // because it can block password-grant login flow for tenant users.
    }
}
