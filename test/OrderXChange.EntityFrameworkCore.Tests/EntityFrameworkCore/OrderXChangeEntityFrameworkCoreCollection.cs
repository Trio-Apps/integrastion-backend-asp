using Xunit;

namespace OrderXChange.EntityFrameworkCore;

[CollectionDefinition(OrderXChangeTestConsts.CollectionDefinitionName)]
public class OrderXChangeEntityFrameworkCoreCollection : ICollectionFixture<OrderXChangeEntityFrameworkCoreFixture>
{

}
