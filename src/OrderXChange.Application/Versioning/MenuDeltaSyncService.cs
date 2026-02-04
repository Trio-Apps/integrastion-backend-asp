using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Application.Validation;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Staging;
using OrderXChange.Domain.Versioning;
using Polly;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Implementation of menu delta synchronization service
/// Provides efficient incremental sync capabilities between Foodics and Talabat
/// </summary>
public class MenuDeltaSyncService : IMenuDeltaSyncService, ITransientDependency
{
    private readonly IRepository<MenuDelta, Guid> _deltaRepository;
    private readonly IRepository<MenuSnapshot, Guid> _snapshotRepository;
    private readonly IRepository<MenuGroupCategory, Guid> _menuGroupCategoryRepository;
    private readonly MenuVersioningService _versioningService;
    private readonly IMenuSoftDeleteService _softDeleteService;
    private readonly IMenuValidationService _validationService;
    private readonly IModifierLifecycleService _modifierLifecycleService;
    private readonly MenuSyncRetryPolicy _retryPolicy;
    private readonly IMenuSyncDlqService _dlqService;
    private readonly MenuSyncPerformanceOptimizer _performanceOptimizer;
    private readonly MenuSyncBatchProcessor _batchProcessor;
    private readonly ILogger<MenuDeltaSyncService> _logger;

    public MenuDeltaSyncService(
        IRepository<MenuDelta, Guid> deltaRepository,
        IRepository<MenuSnapshot, Guid> snapshotRepository,
        IRepository<MenuGroupCategory, Guid> menuGroupCategoryRepository,
        MenuVersioningService versioningService,
        IMenuSoftDeleteService softDeleteService,
        IMenuValidationService validationService,
        IModifierLifecycleService modifierLifecycleService,
        MenuSyncRetryPolicy retryPolicy,
        IMenuSyncDlqService dlqService,
        MenuSyncPerformanceOptimizer performanceOptimizer,
        MenuSyncBatchProcessor batchProcessor,
        ILogger<MenuDeltaSyncService> logger)
    {
        _deltaRepository = deltaRepository;
        _snapshotRepository = snapshotRepository;
        _menuGroupCategoryRepository = menuGroupCategoryRepository;
        _versioningService = versioningService;
        _softDeleteService = softDeleteService;
        _validationService = validationService;
        _modifierLifecycleService = modifierLifecycleService;
        _retryPolicy = retryPolicy;
        _dlqService = dlqService;
        _performanceOptimizer = performanceOptimizer;
        _batchProcessor = batchProcessor;
        _logger = logger;
    }

    public async Task<DeltaGenerationResult> GenerateDeltaAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        Guid? menuGroupId = null,
        bool forceFullSync = false,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString("N");
        
        try
        {
            _logger.LogInformation(
                "Generating delta. AccountId={AccountId}, BranchId={BranchId}, MenuGroupId={MenuGroupId}, ProductsCount={Count}, ForceFullSync={Force}, CorrelationId={CorrelationId}",
                foodicsAccountId, branchId ?? "ALL", menuGroupId?.ToString() ?? "ALL", currentProducts.Count, forceFullSync, correlationId);

            // Execute with retry policy
            var policy = _retryPolicy.CreatePolicy<DeltaGenerationResult>("DeltaGeneration");
            
            return await policy.ExecuteAsync(async () =>
            {
                // Step 0: Filter products by Menu Group if specified
                var filteredProducts = currentProducts;
                if (menuGroupId.HasValue)
                {
                    filteredProducts = await FilterProductsByMenuGroupAsync(
                        menuGroupId.Value, currentProducts, cancellationToken);
                    
                    _logger.LogInformation(
                        "Filtered products by Menu Group. Original={Original}, Filtered={Filtered}, CorrelationId={CorrelationId}",
                        currentProducts.Count, filteredProducts.Count, correlationId);
                }

                // Step 1: Detect changes using existing versioning service (with Menu Group context)
                var changeDetection = await _versioningService.DetectChangesAsync(
                    foodicsAccountId, branchId, filteredProducts, menuGroupId, cancellationToken);

                // Step 1.5: Sync modifiers and detect modifier changes
                var modifierSyncResult = await _modifierLifecycleService.SyncModifiersAsync(
                    foodicsAccountId, branchId, menuGroupId, filteredProducts, cancellationToken);

                var modifierChangeDetection = await _modifierLifecycleService.DetectModifierChangesAsync(
                    foodicsAccountId, branchId, menuGroupId, filteredProducts, cancellationToken);

                // Include modifier changes in overall change detection
                var hasModifierChanges = modifierChangeDetection.HasChanges;
                var hasAnyChanges = changeDetection.HasChanged || hasModifierChanges;

                if (!hasAnyChanges && !forceFullSync)
                {
                    _logger.LogInformation("No changes detected (products or modifiers). Skipping delta generation. CorrelationId={CorrelationId}", correlationId);
                    return new DeltaGenerationResult
                    {
                        Success = true,
                        Payload = null,
                        GenerationTime = stopwatch.Elapsed,
                        ModifierSyncResult = modifierSyncResult
                    };
                }

                _logger.LogInformation(
                    "Changes detected. Products={ProductChanges}, Modifiers={ModifierChanges}, Force={Force}, CorrelationId={CorrelationId}",
                    changeDetection.HasChanged, hasModifierChanges, forceFullSync, correlationId);

                // Step 2: Create new snapshot (with Menu Group context)
                var newSnapshot = await _versioningService.CreateSnapshotAsync(
                    foodicsAccountId,
                    branchId,
                    filteredProducts,
                    changeDetection.CurrentHash,
                    changeDetection.PreviousVersion,
                    menuGroupId,
                    storeCompressedData: true,
                    cancellationToken);

                // Step 3: Generate delta payload (including soft deletes)
                var deltaPayload = await GenerateDeltaPayloadAsync(
                    changeDetection,
                    newSnapshot,
                    filteredProducts,
                    cancellationToken);

                // Step 4: Process soft deletes if this is an incremental sync (with Menu Group context)
                if (!changeDetection.IsFirstSync && changeDetection.LatestSnapshot != null)
                {
                    var previousProducts = await GetPreviousProductsAsync(changeDetection.LatestSnapshot, cancellationToken);
                    var deletions = await _softDeleteService.ProcessDeletedItemsAsync(
                        foodicsAccountId, branchId, filteredProducts, previousProducts, cancellationToken);

                    // Add soft deletes to delta payload
                    if (deletions.Any())
                    {
                        deltaPayload.SoftDeletedItems = deletions.Select(d => new ProductDeletionItem
                        {
                            Id = d.EntityId,
                            Name = d.EntityName ?? "",
                            EntityType = d.EntityType,
                            DeletionReason = d.DeletionReason,
                            DeletedAt = d.ProcessedAt,
                            DeletedBy = d.DeletionSource,
                            IsSyncedToTalabat = d.IsSyncedToTalabat,
                            EntitySnapshot = string.IsNullOrEmpty(d.EntitySnapshotJson) 
                                ? null 
                                : JsonSerializer.Deserialize<Dictionary<string, object>>(d.EntitySnapshotJson)
                        }).ToList();

                        // Update statistics
                        deltaPayload.Metadata.Statistics.SoftDeletedItems = deletions.Count;
                        deltaPayload.Metadata.TotalChanges += deletions.Count;
                    }
                }

                // Step 5: Create and store delta record
                var delta = await CreateDeltaRecordAsync(
                    changeDetection,
                    newSnapshot,
                    deltaPayload,
                    cancellationToken);

                stopwatch.Stop();

                _logger.LogInformation(
                    "Delta generated successfully. DeltaId={DeltaId}, TotalChanges={Changes}, Time={Time}ms, CorrelationId={CorrelationId}",
                    delta.Id, deltaPayload.Metadata.TotalChanges, stopwatch.ElapsedMilliseconds, correlationId);

                return new DeltaGenerationResult
                {
                    Success = true,
                    Payload = deltaPayload,
                    GenerationTime = stopwatch.Elapsed,
                    ModifierSyncResult = modifierSyncResult
                };
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate delta. AccountId={AccountId}, CorrelationId={CorrelationId}", foodicsAccountId, correlationId);
            
            // Store failure in DLQ
            try
            {
                await _dlqService.StoreDeltaGenerationFailureAsync(new MenuSyncDlqRequest
                {
                    CorrelationId = correlationId,
                    FoodicsAccountId = foodicsAccountId,
                    BranchId = branchId,
                    MenuGroupId = menuGroupId,
                    OriginalPayload = JsonSerializer.Serialize(new
                    {
                        FoodicsAccountId = foodicsAccountId,
                        BranchId = branchId,
                        MenuGroupId = menuGroupId,
                        ProductCount = currentProducts.Count,
                        ForceFullSync = forceFullSync
                    }),
                    Exception = ex,
                    AttemptCount = 1,
                    FailureType = IsTransientError(ex) ? DlqFailureTypes.Transient : DlqFailureTypes.Permanent,
                    Priority = DlqPriorities.High,
                    Context = new Dictionary<string, object>
                    {
                        ["FoodicsAccountId"] = foodicsAccountId,
                        ["BranchId"] = branchId ?? "",
                        ["MenuGroupId"] = menuGroupId?.ToString() ?? "",
                        ["ProductCount"] = currentProducts.Count,
                        ["ForceFullSync"] = forceFullSync
                    }
                }, cancellationToken);
            }
            catch (Exception dlqEx)
            {
                _logger.LogError(dlqEx, "Failed to store delta generation failure in DLQ. CorrelationId={CorrelationId}", correlationId);
            }
            
            return new DeltaGenerationResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                GenerationTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<DeltaSyncResult> SyncDeltaToTalabatAsync(
        Guid deltaId,
        string talabatVendorCode,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var correlationId = Guid.NewGuid().ToString("N");
        
        try
        {
            _logger.LogInformation(
                "Starting delta sync. DeltaId={DeltaId}, VendorCode={VendorCode}, CorrelationId={CorrelationId}",
                deltaId, talabatVendorCode, correlationId);

            // Step 1: Load delta and payload
            var delta = await _deltaRepository.GetAsync(deltaId, cancellationToken: cancellationToken);
            var payload = await GetDeltaPayloadAsync(deltaId, cancellationToken);

            if (payload == null)
            {
                throw new InvalidOperationException($"Delta payload not found for DeltaId: {deltaId}");
            }

            // Step 2: Update delta status to InProgress
            delta.SyncStatus = MenuDeltaSyncStatus.InProgress;
            delta.TalabatVendorCode = talabatVendorCode;
            await _deltaRepository.UpdateAsync(delta, autoSave: true, cancellationToken: cancellationToken);

            // Step 3: Validate delta payload before sync
            var validationResult = await _validationService.ValidateDeltaAsync(payload, failFast: true, cancellationToken);
            if (!validationResult.CanSubmitToTalabat)
            {
                throw new InvalidOperationException($"Delta validation failed: {validationResult.GetSummary()}");
            }

            // Step 4: Sync to Talabat with retry policy
            var policy = _retryPolicy.CreatePolicy<DeltaSyncResult>("DeltaSync");
            var syncResult = await policy.ExecuteAsync(async () =>
            {
                return await SyncPayloadToTalabatAsync(payload, talabatVendorCode, cancellationToken);
            });

            // Step 5: Update delta with sync results
            if (syncResult.Success)
            {
                delta.SyncStatus = MenuDeltaSyncStatus.Completed;
                delta.IsSyncedToTalabat = true;
                delta.TalabatImportId = syncResult.TalabatImportId;
                delta.TalabatSyncedAt = DateTime.UtcNow;
            }
            else
            {
                delta.SyncStatus = syncResult.PartialFailures.Any() 
                    ? MenuDeltaSyncStatus.PartiallyFailed 
                    : MenuDeltaSyncStatus.Failed;
                delta.SyncErrorDetails = syncResult.ErrorMessage;
                delta.RetryCount++;
            }

            await _deltaRepository.UpdateAsync(delta, autoSave: true, cancellationToken: cancellationToken);

            stopwatch.Stop();
            syncResult.SyncTime = stopwatch.Elapsed;

            _logger.LogInformation(
                "Delta sync completed. DeltaId={DeltaId}, Success={Success}, Time={Time}ms, CorrelationId={CorrelationId}",
                deltaId, syncResult.Success, stopwatch.ElapsedMilliseconds, correlationId);

            return syncResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to sync delta. DeltaId={DeltaId}, CorrelationId={CorrelationId}", deltaId, correlationId);
            
            // Update delta status to failed
            try
            {
                var delta = await _deltaRepository.GetAsync(deltaId, cancellationToken: cancellationToken);
                delta.SyncStatus = MenuDeltaSyncStatus.Failed;
                delta.SyncErrorDetails = ex.Message;
                delta.RetryCount++;
                await _deltaRepository.UpdateAsync(delta, autoSave: true, cancellationToken: cancellationToken);

                // Store failure in DLQ
                await _dlqService.StoreDeltaSyncFailureAsync(new MenuSyncDlqRequest
                {
                    CorrelationId = correlationId,
                    FoodicsAccountId = delta.FoodicsAccountId,
                    BranchId = delta.BranchId,
                    MenuGroupId = delta.MenuGroupId,
                    DeltaId = deltaId,
                    TalabatVendorCode = talabatVendorCode,
                    OriginalPayload = JsonSerializer.Serialize(new
                    {
                        DeltaId = deltaId,
                        TalabatVendorCode = talabatVendorCode,
                        RetryCount = delta.RetryCount
                    }),
                    Exception = ex,
                    AttemptCount = delta.RetryCount,
                    FailureType = IsTransientError(ex) ? DlqFailureTypes.Transient : DlqFailureTypes.Permanent,
                    Priority = delta.RetryCount > 3 ? DlqPriorities.High : DlqPriorities.Normal,
                    Context = new Dictionary<string, object>
                    {
                        ["DeltaId"] = deltaId,
                        ["TalabatVendorCode"] = talabatVendorCode,
                        ["FoodicsAccountId"] = delta.FoodicsAccountId,
                        ["BranchId"] = delta.BranchId ?? "",
                        ["MenuGroupId"] = delta.MenuGroupId?.ToString() ?? "",
                        ["RetryCount"] = delta.RetryCount
                    }
                }, cancellationToken);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update delta status after sync failure. CorrelationId={CorrelationId}", correlationId);
            }

            return new DeltaSyncResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                SyncTime = stopwatch.Elapsed
            };
        }
    }

    public async Task<DeltaSyncResult> GenerateAndSyncDeltaAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        string talabatVendorCode,
        Guid? menuGroupId = null,
        bool forceFullSync = false,
        CancellationToken cancellationToken = default)
    {
        // Step 1: Generate delta (with Menu Group context)
        var generationResult = await GenerateDeltaAsync(
            foodicsAccountId, branchId, currentProducts, menuGroupId, forceFullSync, cancellationToken);

        if (!generationResult.Success)
        {
            return new DeltaSyncResult
            {
                Success = false,
                ErrorMessage = $"Delta generation failed: {generationResult.ErrorMessage}",
                SyncTime = generationResult.GenerationTime
            };
        }

        // No changes to sync
        if (generationResult.Payload == null)
        {
            return new DeltaSyncResult
            {
                Success = true,
                ProcessedItems = 0,
                SyncTime = generationResult.GenerationTime
            };
        }

        // Step 2: Sync delta
        var syncResult = await SyncDeltaToTalabatAsync(
            generationResult.Payload.Metadata.DeltaId, talabatVendorCode, cancellationToken);

        // Combine generation and sync time
        syncResult.SyncTime = generationResult.GenerationTime + syncResult.SyncTime;

        return syncResult;
    }

    public async Task<MenuDeltaPayload?> GetDeltaPayloadAsync(
        Guid deltaId,
        CancellationToken cancellationToken = default)
    {
        var delta = await _deltaRepository.GetAsync(deltaId, cancellationToken: cancellationToken);
        
        if (delta.CompressedDeltaPayload == null)
        {
            return null;
        }

        var json = DecompressString(delta.CompressedDeltaPayload);
        return JsonSerializer.Deserialize<MenuDeltaPayload>(json);
    }

    public async Task<List<MenuDelta>> GetPendingDeltasAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var query = await _deltaRepository.GetQueryableAsync();
        
        return await query
            .Where(d => d.FoodicsAccountId == foodicsAccountId)
            .Where(d => branchId == null || d.BranchId == branchId)
            .Where(d => menuGroupId == null || d.MenuGroupId == menuGroupId)
            .Where(d => d.SyncStatus == MenuDeltaSyncStatus.Pending || 
                       d.SyncStatus == MenuDeltaSyncStatus.Failed)
            .OrderBy(d => d.CreationTime)
            .ToListAsync(cancellationToken);
    }

    public async Task<DeltaSyncResult> RetryDeltaSyncAsync(
        Guid deltaId,
        CancellationToken cancellationToken = default)
    {
        var delta = await _deltaRepository.GetAsync(deltaId, cancellationToken: cancellationToken);
        
        if (delta.TalabatVendorCode == null)
        {
            throw new InvalidOperationException("Cannot retry delta sync without vendor code");
        }

        // Implement exponential backoff based on retry count
        var backoffDelay = TimeSpan.FromSeconds(Math.Pow(2, delta.RetryCount));
        if (backoffDelay > TimeSpan.FromMinutes(30))
        {
            backoffDelay = TimeSpan.FromMinutes(30); // Max 30 minutes
        }

        _logger.LogInformation(
            "Retrying delta sync after {Delay}s. DeltaId={DeltaId}, RetryCount={RetryCount}",
            backoffDelay.TotalSeconds, deltaId, delta.RetryCount);

        await Task.Delay(backoffDelay, cancellationToken);

        return await SyncDeltaToTalabatAsync(deltaId, delta.TalabatVendorCode, cancellationToken);
    }

    public async Task<DeltaValidationResult> ValidateDeltaPayloadAsync(MenuDeltaPayload payload)
    {
        var result = new DeltaValidationResult { IsValid = true };

        // Validate metadata
        if (payload.Metadata.DeltaId == Guid.Empty)
        {
            result.Errors.Add("Delta ID is required");
            result.IsValid = false;
        }

        // Validate product dependencies
        var categoryIds = payload.AddedProducts.Concat(payload.UpdatedProducts)
            .Where(p => !string.IsNullOrEmpty(p.CategoryId))
            .Select(p => p.CategoryId!)
            .Distinct()
            .ToList();

        var availableCategoryIds = payload.Categories.Select(c => c.Id).ToHashSet();
        var missingCategories = categoryIds.Where(id => !availableCategoryIds.Contains(id)).ToList();
        
        if (missingCategories.Any())
        {
            result.MissingDependencies.AddRange(missingCategories.Select(id => $"Category: {id}"));
            result.Warnings.Add($"Missing {missingCategories.Count} category dependencies");
        }

        // Validate modifier dependencies
        var modifierIds = payload.AddedProducts.Concat(payload.UpdatedProducts)
            .Where(p => p.ModifierIds != null)
            .SelectMany(p => p.ModifierIds!)
            .Distinct()
            .ToList();

        var availableModifierIds = payload.Modifiers.Select(m => m.Id).ToHashSet();
        var missingModifiers = modifierIds.Where(id => !availableModifierIds.Contains(id)).ToList();
        
        if (missingModifiers.Any())
        {
            result.MissingDependencies.AddRange(missingModifiers.Select(id => $"Modifier: {id}"));
            result.Warnings.Add($"Missing {missingModifiers.Count} modifier dependencies");
        }

        return result;
    }

    public async Task<DeltaStatisticsReport> GetDeltaStatisticsAsync(
        Guid foodicsAccountId,
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default)
    {
        var query = await _deltaRepository.GetQueryableAsync();
        
        var deltas = await query
            .Where(d => d.FoodicsAccountId == foodicsAccountId)
            .Where(d => d.CreationTime >= fromDate && d.CreationTime <= toDate)
            .ToListAsync(cancellationToken);

        var report = new DeltaStatisticsReport
        {
            FoodicsAccountId = foodicsAccountId,
            FromDate = fromDate,
            ToDate = toDate,
            TotalDeltas = deltas.Count,
            SuccessfulSyncs = deltas.Count(d => d.SyncStatus == MenuDeltaSyncStatus.Completed),
            FailedSyncs = deltas.Count(d => d.SyncStatus == MenuDeltaSyncStatus.Failed),
            PendingSyncs = deltas.Count(d => d.SyncStatus == MenuDeltaSyncStatus.Pending),
            TotalDataSynced = deltas.Sum(d => d.CompressedDeltaPayload?.Length ?? 0)
        };

        if (deltas.Any(d => d.TalabatSyncedAt.HasValue))
        {
            var syncTimes = deltas
                .Where(d => d.TalabatSyncedAt.HasValue && d.CreationTime != default)
                .Select(d => (d.TalabatSyncedAt!.Value - d.CreationTime).TotalSeconds)
                .ToList();
            
            if (syncTimes.Any())
            {
                report.AverageSyncTime = syncTimes.Average();
            }
        }

        // Change type breakdown
        report.ChangeTypeBreakdown = deltas
            .GroupBy(d => d.DeltaType)
            .ToDictionary(g => g.Key, g => g.Count());

        return report;
    }

    public async Task<DeltaCleanupResult> CleanupOldDeltasAsync(
        int retentionDays = 30,
        CancellationToken cancellationToken = default)
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-retentionDays);
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var query = await _deltaRepository.GetQueryableAsync();
            
            var oldDeltas = await query
                .Where(d => d.CreationTime < cutoffDate)
                .Where(d => d.SyncStatus == MenuDeltaSyncStatus.Completed || 
                           d.SyncStatus == MenuDeltaSyncStatus.Failed)
                .ToListAsync(cancellationToken);

            var freedBytes = oldDeltas.Sum(d => d.CompressedDeltaPayload?.Length ?? 0);
            
            await _deltaRepository.DeleteManyAsync(oldDeltas, autoSave: true, cancellationToken: cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Cleaned up {Count} old deltas, freed {Size}KB in {Time}ms",
                oldDeltas.Count, freedBytes / 1024, stopwatch.ElapsedMilliseconds);

            return new DeltaCleanupResult
            {
                DeletedDeltas = oldDeltas.Count,
                FreedStorageBytes = freedBytes,
                CleanupTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old deltas");
            
            return new DeltaCleanupResult
            {
                CleanupTime = stopwatch.Elapsed,
                Errors = { ex.Message }
            };
        }
    }

    public async Task<ParallelSyncResult> ExecuteHighPerformanceBatchSyncAsync(
        List<Guid> deltaIds,
        string talabatVendorCode,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            _logger.LogInformation(
                "Starting high-performance batch sync. Deltas={Count}, VendorCode={VendorCode}",
                deltaIds.Count, talabatVendorCode);

            // Load and optimize payloads in parallel
            var payloadTasks = deltaIds.Select(async deltaId =>
            {
                var payload = await GetDeltaPayloadAsync(deltaId, cancellationToken);
                if (payload == null)
                {
                    _logger.LogWarning("Delta payload not found for DeltaId: {DeltaId}", deltaId);
                    return null;
                }

                // Optimize payload for better performance
                var optimizedPayload = await _performanceOptimizer.OptimizePayloadAsync(payload, cancellationToken);
                return new { DeltaId = deltaId, Original = payload, Optimized = optimizedPayload };
            });

            var payloadResults = await Task.WhenAll(payloadTasks);
            var validPayloads = payloadResults.Where(p => p != null).ToList();

            if (!validPayloads.Any())
            {
                return new ParallelSyncResult
                {
                    TotalPayloads = deltaIds.Count,
                    FailedSyncs = deltaIds.Count,
                    ErrorMessage = "No valid payloads found"
                };
            }

            // Execute parallel sync with performance optimization
            var syncFunction = async (MenuDeltaPayload payload, string vendorCode, CancellationToken ct) =>
            {
                // Find the delta ID for this payload
                var deltaInfo = validPayloads.FirstOrDefault(p => p.Original.Metadata.DeltaId == payload.Metadata.DeltaId);
                if (deltaInfo == null)
                {
                    return new DeltaSyncResult { Success = false, ErrorMessage = "Delta not found" };
                }

                return await SyncDeltaToTalabatAsync(deltaInfo.DeltaId, vendorCode, ct);
            };

            var originalPayloads = validPayloads.Select(p => p.Original).ToList();
            var result = await _performanceOptimizer.ExecuteParallelBatchSyncAsync(
                originalPayloads, talabatVendorCode, syncFunction, cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "High-performance batch sync completed. Success={Success}, Failed={Failed}, Throughput={Throughput:F1}/sec, Time={Time}ms",
                result.SuccessfulSyncs, result.FailedSyncs, result.OverallThroughput, stopwatch.ElapsedMilliseconds);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "High-performance batch sync failed");
            return new ParallelSyncResult
            {
                TotalPayloads = deltaIds.Count,
                FailedSyncs = deltaIds.Count,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<OptimizedMenuDeltaPayload> OptimizeDeltaPayloadAsync(
        Guid deltaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Optimizing delta payload. DeltaId={DeltaId}", deltaId);

            var payload = await GetDeltaPayloadAsync(deltaId, cancellationToken);
            if (payload == null)
            {
                throw new InvalidOperationException($"Delta payload not found for DeltaId: {deltaId}");
            }

            var optimizedPayload = await _performanceOptimizer.OptimizePayloadAsync(payload, cancellationToken);

            _logger.LogInformation(
                "Delta payload optimized. DeltaId={DeltaId}, CompressionRatio={Ratio:F2}, Time={Time}ms",
                deltaId, optimizedPayload.OptimizationStats.CompressionRatio, 
                optimizedPayload.OptimizationStats.OptimizationTime.TotalMilliseconds);

            return optimizedPayload;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to optimize delta payload. DeltaId={DeltaId}", deltaId);
            throw;
        }
    }

    public async Task<MenuSyncPerformanceRecommendations> GetPerformanceRecommendationsAsync(
        Guid foodicsAccountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Generating performance recommendations. AccountId={AccountId}", foodicsAccountId);

            // Analyze recent sync performance
            var recentDeltas = await GetRecentDeltasForAnalysisAsync(foodicsAccountId, cancellationToken);
            var statistics = await GetDeltaStatisticsAsync(
                foodicsAccountId, 
                DateTime.UtcNow.AddDays(-7), 
                DateTime.UtcNow, 
                cancellationToken);

            var recommendations = new MenuSyncPerformanceRecommendations
            {
                FoodicsAccountId = foodicsAccountId,
                GeneratedAt = DateTime.UtcNow
            };

            // Analyze payload sizes
            var avgPayloadSize = recentDeltas.Any() 
                ? recentDeltas.Average(d => d.CompressedDeltaPayload?.Length ?? 0)
                : 0;

            // Recommend concurrency based on success rate and volume
            if (statistics.SuccessfulSyncs > 100 && statistics.AverageSyncTime < 30)
            {
                recommendations.RecommendedConcurrency = 6;
                recommendations.Recommendations.Add("High success rate - increase concurrency for better throughput");
            }
            else if (statistics.FailedSyncs > statistics.SuccessfulSyncs)
            {
                recommendations.RecommendedConcurrency = 2;
                recommendations.Recommendations.Add("High failure rate - reduce concurrency to improve stability");
            }
            else
            {
                recommendations.RecommendedConcurrency = 4;
            }

            // Recommend batch size based on payload size
            if (avgPayloadSize > 1024 * 1024) // > 1MB
            {
                recommendations.RecommendedBatchSize = 5;
                recommendations.Recommendations.Add("Large payloads detected - use smaller batches");
            }
            else if (avgPayloadSize < 100 * 1024) // < 100KB
            {
                recommendations.RecommendedBatchSize = 20;
                recommendations.Recommendations.Add("Small payloads detected - use larger batches for efficiency");
            }
            else
            {
                recommendations.RecommendedBatchSize = 10;
            }

            // Recommend batching strategy
            var hasMultipleCategories = recentDeltas.Any(d => 
                !string.IsNullOrEmpty(d.DeltaSummaryJson) && 
                d.DeltaSummaryJson.Contains("Categories"));

            recommendations.RecommendedStrategy = hasMultipleCategories 
                ? BatchingStrategy.CategoryGrouped 
                : BatchingStrategy.Balanced;

            // Cache TTL recommendations
            if (statistics.AverageSyncTime > 60)
            {
                recommendations.RecommendedCacheTtl = TimeSpan.FromMinutes(30);
                recommendations.Recommendations.Add("Slow sync times - increase cache TTL to reduce API calls");
            }
            else
            {
                recommendations.RecommendedCacheTtl = TimeSpan.FromMinutes(15);
            }

            // Additional recommendations
            if (avgPayloadSize > 500 * 1024)
            {
                recommendations.Recommendations.Add("Enable payload compression to reduce network overhead");
            }

            if (statistics.TotalDeltas > 50)
            {
                recommendations.Recommendations.Add("Consider implementing smart caching for frequently accessed data");
            }

            _logger.LogInformation(
                "Performance recommendations generated. AccountId={AccountId}, Concurrency={Concurrency}, BatchSize={BatchSize}",
                foodicsAccountId, recommendations.RecommendedConcurrency, recommendations.RecommendedBatchSize);

            return recommendations;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate performance recommendations. AccountId={AccountId}", foodicsAccountId);
            throw;
        }
    }

    private async Task<List<MenuDelta>> GetRecentDeltasForAnalysisAsync(
        Guid foodicsAccountId,
        CancellationToken cancellationToken)
    {
        var query = await _deltaRepository.GetQueryableAsync();
        
        return await query
            .Where(d => d.FoodicsAccountId == foodicsAccountId)
            .Where(d => d.CreationTime >= DateTime.UtcNow.AddDays(-7))
            .OrderByDescending(d => d.CreationTime)
            .Take(100)
            .ToListAsync(cancellationToken);
    }

    #region Private Methods

    /// <summary>
    /// Filters products by Menu Group categories
    /// Returns only products that belong to categories assigned to the specified Menu Group
    /// </summary>
    private async Task<List<FoodicsProductDetailDto>> FilterProductsByMenuGroupAsync(
        Guid menuGroupId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken)
    {
        // Get active category IDs for the Menu Group
        var categoryQueryable = await _menuGroupCategoryRepository.GetQueryableAsync();
        var activeCategoryIds = await categoryQueryable
            .Where(mgc => mgc.MenuGroupId == menuGroupId && mgc.IsActive)
            .Select(mgc => mgc.CategoryId)
            .ToListAsync(cancellationToken);

        if (!activeCategoryIds.Any())
        {
            _logger.LogWarning("Menu Group {MenuGroupId} has no active categories", menuGroupId);
            return new List<FoodicsProductDetailDto>();
        }

        // Filter products by category membership
        var filteredProducts = products
            .Where(p => p.Category != null && activeCategoryIds.Contains(p.Category.Id))
            .ToList();

        _logger.LogDebug(
            "Filtered products by Menu Group {MenuGroupId}. Categories={Categories}, Products={Products}",
            menuGroupId, string.Join(",", activeCategoryIds), filteredProducts.Count);

        return filteredProducts;
    }

    #endregion

    #region Existing Private Methods

    private async Task<MenuDeltaPayload> GenerateDeltaPayloadAsync(
        MenuChangeDetectionResult changeDetection,
        MenuSnapshot newSnapshot,
        List<FoodicsProductDetailDto> currentProducts,
        CancellationToken cancellationToken)
    {
        var payload = new MenuDeltaPayload
        {
            Metadata = new DeltaMetadata
            {
                DeltaId = Guid.NewGuid(),
                FoodicsAccountId = newSnapshot.FoodicsAccountId,
                BranchId = newSnapshot.BranchId,
                SourceVersion = changeDetection.PreviousVersion,
                TargetVersion = newSnapshot.Version,
                DeltaType = changeDetection.IsFirstSync ? MenuDeltaType.FirstSync : MenuDeltaType.Incremental,
                GeneratedAt = DateTime.UtcNow
            }
        };

        if (changeDetection.IsFirstSync)
        {
            // First sync - all items are "added"
            payload.AddedProducts = currentProducts.Select(p => new ProductDeltaItem
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price ?? 0m,
                IsActive = p.IsActive ?? true,
                Description = p.Description,
                CategoryId = p.Category?.Id,
                ModifierIds = p.Modifiers?.Select(m => m.Id).ToList(),
                Operation = "Add"
            }).ToList();

            // Add all categories
            payload.Categories = currentProducts
                .Where(p => p.Category != null)
                .Select(p => p.Category!)
                .DistinctBy(c => c.Id)
                .Select(c => new CategoryDeltaItem
                {
                    Id = c.Id,
                    Name = c.Name,
                    IsActive = true,
                    Operation = "Add"
                }).ToList();

            // Add all modifiers
            payload.Modifiers = currentProducts
                .Where(p => p.Modifiers != null)
                .SelectMany(p => p.Modifiers!)
                .DistinctBy(m => m.Id)
                .Select(m => new ModifierDeltaItem
                {
                    Id = m.Id,
                    Name = m.Name,
                    IsRequired = m.MinAllowed.HasValue && m.MinAllowed.Value > 0,
                    MinSelection = m.MinAllowed ?? 0,
                    MaxSelection = m.MaxAllowed ?? 1,
                    Options = m.Options?.Select(o => new ModifierOptionDeltaItem
                    {
                        Id = o.Id,
                        Name = o.Name,
                        Price = o.Price ?? 0m,
                        IsActive = o.Branches != null && o.Branches.Any(),
                        Operation = "Add"
                    }).ToList() ?? new(),
                    Operation = "Add"
                }).ToList();
        }
        else
        {
            // Incremental sync - analyze changes
            var previousProducts = await GetPreviousProductsAsync(changeDetection.LatestSnapshot!, cancellationToken);
            await PopulateIncrementalChangesAsync(payload, currentProducts, previousProducts);
        }

        // Calculate statistics
        payload.Metadata.Statistics = new DeltaStatistics
        {
            AddedProducts = payload.AddedProducts.Count,
            UpdatedProducts = payload.UpdatedProducts.Count,
            RemovedProducts = payload.RemovedProductIds.Count,
            AddedCategories = payload.Categories.Count(c => c.Operation == "Add"),
            UpdatedCategories = payload.Categories.Count(c => c.Operation == "Update"),
            RemovedCategories = payload.RemovedCategoryIds.Count,
            AddedModifiers = payload.Modifiers.Count(m => m.Operation == "Add"),
            UpdatedModifiers = payload.Modifiers.Count(m => m.Operation == "Update"),
            RemovedModifiers = payload.RemovedModifierIds.Count
        };

        payload.Metadata.TotalChanges = payload.Metadata.Statistics.AddedProducts +
                                       payload.Metadata.Statistics.UpdatedProducts +
                                       payload.Metadata.Statistics.RemovedProducts +
                                       payload.Metadata.Statistics.AddedCategories +
                                       payload.Metadata.Statistics.UpdatedCategories +
                                       payload.Metadata.Statistics.RemovedCategories +
                                       payload.Metadata.Statistics.AddedModifiers +
                                       payload.Metadata.Statistics.UpdatedModifiers +
                                       payload.Metadata.Statistics.RemovedModifiers;

        return payload;
    }

    private async Task<MenuDelta> CreateDeltaRecordAsync(
        MenuChangeDetectionResult changeDetection,
        MenuSnapshot newSnapshot,
        MenuDeltaPayload payload,
        CancellationToken cancellationToken)
    {
        var delta = new MenuDelta
        {
            SourceSnapshotId = changeDetection.LatestSnapshot?.Id,
            TargetSnapshotId = newSnapshot.Id,
            FoodicsAccountId = newSnapshot.FoodicsAccountId,
            BranchId = newSnapshot.BranchId,
            MenuGroupId = newSnapshot.MenuGroupId,
            SourceVersion = changeDetection.PreviousVersion,
            TargetVersion = newSnapshot.Version,
            DeltaType = payload.Metadata.DeltaType,
            TotalChanges = payload.Metadata.TotalChanges,
            AddedCount = payload.Metadata.Statistics.AddedProducts + payload.Metadata.Statistics.AddedCategories + payload.Metadata.Statistics.AddedModifiers,
            UpdatedCount = payload.Metadata.Statistics.UpdatedProducts + payload.Metadata.Statistics.UpdatedCategories + payload.Metadata.Statistics.UpdatedModifiers,
            RemovedCount = payload.Metadata.Statistics.RemovedProducts + payload.Metadata.Statistics.RemovedCategories + payload.Metadata.Statistics.RemovedModifiers,
            DeltaSummaryJson = JsonSerializer.Serialize(payload.Metadata.Statistics),
            CompressedDeltaPayload = CompressString(JsonSerializer.Serialize(payload)),
            SyncStatus = MenuDeltaSyncStatus.Pending
        };

        // Update payload metadata with delta ID
        payload.Metadata.DeltaId = delta.Id;
        delta.CompressedDeltaPayload = CompressString(JsonSerializer.Serialize(payload));

        await _deltaRepository.InsertAsync(delta, autoSave: true, cancellationToken: cancellationToken);
        return delta;
    }

    private async Task<List<FoodicsProductDetailDto>> GetPreviousProductsAsync(
        MenuSnapshot previousSnapshot,
        CancellationToken cancellationToken)
    {
        if (previousSnapshot.CompressedSnapshotData == null)
        {
            // Fallback: re-fetch from Foodics API or return empty list
            _logger.LogWarning("Previous snapshot data not available for comparison");
            return new List<FoodicsProductDetailDto>();
        }

        var json = DecompressString(previousSnapshot.CompressedSnapshotData);
        return JsonSerializer.Deserialize<List<FoodicsProductDetailDto>>(json) ?? new();
    }

    private async Task PopulateIncrementalChangesAsync(
        MenuDeltaPayload payload,
        List<FoodicsProductDetailDto> currentProducts,
        List<FoodicsProductDetailDto> previousProducts)
    {
        var previousDict = previousProducts.ToDictionary(p => p.Id);
        var currentDict = currentProducts.ToDictionary(p => p.Id);

        // Find added products
        var addedProducts = currentProducts.Where(p => !previousDict.ContainsKey(p.Id)).ToList();
        payload.AddedProducts = addedProducts.Select(p => new ProductDeltaItem
        {
            Id = p.Id,
            Name = p.Name,
            Price = p.Price ?? 0m,
            IsActive = p.IsActive ?? true,
            Description = p.Description,
            CategoryId = p.Category?.Id,
            ModifierIds = p.Modifiers?.Select(m => m.Id).ToList(),
            Operation = "Add"
        }).ToList();

        // Find removed products
        var removedProducts = previousProducts.Where(p => !currentDict.ContainsKey(p.Id)).ToList();
        payload.RemovedProductIds = removedProducts.Select(p => p.Id).ToList();

        // Find updated products
        var commonProductIds = currentDict.Keys.Intersect(previousDict.Keys).ToList();
        var updatedProducts = new List<ProductDeltaItem>();

        foreach (var productId in commonProductIds)
        {
            var oldProduct = previousDict[productId];
            var newProduct = currentDict[productId];

            var changedFields = new List<string>();
            var previousValues = new Dictionary<string, object>();

            if (oldProduct.Name != newProduct.Name)
            {
                changedFields.Add("name");
                previousValues["name"] = oldProduct.Name;
            }
            if (oldProduct.Price != newProduct.Price)
            {
                changedFields.Add("price");
                previousValues["price"] = oldProduct.Price;
            }
            if (oldProduct.IsActive != newProduct.IsActive)
            {
                changedFields.Add("is_active");
                previousValues["is_active"] = oldProduct.IsActive;
            }
            if (oldProduct.Description != newProduct.Description)
            {
                changedFields.Add("description");
                previousValues["description"] = oldProduct.Description ?? "";
            }
            if (oldProduct.Category?.Id != newProduct.Category?.Id)
            {
                changedFields.Add("category");
                previousValues["category"] = oldProduct.Category?.Id ?? "";
            }

            if (changedFields.Any())
            {
                updatedProducts.Add(new ProductDeltaItem
                {
                    Id = productId,
                    Name = newProduct.Name,
                    Price = newProduct.Price ?? 0m,
                    IsActive = newProduct.IsActive ?? true,
                    Description = newProduct.Description,
                    CategoryId = newProduct.Category?.Id,
                    ModifierIds = newProduct.Modifiers?.Select(m => m.Id).ToList(),
                    Operation = "Update",
                    ChangedFields = changedFields,
                    PreviousValues = previousValues
                });
            }
        }

        payload.UpdatedProducts = updatedProducts;

        // TODO: Implement similar logic for categories and modifiers
        // For now, include all categories and modifiers from changed products
        var allAffectedProducts = payload.AddedProducts.Concat(payload.UpdatedProducts).ToList();
        
        payload.Categories = allAffectedProducts
            .Where(p => !string.IsNullOrEmpty(p.CategoryId))
            .Select(p => currentProducts.First(cp => cp.Id == p.Id).Category!)
            .DistinctBy(c => c.Id)
            .Select(c => new CategoryDeltaItem
            {
                Id = c.Id,
                Name = c.Name,
                IsActive = true,
                Operation = addedProducts.Any(ap => ap.CategoryId == c.Id) ? "Add" : "Update"
            }).ToList();

        payload.Modifiers = allAffectedProducts
            .Where(p => p.ModifierIds != null && p.ModifierIds.Any())
            .SelectMany(p => currentProducts.First(cp => cp.Id == p.Id).Modifiers ?? new())
            .DistinctBy(m => m.Id)
            .Select(m => new ModifierDeltaItem
            {
                Id = m.Id,
                Name = m.Name,
                IsRequired = m.MinAllowed.HasValue && m.MinAllowed.Value > 0,
                MinSelection = m.MinAllowed ?? 0,
                MaxSelection = m.MaxAllowed ?? 1,
                Options = m.Options?.Select(o => new ModifierOptionDeltaItem
                {
                    Id = o.Id,
                    Name = o.Name,
                    Price = o.Price ?? 0m,
                    IsActive = o.Branches != null && o.Branches.Any(),
                    Operation = "Add"
                }).ToList() ?? new(),
                Operation = "Add"
            }).ToList();
    }

    private async Task<DeltaSyncResult> SyncPayloadToTalabatAsync(
        MenuDeltaPayload payload,
        string talabatVendorCode,
        CancellationToken cancellationToken)
    {
        // TODO: Implement actual Talabat API integration
        // This is a placeholder implementation
        
        _logger.LogInformation(
            "Syncing delta payload to Talabat. VendorCode={VendorCode}, TotalChanges={Changes}",
            talabatVendorCode, payload.Metadata.TotalChanges);

        // Simulate API call delay
        await Task.Delay(1000, cancellationToken);

        // Simulate success for now
        return new DeltaSyncResult
        {
            Success = true,
            TalabatImportId = $"IMPORT_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}",
            ProcessedItems = payload.Metadata.TotalChanges,
            FailedItems = 0
        };
    }

    private byte[] CompressString(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        using var memoryStream = new System.IO.MemoryStream();
        using (var gzipStream = new GZipStream(memoryStream, CompressionMode.Compress))
        {
            gzipStream.Write(bytes, 0, bytes.Length);
        }
        return memoryStream.ToArray();
    }

    private string DecompressString(byte[] compressedBytes)
    {
        using var memoryStream = new System.IO.MemoryStream(compressedBytes);
        using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
        using var reader = new System.IO.StreamReader(gzipStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Determines if an exception represents a transient error that should be retried
    /// </summary>
    private bool IsTransientError(Exception exception)
    {
        return exception switch
        {
            HttpRequestException => true,
            TaskCanceledException => true,
            TimeoutException => true,
            System.Net.Sockets.SocketException => true,
            InvalidOperationException ex when ex.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) => true,
            InvalidOperationException ex when ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase) => true,
            InvalidOperationException ex when ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    #endregion
}