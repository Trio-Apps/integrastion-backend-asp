using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrderXChange.Application.Versioning.DTOs;
using OrderXChange.Domain.Versioning;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace OrderXChange.Application.Versioning;

/// <summary>
/// Service interface for managing Menu Group to Talabat mappings
/// </summary>
public interface IMenuGroupTalabatMappingService : IApplicationService
{
    /// <summary>
    /// Gets all mappings for a Foodics account
    /// </summary>
    Task<PagedResultDto<MenuGroupTalabatMappingDto>> GetMappingsAsync(
        Guid foodicsAccountId,
        PagedAndSortedResultRequestDto input);

    /// <summary>
    /// Gets a specific mapping by ID
    /// </summary>
    Task<MenuGroupTalabatMappingDto> GetMappingAsync(Guid id);

    /// <summary>
    /// Gets mapping by Menu Group ID
    /// </summary>
    Task<MenuGroupTalabatMappingDto?> GetMappingByMenuGroupAsync(Guid menuGroupId);

    /// <summary>
    /// Gets mappings for a specific Talabat vendor
    /// </summary>
    Task<List<MenuGroupTalabatMappingDto>> GetMappingsByVendorAsync(
        Guid foodicsAccountId,
        string talabatVendorCode);

    /// <summary>
    /// Creates a new Menu Group to Talabat mapping
    /// </summary>
    Task<MenuGroupTalabatMappingDto> CreateMappingAsync(CreateMenuGroupTalabatMappingDto input);

    /// <summary>
    /// Updates an existing mapping
    /// </summary>
    Task<MenuGroupTalabatMappingDto> UpdateMappingAsync(Guid id, UpdateMenuGroupTalabatMappingDto input);

    /// <summary>
    /// Deletes a mapping
    /// </summary>
    Task DeleteMappingAsync(Guid id);

    /// <summary>
    /// Activates a mapping
    /// </summary>
    Task ActivateMappingAsync(Guid id);

    /// <summary>
    /// Deactivates a mapping
    /// </summary>
    Task DeactivateMappingAsync(Guid id);

    /// <summary>
    /// Validates a mapping configuration
    /// </summary>
    Task<MenuMappingValidationResult> ValidateMappingAsync(Guid id);

    /// <summary>
    /// Validates a mapping configuration before creation
    /// </summary>
    Task<MenuMappingValidationResult> ValidateMappingAsync(CreateMenuGroupTalabatMappingDto input);

    /// <summary>
    /// Tests connectivity to Talabat for a mapping
    /// </summary>
    Task<TalabatConnectivityTestResult> TestTalabatConnectivityAsync(Guid id);

    /// <summary>
    /// Synchronizes a Menu Group to Talabat using its mapping
    /// </summary>
    Task<MenuGroupSyncResult> SyncMenuGroupAsync(Guid mappingId, bool forceFull = false);

    /// <summary>
    /// Gets sync history for a mapping
    /// </summary>
    Task<PagedResultDto<MenuGroupSyncHistoryDto>> GetSyncHistoryAsync(
        Guid mappingId,
        PagedAndSortedResultRequestDto input);

    /// <summary>
    /// Gets mapping statistics
    /// </summary>
    Task<MenuGroupMappingStatsDto> GetMappingStatsAsync(Guid id);

    /// <summary>
    /// Bulk creates mappings for multiple Menu Groups
    /// </summary>
    Task<List<MenuGroupTalabatMappingDto>> BulkCreateMappingsAsync(
        List<CreateMenuGroupTalabatMappingDto> inputs);

    /// <summary>
    /// Exports mapping configuration
    /// </summary>
    Task<string> ExportMappingConfigurationAsync(Guid id);

    /// <summary>
    /// Imports mapping configuration
    /// </summary>
    Task<MenuGroupTalabatMappingDto> ImportMappingConfigurationAsync(
        Guid menuGroupId,
        string configurationJson);

    /// <summary>
    /// Clones a mapping to another Menu Group
    /// </summary>
    Task<MenuGroupTalabatMappingDto> CloneMappingAsync(
        Guid sourceMappingId,
        Guid targetMenuGroupId,
        string newTalabatMenuId);

    /// <summary>
    /// Gets available Talabat vendors for mapping
    /// </summary>
    Task<List<TalabatVendorInfoDto>> GetAvailableTalabatVendorsAsync(Guid foodicsAccountId);

    /// <summary>
    /// Generates suggested Talabat menu ID for a Menu Group
    /// </summary>
    Task<string> GenerateSuggestedTalabatMenuIdAsync(Guid menuGroupId, string talabatVendorCode);

    /// <summary>
    /// Previews what would be synced for a mapping
    /// </summary>
    Task<MenuGroupSyncPreviewDto> PreviewSyncAsync(Guid mappingId);
}

/// <summary>
/// Result of Talabat connectivity test
/// </summary>
public class TalabatConnectivityTestResult
{
    public bool IsConnected { get; set; }
    public string? ErrorMessage { get; set; }
    public TimeSpan ResponseTime { get; set; }
    public string? TalabatVersion { get; set; }
    public DateTime TestedAt { get; set; }
}

/// <summary>
/// Result of Menu Group sync operation
/// </summary>
public class MenuGroupSyncResult
{
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsSynced { get; set; }
    public int ItemsSkipped { get; set; }
    public int ItemsFailed { get; set; }
    public TimeSpan Duration { get; set; }
    public string? TalabatImportId { get; set; }
    public DateTime SyncedAt { get; set; }
    public List<string> Warnings { get; set; } = new();
}

/// <summary>
/// Sync history entry
/// </summary>
public class MenuGroupSyncHistoryDto
{
    public Guid Id { get; set; }
    public DateTime SyncedAt { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public int ItemsSynced { get; set; }
    public int ItemsSkipped { get; set; }
    public int ItemsFailed { get; set; }
    public TimeSpan Duration { get; set; }
    public string? TalabatImportId { get; set; }
    public string SyncType { get; set; } = string.Empty;
    public string InitiatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Mapping statistics
/// </summary>
public class MenuGroupMappingStatsDto
{
    public int TotalSyncs { get; set; }
    public int SuccessfulSyncs { get; set; }
    public int FailedSyncs { get; set; }
    public DateTime? LastSuccessfulSync { get; set; }
    public DateTime? LastFailedSync { get; set; }
    public TimeSpan AverageSyncDuration { get; set; }
    public int TotalItemsSynced { get; set; }
    public int ActiveItemCount { get; set; }
    public int CategoryCount { get; set; }
    public double SuccessRate { get; set; }
}

/// <summary>
/// Talabat vendor information
/// </summary>
public class TalabatVendorInfoDto
{
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string ChainCode { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ExistingMappingCount { get; set; }
}

/// <summary>
/// Preview of what would be synced
/// </summary>
public class MenuGroupSyncPreviewDto
{
    public int TotalItems { get; set; }
    public int NewItems { get; set; }
    public int UpdatedItems { get; set; }
    public int UnchangedItems { get; set; }
    public int Categories { get; set; }
    public List<string> CategoryNames { get; set; } = new();
    public List<MenuItemPreviewDto> SampleItems { get; set; } = new();
    public List<string> ValidationWarnings { get; set; } = new();
    public DateTime PreviewGeneratedAt { get; set; }
}

/// <summary>
/// Preview of a menu item
/// </summary>
public class MenuItemPreviewDto
{
    public string FoodicsId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public string SyncAction { get; set; } = string.Empty; // New, Update, Skip
    public List<string> Changes { get; set; } = new();
}