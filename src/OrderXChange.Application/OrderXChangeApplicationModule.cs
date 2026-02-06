using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using OrderXChange.Application.Integrations.Foodics;
using Polly;
using Polly.Extensions.Http;
using Volo.Abp.Account;
using Volo.Abp.AuditLogging;
using Volo.Abp.AutoMapper;
using Volo.Abp.FeatureManagement;
using Volo.Abp.Identity;
using Volo.Abp.Modularity;
using Volo.Abp.OpenIddict;
using Volo.Abp.PermissionManagement;
using Volo.Abp.SettingManagement;
using Volo.Abp.TenantManagement;
using Volo.Abp.EventBus.Kafka;

namespace OrderXChange;

[DependsOn(
    typeof(OrderXChangeDomainModule),
    typeof(OrderXChangeApplicationContractsModule),
    typeof(AbpPermissionManagementApplicationModule),
    typeof(AbpFeatureManagementApplicationModule),
    typeof(AbpIdentityApplicationModule),
    typeof(AbpTenantManagementApplicationModule),
    typeof(AbpSettingManagementApplicationModule),
    typeof(AbpEventBusKafkaModule)
    )]
public class OrderXChangeApplicationModule : AbpModule
{
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        Configure<AbpAutoMapperOptions>(options =>
        {
            options.AddMaps<OrderXChangeApplicationModule>();
        });

        context.Services.AddHttpClient<FoodicsMenuClient>()
            .AddPolicyHandler(GetRetryPolicy());

        context.Services.AddHttpClient<FoodicsCatalogClient>()
            .AddPolicyHandler(GetRetryPolicy());

        context.Services.AddHttpClient<FoodicsOrderClient>()
            .AddPolicyHandler(GetRetryPolicy());
    }

    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(response => (int)response.StatusCode == 429)
            .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
    }
}
