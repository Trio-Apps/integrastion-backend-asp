using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderXChange.Application.Integrations.Foodics;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

[Route("api/dlq-test")]
[AllowAnonymous]
public class DlqTestController : AbpController
{
    private readonly FoodicsAccountTokenService _tokenService;

    public DlqTestController(FoodicsAccountTokenService tokenService)
    {
        _tokenService = tokenService;
    }

    [HttpGet("token/{accountId}")]
    public async Task<IActionResult> GetTokenAsync(Guid accountId)
    {
        try
        {
            var token = await _tokenService.GetAccessTokenWithFallbackAsync(accountId);
            return Ok(new
            {
                success = true,
                accountId = accountId,
                tokenPreview = token.Substring(0, Math.Min(50, token.Length)),
                tokenLength = token.Length,
                message = token == "INVALID_TOKEN_FOR_DLQ_TEST" 
                    ? "✅ Using Account Token (INVALID)" 
                    : "⚠️ Using Config Token or different token"
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                accountId = accountId,
                error = ex.Message
            });
        }
    }
}

