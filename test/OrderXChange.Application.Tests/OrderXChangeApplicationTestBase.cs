using Volo.Abp.Modularity;

namespace OrderXChange;

public abstract class OrderXChangeApplicationTestBase<TStartupModule> : OrderXChangeTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
