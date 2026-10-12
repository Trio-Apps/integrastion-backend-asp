using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using OrderXChange.Application.Integrations.Foodics;
using OrderXChange.BackgroundJobs;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace OrderXChange.Authorization;

/// <summary>
/// Reads/writes the user → Foodics branch grants that drive branch-scoped authorization.
/// Available branches are sourced live from Foodics (reusing the menu-sync branch lookup).
/// </summary>
[Authorize(IdentityPermissions.Users.Update)]
public class UserBranchAppService : OrderXChangeAppService, IUserBranchAppService
{
    private readonly IRepository<UserBranch, Guid> _userBranchRepository;
    private readonly IMenuSyncAppService _menuSyncAppService;

    public UserBranchAppService(
        IRepository<UserBranch, Guid> userBranchRepository,
        IMenuSyncAppService menuSyncAppService)
    {
        _userBranchRepository = userBranchRepository;
        _menuSyncAppService = menuSyncAppService;
    }

    public Task<List<FoodicsBranchDto>> GetAvailableBranchesAsync(Guid foodicsAccountId)
    {
        return _menuSyncAppService.GetBranchesForAccountAsync(foodicsAccountId);
    }

    public async Task<List<UserBranchDto>> GetForUserAsync(Guid userId)
    {
        var rows = await _userBranchRepository.GetListAsync(x => x.UserId == userId);
        return rows
            .Select(x => new UserBranchDto
            {
                FoodicsAccountId = x.FoodicsAccountId,
                FoodicsBranchId = x.FoodicsBranchId,
                FoodicsBranchName = x.FoodicsBranchName
            })
            .ToList();
    }

    public async Task UpdateForUserAsync(Guid userId, UpdateUserBranchesDto input)
    {
        // Replace-set semantics: clear the user's current grants, then insert the provided set.
        await _userBranchRepository.DeleteAsync(x => x.UserId == userId, autoSave: true);

        var distinct = (input.Branches ?? new List<UserBranchDto>())
            .Where(b => !string.IsNullOrWhiteSpace(b.FoodicsBranchId))
            .GroupBy(b => new { b.FoodicsAccountId, BranchId = b.FoodicsBranchId.Trim() })
            .Select(g => g.First());

        foreach (var b in distinct)
        {
            await _userBranchRepository.InsertAsync(new UserBranch(
                GuidGenerator.Create(),
                userId,
                b.FoodicsAccountId,
                b.FoodicsBranchId.Trim(),
                b.FoodicsBranchName,
                CurrentTenant.Id));
        }
    }
}
