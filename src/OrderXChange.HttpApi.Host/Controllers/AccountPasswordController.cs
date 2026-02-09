using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Identity;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Users;

namespace OrderXChange.HttpApi.Host.Controllers;

[Authorize]
[Route("api/account/password")]
public class AccountPasswordController : AbpControllerBase
{
    private readonly ICurrentUser _currentUser;
    private readonly IdentityUserManager _identityUserManager;

    public AccountPasswordController(
        ICurrentUser currentUser,
        IdentityUserManager identityUserManager)
    {
        _currentUser = currentUser;
        _identityUserManager = identityUserManager;
    }

    [HttpGet("change-required")]
    public virtual async Task<PasswordChangeRequiredDto> GetChangeRequiredAsync()
    {
        if (!_currentUser.IsAuthenticated || !_currentUser.Id.HasValue)
        {
            return new PasswordChangeRequiredDto(false);
        }

        var user = await _identityUserManager.GetByIdAsync(_currentUser.Id.Value);
        return new PasswordChangeRequiredDto(user.ShouldChangePasswordOnNextLogin);
    }

    [HttpPost("change")]
    public virtual async Task ChangeAsync([FromBody] ForcedPasswordChangeInput input)
    {
        if (!_currentUser.Id.HasValue)
        {
            throw new AbpAuthorizationException("Current user is not authenticated.");
        }

        if (!string.Equals(input.NewPassword, input.ConfirmNewPassword, StringComparison.Ordinal))
        {
            throw new UserFriendlyException("New password and confirmation password do not match.");
        }

        var user = await _identityUserManager.GetByIdAsync(_currentUser.Id.Value);
        var changeResult = await _identityUserManager.ChangePasswordAsync(
            user,
            input.CurrentPassword,
            input.NewPassword
        );

        if (!changeResult.Succeeded)
        {
            throw new UserFriendlyException(string.Join("; ", changeResult.Errors.Select(e => e.Description)));
        }

        if (user.ShouldChangePasswordOnNextLogin)
        {
            user.SetShouldChangePasswordOnNextLogin(false);
            var updateResult = await _identityUserManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                throw new UserFriendlyException(string.Join("; ", updateResult.Errors.Select(e => e.Description)));
            }
        }
    }
}

public record PasswordChangeRequiredDto(bool Required);

public class ForcedPasswordChangeInput
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required]
    [MinLength(6)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
