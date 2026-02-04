using OrderXChange.Samples;
using Xunit;

namespace OrderXChange.EntityFrameworkCore.Domains;

[Collection(OrderXChangeTestConsts.CollectionDefinitionName)]
public class EfCoreSampleDomainTests : SampleDomainTests<OrderXChangeEntityFrameworkCoreTestModule>
{

}
