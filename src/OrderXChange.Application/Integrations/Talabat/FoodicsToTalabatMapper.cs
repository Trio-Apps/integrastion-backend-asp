using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Versioning;
using OrderXChange.Domain.Versioning;

namespace OrderXChange.Application.Integrations.Talabat;

/// <summary>
/// Maps Foodics catalog data to Talabat catalog format using stable ID-based mapping
/// Handles the transformation between the two system's data structures with permanent mapping references
/// </summary>
public class FoodicsToTalabatMapper : ITransientDependency
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<FoodicsToTalabatMapper> _logger;
    private readonly IMenuMappingService _menuMappingService;

    public FoodicsToTalabatMapper(
        IConfiguration configuration,
        ILogger<FoodicsToTalabatMapper> logger,
        IMenuMappingService menuMappingService)
    {
        _configuration = configuration;
        _logger = logger;
        _menuMappingService = menuMappingService;
    }

    /// <summary>
    /// Maps Foodics products to Talabat catalog submit request using stable ID-based mapping
    /// Groups products by category and includes modifiers with permanent remote codes
    /// </summary>
    /// <param name="products">Foodics products with full includes</param>
    /// <param name="foodicsAccountId">Foodics account ID for mapping context</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="vendorCode">Talabat vendor code</param>
    /// <param name="callbackUrl">Optional webhook URL for import status notification</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Talabat catalog submit request with stable remote codes</returns>
    public async Task<TalabatCatalogSubmitRequest> MapToTalabatCatalogAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        string vendorCode,
        string? callbackUrl = null,
        CancellationToken cancellationToken = default)
    {
        var productsList = products.ToList();
        
        _logger.LogInformation(
            "Mapping {ProductCount} Foodics products to Talabat catalog for vendor {VendorCode} using stable ID-based mapping. AccountId={AccountId}, BranchId={BranchId}",
            productsList.Count,
            vendorCode,
            foodicsAccountId,
            branchId ?? "ALL");

        // Step 1: Create or update all mappings in bulk for optimal performance
        var mappings = await _menuMappingService.BulkCreateOrUpdateMappingsAsync(
            foodicsAccountId, branchId, productsList, cancellationToken);

        _logger.LogInformation(
            "Created/updated {MappingCount} stable mappings for menu sync",
            mappings.Count);

        // Step 2: Group products by category using stable category mappings
        var productsByCategory = productsList
            .Where(p => !string.IsNullOrWhiteSpace(GetCategoryId(p)))
            .GroupBy(p => GetCategoryId(p)!)
            .ToList();

        // Also collect products without category
        var uncategorizedProducts = productsList
            .Where(p => string.IsNullOrWhiteSpace(GetCategoryId(p)))
            .ToList();

        var categories = new List<TalabatCategory>();
        var sortOrder = 0;

        // Step 3: Map categorized products using stable remote codes
        foreach (var categoryGroup in productsByCategory)
        {
            var firstProduct = categoryGroup.First();
            var categoryInfo = firstProduct.Category;
            var categoryFoodicsId = categoryGroup.Key;

            // Get stable category mapping
            if (!mappings.TryGetValue(categoryFoodicsId, out var categoryMapping))
            {
                _logger.LogWarning(
                    "Category mapping not found for FoodicsId={CategoryId}. Skipping category.",
                    categoryFoodicsId);
                continue;
            }

            var talabatCategory = new TalabatCategory
            {
                RemoteCode = categoryMapping.TalabatRemoteCode, // Use stable remote code
                Name = categoryInfo?.Name ?? $"Category-{categoryFoodicsId}",
                NameTranslations = BuildNameTranslations(categoryInfo?.NameLocalized),
                SortOrder = sortOrder++,
                IsAvailable = true,
                Products = new List<TalabatProduct>()
            };

            // Map products in this category
            int productSortOrder = 0;
            foreach (var product in categoryGroup)
            {
                var mappedProduct = await MapProductAsync(product, productSortOrder++, mappings, cancellationToken);
                if (mappedProduct != null)
                {
                    talabatCategory.Products.Add(mappedProduct);
                }
            }

            categories.Add(talabatCategory);
        }

        // Step 4: Add uncategorized products to a default category if any exist
        if (uncategorizedProducts.Any())
        {
            var defaultCategory = new TalabatCategory
            {
                RemoteCode = "uncategorized", // Static remote code for uncategorized
                Name = "Other Items",
                NameTranslations = new Dictionary<string, string> { { "ar", "عناصر أخرى" } },
                SortOrder = sortOrder++,
                IsAvailable = true,
                Products = new List<TalabatProduct>()
            };

            int productSortOrder = 0;
            foreach (var product in uncategorizedProducts)
            {
                var mappedProduct = await MapProductAsync(product, productSortOrder++, mappings, cancellationToken);
                if (mappedProduct != null)
                {
                    defaultCategory.Products.Add(mappedProduct);
                }
            }

            categories.Add(defaultCategory);
        }

        // Step 5: Add categories from Groups (NEW)
        var sortOrderConfig = _configuration.GetValue<string>("Talabat:MenuGroups:SortOrder", "AfterCategories");
        
        if (sortOrderConfig == "BeforeCategories")
        {
            // Add group categories at the beginning
            var groupCategories = await CreateCategoriesFromGroupsAsync(
                productsList, 
                mappings, 
                0, 
                cancellationToken);
            
            // Adjust sort order for existing categories
            foreach (var cat in categories)
            {
                cat.SortOrder += groupCategories.Count;
            }
            
            categories.InsertRange(0, groupCategories);
            
            _logger.LogInformation(
                "Added {GroupCategoryCount} categories from groups at the beginning",
                groupCategories.Count);
        }
        else // AfterCategories (default)
        {
            // Add group categories at the end
            var groupCategories = await CreateCategoriesFromGroupsAsync(
                productsList, 
                mappings, 
                categories.Count, 
                cancellationToken);
            
            categories.AddRange(groupCategories);
            
            _logger.LogInformation(
                "Added {GroupCategoryCount} categories from groups at the end",
                groupCategories.Count);
        }

        // Build callback URL if not provided
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            var baseCallbackUrl = _configuration["Talabat:CallbackBaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseCallbackUrl))
            {
                callbackUrl = $"{baseCallbackUrl.TrimEnd('/')}/catalog-status";
            }
        }

        // Talabat API validation: callbackUrl must not contain localhost, test., or example.
        // Pattern: ^(http|https|HTTP|HTTPS)://((?!test.)(?!example.)(?!localhost.).)*$
        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            var callbackUri = callbackUrl.ToLowerInvariant();
            if (callbackUri.Contains("localhost") || 
                callbackUri.Contains("test.") || 
                callbackUri.Contains("example."))
            {
                _logger.LogWarning(
                    "CallbackUrl contains invalid domain (localhost/test/example). Removing callbackUrl from request. Url: {CallbackUrl}",
                    callbackUrl);
                callbackUrl = null; // Remove invalid callbackUrl
            }
        }

        // Get vendor codes for V2 API - vendors array is required
        var platformVendorId = _configuration["Talabat:PlatformVendorId"];
        var defaultVendorCode = _configuration["Talabat:DefaultVendorCode"];
        var vendors = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(platformVendorId))
        {
            vendors.Add(platformVendorId);
        }
        else if (!string.IsNullOrWhiteSpace(defaultVendorCode))
        {
            vendors.Add(defaultVendorCode);
        }

        var request = new TalabatCatalogSubmitRequest
        {
            CallbackUrl = callbackUrl, // Will be null if invalid
            Menu = new TalabatMenu
            {
                Categories = categories,
                Vendors = vendors.Count > 0 ? vendors : null
            }
        };

        var totalProducts = categories.Sum(c => c.Products.Count);
        var groupCategoriesCount = categories.Count(c => c.RemoteCode.StartsWith("group-"));
        _logger.LogInformation(
            "Mapped {CategoryCount} categories (including {GroupCount} from groups) with {ProductCount} total products for vendor {VendorCode} using stable remote codes",
            categories.Count,
            groupCategoriesCount,
            totalProducts,
            vendorCode);

        return request;
    }

    /// <summary>
    /// Legacy method for backward compatibility - now uses stable ID-based mapping
    /// </summary>
    [Obsolete("Use MapToTalabatCatalogAsync with foodicsAccountId and branchId parameters for stable ID-based mapping")]
    public TalabatCatalogSubmitRequest MapToTalabatCatalog(
        IEnumerable<FoodicsProductDetailDto> products,
        string vendorCode,
        string? callbackUrl = null)
    {
        _logger.LogWarning(
            "Using legacy MapToTalabatCatalog method without stable ID mapping. Consider upgrading to MapToTalabatCatalogAsync for better data integrity.");

        // Fallback to original implementation for backward compatibility
        return MapToTalabatCatalogLegacy(products, vendorCode, callbackUrl);
    }

    /// <summary>
    /// Legacy implementation preserved for backward compatibility
    /// </summary>
    private TalabatCatalogSubmitRequest MapToTalabatCatalogLegacy(
        IEnumerable<FoodicsProductDetailDto> products,
        string vendorCode,
        string? callbackUrl = null)
    {
        var productsList = products.ToList();
        
        _logger.LogInformation(
            "Mapping {ProductCount} Foodics products to Talabat catalog for vendor {VendorCode} (legacy mode)",
            productsList.Count,
            vendorCode);

        // Group products by category
        // Note: Foodics returns category as object (Category.Id) not as separate CategoryId field
        // So we check both: Category?.Id (from include) or CategoryId (legacy field)
        var productsByCategory = productsList
            .Where(p => !string.IsNullOrWhiteSpace(GetCategoryId(p)))
            .GroupBy(p => GetCategoryId(p)!)
            .ToList();

        // Also collect products without category
        var uncategorizedProducts = productsList
            .Where(p => string.IsNullOrWhiteSpace(GetCategoryId(p)))
            .ToList();

        var categories = new List<TalabatCategory>();
        var sortOrder = 0;

        // Map categorized products
        foreach (var categoryGroup in productsByCategory)
        {
            var firstProduct = categoryGroup.First();
            var categoryInfo = firstProduct.Category;

            var talabatCategory = new TalabatCategory
            {
                RemoteCode = categoryGroup.Key,
                Name = categoryInfo?.Name ?? $"Category-{categoryGroup.Key}",
                NameTranslations = BuildNameTranslations(categoryInfo?.NameLocalized),
                SortOrder = sortOrder++,
                IsAvailable = true,
                Products = categoryGroup
                    .Select((p, idx) => MapProductLegacy(p, idx))
                    .ToList()
            };

            categories.Add(talabatCategory);
        }

        // Add uncategorized products to a default category if any exist
        if (uncategorizedProducts.Any())
        {
            var defaultCategory = new TalabatCategory
            {
                RemoteCode = "uncategorized",
                Name = "Other Items",
                NameTranslations = new Dictionary<string, string> { { "ar", "عناصر أخرى" } },
                SortOrder = sortOrder++,
                IsAvailable = true,
                Products = uncategorizedProducts
                    .Select((p, idx) => MapProductLegacy(p, idx))
                    .ToList()
            };

            categories.Add(defaultCategory);
        }

        // Build callback URL if not provided
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            var baseCallbackUrl = _configuration["Talabat:CallbackBaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseCallbackUrl))
            {
                callbackUrl = $"{baseCallbackUrl.TrimEnd('/')}/catalog-status";
            }
        }

        // Talabat API validation: callbackUrl must not contain localhost, test., or example.
        // Pattern: ^(http|https|HTTP|HTTPS)://((?!test.)(?!example.)(?!localhost.).)*$
        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            var callbackUri = callbackUrl.ToLowerInvariant();
            if (callbackUri.Contains("localhost") || 
                callbackUri.Contains("test.") || 
                callbackUri.Contains("example."))
            {
                _logger.LogWarning(
                    "CallbackUrl contains invalid domain (localhost/test/example). Removing callbackUrl from request. Url: {CallbackUrl}",
                    callbackUrl);
                callbackUrl = null; // Remove invalid callbackUrl
            }
        }

        // Get vendor codes for V2 API - vendors array is required
        var platformVendorId = _configuration["Talabat:PlatformVendorId"];
        var defaultVendorCode = _configuration["Talabat:DefaultVendorCode"];
        var vendors = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(platformVendorId))
        {
            vendors.Add(platformVendorId);
        }
        else if (!string.IsNullOrWhiteSpace(defaultVendorCode))
        {
            vendors.Add(defaultVendorCode);
        }

        var request = new TalabatCatalogSubmitRequest
        {
            CallbackUrl = callbackUrl, // Will be null if invalid
            Menu = new TalabatMenu
            {
                Categories = categories,
                Vendors = vendors.Count > 0 ? vendors : null
            }
        };

        var totalProducts = categories.Sum(c => c.Products.Count);
        _logger.LogInformation(
            "Mapped {CategoryCount} categories with {ProductCount} total products for vendor {VendorCode} (legacy mode)",
            categories.Count,
            totalProducts,
            vendorCode);

        return request;
    }

    /// <summary>
    /// Legacy product mapping without stable IDs
    /// </summary>
    private TalabatProduct MapProductLegacy(FoodicsProductDetailDto foodicsProduct, int sortOrder)
    {
        var talabatProduct = new TalabatProduct
        {
            RemoteCode = foodicsProduct.Id,
            Name = foodicsProduct.Name ?? $"Product-{foodicsProduct.Id}",
            NameTranslations = BuildNameTranslations(foodicsProduct.NameLocalized),
            Description = foodicsProduct.Description,
            DescriptionTranslations = BuildNameTranslations(foodicsProduct.DescriptionLocalized),
            Price = foodicsProduct.Price ?? 0,
            ImageUrl = NormalizeImageUrl(foodicsProduct.Image),
            IsAvailable = foodicsProduct.IsActive ?? true,
            SortOrder = sortOrder,
            TaxRate = foodicsProduct.TaxGroup?.Rate,
            Sku = foodicsProduct.Sku,
            Tags = MapTags(foodicsProduct.Tags),
            ModifierGroups = MapModifierGroupsLegacy(foodicsProduct.Modifiers)
        };

        return talabatProduct;
    }

    /// <summary>
    /// Legacy modifier mapping without stable IDs
    /// </summary>
    private List<TalabatModifierGroup>? MapModifierGroupsLegacy(List<FoodicsModifierDto>? modifiers)
    {
        if (modifiers == null || modifiers.Count == 0)
            return null;

        return modifiers
            .Where(m => m.Options != null && m.Options.Count > 0)
            .Select((m, idx) => new TalabatModifierGroup
            {
                RemoteCode = m.Id,
                Name = m.Name ?? $"Modifier-{m.Id}",
                NameTranslations = BuildNameTranslations(m.NameLocalized),
                MinSelection = 0, // Foodics doesn't provide this by default
                MaxSelection = m.Options?.Count ?? 1,
                SortOrder = idx,
                Modifiers = m.Options?
                    .Select((o, oIdx) => MapModifierLegacy(o, oIdx))
                    .ToList() ?? new List<TalabatModifier>()
            })
            .ToList();
    }

    /// <summary>
    /// Legacy modifier option mapping without stable IDs
    /// </summary>
    private TalabatModifier MapModifierLegacy(FoodicsModifierOptionDto option, int sortOrder)
    {
        return new TalabatModifier
        {
            RemoteCode = option.Id,
            Name = option.Name ?? $"Option-{option.Id}",
            NameTranslations = BuildNameTranslations(option.NameLocalized),
            Price = option.Price ?? 0,
            IsAvailable = true, // Foodics options from active products are typically available
            IsDefault = false,
            SortOrder = sortOrder
        };
    }

    /// <summary>
    /// Helper method to get category ID from product
    /// </summary>
    private static string? GetCategoryId(FoodicsProductDetailDto product) => 
        !string.IsNullOrWhiteSpace(product.Category?.Id) ? product.Category.Id : product.CategoryId;

    /// <summary>
    /// Creates additional categories from Foodics Groups (async version)
    /// Converts Menu Groups to Talabat Categories for better organization
    /// </summary>
    private async Task<List<TalabatCategory>> CreateCategoriesFromGroupsAsync(
        List<FoodicsProductDetailDto> products,
        Dictionary<string, MenuItemMapping> mappings,
        int startingSortOrder,
        CancellationToken cancellationToken = default)
    {
        var groupCategories = new List<TalabatCategory>();
        
        // Check if Groups feature is enabled
        var groupsEnabled = _configuration.GetValue<bool>("Talabat:MenuGroups:Enabled", false);
        if (!groupsEnabled)
        {
            _logger.LogInformation("Menu Groups feature is disabled in configuration");
            return groupCategories;
        }
        
        var sendAsCategories = _configuration.GetValue<bool>("Talabat:MenuGroups:SendAsCategories", true);
        if (!sendAsCategories)
        {
            _logger.LogInformation("Menu Groups will not be sent as categories");
            return groupCategories;
        }
        
        var categoryPrefix = _configuration.GetValue<string>("Talabat:MenuGroups:CategoryPrefix", "");
        
        // Collect all unique groups from all products
        var allGroups = products
            .Where(p => p.Groups != null && p.Groups.Any())
            .SelectMany(p => p.Groups!)
            .GroupBy(g => g.Id)
            .Select(g => g.First())
            .ToList();
        
        _logger.LogInformation(
            "Found {GroupCount} unique groups across {ProductCount} products",
            allGroups.Count,
            products.Count);
        
        int sortOrder = startingSortOrder;
        
        foreach (var group in allGroups)
        {
            // Get all products that belong to this group
            var groupProducts = products
                .Where(p => p.Groups != null && p.Groups.Any(g => g.Id == group.Id))
                .ToList();
            
            if (!groupProducts.Any())
            {
                continue;
            }
            
            // Create category for this group
            var groupCategory = new TalabatCategory
            {
                RemoteCode = $"group-{group.Id}", // Prefix to distinguish from regular categories
                Name = $"{categoryPrefix}{group.Name}",
                NameTranslations = BuildNameTranslations(group.NameLocalized),
                SortOrder = sortOrder++,
                IsAvailable = true,
                Products = new List<TalabatProduct>()
            };
            
            // Map products in this group
            int productSortOrder = 0;
            foreach (var product in groupProducts)
            {
                var mappedProduct = await MapProductAsync(product, productSortOrder++, mappings, cancellationToken);
                if (mappedProduct != null)
                {
                    groupCategory.Products.Add(mappedProduct);
                }
            }
            
            groupCategories.Add(groupCategory);
            
            _logger.LogInformation(
                "Created category from group: {GroupName} with {ProductCount} products",
                group.Name,
                groupCategory.Products.Count);
        }
        
        return groupCategories;
    }

    /// <summary>
    /// Maps a single Foodics product to Talabat product format using stable remote codes
    /// </summary>
    private async Task<TalabatProduct?> MapProductAsync(
        FoodicsProductDetailDto foodicsProduct, 
        int sortOrder, 
        Dictionary<string, MenuItemMapping> mappings,
        CancellationToken cancellationToken = default)
    {
        // Get stable product mapping
        if (!mappings.TryGetValue(foodicsProduct.Id, out var productMapping))
        {
            _logger.LogWarning(
                "Product mapping not found for FoodicsId={ProductId}. Skipping product.",
                foodicsProduct.Id);
            return null;
        }

        var talabatProduct = new TalabatProduct
        {
            RemoteCode = productMapping.TalabatRemoteCode, // Use stable remote code
            Name = foodicsProduct.Name ?? $"Product-{foodicsProduct.Id}",
            NameTranslations = BuildNameTranslations(foodicsProduct.NameLocalized),
            Description = foodicsProduct.Description,
            DescriptionTranslations = BuildNameTranslations(foodicsProduct.DescriptionLocalized),
            Price = foodicsProduct.Price ?? 0,
            ImageUrl = NormalizeImageUrl(foodicsProduct.Image),
            IsAvailable = foodicsProduct.IsActive ?? true,
            SortOrder = sortOrder,
            TaxRate = foodicsProduct.TaxGroup?.Rate,
            Sku = foodicsProduct.Sku,
            Tags = MapTags(foodicsProduct.Tags),
            ModifierGroups = MapModifierGroupsWithStableIds(foodicsProduct.Modifiers, mappings)
        };

        return talabatProduct;
    }

    /// <summary>
    /// Maps Foodics modifiers to Talabat modifier groups using stable remote codes
    /// </summary>
    private List<TalabatModifierGroup>? MapModifierGroupsWithStableIds(
        List<FoodicsModifierDto>? modifiers, 
        Dictionary<string, MenuItemMapping> mappings)
    {
        if (modifiers == null || modifiers.Count == 0)
            return null;

        var modifierGroups = new List<TalabatModifierGroup>();

        int groupSortOrder = 0;
        foreach (var modifier in modifiers)
        {
            if (modifier.Options == null || modifier.Options.Count == 0)
                continue;

            // Get stable modifier mapping
            if (!mappings.TryGetValue(modifier.Id, out var modifierMapping))
            {
                _logger.LogWarning(
                    "Modifier mapping not found for FoodicsId={ModifierId}. Skipping modifier group.",
                    modifier.Id);
                continue;
            }

            var modifierGroup = new TalabatModifierGroup
            {
                RemoteCode = modifierMapping.TalabatRemoteCode, // Use stable remote code
                Name = modifier.Name ?? $"Modifier-{modifier.Id}",
                NameTranslations = BuildNameTranslations(modifier.NameLocalized),
                MinSelection = 0, // Foodics doesn't provide this by default
                MaxSelection = modifier.Options.Count,
                SortOrder = groupSortOrder++,
                Modifiers = new List<TalabatModifier>()
            };

            // Map modifier options with stable IDs
            int optionSortOrder = 0;
            foreach (var option in modifier.Options)
            {
                var mappedOption = MapModifierOptionWithStableId(option, optionSortOrder++, mappings);
                if (mappedOption != null)
                {
                    modifierGroup.Modifiers.Add(mappedOption);
                }
            }

            if (modifierGroup.Modifiers.Any())
            {
                modifierGroups.Add(modifierGroup);
            }
        }

        return modifierGroups.Any() ? modifierGroups : null;
    }

    /// <summary>
    /// Maps a single Foodics modifier option to Talabat modifier using stable remote code
    /// </summary>
    private TalabatModifier? MapModifierOptionWithStableId(
        FoodicsModifierOptionDto option, 
        int sortOrder, 
        Dictionary<string, MenuItemMapping> mappings)
    {
        // Get stable modifier option mapping
        if (!mappings.TryGetValue(option.Id, out var optionMapping))
        {
            _logger.LogWarning(
                "Modifier option mapping not found for FoodicsId={OptionId}. Skipping option.",
                option.Id);
            return null;
        }

        return new TalabatModifier
        {
            RemoteCode = optionMapping.TalabatRemoteCode, // Use stable remote code
            Name = option.Name ?? $"Option-{option.Id}",
            NameTranslations = BuildNameTranslations(option.NameLocalized),
            Price = option.Price ?? 0,
            IsAvailable = true, // Foodics options from active products are typically available
            IsDefault = false,
            SortOrder = sortOrder
        };
    }

    /// <summary>
    /// Maps Foodics tags to string list
    /// </summary>
    private List<string>? MapTags(List<FoodicsTagDto>? tags)
    {
        if (tags == null || tags.Count == 0)
            return null;

        return tags
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .Select(t => t.Name!)
            .ToList();
    }

    /// <summary>
    /// Builds translations dictionary from localized name
    /// Assumes localized name is in Arabic (ar) which is common for Middle East
    /// </summary>
    private Dictionary<string, string>? BuildNameTranslations(string? localizedName)
    {
        if (string.IsNullOrWhiteSpace(localizedName))
            return null;

        // Default to Arabic for Middle East region
        var defaultLocale = _configuration["Talabat:DefaultLocale"] ?? "ar";
        
        return new Dictionary<string, string>
        {
            { defaultLocale, localizedName }
        };
    }

    /// <summary>
    /// Normalizes image URL to ensure it meets Talabat requirements
    /// - Must be HTTPS
    /// - Should be accessible publicly
    /// - Rejects placeholder/invalid domains (example.com, localhost, test domains)
    /// </summary>
    private string? NormalizeImageUrl(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
            return null;

        // Ensure HTTPS
        if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        {
            imageUrl = "https://" + imageUrl.Substring(7);
        }

        // Basic validation - must be a valid URL
        if (!Uri.TryCreate(imageUrl, UriKind.Absolute, out var uri))
        {
            _logger.LogWarning("Invalid image URL format skipped: {ImageUrl}", imageUrl);
            return null;
        }

        // Must be HTTPS for Talabat
        if (uri.Scheme != "https")
        {
            _logger.LogWarning("Non-HTTPS image URL skipped: {ImageUrl}", imageUrl);
            return null;
        }

        // ✅ NEW: Reject placeholder/invalid domains (same validation as callbackUrl)
        // Talabat API rejects: localhost, test.*, example.* domains
        var host = uri.Host.ToLowerInvariant();
        if (host.Contains("localhost") || 
            host.Contains("test.") || 
            host.Contains("example.") ||
            host.Contains("example.com") ||
            host.Contains("example.org") ||
            host.Contains("placeholder") ||
            host == "127.0.0.1")
        {
            _logger.LogWarning(
                "Image URL contains invalid/placeholder domain (localhost/test/example). Skipping to prevent Talabat upload failure. ImageUrl={ImageUrl}, Host={Host}",
                imageUrl, host);
            return null;
        }

        return imageUrl;
    }

    /// <summary>
    /// Maps item availability changes from Foodics products to Talabat format using stable remote codes
    /// Used for incremental availability updates
    /// </summary>
    public async Task<TalabatUpdateItemAvailabilityRequest> MapToItemAvailabilityUpdateAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken = default)
    {
        var productsList = products.ToList();
        
        _logger.LogInformation(
            "Mapping {ProductCount} products to item availability update using stable remote codes. AccountId={AccountId}, BranchId={BranchId}",
            productsList.Count,
            foodicsAccountId,
            branchId ?? "ALL");

        var items = new List<TalabatItemAvailability>();

        foreach (var product in productsList)
        {
            // Get stable product mapping
            var productMapping = await _menuMappingService.GetMappingByFoodicsIdAsync(
                foodicsAccountId, branchId, MenuMappingEntityType.Product, product.Id, cancellationToken);

            if (productMapping == null)
            {
                _logger.LogWarning(
                    "Product mapping not found for FoodicsId={ProductId}. Skipping availability update.",
                    product.Id);
                continue;
            }

            items.Add(new TalabatItemAvailability
            {
                RemoteCode = productMapping.TalabatRemoteCode, // Use stable remote code
                IsAvailable = product.IsActive ?? false
            });
        }

        _logger.LogInformation(
            "Mapped {ItemCount} items for availability update using stable remote codes",
            items.Count);

        return new TalabatUpdateItemAvailabilityRequest
        {
            Items = items
        };
    }

    /// <summary>
    /// Legacy method for backward compatibility - now uses stable ID-based mapping
    /// </summary>
    [Obsolete("Use MapToItemAvailabilityUpdateAsync with foodicsAccountId and branchId parameters for stable ID-based mapping")]
    public TalabatUpdateItemAvailabilityRequest MapToItemAvailabilityUpdate(
        IEnumerable<FoodicsProductDetailDto> products)
    {
        _logger.LogWarning(
            "Using legacy MapToItemAvailabilityUpdate method without stable ID mapping. Consider upgrading to MapToItemAvailabilityUpdateAsync for better data integrity.");

        return new TalabatUpdateItemAvailabilityRequest
        {
            Items = products.Select(p => new TalabatItemAvailability
            {
                RemoteCode = p.Id, // Direct Foodics ID (legacy behavior)
                IsAvailable = p.IsActive ?? false
            }).ToList()
        };
    }

    /// <summary>
    /// Maps branch/store status to Talabat vendor availability
    /// </summary>
    public TalabatUpdateVendorAvailabilityRequest MapToVendorAvailability(
        bool isAvailable,
        string? reason = null,
        DateTime? availableAt = null)
    {
        return new TalabatUpdateVendorAvailabilityRequest
        {
            IsAvailable = isAvailable,
            Reason = reason,
            AvailableAt = availableAt
        };
    }

    /// <summary>
    /// Maps Foodics products to Talabat V2 catalog format (items-based structure) using stable ID-based mapping
    /// This is the NEW format that Talabat provided in their example with permanent remote codes
    /// </summary>
    public async Task<TalabatV2CatalogSubmitRequest> MapToTalabatV2CatalogAsync(
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string? branchId,
        string chainCode,
        string? vendorCode = null,
        string? callbackUrl = null,
        CancellationToken cancellationToken = default)
    {
        var productsList = products.ToList();
        
        _logger.LogInformation(
            "Mapping {ProductCount} Foodics products to Talabat V2 catalog format for chain {ChainCode} using stable ID-based mapping. AccountId={AccountId}, BranchId={BranchId}, VendorCode={VendorCode}",
            productsList.Count,
            chainCode,
            foodicsAccountId,
            branchId ?? "ALL",
            vendorCode ?? "<none>");

        // Step 1: Create or update all mappings in bulk for optimal performance
        var mappings = await _menuMappingService.BulkCreateOrUpdateMappingsAsync(
            foodicsAccountId, branchId, productsList, cancellationToken);

        _logger.LogInformation(
            "Created/updated {MappingCount} stable mappings for V2 menu sync",
            mappings.Count);

        var items = new Dictionary<string, TalabatV2CatalogItem>();
        var categoryMap = new Dictionary<string, TalabatV2CatalogItem>(); // categoryId -> category item
        var toppingMap = new Dictionary<string, TalabatV2CatalogItem>(); // modifierId -> topping item
        var imageMap = new Dictionary<string, TalabatV2CatalogItem>(); // imageId -> image item
        var hiddenOptionProductIds = new HashSet<string>(); // option products must be listed under hidden category
        var scheduleId = "schedule-01";
        var menuId = "Menu_01";

        // Step 2: Create all products using stable remote codes
        foreach (var product in productsList)
        {
            if (string.IsNullOrWhiteSpace(product.Id))
                continue;

            // Get stable product mapping
            if (!mappings.TryGetValue(product.Id, out var productMapping))
            {
                _logger.LogWarning(
                    "Product mapping not found for FoodicsId={ProductId}. Skipping product.",
                    product.Id);
                continue;
            }

            var productItem = CreateProductItemWithStableId(product, productMapping);
            items[productMapping.TalabatRemoteCode] = productItem; // Use stable remote code as key

            // Create image item if product has image
            if (!string.IsNullOrWhiteSpace(product.Image))
            {
                var imageId = $"image-{productMapping.TalabatRemoteCode}";
                if (!imageMap.ContainsKey(imageId))
                {
                    var imageItem = CreateImageItem(imageId, product.Image, product.Name);
                    if (imageItem != null)
                    {
                        imageMap[imageId] = imageItem;
                        items[imageId] = imageItem;

                        // Link image to product
                        productItem.Images ??= new Dictionary<string, TalabatV2ItemReference>();
                        productItem.Images[imageId] = new TalabatV2ItemReference
                        {
                            Id = imageId,
                            Type = "Image"
                        };
                    }
                    else
                    {
                        // Image URL was rejected (invalid/placeholder domain)
                        _logger.LogWarning(
                            "Product image skipped (invalid URL). ProductId={ProductId}, ProductName={ProductName}, ImageUrl={ImageUrl}, RemoteCode={RemoteCode}",
                            product.Id, product.Name, product.Image, productMapping.TalabatRemoteCode);
                    }
                }
            }

            // Create topping items (modifier groups) and their option products using stable IDs
            if (product.Modifiers != null && product.Modifiers.Count > 0)
            {
                productItem.Toppings ??= new Dictionary<string, TalabatV2ItemReference>();
                
                int toppingOrder = 0;
                foreach (var modifier in product.Modifiers)
                {
                    if (string.IsNullOrWhiteSpace(modifier.Id) || modifier.Options == null || modifier.Options.Count == 0)
                        continue;

                    // Get stable modifier mapping
                    if (!mappings.TryGetValue(modifier.Id, out var modifierMapping))
                    {
                        _logger.LogWarning(
                            "Modifier mapping not found for FoodicsId={ModifierId}. Skipping modifier group.",
                            modifier.Id);
                        continue;
                    }

                    var toppingId = $"tt-{modifierMapping.TalabatRemoteCode}";
                    
                    // Create topping item if not exists
                    if (!toppingMap.ContainsKey(toppingId))
                    {
                        // Pass imageMap and items to support choice option images
                        var (toppingItem, optionProducts) = CreateToppingItemWithStableIds(
                            modifier, 
                            modifierMapping,
                            toppingId, 
                            toppingOrder,
                            mappings,
                            imageMap,
                            items);
                        toppingMap[toppingId] = toppingItem;
                        items[toppingId] = toppingItem;

                        // Add option products to items using stable remote codes
                        foreach (var optionProduct in optionProducts)
                        {
                            items[optionProduct.Id] = optionProduct;
                            hiddenOptionProductIds.Add(optionProduct.Id);
                        }
                    }

                    // Link topping to product with order
                    productItem.Toppings[toppingId] = new TalabatV2ItemReference
                    {
                        Id = toppingId,
                        Type = "Topping",
                        Order = toppingOrder++
                    };
                }
            }
        }

        // Step 3: Create categories and link products using stable remote codes
        var productsByCategory = productsList
            .Where(p => !string.IsNullOrWhiteSpace(p.Category?.Id) || !string.IsNullOrWhiteSpace(p.CategoryId))
            .GroupBy(p => p.Category?.Id ?? p.CategoryId!)
            .ToList();

        int categoryOrder = 0;
        foreach (var categoryGroup in productsByCategory)
        {
            var categoryFoodicsId = categoryGroup.Key;
            
            // Get stable category mapping
            if (!mappings.TryGetValue(categoryFoodicsId, out var categoryMapping))
            {
                _logger.LogWarning(
                    "Category mapping not found for FoodicsId={CategoryId}. Skipping category.",
                    categoryFoodicsId);
                continue;
            }

            var categoryId = $"Category#{categoryMapping.TalabatRemoteCode}";
            var firstProduct = categoryGroup.First();
            var categoryInfo = firstProduct.Category;

            if (!categoryMap.ContainsKey(categoryId))
            {
                var categoryItem = CreateCategoryItemWithStableId(categoryId, categoryInfo, categoryMapping, categoryOrder++);
                categoryMap[categoryId] = categoryItem;
                items[categoryId] = categoryItem;
            }

            // Link products to category with order using stable remote codes
            var categoryItemRef = categoryMap[categoryId];
            categoryItemRef.Products ??= new Dictionary<string, TalabatV2ItemReference>();
            
            int productOrder = 0;
            foreach (var product in categoryGroup)
            {
                if (!string.IsNullOrWhiteSpace(product.Id) && mappings.TryGetValue(product.Id, out var productMapping))
                {
                    categoryItemRef.Products[productMapping.TalabatRemoteCode] = new TalabatV2ItemReference
                    {
                        Id = productMapping.TalabatRemoteCode,
                        Type = "Product",
                        Order = productOrder++
                    };
                }
            }
        }

        // Step 4: Add hidden category that contains all option products used by toppings
        // Talabat requires: All products under Topping must also be listed under Category#hiddenId
        if (hiddenOptionProductIds.Count > 0)
        {
            const string hiddenCategoryId = "Category#hiddenId";
            var hiddenCategoryItem = new TalabatV2CatalogItem
            {
                Id = hiddenCategoryId,
                Type = "Category",
                Order = 999, // Put at end
                Title = new TalabatV2Title { Default = "hidden" },
                Description = new TalabatV2Title { Default = "hidden" },
                Products = new Dictionary<string, TalabatV2ItemReference>()
            };

            int hiddenOrder = 0;
            foreach (var optionProductId in hiddenOptionProductIds)
            {
                hiddenCategoryItem.Products[optionProductId] = new TalabatV2ItemReference
                {
                    Id = optionProductId,
                    Type = "Product",
                    Order = hiddenOrder++
                };
            }

            items[hiddenCategoryId] = hiddenCategoryItem;
            categoryMap[hiddenCategoryId] = hiddenCategoryItem;
            
            _logger.LogInformation(
                "Added hidden category with {Count} topping option products",
                hiddenOptionProductIds.Count);
        }

        // Step 5: Create schedule entry with weekDays (required by Talabat)
        var scheduleItem = new TalabatV2CatalogItem
        {
            Id = scheduleId,
            Type = "ScheduleEntry",
            StartTime = "08:00:00",
            EndTime = "23:59:00",
            WeekDays = new List<string>
            {
                "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY",
                "FRIDAY", "SATURDAY", "SUNDAY"
            }
        };
        items[scheduleId] = scheduleItem;

        // Step 6: Create menu
        var menuItem = new TalabatV2CatalogItem
        {
            Id = menuId,
            Type = "Menu",
            Title = new TalabatV2Title { Default = "Foodics Menu" },
            Description = new TalabatV2Title { Default = "Menu imported from Foodics" },
            MenuType = "DELIVERY",
            Schedule = new Dictionary<string, TalabatV2ItemReference>
            {
                [scheduleId] = new TalabatV2ItemReference
                {
                    Id = scheduleId,
                    Type = "ScheduleEntry"
                }
            },
            Products = new Dictionary<string, TalabatV2ItemReference>()
        };

        // Link all categories to menu (Menu → Categories → Products)
        // This is the correct structure for Talabat V2 API
        foreach (var category in categoryMap)
        {
            menuItem.Products![category.Key] = new TalabatV2ItemReference
            {
                Id = category.Key,
                Type = "Category",
                Order = category.Value.Order
            };
        }

        items[menuId] = menuItem;

        // Build callback URL
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            var baseCallbackUrl = _configuration["Talabat:CallbackBaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseCallbackUrl))
            {
                callbackUrl = $"{baseCallbackUrl.TrimEnd('/')}/catalog-status";
            }
        }

        // Talabat API validation: callbackUrl must not contain localhost, test., or example.
        // Pattern: ^(http|https|HTTP|HTTPS)://((?!test.)(?!example.)(?!localhost.).)*$
        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            var callbackUri = callbackUrl.ToLowerInvariant();
            if (callbackUri.Contains("localhost") || 
                callbackUri.Contains("test.") || 
                callbackUri.Contains("example."))
            {
                _logger.LogWarning(
                    "CallbackUrl contains invalid domain (localhost/test/example). Removing callbackUrl from request. Url: {CallbackUrl}",
                    callbackUrl);
                callbackUrl = null; // Remove invalid callbackUrl
            }
        }

        // Get vendor codes - use specific vendorCode from TalabatAccount if provided
        var vendors = GetVendors(vendorCode);

        var request = new TalabatV2CatalogSubmitRequest
        {
            Catalog = new TalabatV2Catalog { Items = items },
            Vendors = vendors,
            CallbackUrl = callbackUrl // Will be null if invalid
        };

        _logger.LogInformation(
            "Mapped to V2 format with stable IDs: {ItemCount} items, {CategoryCount} categories, {ToppingCount} toppings, {ImageCount} images, VendorCode={VendorCode}",
            items.Count,
            categoryMap.Count,
            toppingMap.Count,
            imageMap.Count,
            vendorCode ?? "<none>");

        return request;
    }

    /// <summary>
    /// Legacy V2 method for backward compatibility - now uses stable ID-based mapping
    /// </summary>
    [Obsolete("Use MapToTalabatV2CatalogAsync with foodicsAccountId and branchId parameters for stable ID-based mapping")]
    public TalabatV2CatalogSubmitRequest MapToTalabatV2Catalog(
        IEnumerable<FoodicsProductDetailDto> products,
        string chainCode,
        string? vendorCode = null,
        string? callbackUrl = null)
    {
        _logger.LogWarning(
            "Using legacy MapToTalabatV2Catalog method without stable ID mapping. Consider upgrading to MapToTalabatV2CatalogAsync for better data integrity.");

        // Fallback to original implementation for backward compatibility
        return MapToTalabatV2CatalogLegacy(products, chainCode, vendorCode, callbackUrl);
    }

    /// <summary>
    /// Legacy V2 implementation preserved for backward compatibility
    /// </summary>
    private TalabatV2CatalogSubmitRequest MapToTalabatV2CatalogLegacy(
        IEnumerable<FoodicsProductDetailDto> products,
        string chainCode,
        string? vendorCode = null,
        string? callbackUrl = null)
    {
        var productsList = products.ToList();
        
        _logger.LogInformation(
            "Mapping {ProductCount} Foodics products to Talabat V2 catalog format for chain {ChainCode} (legacy mode)",
            productsList.Count,
            chainCode);

        var items = new Dictionary<string, TalabatV2CatalogItem>();
        var categoryMap = new Dictionary<string, TalabatV2CatalogItem>(); // categoryId -> category item
        var toppingMap = new Dictionary<string, TalabatV2CatalogItem>(); // modifierId -> topping item
        var imageMap = new Dictionary<string, TalabatV2CatalogItem>(); // imageId -> image item
        var hiddenOptionProductIds = new HashSet<string>(); // option products must be listed under hidden category
        var scheduleId = "schedule-01";
        var menuId = "Menu_01";

        // Step 1: Create all products
        foreach (var product in productsList)
        {
            if (string.IsNullOrWhiteSpace(product.Id))
                continue;

            var productItem = CreateProductItem(product);
            items[product.Id] = productItem;

            // Create image item if product has image
            if (!string.IsNullOrWhiteSpace(product.Image))
            {
                var imageId = $"image-{product.Id}";
                if (!imageMap.ContainsKey(imageId))
                {
                    var imageItem = CreateImageItem(imageId, product.Image, product.Name);
                    if (imageItem != null)
                    {
                        imageMap[imageId] = imageItem;
                        items[imageId] = imageItem;

                        // Link image to product
                        productItem.Images ??= new Dictionary<string, TalabatV2ItemReference>();
                        productItem.Images[imageId] = new TalabatV2ItemReference
                        {
                            Id = imageId,
                            Type = "Image"
                        };
                    }
                    else
                    {
                        // Image URL was rejected (invalid/placeholder domain)
                        _logger.LogWarning(
                            "Product image skipped (invalid URL). ProductId={ProductId}, ProductName={ProductName}, ImageUrl={ImageUrl}",
                            product.Id, product.Name, product.Image);
                    }
                }
            }

            // Create topping items (modifier groups) and their option products
            if (product.Modifiers != null && product.Modifiers.Count > 0)
            {
                productItem.Toppings ??= new Dictionary<string, TalabatV2ItemReference>();
                
                int toppingOrder = 0;
                foreach (var modifier in product.Modifiers)
                {
                    if (string.IsNullOrWhiteSpace(modifier.Id) || modifier.Options == null || modifier.Options.Count == 0)
                        continue;

                    var toppingId = $"tt-{modifier.Id}";
                    
                    // Create topping item if not exists
                    if (!toppingMap.ContainsKey(toppingId))
                    {
                        // Pass imageMap and items to support choice option images
                        var (toppingItem, optionProducts) = CreateToppingItem(
                            modifier, 
                            toppingId, 
                            toppingOrder,
                            imageMap,
                            items);
                        toppingMap[toppingId] = toppingItem;
                        items[toppingId] = toppingItem;

                        // Add option products to items
                        foreach (var optionProduct in optionProducts)
                        {
                            items[optionProduct.Id] = optionProduct;
                            hiddenOptionProductIds.Add(optionProduct.Id);
                        }
                    }

                    // Link topping to product with order
                    productItem.Toppings[toppingId] = new TalabatV2ItemReference
                    {
                        Id = toppingId,
                        Type = "Topping",
                        Order = toppingOrder++
                    };
                }
            }
        }

        // Step 2: Create categories and link products
        // Note: Foodics returns category as object (Category.Id) not as separate CategoryId field
        var productsByCategory = productsList
            .Where(p => !string.IsNullOrWhiteSpace(p.Category?.Id) || !string.IsNullOrWhiteSpace(p.CategoryId))
            .GroupBy(p => p.Category?.Id ?? p.CategoryId!)
            .ToList();

        int categoryOrder = 0;
        foreach (var categoryGroup in productsByCategory)
        {
            var categoryId = $"Category#{categoryGroup.Key}";
            var firstProduct = categoryGroup.First();
            var categoryInfo = firstProduct.Category;

            if (!categoryMap.ContainsKey(categoryId))
            {
                var categoryItem = CreateCategoryItem(categoryId, categoryInfo, categoryOrder++);
                categoryMap[categoryId] = categoryItem;
                items[categoryId] = categoryItem;
            }

            // Link products to category with order
            var categoryItemRef = categoryMap[categoryId];
            categoryItemRef.Products ??= new Dictionary<string, TalabatV2ItemReference>();
            
            int productOrder = 0;
            foreach (var product in categoryGroup)
            {
                if (!string.IsNullOrWhiteSpace(product.Id))
                {
                    categoryItemRef.Products[product.Id] = new TalabatV2ItemReference
                    {
                        Id = product.Id,
                        Type = "Product",
                        Order = productOrder++
                    };
                }
            }
        }

        // Step 2b: Add hidden category that contains all option products used by toppings
        // Talabat requires: All products under Topping must also be listed under Category#hiddenId
        if (hiddenOptionProductIds.Count > 0)
        {
            const string hiddenCategoryId = "Category#hiddenId";
            var hiddenCategoryItem = new TalabatV2CatalogItem
            {
                Id = hiddenCategoryId,
                Type = "Category",
                Order = 999, // Put at end
                Title = new TalabatV2Title { Default = "hidden" },
                Description = new TalabatV2Title { Default = "hidden" },
                Products = new Dictionary<string, TalabatV2ItemReference>()
            };

            int hiddenOrder = 0;
            foreach (var optionProductId in hiddenOptionProductIds)
            {
                hiddenCategoryItem.Products[optionProductId] = new TalabatV2ItemReference
                {
                    Id = optionProductId,
                    Type = "Product",
                    Order = hiddenOrder++
                };
            }

            items[hiddenCategoryId] = hiddenCategoryItem;
            categoryMap[hiddenCategoryId] = hiddenCategoryItem;
            
            _logger.LogInformation(
                "Added hidden category with {Count} topping option products",
                hiddenOptionProductIds.Count);
        }

        // Step 3: Create schedule entry with weekDays (required by Talabat)
        var scheduleItem = new TalabatV2CatalogItem
        {
            Id = scheduleId,
            Type = "ScheduleEntry",
            StartTime = "08:00:00",
            EndTime = "23:59:00",
            WeekDays = new List<string>
            {
                "MONDAY", "TUESDAY", "WEDNESDAY", "THURSDAY",
                "FRIDAY", "SATURDAY", "SUNDAY"
            }
        };
        items[scheduleId] = scheduleItem;

        // Step 4: Create menu
        var menuItem = new TalabatV2CatalogItem
        {
            Id = menuId,
            Type = "Menu",
            Title = new TalabatV2Title { Default = "Foodics Menu" },
            Description = new TalabatV2Title { Default = "Menu imported from Foodics" },
            MenuType = "DELIVERY",
            Schedule = new Dictionary<string, TalabatV2ItemReference>
            {
                [scheduleId] = new TalabatV2ItemReference
                {
                    Id = scheduleId,
                    Type = "ScheduleEntry"
                }
            },
            Products = new Dictionary<string, TalabatV2ItemReference>()
        };

        // Link all categories to menu (Menu → Categories → Products)
        // This is the correct structure for Talabat V2 API
        foreach (var category in categoryMap)
        {
            menuItem.Products![category.Key] = new TalabatV2ItemReference
            {
                Id = category.Key,
                Type = "Category",
                Order = category.Value.Order
            };
        }

        items[menuId] = menuItem;

        // Build callback URL
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            var baseCallbackUrl = _configuration["Talabat:CallbackBaseUrl"];
            if (!string.IsNullOrWhiteSpace(baseCallbackUrl))
            {
                callbackUrl = $"{baseCallbackUrl.TrimEnd('/')}/catalog-status";
            }
        }

        // Talabat API validation: callbackUrl must not contain localhost, test., or example.
        // Pattern: ^(http|https|HTTP|HTTPS)://((?!test.)(?!example.)(?!localhost.).)*$
        if (!string.IsNullOrWhiteSpace(callbackUrl))
        {
            var callbackUri = callbackUrl.ToLowerInvariant();
            if (callbackUri.Contains("localhost") || 
                callbackUri.Contains("test.") || 
                callbackUri.Contains("example."))
            {
                _logger.LogWarning(
                    "CallbackUrl contains invalid domain (localhost/test/example). Removing callbackUrl from request. Url: {CallbackUrl}",
                    callbackUrl);
                callbackUrl = null; // Remove invalid callbackUrl
            }
        }

        // Get vendor codes - use specific vendorCode if provided
        var vendors = GetVendors(vendorCode);

        var request = new TalabatV2CatalogSubmitRequest
        {
            Catalog = new TalabatV2Catalog { Items = items },
            Vendors = vendors,
            CallbackUrl = callbackUrl // Will be null if invalid
        };

        _logger.LogInformation(
            "Mapped to V2 format (legacy): {ItemCount} items, {CategoryCount} categories, {ToppingCount} toppings, {ImageCount} images, VendorCode={VendorCode}",
            items.Count,
            categoryMap.Count,
            toppingMap.Count,
            imageMap.Count);

        return request;
    }

    /// <summary>
    /// Creates product item with stable remote code
    /// </summary>
    private TalabatV2CatalogItem CreateProductItemWithStableId(FoodicsProductDetailDto product, MenuItemMapping productMapping)
    {
        return new TalabatV2CatalogItem
        {
            Id = productMapping.TalabatRemoteCode, // Use stable remote code
            Type = "Product",
            Title = new TalabatV2Title
            {
                Default = product.Name ?? $"Product-{product.Id}"
            },
            Description = product.Description != null
                ? new TalabatV2Title { Default = product.Description }
                : null,
            Price = (product.Price ?? 0).ToString("F2"),
            Active = product.IsActive ?? true,
            IsPrepackedItem = false,
            IsExpressItem = false,
            ExcludeDishInformation = false
        };
    }

    /// <summary>
    /// Creates category item with stable remote code
    /// </summary>
    private TalabatV2CatalogItem CreateCategoryItemWithStableId(string categoryId, FoodicsCategoryInfoDto? categoryInfo, MenuItemMapping categoryMapping, int order = 0)
    {
        return new TalabatV2CatalogItem
        {
            Id = categoryId,
            Type = "Category",
            Order = order,
            Title = new TalabatV2Title
            {
                Default = categoryInfo?.Name ?? $"Category-{categoryMapping.FoodicsId}"
            },
            Description = new TalabatV2Title
            {
                Default = categoryInfo?.NameLocalized ?? categoryInfo?.Name ?? "Category"
            },
            Products = new Dictionary<string, TalabatV2ItemReference>()
        };
    }

    /// <summary>
    /// Creates topping item with stable remote codes for modifier and options
    /// </summary>
    private (TalabatV2CatalogItem ToppingItem, List<TalabatV2CatalogItem> OptionProducts) CreateToppingItemWithStableIds(
        FoodicsModifierDto modifier, 
        MenuItemMapping modifierMapping,
        string toppingId, 
        int toppingOrder,
        Dictionary<string, MenuItemMapping> mappings,
        Dictionary<string, TalabatV2CatalogItem>? imageMap = null,
        Dictionary<string, TalabatV2CatalogItem>? items = null)
    {
        var toppingItem = new TalabatV2CatalogItem
        {
            Id = toppingId,
            Type = "Topping",
            Order = toppingOrder,
            Title = new TalabatV2Title
            {
                Default = modifier.Name ?? $"Topping-{modifier.Id}"
            },
            Quantity = new TalabatV2Quantity
            {
                // Use actual MinAllowed/MaxAllowed from Foodics
                // This is critical for required choices (MinAllowed > 0)
                Minimum = modifier.MinAllowed ?? 0,
                Maximum = modifier.MaxAllowed ?? modifier.Options?.Count ?? 1
            },
            Products = new Dictionary<string, TalabatV2ItemReference>()
        };

        var optionProducts = new List<TalabatV2CatalogItem>();

        // Add modifier options as products with order using stable IDs
        int optionOrder = 0;
        if (modifier.Options != null)
        {
            foreach (var option in modifier.Options)
            {
                if (string.IsNullOrWhiteSpace(option.Id))
                    continue;

                // Get stable option mapping
                if (!mappings.TryGetValue(option.Id, out var optionMapping))
                {
                    _logger.LogWarning(
                        "Modifier option mapping not found for FoodicsId={OptionId}. Skipping option.",
                        option.Id);
                    continue;
                }

                var optionProductId = $"topping-{optionMapping.TalabatRemoteCode}";
                
                // Create product item for modifier option
                var optionProduct = new TalabatV2CatalogItem
                {
                    Id = optionProductId,
                    Type = "Product",
                    Title = new TalabatV2Title
                    {
                        Default = option.Name ?? $"Option-{option.Id}"
                    },
                    Description = !string.IsNullOrWhiteSpace(option.Name) 
                        ? new TalabatV2Title { Default = option.Name } 
                        : null,
                    Price = (option.Price ?? 0).ToString("F2"),
                    Active = true
                };

                // Add image support for choice options (Test Case 2: Menu with Choice Images)
                if (!string.IsNullOrWhiteSpace(option.Image) && imageMap != null && items != null)
                {
                    var optionImageId = $"image-{optionProductId}";
                    
                    // Check if image already exists to avoid duplicates
                    if (!imageMap.ContainsKey(optionImageId))
                    {
                        var optionImageItem = CreateImageItem(optionImageId, option.Image, option.Name);
                        if (optionImageItem != null)
                        {
                            imageMap[optionImageId] = optionImageItem;
                            items[optionImageId] = optionImageItem;
                            
                            _logger.LogDebug(
                                "Created image for choice option. OptionId={OptionId}, OptionName={OptionName}, ImageId={ImageId}",
                                option.Id,
                                option.Name,
                                optionImageId);
                        }
                    }
                    
                    // Link image to option product
                    if (imageMap.ContainsKey(optionImageId))
                    {
                        optionProduct.Images = new Dictionary<string, TalabatV2ItemReference>
                        {
                            [optionImageId] = new TalabatV2ItemReference
                            {
                                Id = optionImageId,
                                Type = "Image"
                            }
                        };
                    }
                }

                optionProducts.Add(optionProduct);

                // Link to topping with order
                toppingItem.Products[optionProductId] = new TalabatV2ItemReference
                {
                    Id = optionProductId,
                    Type = "Product",
                    Order = optionOrder++
                };
            }
        }

        return (toppingItem, optionProducts);
    }
    

    private TalabatV2CatalogItem CreateProductItem(FoodicsProductDetailDto product)
    {
        return new TalabatV2CatalogItem
        {
            Id = product.Id,
            Type = "Product",
            Title = new TalabatV2Title
            {
                Default = product.Name ?? $"Product-{product.Id}"
            },
            Description = product.Description != null
                ? new TalabatV2Title { Default = product.Description }
                : null,
            Price = (product.Price ?? 0).ToString("F2"),
            Active = product.IsActive ?? true,
            IsPrepackedItem = false,
            IsExpressItem = false,
            ExcludeDishInformation = false
        };
    }

    /// <summary>
    /// Gets vendor codes for Talabat catalog submission.
    /// If vendorCode is provided (from TalabatAccount), uses it exclusively.
    /// Otherwise falls back to configuration values (legacy behavior).
    /// </summary>
    private List<string> GetVendors(string? vendorCode = null)
    {
        var vendors = new List<string>();

        // ✅ NEW: If vendorCode is provided from TalabatAccount, use it exclusively
        // This ensures each TalabatAccount gets its own vendor code in the vendors array
        if (!string.IsNullOrWhiteSpace(vendorCode))
        {
            vendors.Add(vendorCode);
            _logger.LogInformation(
                "Using specific vendor code from TalabatAccount: {VendorCode}",
                vendorCode);
            return vendors; // Return early - use specific vendor code
        }

        // Fallback to configuration (legacy behavior for backward compatibility)
        _logger.LogDebug("No vendorCode provided, falling back to configuration values");

        var vendorsFromConfig = _configuration.GetSection("Talabat:Vendors").Get<string[]>();
        if (vendorsFromConfig != null && vendorsFromConfig.Length > 0)
        {
            vendors.AddRange(vendorsFromConfig.Where(v => !string.IsNullOrWhiteSpace(v))!);
        }

        var platformVendorId = _configuration["Talabat:PlatformVendorId"];
        var defaultVendorCode = _configuration["Talabat:DefaultVendorCode"];

        if (!string.IsNullOrWhiteSpace(platformVendorId) && !vendors.Contains(platformVendorId))
        {
            vendors.Add(platformVendorId);
        }
        else if (!string.IsNullOrWhiteSpace(defaultVendorCode) && !vendors.Contains(defaultVendorCode))
        {
            vendors.Add(defaultVendorCode);
        }

        return vendors;
    }

    private TalabatV2CatalogItem CreateCategoryItem(string categoryId, FoodicsCategoryInfoDto? categoryInfo, int order = 0)
    {
        return new TalabatV2CatalogItem
        {
            Id = categoryId,
            Type = "Category",
            Order = order,
            Title = new TalabatV2Title
            {
                Default = categoryInfo?.Name ?? $"Category-{categoryId}"
            },
            Description = new TalabatV2Title
            {
                Default = categoryInfo?.NameLocalized ?? categoryInfo?.Name ?? "Category"
            },
            Products = new Dictionary<string, TalabatV2ItemReference>()
        };
    }

    private (TalabatV2CatalogItem ToppingItem, List<TalabatV2CatalogItem> OptionProducts) CreateToppingItem(
        FoodicsModifierDto modifier, 
        string toppingId, 
        int toppingOrder = 0,
        Dictionary<string, TalabatV2CatalogItem>? imageMap = null,
        Dictionary<string, TalabatV2CatalogItem>? items = null)
    {
        var toppingItem = new TalabatV2CatalogItem
        {
            Id = toppingId,
            Type = "Topping",
            Order = toppingOrder,
            Title = new TalabatV2Title
            {
                Default = modifier.Name ?? $"Topping-{modifier.Id}"
            },
            Quantity = new TalabatV2Quantity
            {
                // Use actual MinAllowed/MaxAllowed from Foodics
                // This is critical for required choices (MinAllowed > 0)
                Minimum = modifier.MinAllowed ?? 0,
                Maximum = modifier.MaxAllowed ?? modifier.Options?.Count ?? 1
            },
            Products = new Dictionary<string, TalabatV2ItemReference>()
        };

        var optionProducts = new List<TalabatV2CatalogItem>();

        // Add modifier options as products with order
        int optionOrder = 0;
        if (modifier.Options != null)
        {
            foreach (var option in modifier.Options)
            {
                if (string.IsNullOrWhiteSpace(option.Id))
                    continue;

                var optionProductId = $"topping-{option.Id}";
                
                // Create product item for modifier option
                var optionProduct = new TalabatV2CatalogItem
                {
                    Id = optionProductId,
                    Type = "Product",
                    Title = new TalabatV2Title
                    {
                        Default = option.Name ?? $"Option-{option.Id}"
                    },
                    Description = !string.IsNullOrWhiteSpace(option.Name) 
                        ? new TalabatV2Title { Default = option.Name } 
                        : null,
                    Price = (option.Price ?? 0).ToString("F2"),
                    Active = true
                };

                // Add image support for choice options (Test Case 2: Menu with Choice Images)
                if (!string.IsNullOrWhiteSpace(option.Image) && imageMap != null && items != null)
                {
                    var optionImageId = $"image-{optionProductId}";
                    
                    // Check if image already exists to avoid duplicates
                    if (!imageMap.ContainsKey(optionImageId))
                    {
                        var optionImageItem = CreateImageItem(optionImageId, option.Image, option.Name);
                        if (optionImageItem != null)
                        {
                            imageMap[optionImageId] = optionImageItem;
                            items[optionImageId] = optionImageItem;
                            
                            _logger.LogDebug(
                                "Created image for choice option. OptionId={OptionId}, OptionName={OptionName}, ImageId={ImageId}",
                                option.Id,
                                option.Name,
                                optionImageId);
                        }
                    }
                    
                    // Link image to option product
                    if (imageMap.ContainsKey(optionImageId))
                    {
                        optionProduct.Images = new Dictionary<string, TalabatV2ItemReference>
                        {
                            [optionImageId] = new TalabatV2ItemReference
                            {
                                Id = optionImageId,
                                Type = "Image"
                            }
                        };
                    }
                }

                optionProducts.Add(optionProduct);

                // Link to topping with order
                toppingItem.Products[optionProductId] = new TalabatV2ItemReference
                {
                    Id = optionProductId,
                    Type = "Product",
                    Price = (option.Price ?? 0).ToString("F2"),
                    Order = optionOrder++
                };
            }
        }

        return (toppingItem, optionProducts);
    }

    private TalabatV2CatalogItem? CreateImageItem(string imageId, string imageUrl, string? altText = null)
    {
        var normalizedUrl = NormalizeImageUrl(imageUrl);
        if (string.IsNullOrWhiteSpace(normalizedUrl))
        {
            return null;
        }

        return new TalabatV2CatalogItem
        {
            Id = imageId,
            Type = "Image",
            Url = normalizedUrl,
            Alt = new TalabatV2Title { Default = altText ?? imageId }
        };
    }
}

