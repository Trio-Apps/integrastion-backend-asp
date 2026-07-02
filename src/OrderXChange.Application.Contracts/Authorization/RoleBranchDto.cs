using System;

namespace OrderXChange.Authorization;

/// <summary>A single Foodics branch granted to a role.</summary>
public class RoleBranchDto
{
    public Guid FoodicsAccountId { get; set; }
    public string FoodicsBranchId { get; set; } = string.Empty;
    public string? FoodicsBranchName { get; set; }
}
