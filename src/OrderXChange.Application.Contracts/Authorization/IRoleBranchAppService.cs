using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.Application.Services;

namespace OrderXChange.Authorization;

/// <summary>
/// Manages which Foodics branches each identity role may access (branch-scoped authorization).
/// Guarded by <c>OrderXChange.Branches.Manage</c>.
/// </summary>
public interface IRoleBranchAppService : IApplicationService
{
    /// <summary>Foodics branches available to pick from, for a given Foodics account.</summary>
    Task<List<FoodicsBranchDto>> GetAvailableBranchesAsync(Guid foodicsAccountId);

    /// <summary>The branches currently granted to a role.</summary>
    Task<List<RoleBranchDto>> GetForRoleAsync(Guid roleId);

    /// <summary>Replace the set of branches granted to a role.</summary>
    Task UpdateForRoleAsync(Guid roleId, UpdateRoleBranchesDto input);
}
