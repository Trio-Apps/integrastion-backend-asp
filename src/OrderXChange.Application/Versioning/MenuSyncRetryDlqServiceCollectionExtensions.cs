using System;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Extension methods for configuring Menu Sync retry and DLQ services
/// </summary>
public static class MenuSyncRetryDlqServiceCollectionExtensions
{
    /// <summary>
    /// Adds Menu Sync retry and DLQ services to the service collection
    /// </summary>
    public static IServiceCollection AddMenuSyncRetryAndDlq(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure retry policy options
        services.Configure<MenuSyncRetryOptions>(
            configuration.GetSection(MenuSyncRetryOptions.SectionName));

        // Configure DLQ background service options
        services.Configure<MenuSyncDlqBackgroundOptions>(
            configuration.GetSection(MenuSyncDlqBackgroundOptions.SectionName));

        // Register core services
        services.AddTransient<MenuSyncRetryPolicy>();
        services.AddTransient<IMenuSyncDlqService, MenuSyncDlqService>();
        services.AddTransient<MenuSyncReplayWorkflowService>();

        // Register background service
        services.AddSingleton<IHostedService, MenuSyncDlqBackgroundService>();

        return services;
    }

    /// <summary>
    /// Adds Menu Sync retry and DLQ services with custom configuration
    /// </summary>
    public static IServiceCollection AddMenuSyncRetryAndDlq(
        this IServiceCollection services,
        Action<MenuSyncRetryOptions> configureRetry,
        Action<MenuSyncDlqBackgroundOptions> configureDlq)
    {
        // Configure options
        services.Configure(configureRetry);
        services.Configure(configureDlq);

        // Register core services
        services.AddTransient<MenuSyncRetryPolicy>();
        services.AddTransient<IMenuSyncDlqService, MenuSyncDlqService>();
        services.AddTransient<MenuSyncReplayWorkflowService>();

        // Register background service
        services.AddSingleton<IHostedService, MenuSyncDlqBackgroundService>();

        return services;
    }
}