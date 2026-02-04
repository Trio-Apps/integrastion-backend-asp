using System;
using System.Collections.Generic;
using OrderXChange.Domain.Versioning;

namespace OrderXChange.Application.Versioning.DTOs;

/// <summary>
/// Example configurations for Menu Group to Talabat mappings
/// These demonstrate different mapping scenarios and configurations
/// </summary>
public static class MenuGroupMappingExamples
{
    /// <summary>
    /// Basic auto-mapping configuration for a single brand
    /// </summary>
    public static CreateMenuGroupTalabatMappingDto BasicAutoMapping(
        Guid menuGroupId, 
        string talabatVendorCode)
    {
        return new CreateMenuGroupTalabatMappingDto
        {
            MenuGroupId = menuGroupId,
            TalabatVendorCode = talabatVendorCode,
            MappingStrategy = MenuMappingStrategy.Auto,
            Priority = 100,
            Configuration = new MenuGroupMappingConfigurationDto
            {
                IsolateMenu = true,
                SyncAvailability = true,
                SyncPricing = true,
                SyncModifiers = true,
                SyncImages = true,
                ValidationRules = new MenuGroupValidationRulesDto
                {
                    RequirePrices = true,
                    RequireDescriptions = false,
                    RequireImages = false
                },
                SyncPreferences = new MenuGroupSyncPreferencesDto
                {
                    AutoSync = true,
                    MaxRetryAttempts = 3,
                    RetryDelayMinutes = 5
                }
            }
        };
    }

    /// <summary>
    /// Multi-brand configuration with custom naming patterns
    /// </summary>
    public static CreateMenuGroupTalabatMappingDto MultiBrandMapping(
        Guid menuGroupId,
        string talabatVendorCode,
        string brandName)
    {
        return new CreateMenuGroupTalabatMappingDto
        {
            MenuGroupId = menuGroupId,
            TalabatVendorCode = talabatVendorCode,
            TalabatMenuName = $"{brandName} Menu",
            TalabatMenuDescription = $"Complete menu for {brandName} brand",
            MappingStrategy = MenuMappingStrategy.Manual,
            Priority = 200,
            Configuration = new MenuGroupMappingConfigurationDto
            {
                IsolateMenu = true,
                IncludeMenuGroupInNames = true,
                NamingPattern = "{menuGroupName} - {itemName}",
                SyncAvailability = true,
                SyncPricing = true,
                SyncModifiers = true,
                SyncImages = true,
                CustomFieldMappings = new Dictionary<string, string>
                {
                    { "brand", brandName },
                    { "menu_type", "full_menu" },
                    { "priority", "high" }
                },
                ValidationRules = new MenuGroupValidationRulesDto
                {
                    MinItemCount = 5,
                    MaxItemCount = 500,
                    RequirePrices = true,
                    RequireDescriptions = true,
                    RequireImages = false
                },
                SyncPreferences = new MenuGroupSyncPreferencesDto
                {
                    AutoSync = true,
                    SyncFrequencyMinutes = 30,
                    OffPeakOnly = false,
                    TimeZone = "Asia/Dubai",
                    PreferredSyncHours = new List<int> { 2, 3, 4, 14, 15 }, // 2-4 AM and 2-3 PM
                    BatchSync = false,
                    MaxRetryAttempts = 5,
                    RetryDelayMinutes = 10
                }
            }
        };
    }

    /// <summary>
    /// Limited menu configuration (e.g., delivery-only items)
    /// </summary>
    public static CreateMenuGroupTalabatMappingDto DeliveryOnlyMapping(
        Guid menuGroupId,
        string talabatVendorCode)
    {
        return new CreateMenuGroupTalabatMappingDto
        {
            MenuGroupId = menuGroupId,
            TalabatVendorCode = talabatVendorCode,
            TalabatMenuName = "Delivery Menu",
            TalabatMenuDescription = "Items available for delivery only",
            MappingStrategy = MenuMappingStrategy.Manual,
            Priority = 150,
            Configuration = new MenuGroupMappingConfigurationDto
            {
                IsolateMenu = true,
                IncludeMenuGroupInNames = false,
                SyncAvailability = true,
                SyncPricing = true,
                SyncModifiers = false, // Simplified for delivery
                SyncImages = true,
                CustomFieldMappings = new Dictionary<string, string>
                {
                    { "delivery_only", "true" },
                    { "menu_type", "delivery" }
                },
                ValidationRules = new MenuGroupValidationRulesDto
                {
                    MinItemCount = 3,
                    RequiredCategories = new List<string> { "main_courses" },
                    ExcludedCategories = new List<string> { "alcohol", "hot_beverages" },
                    RequirePrices = true,
                    RequireDescriptions = true,
                    RequireImages = true
                },
                SyncPreferences = new MenuGroupSyncPreferencesDto
                {
                    AutoSync = true,
                    SyncFrequencyMinutes = 15, // More frequent for delivery
                    OffPeakOnly = false,
                    BatchSync = true,
                    MaxRetryAttempts = 3,
                    RetryDelayMinutes = 2
                }
            }
        };
    }

    /// <summary>
    /// Template-based mapping for franchise operations
    /// </summary>
    public static CreateMenuGroupTalabatMappingDto FranchiseTemplateMapping(
        Guid menuGroupId,
        string talabatVendorCode,
        string franchiseLocation)
    {
        return new CreateMenuGroupTalabatMappingDto
        {
            MenuGroupId = menuGroupId,
            TalabatVendorCode = talabatVendorCode,
            TalabatMenuName = $"Franchise Menu - {franchiseLocation}",
            TalabatMenuDescription = $"Standardized franchise menu for {franchiseLocation} location",
            MappingStrategy = MenuMappingStrategy.Template,
            Priority = 100,
            Configuration = new MenuGroupMappingConfigurationDto
            {
                IsolateMenu = true,
                IncludeMenuGroupInNames = false,
                NamingPattern = "{itemName}", // Keep original names
                SyncAvailability = true,
                SyncPricing = true,
                SyncModifiers = true,
                SyncImages = true,
                CustomFieldMappings = new Dictionary<string, string>
                {
                    { "franchise_location", franchiseLocation },
                    { "menu_template", "standard_franchise" },
                    { "location_type", "franchise" }
                },
                ValidationRules = new MenuGroupValidationRulesDto
                {
                    MinItemCount = 10,
                    MaxItemCount = 200,
                    RequirePrices = true,
                    RequireDescriptions = true,
                    RequireImages = false // Images managed centrally
                },
                SyncPreferences = new MenuGroupSyncPreferencesDto
                {
                    AutoSync = true,
                    SyncFrequencyMinutes = 60, // Hourly sync
                    OffPeakOnly = true,
                    TimeZone = "Asia/Dubai",
                    PreferredSyncHours = new List<int> { 1, 2, 3, 13, 14 },
                    BatchSync = true, // Batch with other franchise locations
                    MaxRetryAttempts = 3,
                    RetryDelayMinutes = 15
                }
            }
        };
    }

    /// <summary>
    /// High-volume restaurant configuration with strict validation
    /// </summary>
    public static CreateMenuGroupTalabatMappingDto HighVolumeMapping(
        Guid menuGroupId,
        string talabatVendorCode)
    {
        return new CreateMenuGroupTalabatMappingDto
        {
            MenuGroupId = menuGroupId,
            TalabatVendorCode = talabatVendorCode,
            TalabatMenuName = "Premium Restaurant Menu",
            TalabatMenuDescription = "Complete premium dining experience menu",
            MappingStrategy = MenuMappingStrategy.Manual,
            Priority = 300, // High priority
            Configuration = new MenuGroupMappingConfigurationDto
            {
                IsolateMenu = true,
                IncludeMenuGroupInNames = false,
                SyncAvailability = true,
                SyncPricing = true,
                SyncModifiers = true,
                SyncImages = true,
                CustomFieldMappings = new Dictionary<string, string>
                {
                    { "restaurant_tier", "premium" },
                    { "volume_category", "high" },
                    { "quality_standard", "premium" }
                },
                ValidationRules = new MenuGroupValidationRulesDto
                {
                    MinItemCount = 20,
                    MaxItemCount = 1000,
                    RequiredCategories = new List<string> 
                    { 
                        "appetizers", "main_courses", "desserts" 
                    },
                    RequirePrices = true,
                    RequireDescriptions = true,
                    RequireImages = true
                },
                SyncPreferences = new MenuGroupSyncPreferencesDto
                {
                    AutoSync = false, // Manual sync for control
                    OffPeakOnly = true,
                    TimeZone = "Asia/Dubai",
                    PreferredSyncHours = new List<int> { 2, 3, 4 }, // Early morning only
                    BatchSync = false, // Individual sync for precision
                    MaxRetryAttempts = 5,
                    RetryDelayMinutes = 30
                }
            }
        };
    }

    /// <summary>
    /// Seasonal/promotional menu configuration
    /// </summary>
    public static CreateMenuGroupTalabatMappingDto SeasonalMapping(
        Guid menuGroupId,
        string talabatVendorCode,
        string seasonName)
    {
        return new CreateMenuGroupTalabatMappingDto
        {
            MenuGroupId = menuGroupId,
            TalabatVendorCode = talabatVendorCode,
            TalabatMenuName = $"{seasonName} Special Menu",
            TalabatMenuDescription = $"Limited time {seasonName.ToLower()} menu items",
            MappingStrategy = MenuMappingStrategy.Manual,
            Priority = 250, // Higher priority for promotions
            Configuration = new MenuGroupMappingConfigurationDto
            {
                IsolateMenu = true,
                IncludeMenuGroupInNames = true,
                NamingPattern = "{menuGroupName} {itemName}",
                SyncAvailability = true,
                SyncPricing = true,
                SyncModifiers = true,
                SyncImages = true,
                CustomFieldMappings = new Dictionary<string, string>
                {
                    { "menu_type", "seasonal" },
                    { "season", seasonName.ToLower() },
                    { "promotion", "limited_time" }
                },
                ValidationRules = new MenuGroupValidationRulesDto
                {
                    MinItemCount = 3,
                    MaxItemCount = 50,
                    RequirePrices = true,
                    RequireDescriptions = true,
                    RequireImages = true // Important for promotional items
                },
                SyncPreferences = new MenuGroupSyncPreferencesDto
                {
                    AutoSync = true,
                    SyncFrequencyMinutes = 10, // Frequent sync for promotions
                    OffPeakOnly = false,
                    BatchSync = false,
                    MaxRetryAttempts = 5,
                    RetryDelayMinutes = 2
                }
            }
        };
    }
}

/// <summary>
/// Helper methods for common mapping scenarios
/// </summary>
public static class MenuGroupMappingHelpers
{
    /// <summary>
    /// Creates a basic configuration with minimal settings
    /// </summary>
    public static MenuGroupMappingConfigurationDto CreateBasicConfiguration()
    {
        return new MenuGroupMappingConfigurationDto
        {
            IsolateMenu = true,
            SyncAvailability = true,
            SyncPricing = true,
            SyncModifiers = true,
            SyncImages = false,
            ValidationRules = new MenuGroupValidationRulesDto
            {
                RequirePrices = true
            },
            SyncPreferences = new MenuGroupSyncPreferencesDto
            {
                AutoSync = true,
                MaxRetryAttempts = 3,
                RetryDelayMinutes = 5
            }
        };
    }

    /// <summary>
    /// Creates a configuration optimized for delivery platforms
    /// </summary>
    public static MenuGroupMappingConfigurationDto CreateDeliveryOptimizedConfiguration()
    {
        return new MenuGroupMappingConfigurationDto
        {
            IsolateMenu = true,
            SyncAvailability = true,
            SyncPricing = true,
            SyncModifiers = false, // Simplified for delivery
            SyncImages = true,
            ValidationRules = new MenuGroupValidationRulesDto
            {
                RequirePrices = true,
                RequireDescriptions = true,
                RequireImages = true
            },
            SyncPreferences = new MenuGroupSyncPreferencesDto
            {
                AutoSync = true,
                SyncFrequencyMinutes = 15,
                BatchSync = true,
                MaxRetryAttempts = 3,
                RetryDelayMinutes = 2
            }
        };
    }

    /// <summary>
    /// Creates a configuration for high-end restaurants with strict validation
    /// </summary>
    public static MenuGroupMappingConfigurationDto CreatePremiumConfiguration()
    {
        return new MenuGroupMappingConfigurationDto
        {
            IsolateMenu = true,
            SyncAvailability = true,
            SyncPricing = true,
            SyncModifiers = true,
            SyncImages = true,
            ValidationRules = new MenuGroupValidationRulesDto
            {
                MinItemCount = 10,
                RequirePrices = true,
                RequireDescriptions = true,
                RequireImages = true
            },
            SyncPreferences = new MenuGroupSyncPreferencesDto
            {
                AutoSync = false, // Manual control
                OffPeakOnly = true,
                MaxRetryAttempts = 5,
                RetryDelayMinutes = 30
            }
        };
    }
}