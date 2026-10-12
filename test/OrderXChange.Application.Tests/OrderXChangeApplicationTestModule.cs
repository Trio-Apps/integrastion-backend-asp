using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.EventBus.Kafka;
using Volo.Abp.Kafka;
using Volo.Abp.Modularity;

namespace OrderXChange;

[DependsOn(
    typeof(OrderXChangeApplicationModule),
    typeof(OrderXChangeDomainTestModule)
)]
public class OrderXChangeApplicationTestModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // OrderXChangeApplicationModule depends on AbpEventBusKafkaModule, but its options are
        // only configured in the HTTP host. Without them the module's initialization throws
        // (topicName is null). Supply the minimal required config so the Kafka module can
        // initialize; it is never used to move messages in tests (see the event-bus swap below).
        Configure<AbpKafkaOptions>(options =>
        {
            options.Connections.Default.BootstrapServers = "localhost:9092";
        });

        Configure<AbpKafkaEventBusOptions>(options =>
        {
            options.ConnectionName = "Default";
            options.GroupId = "OrderXChange_Test_Consumer_Group";
            options.TopicName = "OrderXChange_Test";
        });

        // Route distributed events through the in-process local bus for tests. Integration tests
        // must not require a live Kafka broker: with the broker up the ABP consumer's native poll
        // can crash the test host (0xC0000005 in librdkafka rd_kafka_consumer_poll), and with it
        // down the data-seeder's event publish blocks until the producer delivery timeout (~5 min)
        // and then fails module initialization. LocalDistributedEventBus keeps publishes in-process
        // so neither failure mode can occur and the tests are self-contained.
        context.Services.Replace(
            ServiceDescriptor.Singleton<IDistributedEventBus, LocalDistributedEventBus>());
    }
}
