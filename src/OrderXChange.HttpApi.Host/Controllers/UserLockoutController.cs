using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Identity;

namespace OrderXChange.Controllers;

/// <summary>
/// Clears a user's lockout state (resets failed login attempts and unlocks). Backs the
/// "Reset login attempts" action shown next to each user.
/// </summary>
[Authorize(IdentityPermissions.Users.Update)]
[Route("api/app/user-lockout")]
public class UserLockoutController : AbpController
{
    private readonly IdentityUserManager _userManager;

    public UserLockoutController(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    [HttpPost("{id}/reset")]
    public async Task ResetAsync(Guid id)
    {
        var user = await _userManager.GetByIdAsync(id);
        (await _userManager.ResetAccessFailedCountAsync(user)).CheckErrors();
        (await _userManager.SetLockoutEndDateAsync(user, null)).CheckErrors();
    }
}
