using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Authorization;

/// <summary>
/// Grants an identity role access to a single Foodics branch (branch-scoped authorization).
///
/// A role's accessible branches = the set of <see cref="RoleBranch"/> rows for that role.
/// A user's accessible branches = the union across all roles assigned to the user.
/// Roles/users with the <c>OrderXChange.Branches.All</c> permission (or the admin role)
/// bypass this scope entirely and see every branch. This is resolved at query time by
/// the current-user branch provider; the rows here only define the RESTRICTED set.
/// </summary>
public class RoleBranch : CreationAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>The ABP identity role (AbpRoles.Id) this grant belongs to.</summary>
    public Guid RoleId { get; set; }

    /// <summary>The Foodics account the branch belongs to (branches are per Foodics account).</summary>
    public Guid FoodicsAccountId { get; set; }

    /// <summary>The Foodics branch id (GUID string as returned by the Foodics API).</summary>
    public string FoodicsBranchId { get; set; } = string.Empty;

    /// <summary>Denormalized branch display name (best-effort snapshot for the UI).</summary>
    public string? FoodicsBranchName { get; set; }

    public Guid? TenantId { get; set; }

    protected RoleBranch()
    {
    }

    public RoleBranch(Guid id, Guid roleId, Guid foodicsAccountId, string foodicsBranchId, string? foodicsBranchName, Guid? tenantId)
        : base(id)
    {
        RoleId = roleId;
        FoodicsAccountId = foodicsAccountId;
        FoodicsBranchId = foodicsBranchId;
        FoodicsBranchName = foodicsBranchName;
        TenantId = tenantId;
    }
}
