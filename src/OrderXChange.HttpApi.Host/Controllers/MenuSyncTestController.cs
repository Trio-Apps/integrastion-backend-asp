using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderXChange.BackgroundJobs;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

/// <summary>
/// Test controller for directly triggering menu sync job (bypasses Kafka)
/// </summary>
[Route("api/test/menu-sync")]
[AllowAnonymous] // For testing only
public class MenuSyncTestController : AbpController
{
    private readonly MenuSyncRecurringJob _menuSyncJob;

    public MenuSyncTestController(MenuSyncRecurringJob menuSyncJob)
    {
        _menuSyncJob = menuSyncJob;
    }

    /// <summary>
    /// Directly execute menu sync job to fetch from Foodics and save to staging
    /// Bypasses Kafka consumer (useful when Kafka consumer is not running)
    /// </summary>
    [HttpPost("execute-direct")]
    public async Task<IActionResult> ExecuteDirectAsync(
        [FromQuery] Guid? foodicsAccountId = null,
        [FromQuery] string? branchId = null,
        [FromQuery] bool skipIdempotency = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _menuSyncJob.ExecuteAsync(
                foodicsAccountId, 
                branchId, 
                skipInternalIdempotency: skipIdempotency, 
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = "Menu sync executed successfully",
                foodicsAccountId,
                branchId,
                skipIdempotency,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                message = "Menu sync failed",
                error = ex.Message,
                stackTrace = ex.ToString(),
                timestamp = DateTime.UtcNow
            });
        }
    }
}

