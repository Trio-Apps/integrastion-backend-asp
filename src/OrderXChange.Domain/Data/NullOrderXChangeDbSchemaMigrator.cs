using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Data;

/* This is used if database provider does't define
 * IOrderXChangeDbSchemaMigrator implementation.
 */
public class NullOrderXChangeDbSchemaMigrator : IOrderXChangeDbSchemaMigrator, ITransientDependency
{
    public Task MigrateAsync()
    {
        return Task.CompletedTask;
    }
}
