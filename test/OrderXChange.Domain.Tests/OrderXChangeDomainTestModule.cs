using Volo.Abp.Modularity;

namespace OrderXChange;

[DependsOn(
    typeof(OrderXChangeDomainModule),
    typeof(OrderXChangeTestBaseModule)
)]
public class OrderXChangeDomainTestModule : AbpModule
{

}
