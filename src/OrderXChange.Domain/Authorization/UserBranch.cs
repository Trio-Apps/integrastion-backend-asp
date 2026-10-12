using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace OrderXChange.Authorization;

/// <summary>
/// Grants a single user access to a single Foodics branch (branch-scoped authorization).
///
/// A user's accessible branches = the set of <see cref="UserBranch"/> rows for that user.
/// Users with the <c>OrderXChange.Branches.All</c> permission (or the admin role) bypass this
/// scope entirely and see every branch. This is resolved at query time by the current-user
/// branch provider; the rows here only define the RESTRICTED set.
///
/// Replaces the earlier per-role model (<see cref="RoleBranch"/>): access follows the person,
/// not the job title, so two users sharing a role can still cover different branches.
/// </summary>
public class UserBranch : CreationAuditedEntity<Guid>, IMultiTenant
{
    /// <summary>The ABP identity user (AbpUsers.Id) this grant belongs to.</summary>
    public Guid UserId { get; set; }

    /// <summary>The Foodics account the branch belongs to (branches are per Foodics account).</summary>
    public Guid FoodicsAccountId { get; set; }

    /// <summary>The Foodics branch id (GUID string as returned by the Foodics API).</summary>
    public string FoodicsBranchId { get; set; } = string.Empty;

    /// <summary>Denormalized branch display name (best-effort snapshot for the UI).</summary>
    public string? FoodicsBranchName { get; set; }

    public Guid? TenantId { get; set; }

    protected UserBranch()
    {
    }

    public UserBranch(Guid id, Guid userId, Guid foodicsAccountId, string foodicsBranchId, string? foodicsBranchName, Guid? tenantId)
        : base(id)
    {
        UserId = userId;
        FoodicsAccountId = foodicsAccountId;
        FoodicsBranchId = foodicsBranchId;
        FoodicsBranchName = foodicsBranchName;
        TenantId = tenantId;
    }
}
