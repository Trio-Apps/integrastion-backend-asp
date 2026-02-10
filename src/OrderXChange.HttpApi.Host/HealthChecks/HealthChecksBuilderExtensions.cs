using System;
using System.Linq;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace OrderXChange.HealthChecks;

public static class HealthChecksBuilderExtensions
{
    public static void AddOrderXChangeHealthChecks(this IServiceCollection services)
    {
        // Add your health checks here
        var healthChecksBuilder = services.AddHealthChecks();
        healthChecksBuilder.AddCheck<OrderXChangeDatabaseCheck>("OrderXChange DbContext Check", tags: new string[] { "database" });

        services.ConfigureHealthCheckEndpoint("/health-status");

        var configuration = services.GetConfiguration();
        var healthCheckUrl = configuration["App:HealthCheckUrl"];

        if (string.IsNullOrEmpty(healthCheckUrl))
        {
            healthCheckUrl = "/health-status";
        }

        var healthUiCheckUrl = ResolveHealthUiCheckUrl(configuration, healthCheckUrl);

        var healthChecksUiBuilder = services.AddHealthChecksUI(settings =>
        {
            settings.AddHealthCheckEndpoint("OrderXChange Health Status", healthUiCheckUrl);
        });

        // Set your HealthCheck UI Storage here
        healthChecksUiBuilder.AddInMemoryStorage();

        services.MapHealthChecksUiEndpoints(options =>
        {
            options.UIPath = "/health-ui";
            options.ApiPath = "/health-api";
        });
    }

    private static string ResolveHealthUiCheckUrl(IConfiguration configuration, string healthCheckUrl)
    {
        var configuredHealthUiUrl = configuration["App:HealthUiCheckUrl"];
        if (!string.IsNullOrWhiteSpace(configuredHealthUiUrl))
        {
            return configuredHealthUiUrl;
        }

        if (Uri.TryCreate(healthCheckUrl, UriKind.Absolute, out var absoluteHealthCheckUri))
        {
            return absoluteHealthCheckUri.ToString();
        }

        var normalizedHealthPath = healthCheckUrl.EnsureStartsWith('/');

        var aspNetCoreUrls = configuration["ASPNETCORE_URLS"];
        if (!string.IsNullOrWhiteSpace(aspNetCoreUrls))
        {
            var firstBinding = aspNetCoreUrls
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(url => url.Trim())
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstBinding) && TryCreateLoopbackUri(firstBinding, normalizedHealthPath, out var loopbackUri))
            {
                return loopbackUri;
            }
        }

        return $"http://127.0.0.1:8080{normalizedHealthPath}";
    }

    private static bool TryCreateLoopbackUri(string bindingUrl, string healthPath, out string result)
    {
        result = string.Empty;

        if (!Uri.TryCreate(bindingUrl, UriKind.Absolute, out var bindingUri))
        {
            return false;
        }

        var scheme = string.IsNullOrWhiteSpace(bindingUri.Scheme) ? Uri.UriSchemeHttp : bindingUri.Scheme;
        var port = bindingUri.IsDefaultPort
            ? (string.Equals(scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ? 443 : 80)
            : bindingUri.Port;

        var builder = new UriBuilder(scheme, "127.0.0.1", port, healthPath.TrimStart('/'));
        result = builder.Uri.ToString();

        return true;
    }

    private static IServiceCollection ConfigureHealthCheckEndpoint(this IServiceCollection services, string path)
    {
        services.Configure<AbpEndpointRouterOptions>(options =>
        {
            options.EndpointConfigureActions.Add(endpointContext =>
            {
                endpointContext.Endpoints.MapHealthChecks(
                    new PathString(path.EnsureStartsWith('/')),
                    new HealthCheckOptions
                    {
                        Predicate = _ => true,
                        ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
                        AllowCachingResponses = false,
                    });
            });
        });

        return services;
    }

    private static IServiceCollection MapHealthChecksUiEndpoints(this IServiceCollection services, Action<global::HealthChecks.UI.Configuration.Options>? setupOption = null)
    {
        services.Configure<AbpEndpointRouterOptions>(routerOptions =>
        {
            routerOptions.EndpointConfigureActions.Add(endpointContext =>
            {
                endpointContext.Endpoints.MapHealthChecksUI(setupOption);
            });
        });

        return services;
    }
}
