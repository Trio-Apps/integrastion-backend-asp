using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using OrderXChange.Integrations.Foodics;

namespace OrderXChange.Application.Integrations.Foodics;

/// <summary>
/// Small Hangfire-friendly publisher used to emit retry events after a delay.
/// This maps to logical Kafka retry topics (menu.sync.retry.*).
/// </summary>
public class MenuSyncRetryPublisher : ITransientDependency
{
    private readonly IDistributedEventBus _eventBus;

    public MenuSyncRetryPublisher(IDistributedEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public Task PublishRetryAsync(MenuSyncRetryEto eventData)
    {
        return _eventBus.PublishAsync(eventData);
    }
}


