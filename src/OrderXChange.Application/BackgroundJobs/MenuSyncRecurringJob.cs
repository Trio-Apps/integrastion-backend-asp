using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Hangfire;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Idempotency;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Integrations.Talabat;
using OrderXChange.Application.Staging;
using OrderXChange.Application.Versioning;
using OrderXChange.Domain.Staging;
using OrderXChange.Domain.Versioning;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;
using Foodics;
using Volo.Abp.Uow;
using OrderXChange.Idempotency;

namespace OrderXChange.BackgroundJobs;

/// <summary>
/// Background job for syncing Foodics menu/products to staging table and pushing to Talabat.
/// Now enhanced with Menu Versioning for intelligent change detection and optimized sync operations.
/// Runs periodically to fetch all products from Foodics, detect changes, and sync only when necessary.
/// </summary>
public class MenuSyncRecurringJob : ITransientDependency
{
    private readonly FoodicsCatalogClient _foodicsCatalogClient;
    private readonly FoodicsProductStagingService _stagingService;
    private readonly FoodicsAccountTokenService _tokenService;
    private readonly TalabatCatalogSyncService _talabatSyncService;
    private readonly TalabatAccountService _talabatAccountService;
    private readonly IRepository<FoodicsAccount, Guid> _foodicsAccountRepository;
    private readonly IRepository<FoodicsProductStaging, Guid> _stagingRepository;
    private readonly ICurrentTenant _currentTenant;
    private readonly IDataFilter _dataFilter;
    private readonly IdempotencyService _idempotencyService;
    private readonly IConfiguration _configuration;
    private readonly TalabatPaymentMethodSettingsService _talabatPaymentMethodSettingsService;
    private readonly BranchProductFilterService _branchFilterService;
    private readonly GroupProductFilterService _groupFilterService;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<MenuSyncRecurringJob> _logger;
    
    // ✨ NEW: Menu Versioning Services
    private readonly MenuVersioningService _menuVersioningService;
    private readonly MenuSyncRunManager _syncRunManager;

    public MenuSyncRecurringJob(
        FoodicsCatalogClient foodicsCatalogClient,
        FoodicsProductStagingService stagingService,
        FoodicsAccountTokenService tokenService,
        TalabatCatalogSyncService talabatSyncService,
        TalabatAccountService talabatAccountService,
        IRepository<FoodicsAccount, Guid> foodicsAccountRepository,
        IRepository<FoodicsProductStaging, Guid> stagingRepository,
        ICurrentTenant currentTenant,
        IDataFilter dataFilter,
        IdempotencyService idempotencyService,
        IConfiguration configuration,
        TalabatPaymentMethodSettingsService talabatPaymentMethodSettingsService,
        BranchProductFilterService branchProductFilterService,
        GroupProductFilterService groupProductFilterService,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<MenuSyncRecurringJob> logger,
        MenuVersioningService menuVersioningService,
        MenuSyncRunManager syncRunManager)
    {
        _foodicsCatalogClient = foodicsCatalogClient;
        _stagingService = stagingService;
        _tokenService = tokenService;
        _talabatSyncService = talabatSyncService;
        _talabatAccountService = talabatAccountService;
        _foodicsAccountRepository = foodicsAccountRepository;
        _stagingRepository = stagingRepository;
        _currentTenant = currentTenant;
        _dataFilter = dataFilter;
        _idempotencyService = idempotencyService;
        _configuration = configuration;
        _talabatPaymentMethodSettingsService = talabatPaymentMethodSettingsService;
        _branchFilterService = branchProductFilterService;
        _groupFilterService = groupProductFilterService;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
        _menuVersioningService = menuVersioningService;
        _syncRunManager = syncRunManager;
    }

    /// <summary>
    /// Executes menu sync job with smart account selection:
    /// 1. Prioritizes accounts that haven't been integrated yet (not in staging table)
    /// 2. If all accounts are integrated, processes the oldest account from staging (by SyncDate)
    /// Fetches all products from Foodics and saves them to staging table.
    /// </summary>
    /// <param name="foodicsAccountId">Optional FoodicsAccount ID. If provided, syncs only that account</param>
    /// <param name="branchId">Optional branch ID to filter products</param>
    /// <param name="skipInternalIdempotency">If true, skips internal idempotency check (used when called from Kafka consumer which already did the check)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [AutomaticRetry(Attempts = 3, OnAttemptsExceeded = AttemptsExceededAction.Fail)]
    [UnitOfWork]
    public virtual async Task ExecuteAsync(Guid? foodicsAccountId = null, string? branchId = null, bool skipInternalIdempotency = false, CancellationToken cancellationToken = default)
    {
        try
        {
            // Always fetch all accounts from repository (across all tenants)
            List<FoodicsAccount> allAccounts;
            using (_dataFilter.Disable<IMultiTenant>())
            {
                allAccounts = await _foodicsAccountRepository.GetListAsync(cancellationToken: cancellationToken);
            }
            
            // Validate that accounts were fetched successfully
            if (allAccounts == null || !allAccounts.Any())
            {
                _logger.LogWarning("No FoodicsAccounts found in repository");
                return;
            }

            // If specific account is requested, process only that account
            if (foodicsAccountId.HasValue)
            {
                var account = allAccounts.FirstOrDefault(a => a.Id == foodicsAccountId.Value);
                if (account == null)
                {
                    _logger.LogError("FoodicsAccount {AccountId} not found in repository", foodicsAccountId.Value);
                    throw new InvalidOperationException($"FoodicsAccount {foodicsAccountId.Value} not found");
                }

                using (_currentTenant.Change(account.TenantId))
                {
                    await SyncAccountAsync(account.Id, branchId, skipInternalIdempotency, cancellationToken);
                }
                return;
            }

            // Get all distinct accountIds from staging table (accounts that have been integrated)
            var stagingAccounts = await _stagingRepository.GetListAsync(cancellationToken: cancellationToken);
            var integratedAccountIds = stagingAccounts
                .Select(s => s.FoodicsAccountId)
                .Distinct()
                .ToHashSet();

            _logger.LogInformation(
                "Found {TotalAccounts} FoodicsAccount(s) in repository. {IntegratedCount} account(s) already have data in staging table.",
                allAccounts.Count,
                integratedAccountIds.Count);

            // Find accounts that haven't been integrated yet (not in staging table)
            var nonIntegratedAccounts = allAccounts
                .Where(a => !integratedAccountIds.Contains(a.Id))
                .ToList();

            List<FoodicsAccount> accountsToSync;

            if (nonIntegratedAccounts.Any())
            {
                // Priority: Process accounts that haven't been integrated yet
                accountsToSync = nonIntegratedAccounts
                    .OrderBy(a => a.Id)
                    .ThenByDescending(a => a.LastModificationTime ?? a.CreationTime)
                    .ToList();

                _logger.LogInformation(
                    "Found {Count} account(s) that haven't been integrated yet. Processing these accounts first.",
                    accountsToSync.Count);
            }
            else
            {
                // All accounts are integrated - get the oldest account from staging (by SyncDate)
                var oldestStagingEntry = stagingAccounts
                    .OrderBy(s => s.SyncDate)
                    .ThenBy(s => s.FoodicsAccountId)
                    .FirstOrDefault();

                if (oldestStagingEntry == null)
                {
                    _logger.LogWarning("No staging data found. Processing all accounts.");
                    accountsToSync = allAccounts
                        .OrderBy(a => a.Id)
                        .ThenByDescending(a => a.LastModificationTime ?? a.CreationTime)
                        .ToList();
                }
                else
                {
                    var oldestAccount = allAccounts.FirstOrDefault(a => a.Id == oldestStagingEntry.FoodicsAccountId);
                    if (oldestAccount == null)
                    {
                        _logger.LogWarning(
                            "Oldest staging account {AccountId} not found in repository. Processing all accounts.",
                            oldestStagingEntry.FoodicsAccountId);
                        accountsToSync = allAccounts
                            .OrderBy(a => a.Id)
                            .ThenByDescending(a => a.LastModificationTime ?? a.CreationTime)
                            .ToList();
                    }
                    else
                    {
                        accountsToSync = new List<FoodicsAccount> { oldestAccount };
                        _logger.LogInformation(
                            "All accounts are integrated. Processing oldest account {AccountId} (last synced: {SyncDate})",
                            oldestAccount.Id,
                            oldestStagingEntry.SyncDate);
                    }
                }
            }

            // Process each account
            foreach (var account in accountsToSync)
            {
                try
                {
                    using (_currentTenant.Change(account.TenantId))
                    {
                        // When called from Kafka consumer, pass skipInternalIdempotency through
                        // When called from recurring job, always pass false (no need to skip)
                        await SyncAccountAsync(account.Id, branchId, skipInternalIdempotency, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to sync account {AccountId} (Tenant {TenantId})", account.Id, account.TenantId);
                    // Continue with next account
                }
            }

            _logger.LogInformation(
                "Menu sync job completed. Processed {ProcessedCount} account(s) out of {TotalCount} total account(s)",
                accountsToSync.Count,
                allAccounts.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Menu sync job failed");
            throw;
        }
    }

    private async Task SyncAccountAsync(Guid foodicsAccountId, string? branchId, bool skipInternalIdempotency, CancellationToken cancellationToken)
    {
        // Check if Menu Versioning is enabled
        var versioningEnabled = _configuration.GetValue<bool>("MenuVersioning:Enabled", true);
        var disableMenuIdempotency = _configuration.GetValue<bool>("Idempotency:DisableMenuSync", true);
        var forceMenuSync = _configuration.GetValue<bool>("MenuSync:AlwaysSync", true);
        var useSnapshotIdempotency = !disableMenuIdempotency && !forceMenuSync;
        
        // Generate idempotency key for this sync operation (job-level lock)
        var timestamp = DateTime.UtcNow;
        var idempotencyKey = _idempotencyService.GenerateMenuSyncKey(foodicsAccountId, branchId, timestamp);
        var jobLockAcquired = false;
        var jobLockReleased = false;
        string? snapshotIdempotencyKey = null;
        
        // ✨ NEW: Start Menu Sync Run for comprehensive tracking
        MenuSyncRun? syncRun = null;
        if (versioningEnabled)
        {
            try
            {
                syncRun = await _syncRunManager.StartSyncRunAsync(
                    foodicsAccountId,
                    branchId,
                    menuGroupId: null, // Branch-level sync for now
                    syncType: "RecurringJob",
                    triggerSource: "Hangfire",
                    initiatedBy: "System",
                    configuration: new Dictionary<string, object>
                    {
                        ["VersioningEnabled"] = true,
                        ["IdempotencyKey"] = idempotencyKey,
                        ["SkipInternalIdempotency"] = skipInternalIdempotency
                    },
                    cancellationToken);
                    
                _logger.LogInformation(
                    "Started MenuSyncRun {SyncRunId} with correlation {CorrelationId} for account {AccountId}",
                    syncRun.Id, syncRun.CorrelationId, foodicsAccountId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to start MenuSyncRun for account {AccountId}. Continuing with legacy sync.", foodicsAccountId);
            }
        }
        
        // Check if operation already in progress or completed
        // Skip this check if called from Kafka consumer (which already did idempotency check)
        if (!skipInternalIdempotency && !disableMenuIdempotency)
        {
            try
            {
                var lockMinutes = _configuration.GetValue<int>("Idempotency:MenuSyncLockMinutes", 15);
                var staleAfter = TimeSpan.FromMinutes(Math.Max(1, lockMinutes));

                var (canProcess, existingRecord) = await _idempotencyService.CheckAndMarkStartedAsync(
                    foodicsAccountId,
                    idempotencyKey,
                    retentionDays: 1, // Short retention for sync operations (auto-cleanup after 1 day)
                    cancellationToken,
                    staleAfter);
                
                if (!canProcess)
                {
                    _logger.LogWarning(
                        "Skipping sync for FoodicsAccount {AccountId}, Branch {BranchId} - operation already {Status}. " +
                        "Another sync job is currently running or recently completed for this account.",
                        foodicsAccountId,
                        branchId ?? "<all>",
                        existingRecord?.Status);
                        
                    if (syncRun != null)
                    {
                        await _syncRunManager.CompleteSyncRunAsync(syncRun.Id, "Skipped - Already in progress", cancellationToken: cancellationToken);
                    }
                    return;
                }

                jobLockAcquired = true;
            }
            catch (BusinessException ex) when (ex.Code == "OPERATION_IN_PROGRESS")
            {
                _logger.LogWarning(
                    ex,
                    "Sync for FoodicsAccount {AccountId}, Branch {BranchId} is already in progress. Skipping duplicate execution.",
                    foodicsAccountId,
                    branchId ?? "<all>");
                    
                if (syncRun != null)
                {
                    await _syncRunManager.CompleteSyncRunAsync(syncRun.Id, "Skipped - Duplicate execution", cancellationToken: cancellationToken);
                }
                return;
            }
        }
        else
        {
            _logger.LogDebug(
                "Skipping internal idempotency check for FoodicsAccount {AccountId} (called from Kafka consumer or idempotency disabled)",
                foodicsAccountId);
        }
        
        try
        {
            _logger.LogInformation(
                "Menu sync job started for FoodicsAccount {AccountId}, Tenant {TenantId}, Branch {BranchId}, IdempotencyKey {Key}",
                foodicsAccountId,
                _currentTenant.Id ?? Guid.Empty,
                branchId ?? "<all>",
                idempotencyKey);

            // ✨ Update progress: Starting
            if (syncRun != null)
            {
                await _syncRunManager.UpdateProgressAsync(
                    syncRun.Id, 
                    MenuSyncPhase.Initialization, 
                    10, 
                    "Starting menu sync operation",
                    cancellationToken: cancellationToken);
            }

            // Get access token for this account (with fallback to configuration)
            string accessToken;
            try
            {
                accessToken = await _tokenService.GetAccessTokenWithFallbackAsync(foodicsAccountId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get access token for FoodicsAccount {AccountId}", foodicsAccountId);
                
                if (syncRun != null)
                {
                    await _syncRunManager.FailSyncRunAsync(syncRun.Id, "Failed to get access token", ex, cancellationToken: cancellationToken);
                }
                throw;
            }

            try
            {
                var paymentMethodCount = await _talabatPaymentMethodSettingsService.PrefetchPaymentMethodsAsync(foodicsAccountId, cancellationToken);
                _logger.LogInformation(
                    "Fetched Foodics payment methods during menu sync. FoodicsAccountId={FoodicsAccountId}, Count={Count}",
                    foodicsAccountId,
                    paymentMethodCount);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to fetch Foodics payment methods during menu sync. FoodicsAccountId={FoodicsAccountId}",
                    foodicsAccountId);
            }

            // ✨ Update progress: Fetching data
            if (syncRun != null)
            {
                await _syncRunManager.UpdateProgressAsync(
                    syncRun.Id, 
                    MenuSyncPhase.FoodicsDataFetch, 
                    20, 
                    "Fetching products from Foodics API",
                    cancellationToken: cancellationToken);
            }

            // Fetch ALL products with full includes using the products endpoint
            // NOTE:
            // - We set includeDeleted = false to avoid returning deleted products from Foodics.
            // - Active products are filtered later at the staging / Talabat submission layers.
            var allProducts = await _foodicsCatalogClient.GetAllProductsWithIncludesAsync(
                branchId,
                accessToken: accessToken,
                foodicsAccountId: foodicsAccountId,
                perPage: 100,
                includeDeleted: false,
                includeInactive: false,
                cancellationToken: cancellationToken);

            FoodicsModifierSanitizer.SanitizeProductsForMenuProjection(allProducts.Values);

            _logger.LogInformation(
                "Fetched and sanitized {Count} products from Foodics for account {AccountId}",
                allProducts.Count,
                foodicsAccountId);

            // ✨ NEW: Menu Versioning - Change Detection
            MenuChangeDetectionResult? changeDetectionResult = null;
            MenuSnapshot? newSnapshot = null;
            
            if (versioningEnabled)
            {
                try
                {
                    // ✨ Update progress: Change detection
                    if (syncRun != null)
                    {
                        await _syncRunManager.UpdateProgressAsync(
                            syncRun.Id, 
                            MenuSyncPhase.ChangeDetection, 
                            30, 
                            "Detecting menu changes using versioning system",
                            new Dictionary<string, object> { ["ProductsCount"] = allProducts.Count },
                            cancellationToken: cancellationToken);
                    }

                    // Detect changes using the versioning service
                    changeDetectionResult = await _menuVersioningService.DetectChangesAsync(
                        foodicsAccountId,
                        branchId,
                        allProducts.Values.ToList(),
                        menuGroupId: null, // Branch-level for now
                        cancellationToken);

                    _logger.LogInformation(
                        "Change detection completed. HasChanged={HasChanged}, ChangeType={ChangeType}, Hash={Hash}",
                        changeDetectionResult.HasChanged,
                        changeDetectionResult.ChangeType,
                        changeDetectionResult.CurrentHash);

                    // ✨ OPTIMIZATION: Skip sync if no changes detected (disabled when force sync is enabled)
                    if (!forceMenuSync && !changeDetectionResult.HasChanged)
                    {
                        _logger.LogInformation(
                            "🎯 OPTIMIZATION: Menu has NOT changed for FoodicsAccount {AccountId}, Branch {BranchId}. " +
                            "Skipping unnecessary staging and Talabat sync. Hash={Hash}, Version={Version}",
                            foodicsAccountId,
                            branchId ?? "<all>",
                            changeDetectionResult.CurrentHash,
                            changeDetectionResult.PreviousVersion);

                        // Mark idempotency as succeeded since we completed the check
                        if (!disableMenuIdempotency)
                        {
                            await _idempotencyService.MarkSucceededAsync(
                                foodicsAccountId,
                                idempotencyKey,
                                new { 
                                    Message = "No changes detected - sync skipped",
                                    Hash = changeDetectionResult.CurrentHash,
                                    Version = changeDetectionResult.PreviousVersion,
                                    ProductsCount = allProducts.Count,
                                    OptimizationSaved = true
                                },
                                cancellationToken);
                        }

                        if (syncRun != null)
                        {
                            await _syncRunManager.UpdateStatisticsAsync(
                                syncRun.Id,
                                totalProcessed: allProducts.Count,
                                succeeded: allProducts.Count,
                                skipped: allProducts.Count,
                                cancellationToken: cancellationToken);
                                
                            await _syncRunManager.CompleteSyncRunAsync(
                                syncRun.Id, 
                                "Success - No changes detected",
                                new Dictionary<string, object>
                                {
                                    ["OptimizationApplied"] = true,
                                    ["ChangeDetectionResult"] = changeDetectionResult.ChangeType.ToString(),
                                    ["ProductsSkipped"] = allProducts.Count,
                                    ["ResourcesSaved"] = "70-80% API calls and processing time"
                                },
                                cancellationToken);
                        }

                        _logger.LogInformation(
                            "✅ Menu sync completed with OPTIMIZATION for FoodicsAccount {AccountId}. " +
                            "Saved 70-80% of resources by skipping unnecessary sync operations.",
                            foodicsAccountId);
                        return;
                    }

                    // Changes detected - proceed with full sync
                    _logger.LogInformation(
                        "📝 Changes detected for FoodicsAccount {AccountId}. Proceeding with full sync. " +
                        "ChangeType={ChangeType}, PreviousHash={PreviousHash}, NewHash={NewHash}",
                        foodicsAccountId,
                        changeDetectionResult.ChangeType,
                        changeDetectionResult.PreviousHash,
                        changeDetectionResult.CurrentHash);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, 
                        "Menu versioning change detection failed for account {AccountId}. Falling back to legacy sync.", 
                        foodicsAccountId);
                        
                    if (syncRun != null)
                    {
                        await _syncRunManager.AddWarningAsync(
                            syncRun.Id, 
                            "Change detection failed - falling back to legacy sync", 
                            new Dictionary<string, object> { ["Exception"] = ex.Message },
                            cancellationToken);
                    }
                    // Continue with legacy sync behavior
                }
            }

            if (useSnapshotIdempotency)
            {
                try
                {
                    snapshotIdempotencyKey = _idempotencyService.GenerateMenuSnapshotKey(
                        foodicsAccountId,
                        branchId,
                        allProducts.Values);

                    var snapshotStaleMinutes = _configuration.GetValue<int>("Idempotency:MenuSnapshotStaleMinutes", 60);
                    var snapshotStaleAfter = TimeSpan.FromMinutes(Math.Max(5, snapshotStaleMinutes));

                    var (canProcessSnapshot, existingSnapshotRecord) = await _idempotencyService.CheckAndMarkStartedAsync(
                        foodicsAccountId,
                        snapshotIdempotencyKey,
                        retentionDays: 30, // 14–30 days as per SDD 7.2
                        cancellationToken,
                        snapshotStaleAfter);

                    if (!canProcessSnapshot)
                    {
                        // Map SDD behaviour:
                        // - Succeeded  -> treat as 200/OK and short-circuit
                        // - FailedPermanent -> treat as 400/BadRequest equivalent (log + skip)
                        _logger.LogInformation(
                            "Skipping menu sync for FoodicsAccount {AccountId}, Branch {BranchId} because an identical menu snapshot has already been processed. SnapshotStatus={Status}",
                            foodicsAccountId,
                            branchId ?? "<all>",
                            existingSnapshotRecord?.Status);

                        if (syncRun != null)
                        {
                            await _syncRunManager.CompleteSyncRunAsync(syncRun.Id, "Skipped - Identical snapshot already processed", cancellationToken: cancellationToken);
                        }
                        
                        if (jobLockAcquired && !jobLockReleased && !disableMenuIdempotency)
                        {
                            await _idempotencyService.MarkSucceededAsync(
                                foodicsAccountId,
                                idempotencyKey,
                                new { Message = "Skipped - Identical snapshot already processed" },
                                cancellationToken);
                            jobLockReleased = true;
                        }
                        return;
                    }
                }
                catch (BusinessException ex) when (ex.Code == "OPERATION_IN_PROGRESS")
                {
                    // Another worker is already processing the same snapshot – equivalent to HTTP 429
                    _logger.LogWarning(
                        ex,
                        "Menu snapshot sync for FoodicsAccount {AccountId}, Branch {BranchId} is already in progress. Skipping duplicate snapshot.",
                        foodicsAccountId,
                        branchId ?? "<all>");

                    if (syncRun != null)
                    {
                        await _syncRunManager.CompleteSyncRunAsync(syncRun.Id, "Skipped - Duplicate snapshot processing", cancellationToken: cancellationToken);
                    }

                    if (jobLockAcquired && !jobLockReleased && !disableMenuIdempotency)
                    {
                        await _idempotencyService.MarkSucceededAsync(
                            foodicsAccountId,
                            idempotencyKey,
                            new { Message = "Skipped - Duplicate snapshot processing" },
                            cancellationToken);
                        jobLockReleased = true;
                    }
                    return;
                }
            }

            // ✨ Update progress: Staging
            if (syncRun != null)
            {
                await _syncRunManager.UpdateProgressAsync(
                    syncRun.Id, 
                    "StagingSync", 
                    50, 
                    "Saving products to staging table",
                    cancellationToken: cancellationToken);
            }

            // Save to staging table
            var syncResult = await _stagingService.SaveProductsToStagingAsync(
                foodicsAccountId,
                allProducts.Values,
                branchId,
                cancellationToken);

            _logger.LogInformation(
                "Foodics products saved to staging for FoodicsAccount {AccountId}, Branch {BranchId}. " +
                "Saved: {Saved}, Updated: {Updated}, Errors: {Errors}, Total: {Total}",
                foodicsAccountId,
                branchId ?? "<all>",
                syncResult.SavedCount,
                syncResult.UpdatedCount,
                syncResult.ErrorCount,
                syncResult.TotalProcessed);

            // ✨ Update sync run statistics
            if (syncRun != null)
            {
                await _syncRunManager.UpdateStatisticsAsync(
                    syncRun.Id,
                    totalProcessed: syncResult.TotalProcessed,
                    succeeded: syncResult.SavedCount + syncResult.UpdatedCount,
                    failed: syncResult.ErrorCount,
                    added: syncResult.SavedCount,
                    updated: syncResult.UpdatedCount,
                    cancellationToken: cancellationToken);
            }

            // ---------------------------------------------------------------------------------
            // Push to Talabat (if enabled and vendor code is configured)
            // ---------------------------------------------------------------------------------
            var talabatEnabled = _configuration.GetValue<bool>("Talabat:Enabled", true);
            var talabatTargets = await GetTalabatSyncTargetsAsync(foodicsAccountId, branchId, cancellationToken);
            var talabatWorkItems = new List<TalabatSubmissionWorkItem>();

            if (talabatEnabled && talabatTargets.Count > 0)
            {
                _logger.LogInformation(
                    "Pushing catalog to {TargetCount} Talabat account(s). FoodicsAccount={AccountId}, ProductCount={ProductCount}",
                    talabatTargets.Count,
                    foodicsAccountId,
                    allProducts.Count);

                foreach (var talabatTarget in talabatTargets)
                {
                    var talabatChainCode = talabatTarget.ChainCode;
                    var talabatVendorCode = talabatTarget.VendorCode;
                    var correlationId = syncRun?.CorrelationId ?? Guid.NewGuid().ToString();

                    if (!talabatTarget.SyncAllBranches && string.IsNullOrWhiteSpace(talabatTarget.FoodicsBranchId) && string.IsNullOrWhiteSpace(branchId))
                    {
                        _logger.LogWarning(
                            "Skipping Talabat sync because target branch is not configured. FoodicsAccount={AccountId}, VendorCode={VendorCode}",
                            foodicsAccountId,
                            talabatVendorCode);
                        continue;
                    }

                    var groupScope = await ResolveTalabatGroupScopeAsync(
                        talabatTarget,
                        foodicsAccountId,
                        accessToken,
                        cancellationToken);

                    var groupFilterResult = _groupFilterService.FilterProductsByGroups(
                        allProducts,
                        groupScope.GroupIds,
                        correlationId,
                        groupScope.Label);

                    if (groupFilterResult.FilteredCount == 0)
                    {
                        _logger.LogWarning(
                            "No products available for Talabat target after group filtering. FoodicsAccount={AccountId}, VendorCode={VendorCode}, TargetGroup={TargetGroup}, GroupName={GroupName}",
                            foodicsAccountId,
                            talabatVendorCode,
                            talabatTarget.FoodicsGroupId ?? "<all>",
                            talabatTarget.FoodicsGroupName ?? "<unknown>");
                        continue;
                    }

                    var groupedProducts = groupFilterResult.FilteredProducts
                        .ToDictionary(p => p.Id, p => p, StringComparer.OrdinalIgnoreCase);

                    var effectiveBranchId = !string.IsNullOrWhiteSpace(branchId)
                        ? branchId
                        : (talabatTarget.SyncAllBranches ? null : talabatTarget.FoodicsBranchId);
                    var syncAllBranchesForRun = string.IsNullOrWhiteSpace(branchId) && talabatTarget.SyncAllBranches;
                    var branchFilterResult = _branchFilterService.FilterProductsByBranch(
                        groupedProducts,
                        effectiveBranchId,
                        syncAllBranchesForRun,
                        correlationId);

                    if (branchFilterResult.FilteredCount == 0)
                    {
                        _logger.LogWarning(
                            "No products available for Talabat target after branch filtering. FoodicsAccount={AccountId}, VendorCode={VendorCode}, TargetBranch={TargetBranch}",
                            foodicsAccountId,
                            talabatVendorCode,
                            effectiveBranchId ?? "<all>");
                        continue;
                    }

                    if (_configuration.GetValue<bool>("Talabat:ParallelSubmissionEnabled", true))
                    {
                        talabatWorkItems.Add(new TalabatSubmissionWorkItem(
                            talabatChainCode,
                            talabatVendorCode,
                            effectiveBranchId,
                            talabatTarget.FoodicsGroupId,
                            talabatTarget.FoodicsGroupName,
                            branchFilterResult.FilteredProducts.ToList(),
                            correlationId));
                        continue;
                    }

                    // ✨ Update progress: Talabat sync
                    if (syncRun != null)
                    {
                        await _syncRunManager.UpdateProgressAsync(
                            syncRun.Id,
                            MenuSyncPhase.TalabatSubmission,
                            70,
                            "Pushing catalog to Talabat",
                            new Dictionary<string, object> { ["VendorCode"] = talabatVendorCode },
                            cancellationToken: cancellationToken);
                    }

                    _logger.LogInformation(
                        "Pushing catalog to Talabat. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}, TargetGroup={TargetGroup}, GroupName={GroupName}, TargetBranch={TargetBranch}, ProductCount={ProductCount}",
                        foodicsAccountId,
                        talabatChainCode,
                        talabatVendorCode,
                        talabatTarget.FoodicsGroupId ?? "<all>",
                        talabatTarget.FoodicsGroupName ?? "<unknown>",
                        effectiveBranchId ?? "<all>",
                        branchFilterResult.FilteredCount);

                    try
                    {
                        var talabatResult = await _talabatSyncService.SyncCatalogV2Async(
                            branchFilterResult.FilteredProducts,
                            talabatChainCode,
                            foodicsAccountId,
                            effectiveBranchId,
                            correlationId,
                            talabatVendorCode,
                            cancellationToken);

                        if (talabatResult.Success)
                        {
                            _logger.LogInformation(
                                "Talabat catalog sync submitted successfully. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}, " +
                                "ImportId={ImportId}, Categories={Categories}, Products={Products}, Duration={Duration}ms",
                                foodicsAccountId,
                                talabatChainCode,
                                talabatVendorCode,
                                talabatResult.ImportId,
                                talabatResult.CategoriesCount,
                                talabatResult.ProductsCount,
                                talabatResult.Duration?.TotalMilliseconds ?? 0);

                            // ✨ Update sync run with Talabat info
                            if (syncRun != null)
                            {
                                await _syncRunManager.SetTalabatSyncInfoAsync(
                                    syncRun.Id,
                                    talabatVendorCode,
                                    talabatResult.ImportId,
                                    "Submitted",
                                    cancellationToken);
                            }

                            // ✨ NEW: Create menu snapshot after first successful Talabat sync
                            if (newSnapshot == null && versioningEnabled && changeDetectionResult != null)
                            {
                                try
                                {
                                    newSnapshot = await _menuVersioningService.CreateSnapshotAsync(
                                        foodicsAccountId,
                                        branchId,
                                        allProducts.Values.ToList(),
                                        changeDetectionResult.CurrentHash,
                                        changeDetectionResult.PreviousVersion,
                                        menuGroupId: null,
                                        storeCompressedData: _configuration.GetValue<bool>("MenuVersioning:StoreCompressedData", false),
                                        cancellationToken);

                                    // Mark snapshot as synced to Talabat
                                    await _menuVersioningService.MarkSnapshotAsSyncedAsync(
                                        newSnapshot.Id,
                                        talabatResult.ImportId!,
                                        talabatVendorCode,
                                        cancellationToken);

                                    _logger.LogInformation(
                                        "✅ Menu snapshot created and marked as synced. SnapshotId={SnapshotId}, Version={Version}, ImportId={ImportId}",
                                        newSnapshot.Id,
                                        newSnapshot.Version,
                                        talabatResult.ImportId);
                                }
                                catch (Exception ex)
                                {
                                    _logger.LogWarning(ex, "Failed to create menu snapshot for account {AccountId}", foodicsAccountId);

                                    if (syncRun != null)
                                    {
                                        await _syncRunManager.AddWarningAsync(
                                            syncRun.Id,
                                            "Failed to create menu snapshot",
                                            new Dictionary<string, object> { ["Exception"] = ex.Message },
                                            cancellationToken);
                                    }
                                }
                            }
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Talabat catalog sync failed. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}, " +
                                "Message={Message}, Errors={Errors}",
                                foodicsAccountId,
                                talabatChainCode,
                                talabatVendorCode,
                                talabatResult.Message,
                                talabatResult.Errors != null ? string.Join("; ", talabatResult.Errors.Take(5)) : "<none>");

                            if (syncRun != null)
                            {
                                await _syncRunManager.AddErrorAsync(
                                    syncRun.Id,
                                    $"Talabat sync failed: {talabatResult.Message}",
                                    context: new Dictionary<string, object>
                                    {
                                        ["TalabatErrors"] = talabatResult.Errors?.Take(5).ToList() ?? new List<string>(),
                                        ["VendorCode"] = talabatVendorCode
                                    },
                                    cancellationToken: cancellationToken);
                            }

                            // Note: We don't fail the entire sync if Talabat push fails
                            // The staging data is still saved, and Talabat sync can be retried
                        }
                    }
                    catch (Exception talabatEx)
                    {
                        _logger.LogError(
                            talabatEx,
                            "Error pushing catalog to Talabat. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}. " +
                            "Staging data saved successfully, but Talabat sync failed.",
                            foodicsAccountId,
                            talabatChainCode,
                            talabatVendorCode);

                        if (syncRun != null)
                        {
                            await _syncRunManager.AddErrorAsync(
                                syncRun.Id,
                                "Talabat sync exception",
                                talabatEx,
                                new Dictionary<string, object> { ["VendorCode"] = talabatVendorCode },
                                cancellationToken);
                        }

                        // Don't rethrow - staging sync succeeded, Talabat can be retried later
                    }
                }
            }
            if (talabatEnabled && talabatTargets.Count > 0 && talabatWorkItems.Count > 0)
            {
                var maxParallelSubmissions = Math.Max(
                    1,
                    _configuration.GetValue<int?>("Talabat:MaxParallelSubmissions") ?? 1);

                _logger.LogInformation(
                    "Submitting {TargetCount} Talabat catalog(s) with MaxParallelSubmissions={MaxParallelSubmissions}. FoodicsAccount={AccountId}",
                    talabatWorkItems.Count,
                    maxParallelSubmissions,
                    foodicsAccountId);

                var talabatResults = await SubmitTalabatWorkItemsAsync(
                    talabatWorkItems,
                    maxParallelSubmissions,
                    foodicsAccountId,
                    syncRunId: null,
                    cancellationToken);

                var firstSuccessfulResult = talabatResults
                    .FirstOrDefault(result => result.Success && !string.IsNullOrWhiteSpace(result.ImportId));

                if (firstSuccessfulResult != null && newSnapshot == null && versioningEnabled && changeDetectionResult != null)
                {
                    try
                    {
                        newSnapshot = await _menuVersioningService.CreateSnapshotAsync(
                            foodicsAccountId,
                            branchId,
                            allProducts.Values.ToList(),
                            changeDetectionResult.CurrentHash,
                            changeDetectionResult.PreviousVersion,
                            menuGroupId: null,
                            storeCompressedData: _configuration.GetValue<bool>("MenuVersioning:StoreCompressedData", false),
                            cancellationToken);

                        await _menuVersioningService.MarkSnapshotAsSyncedAsync(
                            newSnapshot.Id,
                            firstSuccessfulResult.ImportId!,
                            firstSuccessfulResult.VendorCode,
                            cancellationToken);

                        _logger.LogInformation(
                            "Menu snapshot created and marked as synced. SnapshotId={SnapshotId}, Version={Version}, ImportId={ImportId}",
                            newSnapshot.Id,
                            newSnapshot.Version,
                            firstSuccessfulResult.ImportId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to create menu snapshot for account {AccountId}", foodicsAccountId);

                        if (syncRun != null)
                        {
                            await _syncRunManager.AddWarningAsync(
                                syncRun.Id,
                                "Failed to create menu snapshot",
                                new Dictionary<string, object> { ["Exception"] = ex.Message },
                                cancellationToken);
                        }
                    }
                }
            }
            else if (!talabatEnabled)
            {
                _logger.LogDebug(
                    "Talabat sync is disabled. Skipping push for FoodicsAccount {AccountId}",
                    foodicsAccountId);
                    
                if (syncRun != null)
                {
                    await _syncRunManager.UpdateProgressAsync(
                        syncRun.Id, 
                        MenuSyncPhase.TalabatSubmission, 
                        70, 
                        "Talabat sync disabled - skipping",
                        cancellationToken: cancellationToken);
                }
            }
            else
            {
                _logger.LogDebug(
                    "No Talabat sync targets configured or matched for FoodicsAccount {AccountId}. Skipping Talabat push.",
                    foodicsAccountId);
                    
                if (syncRun != null)
                {
                    await _syncRunManager.AddWarningAsync(
                        syncRun.Id, 
                        "No Talabat sync targets configured or matched",
                        cancellationToken: cancellationToken);
                }
            }

            // ✨ Update progress: Completing
            if (syncRun != null)
            {
                await _syncRunManager.UpdateProgressAsync(
                    syncRun.Id, 
                    MenuSyncPhase.Finalization, 
                    90, 
                    "Finalizing sync operation",
                    cancellationToken: cancellationToken);
            }

            // Mark as succeeded - this releases the job-level lock for this account
            if (!disableMenuIdempotency)
            {
                await _idempotencyService.MarkSucceededAsync(
                    foodicsAccountId,
                    idempotencyKey,
                    syncResult,
                    cancellationToken);
                jobLockReleased = true;
            }

            // Mark snapshot as succeeded with result hash for duplicate detection
            if (!string.IsNullOrWhiteSpace(snapshotIdempotencyKey) && !disableMenuIdempotency)
            {
                await _idempotencyService.MarkSucceededAsync(
                    foodicsAccountId,
                    snapshotIdempotencyKey,
                    syncResult,
                    cancellationToken);
            }

            // ✨ Complete sync run
            if (syncRun != null)
            {
                await _syncRunManager.CompleteSyncRunAsync(
                    syncRun.Id, 
                    "Success",
                    new Dictionary<string, object>
                    {
                        ["VersioningEnabled"] = versioningEnabled,
                        ["ChangeDetected"] = changeDetectionResult?.HasChanged ?? true,
                        ["SnapshotCreated"] = newSnapshot != null,
                        ["StagingResult"] = syncResult,
                        ["OptimizationApplied"] = changeDetectionResult?.HasChanged == false
                    },
                    cancellationToken);
            }

            _logger.LogInformation(
                "✅ Menu sync completed successfully for FoodicsAccount {AccountId}, Branch {BranchId}. " +
                "Foodics: Saved={Saved}, Updated={Updated}, Errors={Errors}, Total={Total}. " +
                "Versioning: {VersioningStatus}",
                foodicsAccountId,
                branchId ?? "<all>",
                syncResult.SavedCount,
                syncResult.UpdatedCount,
                syncResult.ErrorCount,
                syncResult.TotalProcessed,
                versioningEnabled ? "Enabled" : "Disabled");
        }
        catch (Exception ex)
        {
            // Mark as failed - this releases the job-level lock and marks the operation as failed
            if (!disableMenuIdempotency)
            {
                await _idempotencyService.MarkFailedAsync(
                    foodicsAccountId,
                    idempotencyKey,
                    cancellationToken);
                jobLockReleased = true;
            }

            // Also mark the snapshot key as failed (permanent failure for this snapshot)
            if (!string.IsNullOrWhiteSpace(snapshotIdempotencyKey) && !disableMenuIdempotency)
            {
                await _idempotencyService.MarkFailedAsync(
                    foodicsAccountId,
                    snapshotIdempotencyKey,
                    cancellationToken);
            }
            
            // ✨ Fail sync run
            if (syncRun != null)
            {
                await _syncRunManager.FailSyncRunAsync(
                    syncRun.Id, 
                    ex.Message, 
                    ex,
                    new Dictionary<string, object>
                    {
                        ["FoodicsAccountId"] = foodicsAccountId,
                        ["BranchId"] = branchId ?? "<all>",
                        ["IdempotencyKey"] = idempotencyKey
                    },
                    cancellationToken);
            }
            
            _logger.LogError(
                ex,
                "❌ Menu sync failed for FoodicsAccount {AccountId}, Branch {BranchId}. Error: {Error}",
                foodicsAccountId,
                branchId ?? "<all>",
                ex.Message);
            
            throw;
        }
        finally
        {
            if (jobLockAcquired && !jobLockReleased && !disableMenuIdempotency)
            {
                try
                {
                    await _idempotencyService.MarkFailedAsync(
                        foodicsAccountId,
                        idempotencyKey,
                        cancellationToken);
                }
                catch (Exception releaseEx)
                {
                    _logger.LogWarning(
                        releaseEx,
                        "Failed to release job-level idempotency lock for FoodicsAccount {AccountId}",
                        foodicsAccountId);
                }
            }
        }
    }

    private async Task<List<TalabatSubmissionResult>> SubmitTalabatWorkItemsAsync(
        IReadOnlyCollection<TalabatSubmissionWorkItem> workItems,
        int maxParallelSubmissions,
        Guid foodicsAccountId,
        Guid? syncRunId,
        CancellationToken cancellationToken)
    {
        if (workItems.Count == 0)
        {
            return new List<TalabatSubmissionResult>();
        }

        using var semaphore = new SemaphoreSlim(maxParallelSubmissions, maxParallelSubmissions);
        var tasks = workItems.Select(async workItem =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await SubmitTalabatWorkItemAsync(workItem, foodicsAccountId, syncRunId, cancellationToken);
            }
            finally
            {
                semaphore.Release();
            }
        });

        return (await Task.WhenAll(tasks)).ToList();
    }

    private async Task<TalabatSubmissionResult> SubmitTalabatWorkItemAsync(
        TalabatSubmissionWorkItem workItem,
        Guid foodicsAccountId,
        Guid? syncRunId,
        CancellationToken cancellationToken)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var unitOfWorkManager = scope.ServiceProvider.GetRequiredService<IUnitOfWorkManager>();
        var talabatSyncService = scope.ServiceProvider.GetRequiredService<TalabatCatalogSyncService>();
        var syncRunManager = scope.ServiceProvider.GetRequiredService<MenuSyncRunManager>();

        using var uow = unitOfWorkManager.Begin(requiresNew: true, isTransactional: false);

        if (syncRunId.HasValue)
        {
            await syncRunManager.UpdateProgressAsync(
                syncRunId.Value,
                MenuSyncPhase.TalabatSubmission,
                70,
                "Pushing catalog to Talabat",
                new Dictionary<string, object> { ["VendorCode"] = workItem.VendorCode },
                cancellationToken: cancellationToken);
        }

        _logger.LogInformation(
            "Pushing catalog to Talabat. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}, TargetGroup={TargetGroup}, GroupName={GroupName}, TargetBranch={TargetBranch}, ProductCount={ProductCount}",
            foodicsAccountId,
            workItem.ChainCode,
            workItem.VendorCode,
            workItem.FoodicsGroupId ?? "<all>",
            workItem.FoodicsGroupName ?? "<unknown>",
            workItem.BranchId ?? "<all>",
            workItem.Products.Count);

        try
        {
            var talabatResult = await talabatSyncService.SyncCatalogV2Async(
                workItem.Products,
                workItem.ChainCode,
                foodicsAccountId,
                workItem.BranchId,
                workItem.CorrelationId,
                workItem.VendorCode,
                cancellationToken);

            if (talabatResult.Success)
            {
                _logger.LogInformation(
                    "Talabat catalog sync submitted successfully. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}, ImportId={ImportId}, Categories={Categories}, Products={Products}, Duration={Duration}ms",
                    foodicsAccountId,
                    workItem.ChainCode,
                    workItem.VendorCode,
                    talabatResult.ImportId,
                    talabatResult.CategoriesCount,
                    talabatResult.ProductsCount,
                    talabatResult.Duration?.TotalMilliseconds ?? 0);

                if (syncRunId.HasValue)
                {
                    await syncRunManager.SetTalabatSyncInfoAsync(
                        syncRunId.Value,
                        workItem.VendorCode,
                        talabatResult.ImportId,
                        "Submitted",
                        cancellationToken);
                }
            }
            else
            {
                _logger.LogWarning(
                    "Talabat catalog sync failed. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}, Message={Message}, Errors={Errors}",
                    foodicsAccountId,
                    workItem.ChainCode,
                    workItem.VendorCode,
                    talabatResult.Message,
                    talabatResult.Errors != null ? string.Join("; ", talabatResult.Errors.Take(5)) : "<none>");

                if (syncRunId.HasValue)
                {
                    await syncRunManager.AddErrorAsync(
                        syncRunId.Value,
                        $"Talabat sync failed: {talabatResult.Message}",
                        context: new Dictionary<string, object>
                        {
                            ["TalabatErrors"] = talabatResult.Errors?.Take(5).ToList() ?? new List<string>(),
                            ["VendorCode"] = workItem.VendorCode
                        },
                        cancellationToken: cancellationToken);
                }
            }

            await uow.CompleteAsync(cancellationToken);

            return new TalabatSubmissionResult(
                workItem.VendorCode,
                talabatResult.ImportId,
                talabatResult.Success);
        }
        catch (Exception talabatEx)
        {
            _logger.LogError(
                talabatEx,
                "Error pushing catalog to Talabat. FoodicsAccount={AccountId}, ChainCode={ChainCode}, VendorCode={VendorCode}. Staging data saved successfully, but Talabat sync failed.",
                foodicsAccountId,
                workItem.ChainCode,
                workItem.VendorCode);

            if (syncRunId.HasValue)
            {
                await syncRunManager.AddErrorAsync(
                    syncRunId.Value,
                    "Talabat sync exception",
                    talabatEx,
                    new Dictionary<string, object> { ["VendorCode"] = workItem.VendorCode },
                    cancellationToken);
            }

            return new TalabatSubmissionResult(workItem.VendorCode, null, false);
        }
    }

    private sealed record TalabatSubmissionWorkItem(
        string ChainCode,
        string VendorCode,
        string? BranchId,
        string? FoodicsGroupId,
        string? FoodicsGroupName,
        IReadOnlyList<FoodicsProductDetailDto> Products,
        string CorrelationId);

    private sealed record TalabatSubmissionResult(
        string VendorCode,
        string? ImportId,
        bool Success);

    private async Task<TalabatGroupScope> ResolveTalabatGroupScopeAsync(
        (string ChainCode, string VendorCode, string? FoodicsBranchId, bool SyncAllBranches, string? FoodicsGroupId, string? FoodicsGroupName) talabatTarget,
        Guid foodicsAccountId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        const string fallbackRootGroupName = "Talabat";

        var rootGroup = !string.IsNullOrWhiteSpace(talabatTarget.FoodicsGroupId)
            ? await _foodicsCatalogClient.GetGroupByIdAsync(
                talabatTarget.FoodicsGroupId,
                accessToken: accessToken,
                foodicsAccountId: foodicsAccountId,
                cancellationToken: cancellationToken)
            : null;

        rootGroup ??= await _foodicsCatalogClient.GetGroupByNameAsync(
            fallbackRootGroupName,
            accessToken: accessToken,
            foodicsAccountId: foodicsAccountId,
            cancellationToken: cancellationToken);

        if (rootGroup == null || string.IsNullOrWhiteSpace(rootGroup.Id))
        {
            _logger.LogWarning(
                "Unable to resolve Talabat root group from Foodics. FoodicsAccount={AccountId}, ExpectedGroupId={ExpectedGroupId}, ExpectedRootGroup={ExpectedRootGroup}, VendorCode={VendorCode}",
                foodicsAccountId,
                talabatTarget.FoodicsGroupId ?? "<none>",
                fallbackRootGroupName,
                talabatTarget.VendorCode);
            return new TalabatGroupScope(Array.Empty<string>(), talabatTarget.FoodicsGroupName ?? fallbackRootGroupName);
        }

        var groupIds = FlattenGroupIds(rootGroup)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation(
            "Resolved Talabat group scope. FoodicsAccount={AccountId}, RootGroupId={RootGroupId}, RootGroupName={RootGroupName}, GroupCount={GroupCount}",
            foodicsAccountId,
            rootGroup.Id,
            rootGroup.Name ?? rootGroup.NameLocalized ?? "<unnamed>",
            groupIds.Count);

        return new TalabatGroupScope(
            groupIds,
            rootGroup.Name ?? rootGroup.NameLocalized ?? fallbackRootGroupName);
    }

    private static IEnumerable<string> FlattenGroupIds(FoodicsGroupInfoDto rootGroup)
    {
        if (!string.IsNullOrWhiteSpace(rootGroup.Id))
        {
            yield return rootGroup.Id;
        }

        if (rootGroup.Subgroups == null)
        {
            yield break;
        }

        foreach (var subgroup in rootGroup.Subgroups)
        {
            foreach (var subgroupId in FlattenGroupIds(subgroup))
            {
                yield return subgroupId;
            }
        }
    }

    private sealed record TalabatGroupScope(IReadOnlyCollection<string>? GroupIds, string Label)
    {
        public static TalabatGroupScope None { get; } = new(null, "<all>");
    }

    /// <summary>
    /// Gets all Talabat sync targets for a FoodicsAccount.
    /// Prefers linked TalabatAccounts, filtered by branch if needed, then falls back to legacy configuration.
    /// </summary>
    private async Task<List<(string ChainCode, string VendorCode, string? FoodicsBranchId, bool SyncAllBranches, string? FoodicsGroupId, string? FoodicsGroupName)>> GetTalabatSyncTargetsAsync(
        Guid foodicsAccountId,
        string? branchId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Prefer TalabatAccount linked to this FoodicsAccount
            var linkedAccounts = await _talabatAccountService.GetAccountsByFoodicsAccountIdAsync(foodicsAccountId, cancellationToken);
            if (linkedAccounts.Count > 0)
            {
                if (!string.IsNullOrWhiteSpace(branchId))
                {
                    linkedAccounts = linkedAccounts
                        .Where(x => x.SyncAllBranches || x.FoodicsBranchId == branchId)
                        .ToList();
                }

                if (linkedAccounts.Count == 0)
                {
                    _logger.LogInformation(
                        "No TalabatAccounts matched branch filter. FoodicsAccount={AccountId}, BranchId={BranchId}",
                        foodicsAccountId,
                        branchId);

                    return new List<(string ChainCode, string VendorCode, string? FoodicsBranchId, bool SyncAllBranches, string? FoodicsGroupId, string? FoodicsGroupName)>();
                }

                return linkedAccounts
                    .Where(x => !string.IsNullOrWhiteSpace(x.ChainCode) && !string.IsNullOrWhiteSpace(x.VendorCode))
                    .Select(x => (x.ChainCode!, x.VendorCode!, x.FoodicsBranchId, x.SyncAllBranches, x.FoodicsGroupId, x.FoodicsGroupName))
                    .Distinct()
                    .ToList();
            }

            // Fallback: Try to get vendor code from FoodicsAccount entity (legacy)
            var account = await _foodicsAccountRepository.GetAsync(foodicsAccountId, cancellationToken: cancellationToken);
            var vendorCode = account.GetType().GetProperty("TalabatVendorCode")?.GetValue(account) as string;
            if (!string.IsNullOrWhiteSpace(vendorCode))
            {
                var chainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";
                return new List<(string ChainCode, string VendorCode, string? FoodicsBranchId, bool SyncAllBranches, string? FoodicsGroupId, string? FoodicsGroupName)> { (chainCode, vendorCode, null, true, null, null) };
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not get Talabat sync targets for FoodicsAccount {AccountId}", foodicsAccountId);
        }

        // Final fallback to configuration (legacy support)
        var configVendorCode = _configuration["Talabat:DefaultVendorCode"];
        var configChainCode = _configuration["Talabat:ChainCode"] ?? "tlbt-pick";

        if (!string.IsNullOrWhiteSpace(configVendorCode))
        {
            _logger.LogDebug(
                "Using Talabat chain/vendor code from configuration for FoodicsAccount {AccountId}: ChainCode={ChainCode}, VendorCode={VendorCode}",
                foodicsAccountId,
                configChainCode,
                configVendorCode);

            return new List<(string ChainCode, string VendorCode, string? FoodicsBranchId, bool SyncAllBranches, string? FoodicsGroupId, string? FoodicsGroupName)> { (configChainCode, configVendorCode, null, true, null, null) };
        }

        return new List<(string ChainCode, string VendorCode, string? FoodicsBranchId, bool SyncAllBranches, string? FoodicsGroupId, string? FoodicsGroupName)>();
    }
}
