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
/// Reads/writes the role → Foodics branch grants that drive branch-scoped authorization.
/// Available branches are sourced live from Foodics (reusing the menu-sync branch lookup).
/// </summary>
[Authorize(IdentityPermissions.Roles.Update)]
public class RoleBranchAppService : OrderXChangeAppService, IRoleBranchAppService
{
    private readonly IRepository<RoleBranch, Guid> _roleBranchRepository;
    private readonly IMenuSyncAppService _menuSyncAppService;

    public RoleBranchAppService(
        IRepository<RoleBranch, Guid> roleBranchRepository,
        IMenuSyncAppService menuSyncAppService)
    {
        _roleBranchRepository = roleBranchRepository;
        _menuSyncAppService = menuSyncAppService;
    }

    public Task<List<FoodicsBranchDto>> GetAvailableBranchesAsync(Guid foodicsAccountId)
    {
        return _menuSyncAppService.GetBranchesForAccountAsync(foodicsAccountId);
    }

    public async Task<List<RoleBranchDto>> GetForRoleAsync(Guid roleId)
    {
        var rows = await _roleBranchRepository.GetListAsync(x => x.RoleId == roleId);
        return rows
            .Select(x => new RoleBranchDto
            {
                FoodicsAccountId = x.FoodicsAccountId,
                FoodicsBranchId = x.FoodicsBranchId,
                FoodicsBranchName = x.FoodicsBranchName
            })
            .ToList();
    }

    public async Task UpdateForRoleAsync(Guid roleId, UpdateRoleBranchesDto input)
    {
        // Replace-set semantics: clear the role's current grants, then insert the provided set.
        await _roleBranchRepository.DeleteAsync(x => x.RoleId == roleId, autoSave: true);

        var distinct = (input.Branches ?? new List<RoleBranchDto>())
            .Where(b => !string.IsNullOrWhiteSpace(b.FoodicsBranchId))
            .GroupBy(b => new { b.FoodicsAccountId, BranchId = b.FoodicsBranchId.Trim() })
            .Select(g => g.First());

        foreach (var b in distinct)
        {
            await _roleBranchRepository.InsertAsync(new RoleBranch(
                GuidGenerator.Create(),
                roleId,
                b.FoodicsAccountId,
                b.FoodicsBranchId.Trim(),
                b.FoodicsBranchName,
                CurrentTenant.Id));
        }
    }
}
