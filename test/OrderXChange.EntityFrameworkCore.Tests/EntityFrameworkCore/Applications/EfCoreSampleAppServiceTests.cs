using OrderXChange.Samples;
using Xunit;

namespace OrderXChange.EntityFrameworkCore.Applications;

[Collection(OrderXChangeTestConsts.CollectionDefinitionName)]
public class EfCoreSampleAppServiceTests : SampleAppServiceTests<OrderXChangeEntityFrameworkCoreTestModule>
{

}
