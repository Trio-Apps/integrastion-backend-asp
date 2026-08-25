using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Data;
using Volo.Abp.Identity;

namespace OrderXChange.Controllers;

public class AdminResetPasswordInput
{
    /// <summary>Leave empty to have a temporary password generated.</summary>
    public string? NewPassword { get; set; }
}

public class AdminResetPasswordResultDto
{
    /// <summary>The password to hand over. Only ever returned here, never stored in clear.</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>True when the password was generated rather than supplied by the administrator.</summary>
    public bool Generated { get; set; }
}

/// <summary>
/// Lets an administrator reset another user's password from the Users screen, for people who
/// cannot use the emailed self-service link. The user must change it at their next sign-in.
/// </summary>
[Authorize(IdentityPermissions.Users.Update)]
[Route("api/app/user-password")]
public class UserPasswordResetController : AbpController
{
    private const string ForcePasswordChangeAfterLoginPropertyName = "ForcePasswordChangeAfterLogin";

    private readonly IdentityUserManager _userManager;

    public UserPasswordResetController(IdentityUserManager userManager)
    {
        _userManager = userManager;
    }

    [HttpPost("{id}/reset")]
    public async Task<AdminResetPasswordResultDto> ResetAsync(Guid id, [FromBody] AdminResetPasswordInput input)
    {
        var user = await _userManager.GetByIdAsync(id);

        var generated = string.IsNullOrWhiteSpace(input?.NewPassword);
        var password = generated ? GeneratePassword() : input!.NewPassword!;

        // Reset rather than change: an administrator does not know the current password.
        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, password);

        if (!result.Succeeded)
        {
            throw new UserFriendlyException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        // A password someone else chose must not stay in use.
        user.SetProperty(ForcePasswordChangeAfterLoginPropertyName, true);
        (await _userManager.UpdateAsync(user)).CheckErrors();

        // A reset is also the way out of a lockout.
        (await _userManager.ResetAccessFailedCountAsync(user)).CheckErrors();
        (await _userManager.SetLockoutEndDateAsync(user, null)).CheckErrors();

        return new AdminResetPasswordResultDto { Password = password, Generated = generated };
    }

    /// <summary>Meets the default ABP complexity rules (upper, lower, digit, symbol).</summary>
    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnopqrstuvwxyz";
        const string digits = "23456789";
        const string symbols = "!@#$%*?";
        var all = upper + lower + digits + symbols;

        var chars = new char[12];
        chars[0] = Pick(upper);
        chars[1] = Pick(lower);
        chars[2] = Pick(digits);
        chars[3] = Pick(symbols);
        for (var i = 4; i < chars.Length; i++)
        {
            chars[i] = Pick(all);
        }

        // Shuffle so the guaranteed characters are not always in the same positions.
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var j = System.Security.Cryptography.RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[j]) = (chars[j], chars[i]);
        }

        return new string(chars);

        static char Pick(string set) => set[System.Security.Cryptography.RandomNumberGenerator.GetInt32(set.Length)];
    }
}
