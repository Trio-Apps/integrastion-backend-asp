using Foodics;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Data;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.MultiTenancy;
using Volo.Abp.TenantManagement.Smtp;

namespace Volo.Abp.TenantManagement.EntityFrameworkCore;

[IgnoreMultiTenancy]
[ConnectionStringName(AbpTenantManagementDbProperties.ConnectionStringName)]
public class TenantManagementDbContext : AbpDbContext<TenantManagementDbContext>, ITenantManagementDbContext
{
    public DbSet<Tenant> Tenants { get; set; }

    public DbSet<TenantConnectionString> TenantConnectionStrings { get; set; }
    public DbSet<FoodicsAccount> FoodicsAccounts { get; set; }
    public DbSet<SmtpConfig> SmtpConfigs { get; set; }

    public TenantManagementDbContext(DbContextOptions<TenantManagementDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.ConfigureTenantManagement();
    }
}
