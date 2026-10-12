using OrderXChange.Search;
using Xunit;

namespace OrderXChange.EntityFrameworkCore.Applications;

[Collection(OrderXChangeTestConsts.CollectionDefinitionName)]
public class EfCoreSmartSearchAppServiceTests : SmartSearchAppServiceTests<OrderXChangeEntityFrameworkCoreTestModule>
{
}
