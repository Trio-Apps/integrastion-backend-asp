using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace OrderXChange.EntityFrameworkCore;

/* This class is needed for EF Core console commands
 * (like Add-Migration and Update-Database commands) */
public class OrderXChangeDbContextFactory : IDesignTimeDbContextFactory<OrderXChangeDbContext>
{
    public OrderXChangeDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        
        OrderXChangeEfCoreEntityExtensionMappings.Configure();

        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' not found in configuration.");
        }

        var builder = new DbContextOptionsBuilder<OrderXChangeDbContext>()
            .UseMySql(
                connectionString,
                MySqlServerVersion.LatestSupportedServerVersion);
        
        return new OrderXChangeDbContext(builder.Options);
    }

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../OrderXChange.DbMigrator/"))
            .AddJsonFile("appsettings.json", optional: false);

        return builder.Build();
    }
}
