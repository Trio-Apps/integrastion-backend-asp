using System;

namespace OrderXChange.BackgroundJobs;

public class GetStagingMenuGroupSummaryRequest
{
    /// <summary>
    /// Optional FoodicsAccount ID. If null, uses current tenant's FoodicsAccount (first match).
    /// </summary>
    public Guid? FoodicsAccountId { get; set; }

    /// <summary>
    /// Optional branch ID. When provided, products will be filtered to those available in that branch.
    /// </summary>
    public string? BranchId { get; set; }
}

