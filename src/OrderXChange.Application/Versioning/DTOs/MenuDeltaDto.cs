using System;
using System.Collections.Generic;

namespace OrderXChange.Application.Versioning.DTOs;

/// <summary>
/// Delta sync payload for Talabat API
/// Contains only the changes that need to be synchronized
/// </summary>
public class MenuDeltaPayload
{
    /// <summary>
    /// Delta metadata
    /// </summary>
    public DeltaMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Products that were added
    /// </summary>
    public List<ProductDeltaItem> AddedProducts { get; set; } = new();

    /// <summary>
    /// Products that were updated
    /// </summary>
    public List<ProductDeltaItem> UpdatedProducts { get; set; } = new();

    /// <summary>
    /// Product IDs that were removed
    /// </summary>
    public List<string> RemovedProductIds { get; set; } = new();

    /// <summary>
    /// Product IDs that were soft deleted
    /// </summary>
    public List<ProductDeletionItem> SoftDeletedProducts { get; set; } = new();

    /// <summary>
    /// Categories that were added or updated
    /// </summary>
    public List<CategoryDeltaItem> Categories { get; set; } = new();

    /// <summary>
    /// Category IDs that were removed
    /// </summary>
    public List<string> RemovedCategoryIds { get; set; } = new();

    /// <summary>
    /// Modifiers that were added or updated
    /// </summary>
    public List<ModifierDeltaItem> Modifiers { get; set; } = new();

    /// <summary>
    /// Modifier IDs that were removed
    /// </summary>
    public List<string> RemovedModifierIds { get; set; } = new();

    /// <summary>
    /// Items that were soft deleted (for audit trail)
    /// </summary>
    public List<ProductDeletionItem> SoftDeletedItems { get; set; } = new();
}

/// <summary>
/// Delta metadata information
/// </summary>
public class DeltaMetadata
{
    public Guid DeltaId { get; set; }
    public Guid FoodicsAccountId { get; set; }
    public string? BranchId { get; set; }
    public int? SourceVersion { get; set; }
    public int TargetVersion { get; set; }
    public string DeltaType { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; }
    public int TotalChanges { get; set; }
    public DeltaStatistics Statistics { get; set; } = new();
}

/// <summary>
/// Delta statistics breakdown
/// </summary>
public class DeltaStatistics
{
    public int AddedProducts { get; set; }
    public int UpdatedProducts { get; set; }
    public int RemovedProducts { get; set; }
    public int AddedCategories { get; set; }
    public int UpdatedCategories { get; set; }
    public int RemovedCategories { get; set; }
    public int AddedModifiers { get; set; }
    public int UpdatedModifiers { get; set; }
    public int RemovedModifiers { get; set; }
    public int SoftDeletedItems { get; set; }
}

/// <summary>
/// Product delta item with change information
/// </summary>
public class ProductDeltaItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public string? CategoryId { get; set; }
    public List<string>? ModifierIds { get; set; }
    
    /// <summary>
    /// For updates: indicates which fields changed
    /// </summary>
    public List<string>? ChangedFields { get; set; }
    
    /// <summary>
    /// Change operation: Add, Update, Remove
    /// </summary>
    public string Operation { get; set; } = string.Empty;
    
    /// <summary>
    /// Previous values for updated fields (for audit/rollback)
    /// </summary>
    public Dictionary<string, object>? PreviousValues { get; set; }
}

/// <summary>
/// Category delta item
/// </summary>
public class CategoryDeltaItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Description { get; set; }
    public int SortOrder { get; set; }
    public string Operation { get; set; } = string.Empty;
    public List<string>? ChangedFields { get; set; }
}

/// <summary>
/// Modifier delta item
/// </summary>
public class ModifierDeltaItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsRequired { get; set; }
    public int MinSelection { get; set; }
    public int MaxSelection { get; set; }
    public List<ModifierOptionDeltaItem> Options { get; set; } = new();
    public string Operation { get; set; } = string.Empty;
    public List<string>? ChangedFields { get; set; }
}

/// <summary>
/// Modifier option delta item
/// </summary>
public class ModifierOptionDeltaItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; }
    public string Operation { get; set; } = string.Empty;
}

/// <summary>
/// Delta generation result
/// </summary>
public class DeltaGenerationResult
{
    public bool Success { get; set; }
    public MenuDeltaPayload? Payload { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> Warnings { get; set; } = new();
    public TimeSpan GenerationTime { get; set; }
    public ModifierSyncResult? ModifierSyncResult { get; set; }
}

/// <summary>
/// Delta sync result
/// </summary>
public class DeltaSyncResult
{
    public bool Success { get; set; }
    public string? TalabatImportId { get; set; }
    public string? ErrorMessage { get; set; }
    public List<string> PartialFailures { get; set; } = new();
    public TimeSpan SyncTime { get; set; }
    public int ProcessedItems { get; set; }
    public int FailedItems { get; set; }
}

/// <summary>
/// Product deletion item with metadata
/// </summary>
public class ProductDeletionItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string DeletionReason { get; set; } = string.Empty;
    public DateTime DeletedAt { get; set; }
    public string? DeletedBy { get; set; }
    public bool IsSyncedToTalabat { get; set; }
    public Dictionary<string, object>? EntitySnapshot { get; set; }
}