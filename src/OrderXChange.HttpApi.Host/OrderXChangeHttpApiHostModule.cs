using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication.MicrosoftAccount;
using Microsoft.AspNetCore.Authentication.Twitter;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.HttpOverrides;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Server.AspNetCore;
using OrderXChange.EntityFrameworkCore;
using OrderXChange.MultiTenancy;
using OrderXChange.HealthChecks;
using OrderXChange.BackgroundJobs;
using OrderXChange.Hangfire;
using OrderXChange.HttpApi.Host.Filters;
using Microsoft.OpenApi.Models;
using Volo.Abp;
using Volo.Abp.BackgroundJobs.Hangfire;
using Volo.Abp.Studio;
using Volo.Abp.Account;
using Volo.Abp.Account.Web;
using Volo.Abp.AspNetCore.MultiTenancy;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Autofac;
using Volo.Abp.Localization;
using Volo.Abp.Modularity;
using Volo.Abp.UI.Navigation.Urls;
using Volo.Abp.VirtualFileSystem;
using Volo.Abp.AspNetCore.Mvc.UI.Bundling;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.Shared;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite;
using Volo.Abp.AspNetCore.Mvc.UI.Theme.LeptonXLite.Bundling;
using Microsoft.AspNetCore.Hosting;
using Volo.Abp.AspNetCore.Serilog;
using Volo.Abp.Identity;
using Volo.Abp.OpenIddict;
using Volo.Abp.Swashbuckle;
using Volo.Abp.Studio.Client.AspNetCore;
using Volo.Abp.Security.Claims;
using Volo.Abp.TenantManagement;
using Volo.Abp.Hangfire;
using Hangfire.Storage.MySql;
using Volo.Abp.EventBus.Kafka;
using Volo.Abp.Kafka;
using OrderXChange.Integrations.Foodics;

namespace OrderXChange;

[DependsOn(
    typeof(OrderXChangeHttpApiModule),
    typeof(AbpStudioClientAspNetCoreModule),
    typeof(AbpAspNetCoreMvcUiLeptonXLiteThemeModule),
    typeof(AbpAutofacModule),
    typeof(AbpAspNetCoreMultiTenancyModule),
    typeof(OrderXChangeApplicationModule),
    typeof(OrderXChangeEntityFrameworkCoreModule),
    typeof(AbpSwashbuckleModule),
    typeof(AbpAccountWebOpenIddictModule),
    typeof(AbpAspNetCoreSerilogModule),
    typeof(AbpHangfireModule),
    typeof(AbpEventBusKafkaModule)
    )]
public class OrderXChangeHttpApiHostModule : AbpModule
{
    public override void PreConfigureServices(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();
        var configuration = context.Services.GetConfiguration();

        PreConfigure<OpenIddictBuilder>(builder =>
        {
            builder.AddValidation(options =>
            {
                options.AddAudiences("OrderXChange");
                options.UseLocalServer();
                options.UseAspNetCore();
            });
        });

        if (!hostingEnvironment.IsDevelopment())
        {
            PreConfigure<AbpOpenIddictAspNetCoreOptions>(options =>
            {
                options.AddDevelopmentEncryptionAndSigningCertificate = false;
            });

            PreConfigure<OpenIddictServerBuilder>(serverBuilder =>
            {
                serverBuilder.AddProductionEncryptionAndSigningCertificate("openiddict.pfx", configuration["AuthServer:CertificatePassPhrase"]!);
                serverBuilder.SetIssuer(new Uri(configuration["AuthServer:Authority"]!));
            });
        }
    }

    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        var configuration = context.Services.GetConfiguration();
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (!configuration.GetValue<bool>("App:DisablePII"))
        {
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.ShowPII = true;
            Microsoft.IdentityModel.Logging.IdentityModelEventSource.LogCompleteSecurityArtifact = true;
        }

        if (!configuration.GetValue<bool>("AuthServer:RequireHttpsMetadata"))
        {
            Configure<OpenIddictServerAspNetCoreOptions>(options =>
            {
                options.DisableTransportSecurityRequirement = true;
            });
            
            Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedProto;
            });
        }

        ConfigureAuthentication(context);
        ConfigureUrls(configuration);
        ConfigureBundles();
        ConfigureConventionalControllers();
        ConfigureExternalProviders(context);
        //ConfigureImpersonation(context, configuration);
        ConfigureHealthChecks(context);
        ConfigureKafka(context, configuration);
        ConfigureHangfire(context, configuration);
        ConfigureSwagger(context, configuration);
        ConfigureVirtualFileSystem(context);
        ConfigureCors(context, configuration);
        ConfigureFilters(context);
    }

    private void ConfigureFilters(ServiceConfigurationContext context)
    {
        context.Services.AddMvc(options =>
        {
            options.Filters.AddService(typeof(SlowRequestLoggingFilter));
        });
    }

    private void ConfigureAuthentication(ServiceConfigurationContext context)
    {
        context.Services.ForwardIdentityAuthenticationForBearer(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
        context.Services.Configure<AbpClaimsPrincipalFactoryOptions>(options =>
        {
            options.IsDynamicClaimsEnabled = true;
        });
    }

    private void ConfigureUrls(IConfiguration configuration)
    {
        Configure<AppUrlOptions>(options =>
        {
            options.Applications["MVC"].RootUrl = configuration["App:SelfUrl"];
            options.Applications["Angular"].RootUrl = configuration["App:AngularUrl"];
            options.Applications["Angular"].Urls[AccountUrlNames.PasswordReset] = "account/reset-password";
            options.RedirectAllowedUrls.AddRange(configuration["App:RedirectAllowedUrls"]?.Split(',') ?? Array.Empty<string>());
        });
    }

    private void ConfigureBundles()
    {
        Configure<AbpBundlingOptions>(options =>
        {
            options.StyleBundles.Configure(
                LeptonXLiteThemeBundles.Styles.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-styles.css");
                }
            );

            options.ScriptBundles.Configure(
                LeptonXLiteThemeBundles.Scripts.Global,
                bundle =>
                {
                    bundle.AddFiles("/global-scripts.js");
                }
            );
        });
    }


    private void ConfigureVirtualFileSystem(ServiceConfigurationContext context)
    {
        var hostingEnvironment = context.Services.GetHostingEnvironment();

        if (hostingEnvironment.IsDevelopment())
        {
            Configure<AbpVirtualFileSystemOptions>(options =>
            {
                options.FileSets.ReplaceEmbeddedByPhysical<OrderXChangeDomainSharedModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}OrderXChange.Domain.Shared"));
                options.FileSets.ReplaceEmbeddedByPhysical<OrderXChangeDomainModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}OrderXChange.Domain"));
                options.FileSets.ReplaceEmbeddedByPhysical<OrderXChangeApplicationContractsModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}OrderXChange.Application.Contracts"));
                options.FileSets.ReplaceEmbeddedByPhysical<OrderXChangeApplicationModule>(Path.Combine(hostingEnvironment.ContentRootPath, $"..{Path.DirectorySeparatorChar}OrderXChange.Application"));
            });
        }
    }

    private void ConfigureConventionalControllers()
    {
        Configure<AbpAspNetCoreMvcOptions>(options =>
        {
            options.ConventionalControllers.Create(typeof(OrderXChangeApplicationModule).Assembly);
        });
    }

    private static void ConfigureSwagger(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddAbpSwaggerGenWithOidc(
            configuration["AuthServer:Authority"]!,
            ["OrderXChange"],
            [AbpSwaggerOidcFlows.AuthorizationCode],
            null,
            options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo { Title = "OrderXChange API", Version = "v1" });
                options.DocInclusionPredicate((docName, description) => true);
                options.CustomSchemaIds(type => type.FullName);
            });
    }

    private void ConfigureCors(ServiceConfigurationContext context, IConfiguration configuration)
    {
        context.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder
                    .WithOrigins(
                        configuration["App:CorsOrigins"]?
                            .Split(",", StringSplitOptions.RemoveEmptyEntries)
                            .Select(o => o.Trim().RemovePostFix("/"))
                            .ToArray() ?? Array.Empty<string>()
                    )
                    .WithAbpExposedHeaders()
                    .SetIsOriginAllowedToAllowWildcardSubdomains()
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });
    }
    
    private void ConfigureExternalProviders(ServiceConfigurationContext context)
    {
        //context.Services.AddAuthentication()
        //    .AddGoogle(GoogleDefaults.AuthenticationScheme, options =>
        //    {
        //        options.ClaimActions.MapJsonKey(AbpClaimTypes.Picture, "picture");
        //    })
        //    .WithDynamicOptions<GoogleOptions, GoogleHandler>(
        //        GoogleDefaults.AuthenticationScheme,
        //        options =>
        //        {
        //            options.WithProperty(x => x.ClientId);
        //            options.WithProperty(x => x.ClientSecret, isSecret: true);
        //        }
        //    )
        //    .AddMicrosoftAccount(MicrosoftAccountDefaults.AuthenticationScheme, options =>
        //    {
        //        //Personal Microsoft accounts as an example.
        //        options.AuthorizationEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize";
        //        options.TokenEndpoint = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

        //        options.ClaimActions.MapCustomJson("picture", _ => "https://graph.microsoft.com/v1.0/me/photo/$value");
        //        options.SaveTokens = true;
        //    })
        //    .WithDynamicOptions<MicrosoftAccountOptions, MicrosoftAccountHandler>(
        //        MicrosoftAccountDefaults.AuthenticationScheme,
        //        options =>
        //        {
        //            options.WithProperty(x => x.ClientId);
        //            options.WithProperty(x => x.ClientSecret, isSecret: true);
        //        }
        //    )
        //    .AddTwitter(TwitterDefaults.AuthenticationScheme, options =>
        //    {
        //        options.ClaimActions.MapJsonKey(AbpClaimTypes.Picture, "profile_image_url_https");
        //        options.RetrieveUserDetails = true;
        //    })
        //    .WithDynamicOptions<TwitterOptions, TwitterHandler>(
        //        TwitterDefaults.AuthenticationScheme,
        //        options =>
        //        {
        //            options.WithProperty(x => x.ConsumerKey);
        //            options.WithProperty(x => x.ConsumerSecret, isSecret: true);
        //        }
        //    );
    }

    //private void ConfigureImpersonation(ServiceConfigurationContext context, IConfiguration configuration)
    //{
    //    context.Services.Configure<AbpAccountOptions>(options =>
    //    {
    //        options.
    //        options.ImpersonationTenantPermission = TenantManagementPermissions.Tenants.Default;
    //        options.ImpersonationUserPermission = IdentityPermissions.Users.Impersonation;
    //    });
    //}

    private void ConfigureHealthChecks(ServiceConfigurationContext context)
    {
        context.Services.AddOrderXChangeHealthChecks();
    }

    private void ConfigureKafka(ServiceConfigurationContext context, IConfiguration configuration)
    {
        // Configure ABP Kafka Event Bus with basic settings
        // The Kafka connection details should be configured in appsettings.json
        Configure<AbpKafkaOptions>(options =>
        {
            var servers = configuration["Kafka:Connections:Default:BootstrapServers"]
                          ?? configuration["Kafka:Connections:Default:Servers"]
                          ?? "localhost:9092";

            options.Connections.Default.BootstrapServers = servers;
        });

        Configure<AbpKafkaEventBusOptions>(options =>
        {
            options.ConnectionName = configuration["Kafka:EventBus:ConnectionName"] ?? "Default";
            options.GroupId = configuration["Kafka:EventBus:GroupId"] ?? "OrderXChange_Consumer_Group";
            options.TopicName = configuration["Kafka:EventBus:TopicName"] ?? "OrderXChange";
        });
    }

    private void ConfigureHangfire(ServiceConfigurationContext context, IConfiguration configuration)
    {
        var hangfireConnectionString = configuration.GetConnectionString("Hangfire") ?? configuration.GetConnectionString("Default") ?? throw new AbpException("No connection string configured for Hangfire or Default.");

        context.Services.AddHangfire(config =>
        {
            config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180);
            config.UseSimpleAssemblyNameTypeSerializer();
            config.UseRecommendedSerializerSettings();
            config.UseStorage(new MySqlStorage(
                hangfireConnectionString,
                new MySqlStorageOptions
                {
                    TablesPrefix = "Hangfire_",
                    QueuePollInterval = TimeSpan.FromSeconds(configuration.GetValue<int?>("Hangfire:QueuePollIntervalSeconds") ?? 15),
                }));
        });

        context.Services.AddHangfireServer(options =>
        {
            options.WorkerCount = configuration.GetValue<int?>("Hangfire:WorkerCount") ?? Math.Max(Environment.ProcessorCount, 20);
        });
    }

    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        var app = context.GetApplicationBuilder();
        var env = context.GetEnvironment();
        var configuration = context.ServiceProvider.GetRequiredService<IConfiguration>();
        var logger = context.ServiceProvider.GetRequiredService<ILogger<OrderXChangeHttpApiHostModule>>();

        app.UseForwardedHeaders();

        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseAbpRequestLocalization();

        if (!env.IsDevelopment())
        {
            app.UseErrorPage();
        }

        app.UseRouting();
        app.MapAbpStaticAssets();
        app.UseAbpStudioLink();
        app.UseAbpSecurityHeaders();
        app.UseCors();
        app.UseAuthentication();
        app.UseAbpOpenIddictValidation();

        if (MultiTenancyConsts.IsEnabled)
        {
            app.UseMultiTenancy();
        }

        app.UseUnitOfWork();
        app.UseDynamicClaims();
        app.UseAuthorization();

        app.UseSwagger();
        app.UseAbpSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "OrderXChange API");

            options.OAuthClientId(configuration["AuthServer:SwaggerClientId"]);
        });
        app.UseHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[] { new AllowAllDashboardAuthorizationFilter() }
        });
        app.UseAuditing();
        app.UseAbpSerilogEnrichers();
        ScheduleRecurringJobs(context.ServiceProvider, configuration, logger);
        app.UseConfiguredEndpoints();
    }

    private static void ScheduleRecurringJobs(IServiceProvider serviceProvider, IConfiguration configuration, ILogger<OrderXChangeHttpApiHostModule> logger)
    {
        var recurringJobManager = serviceProvider.GetRequiredService<IRecurringJobManager>();
        var orderCronExpression = configuration["BackgroundJobs:OrderSync:Cron"] ?? Cron.Daily();
        var menuCronExpression = configuration["BackgroundJobs:MenuSync:Cron"] ?? Cron.Hourly();

        //if (!CronExpression.IsValidExpression(orderCronExpression))
        //{
        //    logger.LogWarning("Invalid cron expression {CronExpression} configured for OrderSync recurring job. Falling back to daily schedule.", cronExpression);
        //    cronExpression = Cron.Daily();
        //}

        recurringJobManager.AddOrUpdate<OrderSyncRecurringJob>(
            "OrderSyncRecurringJob",
            job => job.ExecuteAsync(),
            orderCronExpression);

        logger.LogInformation("Recurring job OrderSyncRecurringJob scheduled with cron expression {CronExpression}.", orderCronExpression);

        recurringJobManager.AddOrUpdate<MenuSyncScheduler>(
            "MenuSyncScheduler",
            job => job.PublishMenuSyncEventAsync(null, null, CancellationToken.None),
            menuCronExpression);

        logger.LogInformation("Recurring job MenuSyncScheduler scheduled with cron expression {CronExpression}.", menuCronExpression);
    }
}
