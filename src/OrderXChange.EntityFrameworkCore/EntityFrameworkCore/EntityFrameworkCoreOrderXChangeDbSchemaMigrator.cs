using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderXChange.Data;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.EntityFrameworkCore;

public class EntityFrameworkCoreOrderXChangeDbSchemaMigrator
    : IOrderXChangeDbSchemaMigrator, ITransientDependency
{
    private readonly IServiceProvider _serviceProvider;

    public EntityFrameworkCoreOrderXChangeDbSchemaMigrator(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task MigrateAsync()
    {
        /* We intentionally resolving the OrderXChangeDbContext
         * from IServiceProvider (instead of directly injecting it)
         * to properly get the connection string of the current tenant in the
         * current scope.
         */

        await _serviceProvider
            .GetRequiredService<OrderXChangeDbContext>()
            .Database
            .MigrateAsync();
    }
}
