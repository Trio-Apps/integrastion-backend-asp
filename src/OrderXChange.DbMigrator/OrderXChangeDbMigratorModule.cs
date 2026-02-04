using OrderXChange.EntityFrameworkCore;
using Volo.Abp.Autofac;
using Volo.Abp.Modularity;

namespace OrderXChange.DbMigrator;

[DependsOn(
    typeof(AbpAutofacModule),
    typeof(OrderXChangeEntityFrameworkCoreModule),
    typeof(OrderXChangeApplicationContractsModule)
)]
public class OrderXChangeDbMigratorModule : AbpModule
{
}
