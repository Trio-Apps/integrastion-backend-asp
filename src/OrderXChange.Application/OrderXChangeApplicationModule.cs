using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Resilience;
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

        // Centralized outbound rate-limit / 429 handling for Foodics and Talabat.
        var configuration = context.Services.GetConfiguration();
        context.Services.Configure<OutboundRateLimitOptions>(
            configuration.GetSection(OutboundRateLimitOptions.SectionName));
        context.Services.PostConfigure<OutboundRateLimitOptions>(SeedDefaultProviders);
        context.Services.AddTransient<RateLimitingHandler>();

        // Foodics typed clients run through the rate-limit handler.
        context.Services.AddHttpClient<FoodicsMenuClient>().AddHttpMessageHandler<RateLimitingHandler>();
        context.Services.AddHttpClient<FoodicsCatalogClient>().AddHttpMessageHandler<RateLimitingHandler>();
        context.Services.AddHttpClient<FoodicsOrderClient>().AddHttpMessageHandler<RateLimitingHandler>();
        context.Services.AddHttpClient<FoodicsCustomerClient>().AddHttpMessageHandler<RateLimitingHandler>();
        context.Services.AddHttpClient<FoodicsChargeClient>().AddHttpMessageHandler<RateLimitingHandler>();
        context.Services.AddHttpClient<FoodicsPaymentMethodClient>().AddHttpMessageHandler<RateLimitingHandler>();
        context.Services.AddHttpClient<FoodicsAuthClient>().AddHttpMessageHandler<RateLimitingHandler>();

        // Talabat clients receive the default (unnamed) HttpClient; route it through the
        // same handler. Unknown hosts are passed through untouched by the handler.
        context.Services.AddHttpClient(string.Empty).AddHttpMessageHandler<RateLimitingHandler>();
    }

    /// <summary>
    /// Provider defaults applied when the OutboundRateLimit section defines no providers,
    /// so throttling works out of the box. Foodics enforces 90 req/min per token — we stay
    /// under it. Talabat's published limit is unconfirmed; the conservative default is tunable
    /// via appsettings.
    /// </summary>
    private static void SeedDefaultProviders(OutboundRateLimitOptions options)
    {
        if (options.Providers.Count > 0)
        {
            return;
        }

        options.Providers.Add(new ProviderRateLimit
        {
            Name = "Foodics",
            Hosts = new List<string> { "foodics.com" },
            PermitLimit = 80,
            WindowSeconds = 60,
            SegmentsPerWindow = 6,
            QueueLimit = 2000
        });
        options.Providers.Add(new ProviderRateLimit
        {
            Name = "Talabat",
            Hosts = new List<string> { "restaurant-partners.com" },
            PermitLimit = 100,
            WindowSeconds = 60,
            SegmentsPerWindow = 6,
            QueueLimit = 2000
        });
    }
}
