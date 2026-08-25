using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.Application.Services;

namespace OrderXChange.Authorization;

/// <summary>A single Foodics branch granted to a user.</summary>
public class UserBranchDto
{
    public Guid FoodicsAccountId { get; set; }
    public string FoodicsBranchId { get; set; } = string.Empty;
    public string? FoodicsBranchName { get; set; }
}

/// <summary>Replace-set of the branches a user may access.</summary>
public class UpdateUserBranchesDto
{
    public List<UserBranchDto> Branches { get; set; } = new();
}

/// <summary>
/// Manages which Foodics branches each user may access (branch-scoped authorization).
/// Guarded by <c>AbpIdentity.Users.Update</c> — whoever may edit a user may set their branches.
/// </summary>
public interface IUserBranchAppService : IApplicationService
{
    /// <summary>Foodics branches available to pick from, for a given Foodics account.</summary>
    Task<List<FoodicsBranchDto>> GetAvailableBranchesAsync(Guid foodicsAccountId);

    /// <summary>The branches currently granted to a user.</summary>
    Task<List<UserBranchDto>> GetForUserAsync(Guid userId);

    /// <summary>Replace the set of branches granted to a user.</summary>
    Task UpdateForUserAsync(Guid userId, UpdateUserBranchesDto input);
}
