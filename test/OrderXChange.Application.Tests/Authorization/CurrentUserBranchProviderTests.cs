using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using OrderXChange.Authorization;
using OrderXChange.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;
using Xunit;

namespace OrderXChange.Authorization;

/// <summary>
/// Verifies CurrentUserBranchProvider scope resolution:
///   - admin (Branches.All) → unrestricted
///   - restricted user with 1 branch grant → sees only that branch
///   - restricted user with no grants → fail-closed (sees nothing)
/// </summary>
public class CurrentUserBranchProviderTests
{
    private static readonly Guid UserId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid RoleId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid AccountId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");

    private static CurrentUserBranchProvider Build(
        Guid? currentUserId,
        bool hasBranchesAll,
        List<IdentityRole>? roles = null,
        List<RoleBranch>? roleBranches = null)
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.SetupGet(u => u.Id).Returns(currentUserId);

        var permChecker = new Mock<IPermissionChecker>();
        permChecker
            .Setup(p => p.IsGrantedAsync(OrderXChangePermissions.Branches.All))
            .ReturnsAsync(hasBranchesAll);

        var identityUserRepo = new Mock<IIdentityUserRepository>();
        identityUserRepo
            .Setup(r => r.GetRolesAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles ?? new List<IdentityRole>());

        var roleBranchRepo = new Mock<IRepository<RoleBranch, Guid>>();
        roleBranchRepo
            .Setup(r => r.GetListAsync(
                It.IsAny<Expression<Func<RoleBranch, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(roleBranches ?? new List<RoleBranch>());

        return new CurrentUserBranchProvider(
            currentUser.Object,
            permChecker.Object,
            roleBranchRepo.Object,
            identityUserRepo.Object);
    }

    private static IdentityRole MakeRole(Guid id, string name) => new(id, name);

    // ── Admin / unrestricted scenarios ───────────────────────────────────────

    [Fact]
    public async Task Unauthenticated_context_is_unrestricted()
    {
        var provider = Build(currentUserId: null, hasBranchesAll: false);

        var scope = await provider.GetScopeAsync();

        Assert.True(scope.AllBranches);
    }

    [Fact]
    public async Task Admin_with_Branches_All_permission_is_unrestricted()
    {
        var provider = Build(currentUserId: UserId, hasBranchesAll: true);

        var scope = await provider.GetScopeAsync();

        Assert.True(scope.AllBranches);
        Assert.True(scope.CanAccess("any-branch"));
    }

    // ── Single-branch user ───────────────────────────────────────────────────

    [Fact]
    public async Task User_with_one_branch_grant_sees_only_that_branch()
    {
        const string allowed = "branch-abc";
        var role = MakeRole(RoleId, "branch-manager");
        var grant = new RoleBranch(Guid.NewGuid(), RoleId, AccountId, allowed, "Branch ABC", null);

        var provider = Build(
            currentUserId: UserId,
            hasBranchesAll: false,
            roles: [role],
            roleBranches: [grant]);

        var scope = await provider.GetScopeAsync();

        Assert.False(scope.AllBranches);
        Assert.Single(scope.BranchIds);
        Assert.True(scope.CanAccess(allowed));
        Assert.False(scope.CanAccess("other-branch"));
    }

    // ── Fail-closed scenarios ────────────────────────────────────────────────

    [Fact]
    public async Task User_with_roles_but_no_branch_grants_sees_nothing()
    {
        var role = MakeRole(RoleId, "some-role");

        var provider = Build(
            currentUserId: UserId,
            hasBranchesAll: false,
            roles: [role],
            roleBranches: []); // no RoleBranch rows for this role

        var scope = await provider.GetScopeAsync();

        Assert.False(scope.AllBranches);
        Assert.Empty(scope.BranchIds);
        Assert.False(scope.CanAccess("any-branch"));
    }

    [Fact]
    public async Task User_with_no_roles_sees_nothing()
    {
        var provider = Build(
            currentUserId: UserId,
            hasBranchesAll: false,
            roles: [], // zero roles
            roleBranches: null);

        var scope = await provider.GetScopeAsync();

        Assert.False(scope.AllBranches);
        Assert.Empty(scope.BranchIds);
        Assert.False(scope.CanAccess("any-branch"));
    }

    // ── Multi-role union ─────────────────────────────────────────────────────

    [Fact]
    public async Task User_with_two_roles_sees_union_of_their_branch_grants()
    {
        var role1 = MakeRole(Guid.NewGuid(), "role-1");
        var role2 = MakeRole(Guid.NewGuid(), "role-2");
        const string branch1 = "branch-1";
        const string branch2 = "branch-2";

        var grants = new List<RoleBranch>
        {
            new(Guid.NewGuid(), role1.Id, AccountId, branch1, "Branch 1", null),
            new(Guid.NewGuid(), role2.Id, AccountId, branch2, "Branch 2", null),
        };

        var provider = Build(
            currentUserId: UserId,
            hasBranchesAll: false,
            roles: [role1, role2],
            roleBranches: grants);

        var scope = await provider.GetScopeAsync();

        Assert.False(scope.AllBranches);
        Assert.Equal(2, scope.BranchIds.Count);
        Assert.True(scope.CanAccess(branch1));
        Assert.True(scope.CanAccess(branch2));
        Assert.False(scope.CanAccess("branch-3"));
    }
}
