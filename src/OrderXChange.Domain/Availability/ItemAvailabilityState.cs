using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Availability;

/// <summary>
/// Manual in/out-of-stock state for a Foodics product on a Talabat vendor (branch).
/// The absence of a row means the item is in stock (the default). A row with
/// <see cref="IsInStock"/> = false marks it out of stock, optionally for a single day
/// (auto-restored at <see cref="RestoreAtUtc"/>) or until further notice.
/// </summary>
public class ItemAvailabilityState : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public Guid? TenantId { get; set; }

    public Guid FoodicsAccountId { get; set; }

    [Required]
    [MaxLength(100)]
    public string FoodicsProductId { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string VendorCode { get; set; } = string.Empty;

    public bool IsInStock { get; set; } = true;

    /// <summary>"ForADay" or "TillFurtherNotice"; null while in stock.</summary>
    [MaxLength(30)]
    public string? Mode { get; set; }

    /// <summary>When a "ForADay" item should be auto-restored to in stock (UTC).</summary>
    public DateTime? RestoreAtUtc { get; set; }
}

public static class AvailabilityMode
{
    public const string ForADay = "ForADay";
    public const string TillFurtherNotice = "TillFurtherNotice";
}
