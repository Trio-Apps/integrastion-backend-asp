using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.BackgroundJobs;

public class OrderSyncRecurringJob : ITransientDependency
{
    private readonly ILogger<OrderSyncRecurringJob> _logger;

    public OrderSyncRecurringJob(ILogger<OrderSyncRecurringJob> logger)
    {
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("OrderSyncRecurringJob started at {Timestamp}", DateTimeOffset.UtcNow);

        // TODO: Inject required services and implement the recurring work here.
        await Task.CompletedTask;

        _logger.LogInformation("OrderSyncRecurringJob finished at {Timestamp}", DateTimeOffset.UtcNow);
    }
}

