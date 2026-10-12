using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using OrderXChange.Permissions;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Users;

namespace OrderXChange.Authorization;

public class CurrentUserBranchProvider : ICurrentUserBranchProvider, ITransientDependency
{
    private readonly ICurrentUser _currentUser;
    private readonly IPermissionChecker _permissionChecker;
    private readonly IRepository<UserBranch, Guid> _userBranchRepository;

    public CurrentUserBranchProvider(
        ICurrentUser currentUser,
        IPermissionChecker permissionChecker,
        IRepository<UserBranch, Guid> userBranchRepository)
    {
        _currentUser = currentUser;
        _permissionChecker = permissionChecker;
        _userBranchRepository = userBranchRepository;
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

        // Otherwise: the branches granted to this user. Branches are assigned to the person
        // rather than to their role, so two users sharing a role can cover different branches.
        // Fail-closed: a user with no grants sees nothing.
        var rows = await _userBranchRepository.GetListAsync(x => x.UserId == _currentUser.Id.Value);
        var branchIds = rows
            .Select(x => x.FoodicsBranchId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return UserBranchScope.Restricted(branchIds);
    }
}
