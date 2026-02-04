using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.Domain.Versioning;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service interface for managing menu item mappings between Foodics and Talabat
/// Provides stable ID-based mapping that survives name changes
/// </summary>
public interface IMenuMappingService
{
    /// <summary>
    /// Gets or creates mapping for a Foodics entity
    /// Ensures stable Talabat remote codes across syncs
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="entityType">Type of entity (Product, Category, etc.)</param>
    /// <param name="foodicsId">Foodics entity ID</param>
    /// <param name="currentName">Current name in Foodics (for reference)</param>
    /// <param name="parentFoodicsId">Parent entity Foodics ID (for hierarchical relationships)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Menu item mapping</returns>
    Task<MenuItemMapping> GetOrCreateMappingAsync(
        Guid foodicsAccountId,
        string? branchId,
        string entityType,
        string foodicsId,
        string? currentName = null,
        string? parentFoodicsId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets mapping by Foodics ID
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="entityType">Entity type</param>
    /// <param name="foodicsId">Foodics entity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Menu item mapping or null if not found</returns>
    Task<MenuItemMapping?> GetMappingByFoodicsIdAsync(
        Guid foodicsAccountId,
        string? branchId,
        string entityType,
        string foodicsId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets mapping by Talabat remote code
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="talabatRemoteCode">Talabat remote code</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Menu item mapping or null if not found</returns>
    Task<MenuItemMapping?> GetMappingByTalabatRemoteCodeAsync(
        Guid foodicsAccountId,
        string? branchId,
        string talabatRemoteCode,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk creates or updates mappings for a list of Foodics products
    /// Optimized for large menu syncs
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="products">Foodics products with full details</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping Foodics ID to MenuItemMapping</returns>
    Task<Dictionary<string, MenuItemMapping>> BulkCreateOrUpdateMappingsAsync(
        Guid foodicsAccountId,
        string? branchId,
        List<FoodicsProductDetailDto> products,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates Talabat internal IDs after successful import
    /// </summary>
    /// <param name="mappingUpdates">Dictionary of TalabatRemoteCode -> TalabatInternalId</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of mappings updated</returns>
    Task<int> UpdateTalabatInternalIdsAsync(
        Dictionary<string, string> mappingUpdates,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deactivates mappings for entities that no longer exist in Foodics
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="currentFoodicsIds">Set of currently existing Foodics IDs</param>
    /// <param name="entityType">Entity type to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of mappings deactivated</returns>
    Task<int> DeactivateObsoleteMappingsAsync(
        Guid foodicsAccountId,
        string? branchId,
        HashSet<string> currentFoodicsIds,
        string entityType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all active mappings for an account/branch
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="entityType">Optional entity type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of active mappings</returns>
    Task<List<MenuItemMapping>> GetActiveMappingsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        string? entityType = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates mapping integrity and fixes inconsistencies
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with fixes applied</returns>
    Task<MappingValidationResult> ValidateAndFixMappingsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cleans up old inactive mappings
    /// </summary>
    /// <param name="retentionDays">Number of days to retain inactive mappings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of mappings cleaned up</returns>
    Task<int> CleanupOldMappingsAsync(
        int retentionDays = 90,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets mapping statistics for monitoring
    /// </summary>
    /// <param name="foodicsAccountId">Foodics account ID</param>
    /// <param name="branchId">Branch ID (null for all branches)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Mapping statistics</returns>
    Task<MappingStatistics> GetMappingStatisticsAsync(
        Guid foodicsAccountId,
        string? branchId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of mapping validation
/// </summary>
public class MappingValidationResult
{
    public bool IsValid { get; set; }
    public int TotalMappings { get; set; }
    public int ValidMappings { get; set; }
    public int FixedMappings { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> FixesApplied { get; set; } = new();
}

/// <summary>
/// Mapping statistics for monitoring
/// </summary>
public class MappingStatistics
{
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public int TotalMappings { get; set; }
    public int ActiveMappings { get; set; }
    public int InactiveMappings { get; set; }
    public Dictionary<string, int> MappingsByEntityType { get; set; } = new();
    public DateTime OldestMapping { get; set; }
    public DateTime NewestMapping { get; set; }
    public double AverageSyncCount { get; set; }
    public int MappingsWithTalabatIds { get; set; }
    public int OrphanedMappings { get; set; }
}