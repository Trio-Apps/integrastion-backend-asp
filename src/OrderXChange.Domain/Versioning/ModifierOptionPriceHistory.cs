using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Domain.Versioning;

/// <summary>
/// Tracks price change history for modifier options
/// Provides audit trail for price changes and analysis capabilities
/// </summary>
public class ModifierOptionPriceHistory : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    /// <summary>
    /// Foreign key to ModifierOption
    /// </summary>
    [Required]
    public Guid ModifierOptionId { get; set; }

    /// <summary>
    /// Previous price before the change
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal OldPrice { get; set; }

    /// <summary>
    /// New price after the change
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal NewPrice { get; set; }

    /// <summary>
    /// When the price change occurred
    /// </summary>
    public DateTime ChangedAt { get; set; }

    /// <summary>
    /// Reason for the price change (optional)
    /// </summary>
    [MaxLength(1000)]
    public string? Reason { get; set; }

    /// <summary>
    /// Price change percentage
    /// </summary>
    [Column(TypeName = "decimal(10,4)")]
    public decimal ChangePercentage { get; set; }

    /// <summary>
    /// Price change amount (absolute)
    /// </summary>
    [Column(TypeName = "decimal(18,4)")]
    public decimal ChangeAmount { get; set; }

    /// <summary>
    /// Type of price change
    /// </summary>
    [Required]
    [MaxLength(50)]
    public string ChangeType { get; set; } = PriceChangeType.Manual;

    /// <summary>
    /// Source of the price change (Foodics, Manual, Bulk Update, etc.)
    /// </summary>
    [MaxLength(100)]
    public string? ChangeSource { get; set; }

    /// <summary>
    /// User who initiated the change (if applicable)
    /// </summary>
    [MaxLength(200)]
    public string? ChangedBy { get; set; }

    /// <summary>
    /// Whether this price change was synced to Talabat
    /// </summary>
    public bool IsSyncedToTalabat { get; set; }

    /// <summary>
    /// When this price change was synced to Talabat
    /// </summary>
    public DateTime? SyncedToTalabatAt { get; set; }

    /// <summary>
    /// Tenant ID for multi-tenancy
    /// </summary>
    public Guid? TenantId { get; set; }

    // Navigation properties
    public virtual ModifierOption ModifierOption { get; set; } = null!;

    #region Business Methods

    /// <summary>
    /// Calculates price change metrics
    /// </summary>
    public void CalculateChangeMetrics()
    {
        ChangeAmount = NewPrice - OldPrice;
        
        if (OldPrice != 0)
        {
            ChangePercentage = (ChangeAmount / OldPrice) * 100;
        }
        else
        {
            ChangePercentage = 0; // Avoid division by zero
        }
    }

    /// <summary>
    /// Determines the type of price change
    /// </summary>
    public void DetermineChangeType()
    {
        if (NewPrice > OldPrice)
        {
            ChangeType = PriceChangeType.Increase;
        }
        else if (NewPrice < OldPrice)
        {
            ChangeType = PriceChangeType.Decrease;
        }
        else
        {
            ChangeType = PriceChangeType.NoChange;
        }
    }

    /// <summary>
    /// Marks as synced to Talabat
    /// </summary>
    public void MarkAsSyncedToTalabat()
    {
        IsSyncedToTalabat = true;
        SyncedToTalabatAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Checks if this is a significant price change
    /// </summary>
    public bool IsSignificantChange(decimal thresholdPercentage = 10m)
    {
        return Math.Abs(ChangePercentage) >= thresholdPercentage;
    }

    #endregion
}

/// <summary>
/// Constants for price change types
/// </summary>
public static class PriceChangeType
{
    public const string Increase = "Increase";
    public const string Decrease = "Decrease";
    public const string NoChange = "NoChange";
    public const string Manual = "Manual";
    public const string Automatic = "Automatic";
    public const string BulkUpdate = "BulkUpdate";
}