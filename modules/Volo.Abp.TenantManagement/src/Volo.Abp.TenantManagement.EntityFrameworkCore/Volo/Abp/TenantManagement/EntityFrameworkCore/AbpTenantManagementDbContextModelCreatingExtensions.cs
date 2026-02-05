using Foodics;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore.Modeling;
using Volo.Abp.TenantManagement.Smtp;

namespace Volo.Abp.TenantManagement.EntityFrameworkCore;

public static class AbpTenantManagementDbContextModelCreatingExtensions
{
    public static void ConfigureTenantManagement(
        this ModelBuilder builder)
    {
        Check.NotNull(builder, nameof(builder));

        if (builder.IsTenantOnlyDatabase())
        {
            return;
        }

        builder.Entity<Tenant>(b =>
        {
            b.ToTable(AbpTenantManagementDbProperties.DbTablePrefix + "Tenants", AbpTenantManagementDbProperties.DbSchema);

            b.ConfigureByConvention();

            b.Property(t => t.Name).IsRequired().HasMaxLength(TenantConsts.MaxNameLength);
            b.Property(t => t.NormalizedName).IsRequired().HasMaxLength(TenantConsts.MaxNameLength);

            b.HasMany(u => u.ConnectionStrings).WithOne().HasForeignKey(uc => uc.TenantId).IsRequired();

            b.HasIndex(u => u.Name);
            b.HasIndex(u => u.NormalizedName);

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<TenantConnectionString>(b =>
        {
            b.ToTable(AbpTenantManagementDbProperties.DbTablePrefix + "TenantConnectionStrings", AbpTenantManagementDbProperties.DbSchema);

            b.ConfigureByConvention();

            b.HasKey(x => new { x.TenantId, x.Name });

            b.Property(cs => cs.Name).IsRequired().HasMaxLength(TenantConnectionStringConsts.MaxNameLength);
            b.Property(cs => cs.Value).IsRequired().HasMaxLength(TenantConnectionStringConsts.MaxValueLength);

            b.ApplyObjectExtensionMappings();
        });

        builder.Entity<FoodicsAccount>(b =>
        {
            b.ToTable("FoodicsAccounts");
            b.ConfigureByConvention();
        });

        builder.Entity<SmtpConfig>(b =>
        {
            b.ToTable("SmtpConfigs");
            b.ConfigureByConvention();

            b.HasIndex(x => x.TenantId)
                .IsUnique()
                .HasDatabaseName("IX_SmtpConfigs_TenantId");

            b.Property(x => x.Host).IsRequired().HasMaxLength(256);
            b.Property(x => x.UserName).IsRequired().HasMaxLength(256);
            b.Property(x => x.Password).IsRequired().HasMaxLength(512);
            b.Property(x => x.FromName).IsRequired().HasMaxLength(256);
            b.Property(x => x.FromEmail).IsRequired().HasMaxLength(256);
            b.Property(x => x.Port).IsRequired();
            b.Property(x => x.EnableSsl).IsRequired();
            b.Property(x => x.UseStartTls).IsRequired();
        });

        builder.TryConfigureObjectExtensions<TenantManagementDbContext>();
    }
}
