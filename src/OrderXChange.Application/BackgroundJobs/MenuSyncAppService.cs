using System;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.Application.Services;
using Volo.Abp.DependencyInjection;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderXChange.Application.Staging;
using OrderXChange.Domain.Staging;
using OrderXChange.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;
using Volo.Abp;
using Foodics;
using Volo.Abp.TenantManagement.Talabat;
using Hangfire;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// Menu sync service that builds menu structure directly from products endpoint.
/// Uses /products?include=category,price_tags,tax_group,tags,branches,ingredients.branches,modifiers,modifiers.options,modifiers.options.branches,discounts,timed_events,groups
/// </summary>
public class MenuSyncAppService : ApplicationService, IMenuSyncAppService, ITransientDependency
{
    private readonly FoodicsCatalogClient _foodicsCatalogClient;
    private readonly FoodicsAccountTokenService _tokenService;
    private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
    private readonly IRepository<TalabatAccount, Guid> _talabatAccountRepository;
    private readonly IDbContextProvider<OrderXChangeDbContext> _dbContextProvider;
    private readonly FoodicsProductStagingToFoodicsConverter _stagingConverter;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public MenuSyncAppService(
        FoodicsCatalogClient foodicsCatalogClient,
        FoodicsAccountTokenService tokenService,
        IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
        IRepository<TalabatAccount, Guid> talabatAccountRepository,
        IDbContextProvider<OrderXChangeDbContext> dbContextProvider,
        FoodicsProductStagingToFoodicsConverter stagingConverter,
        IBackgroundJobClient backgroundJobClient)
    {
        _foodicsCatalogClient = foodicsCatalogClient;
        _tokenService = tokenService;
        _foodicsAccountRepository = foodicsAccountRepository;
        _talabatAccountRepository = talabatAccountRepository;
        _dbContextProvider = dbContextProvider;
        _stagingConverter = stagingConverter;
        _backgroundJobClient = backgroundJobClient;
    }

    /// <summary>
    /// Manually trigger a menu sync for a specific account via Kafka
    /// </summary>
    public async Task TriggerMenuSyncAsync(
        Guid? foodicsAccountId = null,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var jobId = _backgroundJobClient.Enqueue<MenuSyncRecurringJob>(job =>
            job.ExecuteAsync(foodicsAccountId, branchId, false, default));

        Logger.LogInformation(
            "Enqueued MenuSyncRecurringJob {JobId}. FoodicsAccountId={FoodicsAccountId}, BranchId={BranchId}",
            jobId,
            foodicsAccountId?.ToString() ?? "<all>",
            branchId ?? "<all>");

        await Task.CompletedTask;
    }

    /// <summary>
    /// Legacy method - kept for backward compatibility but delegates to GetAggregatedAsync
    /// </summary>
    public Task<FoodicsMenuDisplayResponseDto> GetAsync(string? branchId = null)
    {
        // Return empty menu display - this method is deprecated in favor of GetAggregatedAsync
        return Task.FromResult(new FoodicsMenuDisplayResponseDto());
    }

    /// <summary>
    /// Gets aggregated menu by fetching all products with full includes and building menu structure from products.
    /// Groups products by category and by custom groups.
    /// </summary>
    /// <param name="branchId">Optional branch ID to filter products</param>
    /// <param name="foodicsAccountId">Optional FoodicsAccount ID. If not provided, uses current tenant's account or configuration token</param>
    public async Task<FoodicsAggregatedMenuDto> GetAggregatedAsync(string? branchId = null, Guid? foodicsAccountId = null)
    {
        // Get access token from FoodicsAccount or fallback to configuration
        var accessToken = await _tokenService.GetAccessTokenWithFallbackAsync(foodicsAccountId, CancellationToken.None);

        // Fetch ALL products with full includes using the products endpoint
        // This single call provides all data needed: products, categories, groups, modifiers, etc.
        var allProducts = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
            branchId, 
            accessToken: accessToken,
            perPage: 100,
            includeDeleted: false,
            CancellationToken.None);

        // Build menu structure from products
        var result = BuildMenuFromProducts(allProducts.Values);

        return result;
    }

    /// <summary>
    /// Gets enhanced aggregated menu with detailed branch-level analysis.
    /// Shows categories, menu groups, and products breakdown for each branch.
    /// </summary>
    /// <param name="request">Request parameters for enhanced analysis</param>
    public async Task<FoodicsEnhancedAggregatedMenuDto> GetEnhancedAggregatedAsync(GetEnhancedAggregatedMenuRequest request)
    {
        // NEW: Load products from staging table (AppFoodicsProductStaging) instead of calling Foodics API
        // This makes the dashboard consistent with what will be submitted to Talabat.
        var foodicsAccountId = await ResolveFoodicsAccountIdAsync(request.FoodicsAccountId, CancellationToken.None);
        var stagingProducts = await LoadStagingProductsAsync(foodicsAccountId, CancellationToken.None);
        var productsList = _stagingConverter.ConvertToFoodicsDto(stagingProducts);

        // Filter products based on request parameters
        if (!request.IncludeInactiveProducts)
        {
            productsList = productsList.Where(p => p.IsActive == true).ToList();
        }

        if (!request.IncludeUncategorizedProducts)
        {
            productsList = productsList.Where(p => !string.IsNullOrWhiteSpace(p.CategoryId)).ToList();
        }

        // Optional branch filter: keep only products available in the requested branch (based on Branches list)
        if (!string.IsNullOrWhiteSpace(request.BranchId))
        {
            var branchId = request.BranchId;
            productsList = productsList
                .Where(p => p.Branches != null && p.Branches.Any(b => b.Id == branchId))
                .ToList();
        }

        // Build enhanced menu structure with branch analysis
        var result = BuildEnhancedMenuFromProducts(productsList, request);

        return result;
    }

    public async Task<List<StagingMenuGroupSummaryDto>> GetStagingMenuGroupSummaryAsync(GetStagingMenuGroupSummaryRequest request)
    {
        var foodicsAccountId = await ResolveFoodicsAccountIdAsync(request.FoodicsAccountId, CancellationToken.None);
        var stagingProducts = await LoadStagingProductsAsync(foodicsAccountId, CancellationToken.None);

        // Active products only (as requested)
        stagingProducts = stagingProducts
            .Where(x => x.IsActive && !x.IsDeleted)
            .ToList();

        var products = _stagingConverter.ConvertToFoodicsDto(stagingProducts);

        // Optional branch filter (based on Branches list)
        if (!string.IsNullOrWhiteSpace(request.BranchId))
        {
            var branchId = request.BranchId;
            products = products
                .Where(p => p.Branches != null && p.Branches.Any(b => b.Id == branchId))
                .ToList();
        }

        var map = new Dictionary<string, (string Name, HashSet<string> ProductNames, HashSet<string> CategoryKeys)>(StringComparer.OrdinalIgnoreCase);

        foreach (var p in products)
        {
            if (p.Groups == null || p.Groups.Count == 0)
            {
                continue;
            }

            var categoryKey = !string.IsNullOrWhiteSpace(p.CategoryId)
                ? $"id:{p.CategoryId}"
                : (!string.IsNullOrWhiteSpace(p.Category?.Name) ? $"name:{p.Category.Name}" : "none");

            foreach (var g in p.Groups.Where(x => !string.IsNullOrWhiteSpace(x.Id)))
            {
                var groupId = g.Id!;
                var groupName = g.Name ?? g.NameLocalized ?? groupId;

                if (!map.TryGetValue(groupId, out var bucket))
                {
                    bucket = (groupName, new HashSet<string>(StringComparer.OrdinalIgnoreCase), new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }

                bucket.ProductNames.Add(p.Name ?? p.Id ?? string.Empty);
                if (categoryKey != "none")
                {
                    bucket.CategoryKeys.Add(categoryKey);
                }

                // Update tuple (struct-like)
                map[groupId] = bucket;
            }
        }

        // Get all TalabatAccounts that have FoodicsGroupId configured (mapped groups)
        var talabatAccountsQueryable = await _talabatAccountRepository.GetQueryableAsync();
        var mappedGroupIds = await talabatAccountsQueryable
            .Where(ta => ta.IsActive && !string.IsNullOrWhiteSpace(ta.FoodicsGroupId))
            .Select(ta => ta.FoodicsGroupId!)
            .Distinct()
            .ToListAsync(CancellationToken.None);

        var mappedGroupIdsSet = new HashSet<string>(mappedGroupIds, StringComparer.OrdinalIgnoreCase);

        var result = map
            .Select(kvp => new StagingMenuGroupSummaryDto
            {
                GroupId = kvp.Key,
                GroupName = kvp.Value.Name,
                TotalProducts = kvp.Value.ProductNames.Count,
                TotalCategories = kvp.Value.CategoryKeys.Count,
                IsMappedToTalabatAccount = mappedGroupIdsSet.Contains(kvp.Key)
            })
            .OrderByDescending(x => x.TotalProducts)
            .ThenBy(x => x.GroupName)
            .ToList();

        return result;
    }

    private async Task<Guid> ResolveFoodicsAccountIdAsync(Guid? foodicsAccountId, CancellationToken cancellationToken)
    {
        if (foodicsAccountId.HasValue)
        {
            return foodicsAccountId.Value;
        }

        if (!CurrentTenant.Id.HasValue)
        {
            throw new UserFriendlyException("No FoodicsAccountId provided and no tenant context is available.");
        }

        var account = await _foodicsAccountRepository.FirstOrDefaultAsync(
            x => x.TenantId == CurrentTenant.Id.Value,
            cancellationToken: cancellationToken);

        if (account == null)
        {
            throw new UserFriendlyException("No Foodics account is configured for the current tenant.");
        }

        return account.Id;
    }

    private async Task<List<FoodicsProductStaging>> LoadStagingProductsAsync(Guid foodicsAccountId, CancellationToken cancellationToken)
    {
        var dbContext = await _dbContextProvider.GetDbContextAsync();

        // NOTE: one row per product per Foodics account (BranchId is not part of the unique key).
        return await dbContext.Set<FoodicsProductStaging>()
            .AsNoTracking()
            .Where(x => x.FoodicsAccountId == foodicsAccountId)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Builds enhanced aggregated menu structure with detailed branch analysis from product collection.
    /// Analyzes categories, menu groups, and products for each branch.
    /// </summary>
    private static FoodicsEnhancedAggregatedMenuDto BuildEnhancedMenuFromProducts(
        IEnumerable<FoodicsProductDetailDto> products, 
        GetEnhancedAggregatedMenuRequest request)
    {
        var productsList = products.ToList();
        
        // Build account-level summary
        var accountSummary = BuildAccountSummary(productsList);
        
        // Get all unique branches from products
        var allBranches = productsList
            .Where(p => p.Branches != null && p.Branches.Count > 0)
            .SelectMany(p => p.Branches!)
            .GroupBy(b => b.Id)
            .Select(g => g.First())
            .ToList();

        // If specific branch requested, filter to that branch
        if (!string.IsNullOrWhiteSpace(request.BranchId))
        {
            allBranches = allBranches.Where(b => b.Id == request.BranchId).ToList();
        }

        // Build branch analysis for each branch
        var branchAnalysis = allBranches.Select(branch => 
            BuildBranchAnalysis(branch, productsList, request.IncludeProductDetails))
            .ToList();

        // Build legacy structures for backward compatibility
        var legacyMenu = BuildMenuFromProducts(productsList);

        return new FoodicsEnhancedAggregatedMenuDto
        {
            AccountSummary = accountSummary,
            BranchAnalysis = branchAnalysis,
            Categories = legacyMenu.Categories,
            Custom = legacyMenu.Custom
        };
    }

    /// <summary>
    /// Builds account-level summary statistics
    /// </summary>
    private static FoodicsAccountSummaryDto BuildAccountSummary(List<FoodicsProductDetailDto> products)
    {
        var allBranches = products
            .Where(p => p.Branches != null && p.Branches.Count > 0)
            .SelectMany(p => p.Branches!)
            .GroupBy(b => b.Id)
            .Select(g => g.First())
            .ToList();

        var allCategories = products
            .Where(p => !string.IsNullOrWhiteSpace(p.CategoryId))
            .Select(p => p.CategoryId!)
            .Distinct()
            .ToList();

        var allMenuGroups = products
            .Where(p => p.Groups != null && p.Groups.Count > 0)
            .SelectMany(p => p.Groups!)
            .Select(g => g.Id)
            .Distinct()
            .ToList();

        return new FoodicsAccountSummaryDto
        {
            TotalBranches = allBranches.Count,
            TotalProducts = products.Count,
            TotalCategories = allCategories.Count,
            TotalMenuGroups = allMenuGroups.Count,
            ActiveProducts = products.Count(p => p.IsActive == true),
            InactiveProducts = products.Count(p => p.IsActive != true),
            AllCategoryIds = allCategories,
            AllMenuGroupIds = allMenuGroups
        };
    }

    /// <summary>
    /// Builds detailed analysis for a specific branch
    /// </summary>
    private static FoodicsBranchAnalysisDto BuildBranchAnalysis(
        FoodicsBranchDto branch, 
        List<FoodicsProductDetailDto> allProducts,
        bool includeProductDetails)
    {
        // Get products available in this branch
        var branchProducts = allProducts
            .Where(p => p.Branches != null && p.Branches.Any(b => b.Id == branch.Id))
            .ToList();

        // Build branch statistics
        var stats = new FoodicsBranchStatsDto
        {
            TotalProducts = branchProducts.Count,
            ActiveProducts = branchProducts.Count(p => p.IsActive == true),
            InactiveProducts = branchProducts.Count(p => p.IsActive != true),
            ProductsWithModifiers = branchProducts.Count(p => p.Modifiers != null && p.Modifiers.Count > 0),
            ProductsWithoutCategory = branchProducts.Count(p => string.IsNullOrWhiteSpace(p.CategoryId)),
            ProductsWithoutMenuGroup = branchProducts.Count(p => p.Groups == null || p.Groups.Count == 0)
        };

        // Build categories analysis for this branch
        var branchCategories = BuildBranchCategories(branchProducts);
        stats.CategoriesCount = branchCategories.Count;

        // Build menu groups analysis for this branch
        var branchMenuGroups = BuildBranchMenuGroups(branchProducts);
        stats.MenuGroupsCount = branchMenuGroups.Count;

        // Build products by category structure if detailed info requested
        var productsByCategory = includeProductDetails 
            ? BuildProductsByCategoryForBranch(branchProducts)
            : new List<FoodicsAggregatedCategoryDto>();

        return new FoodicsBranchAnalysisDto
        {
            Branch = branch,
            Stats = stats,
            Categories = branchCategories,
            MenuGroups = branchMenuGroups,
            ProductsByCategory = productsByCategory
        };
    }

    /// <summary>
    /// Builds category analysis for a branch
    /// </summary>
    private static List<FoodicsBranchCategoryDto> BuildBranchCategories(List<FoodicsProductDetailDto> branchProducts)
    {
        return branchProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.CategoryId))
            .GroupBy(p => p.CategoryId!)
            .Select(categoryGroup =>
            {
                var firstProduct = categoryGroup.First();
                var category = firstProduct.Category ?? new FoodicsCategoryInfoDto 
                { 
                    Id = firstProduct.CategoryId! 
                };

                return new FoodicsBranchCategoryDto
                {
                    Category = category,
                    ProductCount = categoryGroup.Count(),
                    ActiveProductCount = categoryGroup.Count(p => p.IsActive == true),
                    ProductIds = categoryGroup.Select(p => p.Id).ToList()
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds menu group analysis for a branch
    /// </summary>
    private static List<FoodicsBranchMenuGroupDto> BuildBranchMenuGroups(List<FoodicsProductDetailDto> branchProducts)
    {
        return branchProducts
            .Where(p => p.Groups != null && p.Groups.Count > 0)
            .SelectMany(p => p.Groups!.Select(g => new { Group = g, Product = p }))
            .GroupBy(x => x.Group.Id)
            .Select(groupData =>
            {
                var group = groupData.First().Group;
                var groupProducts = groupData.Select(x => x.Product).ToList();
                
                return new FoodicsBranchMenuGroupDto
                {
                    GroupId = group.Id,
                    GroupName = group.Name,
                    ProductCount = groupProducts.Count,
                    ActiveProductCount = groupProducts.Count(p => p.IsActive == true),
                    ProductIds = groupProducts.Select(p => p.Id).Distinct().ToList(),
                    CategoryIds = groupProducts
                        .Where(p => !string.IsNullOrWhiteSpace(p.CategoryId))
                        .Select(p => p.CategoryId!)
                        .Distinct()
                        .ToList()
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds products by category structure for a specific branch
    /// </summary>
    private static List<FoodicsAggregatedCategoryDto> BuildProductsByCategoryForBranch(List<FoodicsProductDetailDto> branchProducts)
    {
        return branchProducts
            .Where(p => !string.IsNullOrWhiteSpace(p.CategoryId))
            .GroupBy(p => p.CategoryId!)
            .Select(categoryGroup =>
            {
                var firstProduct = categoryGroup.First();
                var category = firstProduct.Category ?? new FoodicsCategoryInfoDto 
                { 
                    Id = firstProduct.CategoryId! 
                };

                return new FoodicsAggregatedCategoryDto
                {
                    Category = category,
                    Children = categoryGroup.Select(p => new FoodicsAggregatedChildDto
                    {
                        Type = "product",
                        Id = p.Id,
                        Product = p
                    }).ToList()
                };
            })
            .ToList();
    }

    /// <summary>
    /// Builds aggregated menu structure from product collection.
    /// Groups products by category and by custom groups.
    /// </summary>
    private static FoodicsAggregatedMenuDto BuildMenuFromProducts(IEnumerable<FoodicsProductDetailDto> products)
    {
        var productsList = products.ToList();
        
        // Group products by category
        var productsByCategory = productsList
            .Where(p => !string.IsNullOrWhiteSpace(p.CategoryId))
            .GroupBy(p => p.CategoryId!)
            .ToList();

        // Build categories with products
        var categories = productsByCategory.Select(categoryGroup =>
        {
            var firstProduct = categoryGroup.First();
            var category = firstProduct.Category ?? new FoodicsCategoryInfoDto 
            { 
                Id = firstProduct.CategoryId! 
            };

            return new FoodicsAggregatedCategoryDto
            {
                Category = category,
                Children = categoryGroup.Select(p => new FoodicsAggregatedChildDto
                {
                    Type = "product",
                    Id = p.Id,
                    // Use full product details with all includes: branches, groups, modifiers, price_tags, tax_group, etc.
                    Product = p
                }).ToList()
            };
        }).ToList();

        // Group products by custom groups
        // Products can belong to multiple groups, so we flatten and group
        var productsByGroup = productsList
            .Where(p => p.Groups != null && p.Groups.Count > 0)
            .SelectMany(p => p.Groups!.Select(g => new { Group = g, Product = p }))
            .GroupBy(x => x.Group.Id)
            .ToList();

        var customGroups = productsByGroup.Select(group =>
        {
            var groupInfo = group.First().Group;
            // Use HashSet to ensure unique products by ID
            var uniqueProducts = new HashSet<string>();
            var groupProducts = group
                .Where(x => !string.IsNullOrWhiteSpace(x.Product.Id) && uniqueProducts.Add(x.Product.Id))
                .Select(x => x.Product);

            return new FoodicsAggregatedCustomGroupDto
            {
                GroupId = groupInfo.Id,
                Children = groupProducts.Select(p => new FoodicsAggregatedChildDto
                {
                    Type = "product",
                    Id = p.Id,
                    // Use full product details with all includes: branches, groups, modifiers, price_tags, tax_group, etc.
                    Product = p
                }).ToList()
            };
        }).ToList();

        return new FoodicsAggregatedMenuDto
        {
            Categories = categories,
            Custom = customGroups
        };
    }

    /// <summary>
    /// Gets available active branches for a specific FoodicsAccount.
    /// Only returns branches where is_active is true.
    /// Used for dropdown selection when configuring TalabatAccount.
    /// </summary>
    public async Task<List<FoodicsBranchDto>> GetBranchesForAccountAsync(Guid foodicsAccountId)
    {
        var accessToken = await _tokenService.GetAccessTokenWithFallbackAsync(foodicsAccountId, CancellationToken.None);
        
        var allProducts = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
            branchId: null,
            accessToken: accessToken,
            perPage: 100,
            includeDeleted: false,
            CancellationToken.None);
        
        // Extract unique ACTIVE branches from products
        var branches = allProducts.Values
            .Where(p => p.Branches != null && p.Branches.Count > 0)
            .SelectMany(p => p.Branches!)
            .GroupBy(b => b.Id)
            .Select(g => g.First())
            .Where(b => !string.IsNullOrEmpty(b.Id) && b.IsActive == true) // Only active branches
            .OrderBy(b => b.Name)
            .ToList();
        
        return branches;
    }

    /// <summary>
    /// Gets available groups for a specific FoodicsAccount.
    /// Used for dropdown selection when configuring TalabatAccount group filtering.
    /// Returns all unique groups that have products assigned to them.
    /// </summary>
    public async Task<List<FoodicsGroupWithProductCountDto>> GetGroupsForAccountAsync(Guid foodicsAccountId)
    {
        var accessToken = await _tokenService.GetAccessTokenWithFallbackAsync(foodicsAccountId, CancellationToken.None);
        
        var allProducts = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
            branchId: null,
            accessToken: accessToken,
            perPage: 100,
            includeDeleted: false,
            CancellationToken.None);
        
        // Extract unique groups from products and count products per group
        var groupSummaries = allProducts.Values
            .Where(p => p.Groups != null && p.Groups.Any())
            .SelectMany(p => p.Groups!)
            .GroupBy(g => g.Id)
            .Select(grp =>
            {
                var firstGroup = grp.First();
                var productCount = allProducts.Values.Count(p =>
                    p.Groups != null &&
                    p.Groups.Any(pg => string.Equals(pg.Id, firstGroup.Id, StringComparison.OrdinalIgnoreCase)));

                return new FoodicsGroupWithProductCountDto
                {
                    Id = firstGroup.Id,
                    Name = firstGroup.Name,
                    NameLocalized = firstGroup.NameLocalized,
                    ProductCount = productCount
                };
            })
            .Where(g => !string.IsNullOrEmpty(g.Id)) // Only groups with valid IDs
            .OrderBy(g => g.Name)
            .ToList();
        
        return groupSummaries;
    }
}
