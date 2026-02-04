using Volo.Abp.Modularity;

namespace OrderXChange;

[DependsOn(
    typeof(OrderXChangeApplicationModule),
    typeof(OrderXChangeDomainTestModule)
)]
public class OrderXChangeApplicationTestModule : AbpModule
{

}
