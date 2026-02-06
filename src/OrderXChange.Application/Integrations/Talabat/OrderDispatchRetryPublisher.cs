using System.Threading.Tasks;
using OrderXChange.Integrations.Talabat;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;

namespace OrderXChange.Application.Integrations.Talabat;

public class OrderDispatchRetryPublisher : ITransientDependency
{
    private readonly IDistributedEventBus _eventBus;

    public OrderDispatchRetryPublisher(IDistributedEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public Task PublishRetryAsync(OrderDispatchRetryEto eventData)
    {
        return _eventBus.PublishAsync(eventData);
    }
}
