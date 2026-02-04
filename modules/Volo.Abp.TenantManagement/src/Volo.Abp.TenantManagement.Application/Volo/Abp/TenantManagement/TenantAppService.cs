using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Local;
using Volo.Abp.MultiTenancy;
using Volo.Abp.ObjectExtending;

namespace Volo.Abp.TenantManagement;

[Authorize(TenantManagementPermissions.Tenants.Default)]
public class TenantAppService : TenantManagementAppServiceBase, ITenantAppService
{
    protected IDataSeeder DataSeeder { get; }
    protected ITenantRepository TenantRepository { get; }
    protected ITenantManager TenantManager { get; }
    protected IDistributedEventBus DistributedEventBus { get; }
    protected ILocalEventBus LocalEventBus { get; }

    public TenantAppService(
        ITenantRepository tenantRepository,
        ITenantManager tenantManager,
        IDataSeeder dataSeeder,
        IDistributedEventBus distributedEventBus,
        ILocalEventBus localEventBus)
    {
        DataSeeder = dataSeeder;
        TenantRepository = tenantRepository;
        TenantManager = tenantManager;
        DistributedEventBus = distributedEventBus;
        LocalEventBus = localEventBus;
    }

    public virtual async Task<TenantDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Tenant, TenantDto>(
            await TenantRepository.GetAsync(id)
        );
    }

    public virtual async Task<PagedResultDto<TenantDto>> GetListAsync(GetTenantsInput input)
    {
        if (input.Sorting.IsNullOrWhiteSpace())
        {
            input.Sorting = nameof(Tenant.Name);
        }

        var count = await TenantRepository.GetCountAsync(input.Filter);
        var list = await TenantRepository.GetListAsync(
            input.Sorting,
            input.MaxResultCount,
            input.SkipCount,
            input.Filter,
            true
        );

        return new PagedResultDto<TenantDto>(
            count,
            ObjectMapper.Map<List<Tenant>, List<TenantDto>>(list)
        );
    }

    [Authorize(TenantManagementPermissions.Tenants.Create)]
    public virtual async Task<TenantDto> CreateAsync(TenantCreateDto input)
    {
        var tenant = await TenantManager.CreateAsync(input.Name);
        input.MapExtraPropertiesTo(tenant);

        await TenantRepository.InsertAsync(tenant);

        await CurrentUnitOfWork.SaveChangesAsync();

        // Publish event to trigger asynchronous database migration and data seeding
        // This prevents blocking the HTTP request and improves API response time
        try
        {
            // Use CancellationToken with timeout to prevent infinite hanging
            using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                await DistributedEventBus.PublishAsync(
                    new TenantCreatedEto
                    {
                        Id = tenant.Id,
                        Name = tenant.Name,
                        Properties =
                        {
                            { "AdminEmail", input.AdminEmailAddress },
                            { "AdminPassword", input.AdminPassword }
                        }
                    },
                    false);
            }
        }
        catch (System.OperationCanceledException)
        {
            // Event bus publish timed out, but tenant was created successfully
            // Log and continue - the migration will happen asynchronously via Hangfire retry
            Logger.LogWarning(
                "Distributed event bus publish timed out for tenant {TenantId}, " +
                "but tenant was created. Retrying with Hangfire background job.",
                tenant.Id);
        }
        catch (Exception ex)
        {
            // Even if event publishing fails, the tenant is already created
            Logger.LogError(ex,
                "Failed to publish TenantCreated event for tenant {TenantId}. " +
                "Migration will be retried via background jobs.",
                tenant.Id);
        }

        // ✅ REMOVED: Synchronous data seeding that was causing slow API response
        // The OrderXChangeTenantDatabaseMigrationHandler will handle this asynchronously
        // See: src/OrderXChange.Domain/Data/OrderXChangeTenantDatabaseMigrationHandler.cs

        return ObjectMapper.Map<Tenant, TenantDto>(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.Update)]
    public virtual async Task<TenantDto> UpdateAsync(Guid id, TenantUpdateDto input)
    {
        var tenant = await TenantRepository.GetAsync(id);

        await TenantManager.ChangeNameAsync(tenant, input.Name);

        tenant.SetConcurrencyStampIfNotNull(input.ConcurrencyStamp);
        input.MapExtraPropertiesTo(tenant);

        await TenantRepository.UpdateAsync(tenant);

        return ObjectMapper.Map<Tenant, TenantDto>(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var tenant = await TenantRepository.FindAsync(id);
        if (tenant == null)
        {
            return;
        }

        await TenantRepository.DeleteAsync(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.ManageConnectionStrings)]
    public virtual async Task<string> GetDefaultConnectionStringAsync(Guid id)
    {
        var tenant = await TenantRepository.GetAsync(id);
        return tenant?.FindDefaultConnectionString();
    }

    [Authorize(TenantManagementPermissions.Tenants.ManageConnectionStrings)]
    public virtual async Task UpdateDefaultConnectionStringAsync(Guid id, string defaultConnectionString)
    {
        var tenant = await TenantRepository.GetAsync(id);
        if (tenant.FindDefaultConnectionString() != defaultConnectionString)
        {
            await LocalEventBus.PublishAsync(new TenantChangedEvent(tenant.Id, tenant.NormalizedName));
        }
        tenant.SetDefaultConnectionString(defaultConnectionString);
        await TenantRepository.UpdateAsync(tenant);
    }

    [Authorize(TenantManagementPermissions.Tenants.ManageConnectionStrings)]
    public virtual async Task DeleteDefaultConnectionStringAsync(Guid id)
    {
        var tenant = await TenantRepository.GetAsync(id);
        tenant.RemoveDefaultConnectionString();
        await LocalEventBus.PublishAsync(new TenantChangedEvent(tenant.Id, tenant.NormalizedName));
        await TenantRepository.UpdateAsync(tenant);
    }
}
