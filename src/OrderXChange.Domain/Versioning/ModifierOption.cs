using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Represents a modifier option with price tracking and versioning
/// Handles price changes safely and maintains price history
/// </summary>
public class ModifierOption : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to ModifierGroup
    /// </summary>
    [Required]
    public Guid ModifierGroupId { get; set; }

    /// <summary>
    /// Foodics modifier option ID (remote identifier)
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string FoodicsModifierOptionId { get; set; } = string.Empty;

    /// <summary>
    /// Option name
    /// </summary>
    [Required]
    [MaxLength(500)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Localized name (if available)
    /// </summary>
    [MaxLength(500)]
    public string? NameLocalized { get; set; }

    /// <summary>
    /// Current price of the option
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal Price { get; set; }

    /// <summary>
    /// Previous price (for change tracking)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal? PreviousPrice { get; set; }

    /// <summary>
    /// When the price was last changed
    /// </summary>
    public DateTime? PriceChangedAt { get; set; }

    /// <summary>
    /// Current version of this option
    /// Incremented when price or properties change
    /// </summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Whether this option is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Display order within the modifier group
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// Image URL for the option
    /// </summary>
    [MaxLength(1000)]
    public string? ImageUrl { get; set; }

    /// <summary>
    /// When this option was last updated in Foodics
    /// </summary>
    public DateTime LastUpdatedAt { get; set; }

    /// <summary>
    /// Whether this option has been synced to Talabat
    /// </summary>
    public bool IsSyncedToTalabat { get; set; }

    /// <summary>
    /// When this option was last synced to Talabat
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }

    /// <summary>
    /// Hash of the option properties (name + price + active status)
    /// Used for change detection
    /// </summary>
    [Required]
    [MaxLength(64)]
    public string PropertyHash { get; set; } = string.Empty;

    /// <summary>
    /// JSON metadata for additional properties
    /// </summary>
    [Column(TypeName = "TEXT")]
    public string? MetadataJson { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual ModifierGroup ModifierGroup { get; set; } = null!;
    public virtual ICollection<ModifierOptionPriceHistory> PriceHistory { get; set; } = new List<ModifierOptionPriceHistory>();
    public virtual ICollection<ModifierOptionVersion> Versions { get; set; } = new List<ModifierOptionVersion>();

    #region Business Methods

    /// <summary>
    /// Updates the option price safely with history tracking
    /// </summary>
    public void UpdatePrice(decimal newPrice, string? reason = null)
    {
        if (Price != newPrice)
        {
            // Create price history entry
            var priceHistoryEntry = new ModifierOptionPriceHistory
            {
                ModifierOptionId = Id,
                OldPrice = Price,
                NewPrice = newPrice,
                ChangedAt = DateTime.UtcNow,
                Reason = reason,
                TenantId = TenantId
            };

            PriceHistory.Add(priceHistoryEntry);

            // Update current price
            PreviousPrice = Price;
            Price = newPrice;
            PriceChangedAt = DateTime.UtcNow;
            Version++;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false; // Mark as needing sync

            // Update property hash
            UpdatePropertyHash();
        }
    }

    /// <summary>
    /// Updates the option properties
    /// </summary>
    public void UpdateProperties(string name, string? nameLocalized, string? imageUrl, bool isActive, int sortOrder)
    {
        var hasChanges = Name != name || 
                        NameLocalized != nameLocalized || 
                        ImageUrl != imageUrl || 
                        IsActive != isActive || 
                        SortOrder != sortOrder;

        if (hasChanges)
        {
            // Create version snapshot before updating
            CreateVersionSnapshot();

            Name = name;
            NameLocalized = nameLocalized;
            ImageUrl = imageUrl;
            IsActive = isActive;
            SortOrder = sortOrder;
            Version++;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;

            // Update property hash
            UpdatePropertyHash();
        }
    }

    /// <summary>
    /// Activates the option
    /// </summary>
    public void Activate()
    {
        if (!IsActive)
        {
            IsActive = true;
            Version++;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;
            UpdatePropertyHash();
        }
    }

    /// <summary>
    /// Deactivates the option
    /// </summary>
    public void Deactivate()
    {
        if (IsActive)
        {
            IsActive = false;
            Version++;
            LastUpdatedAt = DateTime.UtcNow;
            IsSyncedToTalabat = false;
            UpdatePropertyHash();
        }
    }

    /// <summary>
    /// Marks as synced to Talabat
    /// </summary>
    public void MarkAsSynced()
    {
        IsSyncedToTalabat = true;
        LastSyncedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the price change percentage compared to previous price
    /// </summary>
    public decimal? GetPriceChangePercentage()
    {
        if (!PreviousPrice.HasValue || PreviousPrice.Value == 0)
            return null;

        return ((Price - PreviousPrice.Value) / PreviousPrice.Value) * 100;
    }

    /// <summary>
    /// Checks if the price has changed significantly
    /// </summary>
    public bool HasSignificantPriceChange(decimal thresholdPercentage = 10m)
    {
        var changePercentage = GetPriceChangePercentage();
        return changePercentage.HasValue && Math.Abs(changePercentage.Value) >= thresholdPercentage;
    }

    /// <summary>
    /// Gets the unique context key for this option
    /// </summary>
    public string GetContextKey()
    {
        return $"{ModifierGroup?.GetContextKey()}:{FoodicsModifierOptionId}";
    }

    /// <summary>
    /// Updates the property hash based on current values
    /// </summary>
    private void UpdatePropertyHash()
    {
        var hashInput = $"{Name}|{Price}|{IsActive}|{Version}";
        PropertyHash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(hashInput))
            .Take(32).Aggregate("", (s, b) => s + b.ToString("x2"));
    }

    /// <summary>
    /// Creates a version snapshot before making changes
    /// </summary>
    private void CreateVersionSnapshot()
    {
        var versionSnapshot = new ModifierOptionVersion
        {
            ModifierOptionId = Id,
            Version = Version,
            Name = Name,
            NameLocalized = NameLocalized,
            Price = Price,
            IsActive = IsActive,
            SortOrder = SortOrder,
            ImageUrl = ImageUrl,
            PropertyHash = PropertyHash,
            SnapshotDate = DateTime.UtcNow,
            TenantId = TenantId
        };

        Versions.Add(versionSnapshot);
    }

    #endregion
}