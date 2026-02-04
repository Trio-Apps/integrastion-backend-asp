using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service for managing menu versioning and change detection.
/// Implements efficient hash-based comparison to avoid unnecessary full re-syncs.
/// </summary>
public class MenuVersioningService : ITransientDependency
{
    private readonly IRepository<MenuSnapshot, Guid> _snapshotRepository;
    private readonly IRepository<MenuChangeLog, Guid> _changeLogRepository;
    private readonly ILogger<MenuVersioningService> _logger;

    public MenuVersioningService(
        IRepository<MenuSnapshot, Guid> snapshotRepository,
        IRepository<MenuChangeLog, Guid> changeLogRepository,
        ILogger<MenuVersioningService> logger)
    {
        _snapshotRepository = snapshotRepository;
        _changeLogRepository = changeLogRepository;
        _logger = logger;
    }

    /// <summary>
    /// Checks if menu has changed by comparing hash with latest snapshot.
    /// Returns true if menu changed or no previous snapshot exists.
    /// </summary>
    public async Task<MenuChangeDetectionResult> DetectChangesAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> currentProducts,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Detecting menu changes. FoodicsAccountId={AccountId}, BranchId={BranchId}, MenuGroupId={MenuGroupId}, ProductsCount={Count}",
            foodicsAccountId,
            branchId ?? "ALL",
            menuGroupId?.ToString() ?? "ALL",
            currentProducts.Count);

        // Calculate hash of current menu
        var currentHash = CalculateMenuHash(currentProducts);

        // Get latest snapshot for this account/branch/menu group
        var latestSnapshot = await GetLatestSnapshotAsync(
            foodicsAccountId,
            branchId,
            menuGroupId,
            cancellationToken);

        if (latestSnapshot == null)
        {
            _logger.LogInformation(
                "No previous snapshot found. This is the first sync. Hash={Hash}",
                currentHash);

            return new MenuChangeDetectionResult
            {
                HasChanged = true,
                IsFirstSync = true,
                CurrentHash = currentHash,
                PreviousHash = null,
                PreviousVersion = null,
                ChangeType = MenuChangeDetectionType.FirstSync
            };
        }

        // Compare hashes
        var hasChanged = !string.Equals(currentHash, latestSnapshot.SnapshotHash, StringComparison.Ordinal);

        if (!hasChanged)
        {
            _logger.LogInformation(
                "Menu has NOT changed. Hash={Hash}, Version={Version}",
                currentHash,
                latestSnapshot.Version);

            return new MenuChangeDetectionResult
            {
                HasChanged = false,
                IsFirstSync = false,
                CurrentHash = currentHash,
                PreviousHash = latestSnapshot.SnapshotHash,
                PreviousVersion = latestSnapshot.Version,
                LatestSnapshot = latestSnapshot,
                ChangeType = MenuChangeDetectionType.NoChange
            };
        }

        _logger.LogInformation(
            "Menu HAS changed. OldHash={OldHash}, NewHash={NewHash}, OldVersion={Version}",
            latestSnapshot.SnapshotHash,
            currentHash,
            latestSnapshot.Version);

        return new MenuChangeDetectionResult
        {
            HasChanged = true,
            IsFirstSync = false,
            CurrentHash = currentHash,
            PreviousHash = latestSnapshot.SnapshotHash,
            PreviousVersion = latestSnapshot.Version,
            LatestSnapshot = latestSnapshot,
            ChangeType = MenuChangeDetectionType.Changed
        };
    }

    /// <summary>
    /// Creates a new menu snapshot after successful sync.
    /// Optionally stores compressed snapshot data for rollback capability.
    /// </summary>
    public async Task<MenuSnapshot> CreateSnapshotAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> products,
        string snapshotHash,
        int? previousVersion = null,
        Guid? menuGroupId = null,
        bool storeCompressedData = false,
        CancellationToken cancellationToken = default)
    {
        var newVersion = (previousVersion ?? 0) + 1;

        _logger.LogInformation(
            "Creating menu snapshot. FoodicsAccountId={AccountId}, BranchId={BranchId}, MenuGroupId={MenuGroupId}, Version={Version}, Hash={Hash}",
            foodicsAccountId,
            branchId ?? "ALL",
            menuGroupId?.ToString() ?? "ALL",
            newVersion,
            snapshotHash);

        // Count categories and modifiers
        var categoriesCount = products
            .Select(p => p.Category?.Id)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct()
            .Count();

        var modifiersCount = products
            .Where(p => p.Modifiers != null)
            .Sum(p => p.Modifiers!.Count);

        // Create snapshot
        var snapshot = new MenuSnapshot
        {
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            MenuGroupId = menuGroupId,
            Version = newVersion,
            SnapshotHash = snapshotHash,
            ProductsCount = products.Count,
            CategoriesCount = categoriesCount,
            ModifiersCount = modifiersCount,
            SnapshotDate = DateTime.UtcNow,
            IsSyncedToTalabat = false
        };

        // Optionally store compressed snapshot data
        if (storeCompressedData)
        {
            var json = JsonSerializer.Serialize(products);
            snapshot.CompressedSnapshotData = CompressString(json);
            
            _logger.LogDebug(
                "Stored compressed snapshot data. OriginalSize={Original}KB, CompressedSize={Compressed}KB",
                json.Length / 1024,
                snapshot.CompressedSnapshotData.Length / 1024);
        }

        await _snapshotRepository.InsertAsync(snapshot, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Menu snapshot created. SnapshotId={SnapshotId}, Version={Version}",
            snapshot.Id,
            snapshot.Version);

        return snapshot;
    }

    /// <summary>
    /// Analyzes detailed changes between two product lists and creates change logs.
    /// Now includes soft delete detection and processing.
    /// </summary>
    public async Task<List<MenuChangeLog>> AnalyzeAndLogChangesAsync(
        MenuSnapshot newSnapshot,
        List<FoodicsProductDetailDto> currentProducts,
        List<FoodicsProductDetailDto>? previousProducts = null,
        CancellationToken cancellationToken = default)
    {
        if (previousProducts == null || previousProducts.Count == 0)
        {
            _logger.LogInformation(
                "No previous products to compare. Logging all as Added. Count={Count}",
                currentProducts.Count);

            // All products are new
            var addedLogs = currentProducts.Select(p => new MenuChangeLog
            {
                MenuSnapshotId = newSnapshot.Id,
                PreviousVersion = newSnapshot.Version - 1 > 0 ? newSnapshot.Version - 1 : null,
                CurrentVersion = newSnapshot.Version,
                ChangeType = MenuChangeType.Added,
                EntityType = MenuEntityType.Product,
                EntityId = p.Id,
                EntityName = p.Name,
                NewValueJson = JsonSerializer.Serialize(new { p.Name, p.Price, p.IsActive })
            }).ToList();

            await _changeLogRepository.InsertManyAsync(addedLogs, autoSave: true, cancellationToken: cancellationToken);
            return addedLogs;
        }

        var changeLogs = new List<MenuChangeLog>();

        // Create lookup dictionaries
        var previousDict = previousProducts.ToDictionary(p => p.Id);
        var currentDict = currentProducts.ToDictionary(p => p.Id);

        // Find added products
        var addedProducts = currentProducts.Where(p => !previousDict.ContainsKey(p.Id)).ToList();
        foreach (var product in addedProducts)
        {
            changeLogs.Add(new MenuChangeLog
            {
                MenuSnapshotId = newSnapshot.Id,
                PreviousVersion = newSnapshot.Version - 1,
                CurrentVersion = newSnapshot.Version,
                ChangeType = MenuChangeType.Added,
                EntityType = MenuEntityType.Product,
                EntityId = product.Id,
                EntityName = product.Name,
                NewValueJson = JsonSerializer.Serialize(new 
                { 
                    product.Name, 
                    product.Price, 
                    product.IsActive,
                    CategoryId = product.Category?.Id
                })
            });
        }

        // Find soft deleted products (removed from current but existed in previous)
        var softDeletedProducts = previousProducts.Where(p => !currentDict.ContainsKey(p.Id)).ToList();
        foreach (var product in softDeletedProducts)
        {
            changeLogs.Add(new MenuChangeLog
            {
                MenuSnapshotId = newSnapshot.Id,
                PreviousVersion = newSnapshot.Version - 1,
                CurrentVersion = newSnapshot.Version,
                ChangeType = MenuChangeType.SoftDeleted,
                EntityType = MenuEntityType.Product,
                EntityId = product.Id,
                EntityName = product.Name,
                OldValueJson = JsonSerializer.Serialize(new 
                { 
                    product.Name, 
                    product.Price, 
                    product.IsActive,
                    CategoryId = product.Category?.Id,
                    DeletedAt = DateTime.UtcNow
                })
            });
        }

        // Find modified products
        var commonProductIds = currentDict.Keys.Intersect(previousDict.Keys).ToList();
        foreach (var productId in commonProductIds)
        {
            var oldProduct = previousDict[productId];
            var newProduct = currentDict[productId];

            var changedFields = new List<string>();

            if (oldProduct.Name != newProduct.Name) changedFields.Add("name");
            if (oldProduct.Price != newProduct.Price) changedFields.Add("price");
            if (oldProduct.IsActive != newProduct.IsActive) changedFields.Add("is_active");
            if (oldProduct.Description != newProduct.Description) changedFields.Add("description");
            if (oldProduct.Category?.Id != newProduct.Category?.Id) changedFields.Add("category");

            // Check modifiers count change
            var oldModCount = oldProduct.Modifiers?.Count ?? 0;
            var newModCount = newProduct.Modifiers?.Count ?? 0;
            if (oldModCount != newModCount) changedFields.Add("modifiers");

            // Special case: Check if product was restored from soft delete
            if (oldProduct.IsActive == false && newProduct.IsActive == true)
            {
                changeLogs.Add(new MenuChangeLog
                {
                    MenuSnapshotId = newSnapshot.Id,
                    PreviousVersion = newSnapshot.Version - 1,
                    CurrentVersion = newSnapshot.Version,
                    ChangeType = MenuChangeType.Restored,
                    EntityType = MenuEntityType.Product,
                    EntityId = productId,
                    EntityName = newProduct.Name,
                    OldValueJson = JsonSerializer.Serialize(new 
                    { 
                        oldProduct.Name, 
                        oldProduct.Price, 
                        oldProduct.IsActive,
                        CategoryId = oldProduct.Category?.Id
                    }),
                    NewValueJson = JsonSerializer.Serialize(new 
                    { 
                        newProduct.Name, 
                        newProduct.Price, 
                        newProduct.IsActive,
                        CategoryId = newProduct.Category?.Id,
                        RestoredAt = DateTime.UtcNow
                    })
                });
            }
            else if (changedFields.Any())
            {
                changeLogs.Add(new MenuChangeLog
                {
                    MenuSnapshotId = newSnapshot.Id,
                    PreviousVersion = newSnapshot.Version - 1,
                    CurrentVersion = newSnapshot.Version,
                    ChangeType = MenuChangeType.Modified,
                    EntityType = MenuEntityType.Product,
                    EntityId = productId,
                    EntityName = newProduct.Name,
                    ChangedFields = string.Join(",", changedFields),
                    OldValueJson = JsonSerializer.Serialize(new 
                    { 
                        oldProduct.Name, 
                        oldProduct.Price, 
                        oldProduct.IsActive,
                        CategoryId = oldProduct.Category?.Id
                    }),
                    NewValueJson = JsonSerializer.Serialize(new 
                    { 
                        newProduct.Name, 
                        newProduct.Price, 
                        newProduct.IsActive,
                        CategoryId = newProduct.Category?.Id
                    })
                });
            }
        }

        if (changeLogs.Any())
        {
            await _changeLogRepository.InsertManyAsync(changeLogs, autoSave: true, cancellationToken: cancellationToken);
            
            _logger.LogInformation(
                "Logged {Count} changes. Added={Added}, SoftDeleted={SoftDeleted}, Modified={Modified}, Restored={Restored}",
                changeLogs.Count,
                changeLogs.Count(c => c.ChangeType == MenuChangeType.Added),
                changeLogs.Count(c => c.ChangeType == MenuChangeType.SoftDeleted),
                changeLogs.Count(c => c.ChangeType == MenuChangeType.Modified),
                changeLogs.Count(c => c.ChangeType == MenuChangeType.Restored));
        }

        return changeLogs;
    }

    /// <summary>
    /// Marks a snapshot as synced to Talabat.
    /// </summary>
    public async Task MarkSnapshotAsSyncedAsync(
        Guid snapshotId,
        string talabatImportId,
        string talabatVendorCode,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await _snapshotRepository.GetAsync(snapshotId, cancellationToken: cancellationToken);
        
        snapshot.IsSyncedToTalabat = true;
        snapshot.TalabatImportId = talabatImportId;
        snapshot.TalabatVendorCode = talabatVendorCode;
        snapshot.TalabatSyncedAt = DateTime.UtcNow;

        await _snapshotRepository.UpdateAsync(snapshot, autoSave: true, cancellationToken: cancellationToken);

        _logger.LogInformation(
            "Marked snapshot as synced. SnapshotId={SnapshotId}, ImportId={ImportId}",
            snapshotId,
            talabatImportId);
    }

    /// <summary>
    /// Gets the latest snapshot for an account/branch.
    /// </summary>
    public async Task<MenuSnapshot?> GetLatestSnapshotAsync(
        Guid foodicsAccountId,
        string? branchId,
        Guid? menuGroupId = null,
        CancellationToken cancellationToken = default)
    {
        var query = await _snapshotRepository.GetQueryableAsync();

        return await query
            .Where(s => s.FoodicsAccountId == foodicsAccountId)
            .Where(s => s.BranchId == branchId)
            .Where(s => s.MenuGroupId == menuGroupId)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// Gets change logs for a specific snapshot.
    /// </summary>
    public async Task<List<MenuChangeLog>> GetChangeLogsAsync(
        Guid snapshotId,
        CancellationToken cancellationToken = default)
    {
        return await _changeLogRepository.GetListAsync(
            c => c.MenuSnapshotId == snapshotId,
            cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Calculates SHA256 hash of menu structure for change detection.
    /// Hash includes: product IDs, names, prices, active status, categories, modifiers.
    /// </summary>
    private string CalculateMenuHash(List<FoodicsProductDetailDto> products)
    {
        // Sort products by ID for consistent hashing
        var sortedProducts = products.OrderBy(p => p.Id).ToList();

        // Build hash input string
        var sb = new StringBuilder();
        foreach (var product in sortedProducts)
        {
            sb.Append($"{product.Id}|");
            sb.Append($"{product.Name}|");
            sb.Append($"{product.Price}|");
            sb.Append($"{product.IsActive}|");
            sb.Append($"{product.Category?.Id}|");
            
            // Include modifiers in hash
            if (product.Modifiers != null && product.Modifiers.Count > 0)
            {
                var sortedModifiers = product.Modifiers.OrderBy(m => m.Id).ToList();
                foreach (var modifier in sortedModifiers)
                {
                    sb.Append($"M:{modifier.Id}|");
                    if (modifier.Options != null)
                    {
                        var sortedOptions = modifier.Options.OrderBy(o => o.Id).ToList();
                        foreach (var option in sortedOptions)
                        {
                            sb.Append($"O:{option.Id},{option.Price}|");
                        }
                    }
                }
            }
            
            sb.Append(";");
        }

        // Calculate SHA256 hash
        using var sha256 = SHA256.Create();
        var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(sb.ToString()));
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();
    }

    /// <summary>
    /// Compresses a string using GZip.
    /// </summary>
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

    /// <summary>
    /// Decompresses a GZip compressed byte array to string.
    /// </summary>
    private string DecompressString(byte[] compressedBytes)
    {
        using var memoryStream = new System.IO.MemoryStream(compressedBytes);
        using var gzipStream = new GZipStream(memoryStream, CompressionMode.Decompress);
        using var reader = new System.IO.StreamReader(gzipStream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}

/// <summary>
/// Result of menu change detection
/// </summary>
public class MenuChangeDetectionResult
{
    public bool HasChanged { get; set; }
    public bool IsFirstSync { get; set; }
    public string CurrentHash { get; set; } = string.Empty;
    public string? PreviousHash { get; set; }
    public int? PreviousVersion { get; set; }
    public MenuSnapshot? LatestSnapshot { get; set; }
    public MenuChangeDetectionType ChangeType { get; set; }
}

/// <summary>
/// Type of menu change detection
/// </summary>
public enum MenuChangeDetectionType
{
    FirstSync,
    NoChange,
    Changed
}
