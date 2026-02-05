using Foodics;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement.Smtp;

namespace Volo.Abp.TenantManagement.EntityFrameworkCore;

[IgnoreMultiTenancy]
[ConnectionStringName(AbpTenantManagementDbProperties.ConnectionStringName)]
public interface ITenantManagementDbContext : IEfCoreDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<TenantConnectionString> TenantConnectionStrings { get; }
    DbSet<FoodicsAccount> FoodicsAccounts { get; }
    DbSet<SmtpConfig> SmtpConfigs { get; }
}
