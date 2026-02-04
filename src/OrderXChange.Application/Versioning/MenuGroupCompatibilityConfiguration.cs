using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Configuration service for Menu Group backward compatibility features
/// Manages settings and feature flags for compatibility behavior
/// </summary>
public class MenuGroupCompatibilityConfiguration : ISingletonDependency
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MenuGroupCompatibilityConfiguration> _logger;

    public MenuGroupCompatibilityConfiguration(
        IConfiguration configuration,
        ILogger<MenuGroupCompatibilityConfiguration> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Gets the current compatibility configuration
    /// </summary>
    public MenuGroupCompatibilitySettings GetSettings()
    {
        var settings = new MenuGroupCompatibilitySettings();
        _configuration.GetSection("MenuGroup:Compatibility").Bind(settings);
        
        // Apply defaults if not configured
        ApplyDefaults(settings);
        
        return settings;
    }

    /// <summary>
    /// Validates compatibility configuration
    /// </summary>
    public ValidationResult ValidateConfiguration()
    {
        var settings = GetSettings();
        var result = new ValidationResult { IsValid = true };

        if (settings.AutoCreateDefaultMenuGroup && string.IsNullOrWhiteSpace(settings.DefaultMenuGroupName))
        {
            result.IsValid = false;
            result.Errors.Add("DefaultMenuGroupName is required when AutoCreateDefaultMenuGroup is enabled");
        }

        if (settings.RollbackPreservationDays < 0)
        {
            result.IsValid = false;
            result.Errors.Add("RollbackPreservationDays cannot be negative");
        }

        if (settings.MaxAutoCreatedMenuGroups < 1)
        {
            result.IsValid = false;
            result.Errors.Add("MaxAutoCreatedMenuGroups must be at least 1");
        }

        return result;
    }

    /// <summary>
    /// Checks if a specific compatibility feature is enabled
    /// </summary>
    public bool IsFeatureEnabled(string featureName)
    {
        var settings = GetSettings();
        
        return featureName.ToLowerInvariant() switch
        {
            "autocreatedefaultmenugroup" => settings.AutoCreateDefaultMenuGroup,
            "enablelegacysyncmode" => settings.EnableLegacySyncMode,
            "allowmenugrouppromoting" => settings.AllowMenuGroupPromoting,
            "enablerollbackcapabilities" => settings.EnableRollbackCapabilities,
            "strictcompatibilityvalidation" => settings.StrictCompatibilityValidation,
            _ => false
        };
    }

    /// <summary>
    /// Gets feature-specific configuration
    /// </summary>
    public T GetFeatureConfiguration<T>(string featureName) where T : class, new()
    {
        var config = new T();
        _configuration.GetSection($"MenuGroup:Compatibility:Features:{featureName}").Bind(config);
        return config;
    }

    private void ApplyDefaults(MenuGroupCompatibilitySettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.DefaultMenuGroupName))
        {
            settings.DefaultMenuGroupName = "All Categories";
        }

        if (string.IsNullOrWhiteSpace(settings.DefaultMenuGroupDescription))
        {
            settings.DefaultMenuGroupDescription = "Default menu group containing all categories (created for backward compatibility)";
        }

        if (settings.RollbackPreservationDays == 0)
        {
            settings.RollbackPreservationDays = 30;
        }

        if (settings.MaxAutoCreatedMenuGroups == 0)
        {
            settings.MaxAutoCreatedMenuGroups = 5;
        }

        if (settings.CompatibilityValidationTimeoutMinutes == 0)
        {
            settings.CompatibilityValidationTimeoutMinutes = 10;
        }
    }
}

/// <summary>
/// Configuration settings for Menu Group backward compatibility
/// </summary>
public class MenuGroupCompatibilitySettings
{
    /// <summary>
    /// Whether to automatically create default Menu Groups when none exist
    /// </summary>
    public bool AutoCreateDefaultMenuGroup { get; set; } = true;

    /// <summary>
    /// Whether to enable legacy branch-level sync mode
    /// </summary>
    public bool EnableLegacySyncMode { get; set; } = true;

    /// <summary>
    /// Whether to allow promoting branch-level data to Menu Group-scoped
    /// </summary>
    public bool AllowMenuGroupPromoting { get; set; } = true;

    /// <summary>
    /// Whether rollback capabilities are enabled
    /// </summary>
    public bool EnableRollbackCapabilities { get; set; } = true;

    /// <summary>
    /// Whether to perform strict compatibility validation
    /// </summary>
    public bool StrictCompatibilityValidation { get; set; } = false;

    /// <summary>
    /// Default name for auto-created Menu Groups
    /// </summary>
    [Required]
    public string DefaultMenuGroupName { get; set; } = "All Categories";

    /// <summary>
    /// Default description for auto-created Menu Groups
    /// </summary>
    public string DefaultMenuGroupDescription { get; set; } = "Default menu group containing all categories (created for backward compatibility)";

    /// <summary>
    /// How long to preserve data during rollback operations (days)
    /// </summary>
    public int RollbackPreservationDays { get; set; } = 30;

    /// <summary>
    /// Maximum number of Menu Groups that can be auto-created per account
    /// </summary>
    public int MaxAutoCreatedMenuGroups { get; set; } = 5;

    /// <summary>
    /// Timeout for compatibility validation operations (minutes)
    /// </summary>
    public int CompatibilityValidationTimeoutMinutes { get; set; } = 10;

    /// <summary>
    /// Whether to log detailed compatibility operations
    /// </summary>
    public bool EnableDetailedLogging { get; set; } = false;

    /// <summary>
    /// Custom metadata to include in auto-created Menu Groups
    /// </summary>
    public Dictionary<string, object> DefaultMenuGroupMetadata { get; set; } = new();

    /// <summary>
    /// Feature-specific configurations
    /// </summary>
    public Dictionary<string, object> FeatureConfigurations { get; set; } = new();
}

/// <summary>
/// Validation result for configuration
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Extension methods for compatibility configuration
/// </summary>
public static class MenuGroupCompatibilityConfigurationExtensions
{
    /// <summary>
    /// Checks if auto-creation is enabled and allowed for the specified account
    /// </summary>
    public static bool CanAutoCreateMenuGroup(this MenuGroupCompatibilitySettings settings, Guid foodicsAccountId, int currentMenuGroupCount)
    {
        if (!settings.AutoCreateDefaultMenuGroup)
            return false;

        if (currentMenuGroupCount >= settings.MaxAutoCreatedMenuGroups)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the timeout for compatibility operations
    /// </summary>
    public static TimeSpan GetCompatibilityTimeout(this MenuGroupCompatibilitySettings settings)
    {
        return TimeSpan.FromMinutes(settings.CompatibilityValidationTimeoutMinutes);
    }

    /// <summary>
    /// Gets the rollback preservation period
    /// </summary>
    public static TimeSpan GetRollbackPreservationPeriod(this MenuGroupCompatibilitySettings settings)
    {
        return TimeSpan.FromDays(settings.RollbackPreservationDays);
    }
}