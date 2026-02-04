using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using OrderXChange.BackgroundJobs;
using OrderXChange.Integrations.Foodics;
using System.Text.Json;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

/// <summary>
/// API controller for managing Menu Sync operations
/// </summary>
[Route("api/menu-sync")]
public class MenuSyncController : AbpController
{
    private readonly IMenuSyncAppService _menuSyncAppService;
    private readonly Volo.Abp.EventBus.Distributed.IDistributedEventBus _eventBus;

    public MenuSyncController(
        IMenuSyncAppService menuSyncAppService,
        Volo.Abp.EventBus.Distributed.IDistributedEventBus eventBus)
    {
        _menuSyncAppService = menuSyncAppService;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Manually trigger a menu sync for a specific account
    /// </summary>
    [HttpPost("trigger")]
    public async Task<IActionResult> TriggerMenuSyncAsync(
        [FromQuery] Guid? foodicsAccountId = null,
        [FromQuery] string? branchId = null,
        CancellationToken cancellationToken = default)
    {
        await _menuSyncAppService.TriggerMenuSyncAsync(
            foodicsAccountId,
            branchId,
            cancellationToken);

        return Ok(new
        {
            Message = "Menu sync event published to Kafka successfully",
            FoodicsAccountId = foodicsAccountId,
            BranchId = branchId,
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Get menu sync status information
    /// </summary>
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            Message = "Menu sync is configured and running via Kafka",
            KafkaEnabled = true,
            Topics = new
            {
                Main = "menu.sync",
                Retry = "menu.sync.retry.*",
                DLQ = "menu.sync.dlq"
            }
        });
    }

    /// <summary>
    /// Gets available groups for a specific FoodicsAccount.
    /// Used for dropdown selection when configuring TalabatAccount group filtering.
    /// </summary>
    [HttpGet("groups-for-account")]
    public async Task<IActionResult> GetGroupsForAccountAsync(
        [FromQuery] Guid foodicsAccountId,
        CancellationToken cancellationToken = default)
    {
        var groups = await _menuSyncAppService.GetGroupsForAccountAsync(foodicsAccountId);
        return Ok(groups);
    }

    /// <summary>
    /// Replay a MenuSync message from DLQ by resubmitting the original payload.
    /// This is a simple ops tool; in a real setup the payload would typically
    /// come from a DLQ store rather than manual copy-paste.
    /// </summary>
    [HttpPost("replay")]
    public async Task<IActionResult> ReplayFromDlqAsync([FromBody] MenuSyncReplayInput input)
    {
        if (string.IsNullOrWhiteSpace(input.OriginalMessageJson))
        {
            return BadRequest("OriginalMessageJson is required.");
        }

        MenuSyncEto? original;
        try
        {
            original = JsonSerializer.Deserialize<MenuSyncEto>(input.OriginalMessageJson);
        }
        catch (Exception)
        {
            return BadRequest("Invalid OriginalMessageJson payload.");
        }

        if (original == null)
        {
            return BadRequest("Could not deserialize OriginalMessageJson to MenuSyncEto.");
        }

        // Reset OccurredAt to "now" for observability; IdempotencyKey is reused.
        original.OccurredAt = DateTime.UtcNow;

        await _eventBus.PublishAsync(original);

        return Ok(new
        {
            Message = "Menu sync message replayed from DLQ",
            original.CorrelationId,
            original.AccountId,
            original.BranchId
        });
    }
}

public class MenuSyncReplayInput
{
    /// <summary>
    /// JSON serialized MenuSyncEto (typically taken from MenuSyncFailedEto.OriginalMessage).
    /// </summary>
    public string OriginalMessageJson { get; set; } = string.Empty;
}

