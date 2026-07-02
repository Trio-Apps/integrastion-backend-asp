using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderXChange.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace OrderXChange.Authorization;

public class CurrentUserBranchProvider : ICurrentUserBranchProvider, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IRepository<RoleBranch, Guid> _roleBranchRepository;
    private readonly IIdentityUserRepository _identityUserRepository;

    public CurrentUserBranchProvider(
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IRepository<RoleBranch, Guid> roleBranchRepository,
        IIdentityUserRepository identityUserRepository)
    {
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
        _roleBranchRepository = roleBranchRepository;
        _identityUserRepository = identityUserRepository;
    }

    public async Task<UserBranchScope> GetScopeAsync()
    {
        // Unauthenticated / background (system) context is unrestricted.
        if (_currentUser.Id == null)
        {
            return UserBranchScope.All();
        }

        // Explicit "all branches" grant (e.g. admin roles) bypasses branch scoping.
        if (await _permissionChecker.IsGrantedAsync(OrderXChangePermissions.Branches.All))
        {
            return UserBranchScope.All();
        }

        // Otherwise: the union of branches granted to the user's roles. Fail-closed:
        // a user with no matching grants sees nothing.
        var roles = await _identityUserRepository.GetRolesAsync(_currentUser.Id.Value);
        var roleIds = roles.Select(r => r.Id).ToList();
        if (roleIds.Count == 0)
        {
            return UserBranchScope.Restricted(new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        }

        var rows = await _roleBranchRepository.GetListAsync(x => roleIds.Contains(x.RoleId));
        var branchIds = rows
            .Select(x => x.FoodicsBranchId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return UserBranchScope.Restricted(branchIds);
    }
}
