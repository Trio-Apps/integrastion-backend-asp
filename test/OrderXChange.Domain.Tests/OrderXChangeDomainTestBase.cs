using Volo.Abp.Modularity;

namespace OrderXChange;

/* Inherit from this class for your domain layer tests. */
public abstract class OrderXChangeDomainTestBase<TStartupModule> : OrderXChangeTestBase<TStartupModule>
    where TStartupModule : IAbpModule
{

}
