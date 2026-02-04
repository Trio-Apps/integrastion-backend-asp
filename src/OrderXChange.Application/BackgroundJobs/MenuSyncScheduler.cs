using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Idempotency;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Application.Staging;
using OrderXChange.Domain.Staging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.MultiTenancy;
using Volo.Abp.Domain.Repositories;
using Microsoft.Extensions.Configuration;
using System.Linq;
using Foodics;
using Volo.Abp.TenantManagement.Talabat;
using Volo.Abp.Data;
using Volo.Abp.TenantManagement;
using Volo.Abp.Uow;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// UPDATED: Now works with database (FoodicsAccount and TalabatAccount entities) instead of hardcoded values
/// UPDATED: Works with Hangfire Job without tenant context - processes all tenants automatically
/// FIXED: Uses IUnitOfWorkManager with requiresNew:true to avoid disposed DbContext issues
/// Hangfire scheduler that publishes MenuSync events to Kafka
/// This is the entry point triggered by Hangfire recurring jobs
/// Actual processing happens in MenuSyncDistributedEventHandler (Kafka consumer)
/// </summary>
public class MenuSyncScheduler : ITransientDependency
{
    private readonly IDistributedEventBus _eventBus;
    private readonly IdempotencyService _idempotencyService;
    private readonly ICurrentTenant _currentTenant;
    private readonly ILogger<MenuSyncScheduler> _logger;
    private readonly FoodicsCatalogClient _foodicsCatalogClient;
    private readonly FoodicsAccountTokenService _tokenService;
    private readonly FoodicsProductStagingService _stagingService;
    private readonly FoodicsProductStagingToFoodicsConverter _stagingConverter;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
    private readonly TalabatCatalogSyncService _talabatSyncService;
    private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
    private readonly TalabatAccountService _talabatAccountService;
    private readonly IDataFilter _dataFilter;
    private readonly ITenantRepository _tenantRepository;
    private readonly IUnitOfWorkManager _unitOfWorkManager;
    private readonly GroupProductFilterService _groupFilterService;

    public MenuSyncScheduler(
        IDistributedEventBus eventBus,
        IdempotencyService idempotencyService,
        ICurrentTenant currentTenant,
        FoodicsCatalogClient foodicsCatalogClient,
        FoodicsAccountTokenService tokenService,
        FoodicsProductStagingService stagingService,
        FoodicsProductStagingToFoodicsConverter stagingConverter,
        IRepository<FoodicsProductStaging, Guid> stagingRepository,
        TalabatCatalogSyncService talabatSyncService,
        IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
        TalabatAccountService talabatAccountService,
        IDataFilter dataFilter,
        ITenantRepository tenantRepository,
        IUnitOfWorkManager unitOfWorkManager,
        GroupProductFilterService groupFilterService,
        ILogger<MenuSyncScheduler> logger)
    {
        _eventBus = eventBus;
        _idempotencyService = idempotencyService;
        _currentTenant = currentTenant;
        _foodicsCatalogClient = foodicsCatalogClient;
        _tokenService = tokenService;
        _stagingService = stagingService;
        _stagingConverter = stagingConverter;
        _stagingRepository = stagingRepository;
        _talabatSyncService = talabatSyncService;
        _foodicsAccountRepository = foodicsAccountRepository;
        _talabatAccountService = talabatAccountService;
        _dataFilter = dataFilter;
        _tenantRepository = tenantRepository;
        _unitOfWorkManager = unitOfWorkManager;
        _groupFilterService = groupFilterService;
        _logger = logger;
    }

    /// <summary>
    /// UPDATED: Now works with database entities instead of hardcoded values
    /// UPDATED: Works with Hangfire Job without tenant context - processes all tenants automatically
    /// Full flow: Foodics -> Staging -> Talabat (bypass Kafka)
    /// This method is called by Hangfire on a schedule
    /// 
    /// Flow:
    /// 1. Disable multi-tenancy filter to get all FoodicsAccounts from all tenants (or specific account)
    /// 2. For each FoodicsAccount:
    ///    - Change to that account's tenant context
    ///    - Get linked TalabatAccounts via FoodicsAccountId
    ///    - Fetch products from Foodics
    ///    - Save to staging
    ///    - Submit to each linked TalabatAccount (using ChainCode from DB)
    /// </summary>
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [DisableConcurrentExecution(timeoutInSeconds: 60 * 30)] // prevent overlapping runs (30 min)
    //[UnitOfWork]
    public async Task PublishMenuSyncEventAsync(
        Guid? foodicsAccountId = null,
        string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid().ToString();

        _logger.LogInformation("⚡ [Job] Starting menu sync from DB. CorrelationId={CorrelationId}, CurrentTenantId={TenantId}, FoodicsAccountId={FoodicsAccountId}, Branch={Branch}",
            correlationId, _currentTenant.Id, foodicsAccountId?.ToString() ?? "<all>", branchId ?? "<all>");

        try
        {
            // FIXED: Use requiresNew UoW to get a fresh DbContext for background job
            // This prevents "disposed context" errors when the job runs outside normal request scope
            List<FoodicsAccount> foodicsAccounts;

            using (var uow = _unitOfWorkManager.Begin(requiresNew: true))
            {
                using (_dataFilter.Disable<IMultiTenant>())
                {
                    if (foodicsAccountId.HasValue)
                    {
                        try
                        {
                            var account = await _foodicsAccountRepository.GetAsync(
                                x => x.Id == foodicsAccountId.Value,
                                cancellationToken: cancellationToken);
                            foodicsAccounts = new List<FoodicsAccount> { account };
                            
                            _logger.LogInformation("⚡ [Job] Specific FoodicsAccount requested. AccountId={AccountId}, TenantId={TenantId}, Found=true, CorrelationId={CorrelationId}",
                                foodicsAccountId.Value, account.TenantId, correlationId);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "⚡ [Job] Specific FoodicsAccount not found. AccountId={AccountId}, CorrelationId={CorrelationId}",
                                foodicsAccountId.Value, correlationId);
                            foodicsAccounts = new List<FoodicsAccount>();
                        }
                    }
                    else if (_currentTenant.Id.HasValue)
                    {
                        foodicsAccounts = await _foodicsAccountRepository.GetListAsync(
                            x => x.TenantId == _currentTenant.Id.Value,
                            cancellationToken: cancellationToken);
                        
                        _logger.LogInformation("⚡ [Job] Getting FoodicsAccounts for current tenant. TenantId={TenantId}, Count={Count}, CorrelationId={CorrelationId}",
                            _currentTenant.Id.Value, foodicsAccounts.Count, correlationId);
                    }
                    else
                    {
                        foodicsAccounts = await _foodicsAccountRepository.GetListAsync(cancellationToken: cancellationToken);
                        
                        _logger.LogInformation("⚡ [Job] No tenant context (Hangfire). Getting ALL FoodicsAccounts from ALL tenants. Count={Count}, CorrelationId={CorrelationId}",
                            foodicsAccounts.Count, correlationId);
                    }
                }
                
                await uow.CompleteAsync(cancellationToken);
            }

            if (!foodicsAccounts.Any())
            {
                _logger.LogWarning("⚡ [Job] No FoodicsAccounts found. CorrelationId={CorrelationId}", correlationId);
                return;
            }

            _logger.LogInformation("⚡ [Job] Found {Count} FoodicsAccount(s). Processing each one. CorrelationId={CorrelationId}",
                foodicsAccounts.Count, correlationId);

            // ✅ Process each FoodicsAccount
            // Each account will be processed in its own tenant context
            foreach (var foodicsAccount in foodicsAccounts)
            {
                if (!foodicsAccount.TenantId.HasValue)
                {
                    _logger.LogWarning("⚡ [Job] FoodicsAccount {AccountId} has no TenantId. Skipping. CorrelationId={CorrelationId}",
                        foodicsAccount.Id, correlationId);
                    continue;
                }

                // Change to the account's tenant context before processing
                using (_currentTenant.Change(foodicsAccount.TenantId.Value))
                {
                    await ProcessFoodicsAccountSyncAsync(foodicsAccount, branchId, correlationId, cancellationToken);
                }
            }

            _logger.LogInformation("✅ [Job] Menu sync completed. Processed {Count} FoodicsAccount(s). CorrelationId={CorrelationId}",
                foodicsAccounts.Count, correlationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 [Job] Error during menu sync from DB. CorrelationId={CorrelationId}", correlationId);
            throw; // Re-throw to let Hangfire handle retry
        }
    }

    /// <summary>
    /// Process sync for a specific FoodicsAccount
    /// Gets linked TalabatAccounts and submits products to each one
    /// FIXED: Uses requiresNew UoW for all database operations to avoid disposed DbContext issues
    /// </summary>
    private async Task ProcessFoodicsAccountSyncAsync(
        FoodicsAccount foodicsAccount,
        string? branchId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("⚡ [Job] Processing FoodicsAccount. AccountId={AccountId}, BrandName={BrandName}, CorrelationId={CorrelationId}",
            foodicsAccount.Id, foodicsAccount.BrandName ?? foodicsAccount.OAuthClientId, correlationId);

        try
        {
            // FIXED: Get linked TalabatAccounts from database using service with requiresNew UoW
            var talabatAccounts = await _talabatAccountService.GetAccountsByFoodicsAccountIdAsync(
                foodicsAccount.Id, cancellationToken);

            if (!talabatAccounts.Any())
            {
                _logger.LogWarning("⚡ [Job] No TalabatAccounts linked to FoodicsAccount {AccountId}. CorrelationId={CorrelationId}",
                    foodicsAccount.Id, correlationId);
                return;
            }

            // Filter Talabat accounts if a specific branch is requested
            if (!string.IsNullOrEmpty(branchId))
            {
                var originalCount = talabatAccounts.Count;
                
                // Matches if configured to sync all branches OR targets the specific branch
                talabatAccounts = talabatAccounts
                    .Where(x => x.SyncAllBranches || x.FoodicsBranchId == branchId)
                    .ToList();

                if (!talabatAccounts.Any())
                {
                    _logger.LogInformation(
                        "⚡ [Job] No matching TalabatAccounts after branch filtering. " +
                        "RequestedBranch={Branch}, OriginalCount={Original}, FilteredCount=0. " +
                        "CorrelationId={CorrelationId}",
                        branchId, originalCount, correlationId);
                    return;
                }
            }

            _logger.LogInformation("⚡ [Job] Found {Count} TalabatAccounts linked to FoodicsAccount {AccountId}. CorrelationId={CorrelationId}",
                talabatAccounts.Count, foodicsAccount.Id, correlationId);

            // FIXED: Get access token using service with requiresNew UoW
            var accessToken = await _tokenService.GetAccessTokenAsync(foodicsAccount.Id, cancellationToken);


            var allProducts = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId: null,  // Always fetch ALL products to get branch info
                accessToken: accessToken,
                perPage: 100,
                includeDeleted: false,  
                cancellationToken: cancellationToken);

            _logger.LogInformation("⚡ [Job] Fetched {Count} products from Foodics (all branches). AccountId={AccountId}, CorrelationId={CorrelationId}",
                allProducts.Count, foodicsAccount.Id, correlationId);

            if (!allProducts.Any())
            {
                _logger.LogWarning("⚡ [Job] No products returned from Foodics. AccountId={AccountId}, CorrelationId={CorrelationId}",
                    foodicsAccount.Id, correlationId);
                return;
            }

            // IDEMPOTENCY CHECK
            // Generate a snapshot key based on the content of the products
            //var idempotencyKey = _idempotencyService.GenerateMenuSnapshotKey(foodicsAccount.Id, branchId, allProducts);
            
            //var (canProcess, _) = await _idempotencyService.CheckAndMarkStartedAsync(
            //    foodicsAccount.Id, 
            //    idempotencyKey, 
            //    retentionDays: 7, 
            //    cancellationToken: cancellationToken);

            //if (!canProcess)
            //{
            //    _logger.LogInformation(
            //        "⚡ [Job] Menu content has not changed since last successful sync. Skipping processing. " +
            //        "AccountId={AccountId}, Key={Key}, CorrelationId={CorrelationId}",
            //        foodicsAccount.Id, idempotencyKey, correlationId);
            //    return;
            //}

            // Save to staging (staging service should handle its own UoW)
            var stagingResult = await _stagingService.SaveProductsToStagingAsync(
                foodicsAccount.Id,
                allProducts.Values,
                branchId,
                cancellationToken);

            _logger.LogInformation("⚡ [Job] Staging result: Saved={Saved}, Updated={Updated}, Errors={Errors}, Total={Total}, AccountId={AccountId}, CorrelationId={CorrelationId}",
                stagingResult.SavedCount, stagingResult.UpdatedCount, stagingResult.ErrorCount,
                stagingResult.TotalProcessed, foodicsAccount.Id, correlationId);

            // CLEANUP: Removed redundant staging read and unused foodicsDtos conversion
            // We already have the products in 'allProducts', so we can proceed directly

            var activeTalabatAccounts = talabatAccounts.Where(x => x.IsActive).ToList();
            
            //if (!activeTalabatAccounts.Any())
            //{
            //    _logger.LogWarning("⚡ [Job] No active TalabatAccounts found. AccountId={AccountId}, CorrelationId={CorrelationId}",
            //        foodicsAccount.Id, correlationId);
                
            //    // Mark as succeeded even if no active accounts, as we processed the menu
            //    await _idempotencyService.MarkSucceededAsync(foodicsAccount.Id, idempotencyKey, null, cancellationToken);
            //    return;
            //}

            foreach (var talabatAccount in activeTalabatAccounts)
            {
                await SubmitToTalabatAccountWithBranchFilteringAsync(
                    talabatAccount,
                    allProducts,  // Pass all products for filtering
                    foodicsAccount.Id,
                    correlationId,
                    cancellationToken);
            }

            // Mark the operation as succeeded
            //await _idempotencyService.MarkSucceededAsync(foodicsAccount.Id, idempotencyKey, null, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 [Job] Error processing FoodicsAccount {AccountId}. CorrelationId={CorrelationId}",
                foodicsAccount.Id, correlationId);
        }
    }

    /// <summary>
    /// Submit products to a specific TalabatAccount with group filtering applied.
    /// This method filters products based on the TalabatAccount's FoodicsGroupId configuration.
    /// Products not belonging to the configured group (or without any group) are EXCLUDED.
    /// Branch filtering has been removed - all products are synced regardless of branch.
    /// </summary>
    private async Task SubmitToTalabatAccountWithBranchFilteringAsync(
        TalabatAccount talabatAccount,
        Dictionary<string, FoodicsProductDetailDto> allProducts,
        Guid foodicsAccountId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        var chainCode = talabatAccount.ChainCode;

        _logger.LogInformation("⚡ [Job-Validation] Starting Talabat submission validation for account {Name}. ChainCode={ChainCode}, CorrelationId={CorrelationId}",
            talabatAccount.Name, chainCode ?? "<null>", correlationId);

        // VALIDATION 1️⃣: ChainCode is required
        if (string.IsNullOrWhiteSpace(chainCode))
        {
            _logger.LogWarning("⚡ [Job-Validation] ❌ FAILED: TalabatAccount {Name} (VendorCode={VendorCode}) has no ChainCode configured. Skipping sync. CorrelationId={CorrelationId}",
                talabatAccount.Name, talabatAccount.VendorCode, correlationId);
            return;
        }

        _logger.LogInformation("⚡ [Job-Validation] ✅ ChainCode validation passed. ChainCode={ChainCode}, CorrelationId={CorrelationId}",
            chainCode, correlationId);

        // VALIDATION 2️⃣: FoodicsGroupId is required for group-based filtering
        if (string.IsNullOrWhiteSpace(talabatAccount.FoodicsGroupId))
        {
            _logger.LogWarning(
                "⚡ [Job-Validation] ❌ FAILED: TalabatAccount {Name} (VendorCode={VendorCode}) has no FoodicsGroupId configured. " +
                "Skipping sync as group filtering is required. CorrelationId={CorrelationId}",
                talabatAccount.Name, talabatAccount.VendorCode, correlationId);
            return;
        }

        _logger.LogInformation("⚡ [Job-Validation] ✅ FoodicsGroupId validation passed. FoodicsGroupId={GroupId}, GroupName={GroupName}, CorrelationId={CorrelationId}",
            talabatAccount.FoodicsGroupId, talabatAccount.FoodicsGroupName ?? "<unknown>", correlationId);


        _logger.LogInformation("⚡ [Job-Filtering] Starting group-based product filtering. TargetGroup={TargetGroup}, TotalProductsToFilter={Total}, CorrelationId={CorrelationId}",
            talabatAccount.FoodicsGroupId, allProducts.Count, correlationId);

        var groupFilterResult = _groupFilterService.FilterProductsByGroup(
            allProducts,
            talabatAccount.FoodicsGroupId,
            correlationId);

        _logger.LogInformation("⚡ [Job-Filtering] Group filtering completed. FilteredCount={Filtered}, ProductsWithoutGroups={NoGroups}, ProductsNotInTargetGroup={NotInTarget}, Reason={Reason}, CorrelationId={CorrelationId}",
            groupFilterResult.FilteredCount, groupFilterResult.ProductsWithoutGroups, 
            groupFilterResult.ProductsNotInTargetGroup, groupFilterResult.FilterReason, correlationId);

        if (!groupFilterResult.FilteredProducts.Any())
        {
            _logger.LogWarning(
                "⚡ [Job-Filtering] ❌ No products available after group filtering. " +
                "TalabatAccount={Name}, VendorCode={VendorCode}, " +
                "TargetGroup={TargetGroup}, TotalProducts={Total}, " +
                "ProductsWithoutGroups={NoGroups}, Reason={Reason}, CorrelationId={CorrelationId}",
                talabatAccount.Name, talabatAccount.VendorCode,
                talabatAccount.FoodicsGroupId, allProducts.Count,
                groupFilterResult.ProductsWithoutGroups,
                groupFilterResult.FilterReason, correlationId);
            return;
        }

        _logger.LogInformation(
            "⚡ [Job-Filtering] ✅ Group filtering successful. " +
            "TalabatAccount={Name}, VendorCode={VendorCode}, " +
            "TargetGroup={TargetGroup}, GroupName={GroupName}, " +
            "TotalProducts={Total}, FilteredProducts={Filtered}, CorrelationId={CorrelationId}",
            talabatAccount.Name, talabatAccount.VendorCode,
            talabatAccount.FoodicsGroupId, talabatAccount.FoodicsGroupName ?? "<unknown>",
            allProducts.Count, groupFilterResult.FilteredCount, correlationId);

        _logger.LogInformation("⚡ [Job-Submission] Starting Talabat submission. ChainCode={ChainCode}, VendorCode={VendorCode}, ProductsToSubmit={Count}, CorrelationId={CorrelationId}",
            chainCode, talabatAccount.VendorCode, groupFilterResult.FilteredCount, correlationId);

        var vendorCode = talabatAccount.VendorCode;
        
        var submitResult = await _talabatSyncService.SyncCatalogV2Async(
            groupFilterResult.FilteredProducts,
            chainCode,
            foodicsAccountId,
            correlationId,
            vendorCode,
            cancellationToken);

        _logger.LogInformation("⚡ [Job-Submission] Talabat submission response received. Success={Success}, ImportId={ImportId}, Message={Message}, CorrelationId={CorrelationId}",
            submitResult.Success, submitResult.ImportId ?? "<none>", submitResult.Message, correlationId);

        // RESULT 5️⃣: Log final result
        if (submitResult.Success)
        {
            _logger.LogInformation(
                "✅ [Job-Result] Talabat submission SUCCESS. " +
                "TalabatAccount={Name}, VendorCode={VendorCode}, ChainCode={ChainCode}, " +
                "TargetGroup={TargetGroup}, GroupName={GroupName}, ImportId={ImportId}, " +
                "Categories={Categories}, Products={Products}, " +
                "FilteredProducts={Filtered}, CorrelationId={CorrelationId}",
                talabatAccount.Name, talabatAccount.VendorCode, chainCode,
                talabatAccount.FoodicsGroupId, talabatAccount.FoodicsGroupName ?? "<unknown>",
                submitResult.ImportId, submitResult.CategoriesCount, submitResult.ProductsCount,
                groupFilterResult.FilteredCount, correlationId);
        }
        else
        {
            _logger.LogWarning(
                "❌ [Job-Result] Talabat submission FAILED. " +
                "TalabatAccount={Name}, VendorCode={VendorCode}, ChainCode={ChainCode}, " +
                "TargetGroup={TargetGroup}, GroupName={GroupName}, " +
                "Message={Message}, ErrorCount={ErrorCount}, " +
                "FilteredProducts={Filtered}, CorrelationId={CorrelationId}",
                talabatAccount.Name, talabatAccount.VendorCode, chainCode,
                talabatAccount.FoodicsGroupId, talabatAccount.FoodicsGroupName ?? "<unknown>",
                submitResult.Message,
                submitResult.Errors?.Count ?? 0,
                groupFilterResult.FilteredCount, correlationId);

            if (submitResult.Errors?.Any() == true)
            {
                _logger.LogWarning("⚡ [Job-Result] Error details (first 3): {Errors}, CorrelationId={CorrelationId}",
                    string.Join("; ", submitResult.Errors.Take(3)), correlationId);
            }
        }
    }

    /// <summary>
    /// Legacy method - kept for backward compatibility but now logs a warning
    /// </summary>
    [Obsolete("Use SubmitToTalabatAccountWithBranchFilteringAsync for proper group filtering")]
    private async Task SubmitToTalabatAccountAsync(
        TalabatAccount talabatAccount,
        IEnumerable<FoodicsProductDetailDto> products,
        Guid foodicsAccountId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning(
            "⚠️ [Job] Using legacy SubmitToTalabatAccountAsync without group filtering. " +
            "TalabatAccount={Name}, VendorCode={VendorCode}, CorrelationId={CorrelationId}",
            talabatAccount.Name, talabatAccount.VendorCode, correlationId);

        // Convert to dictionary for the new method
        var allProducts = products.ToDictionary(p => p.Id, p => p);
        
        // Call the new method with group filtering
        await SubmitToTalabatAccountWithBranchFilteringAsync(
            talabatAccount, allProducts, foodicsAccountId, correlationId, cancellationToken);
    }
}

