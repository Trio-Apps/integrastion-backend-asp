using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderXChange.EntityFrameworkCore;
using OrderXChange.Idempotency;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EntityFrameworkCore;

namespace OrderXChange.Controllers;

/// <summary>
/// Controller for managing idempotency records (testing only)
/// </summary>
[Route("api/test/idempotency")]
[AllowAnonymous] // For testing only - remove in production
public class IdempotencyTestController : AbpController
{
    private readonly IRepository<IdempotencyRecord> _idempotencyRepository;
    private readonly IDbContextProvider<OrderXChangeDbContext> _dbContextProvider;

    public IdempotencyTestController(
        IRepository<IdempotencyRecord> idempotencyRepository,
        IDbContextProvider<OrderXChangeDbContext> dbContextProvider)
    {
        _idempotencyRepository = idempotencyRepository;
        _dbContextProvider = dbContextProvider;
    }

    /// <summary>
    /// Get all idempotency records
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListAsync(CancellationToken cancellationToken = default)
    {
        var records = await _idempotencyRepository.GetListAsync(cancellationToken: cancellationToken);
        
        return Ok(new
        {
            totalRecords = records.Count,
            records = records.Select(r => new
            {
                accountId = r.AccountId,
                key = r.IdempotencyKey,
                status = r.Status.ToString(),
                firstSeen = r.FirstSeenUtc,
                lastProcessed = r.LastProcessedUtc,
                expiresAt = r.ExpiresAt
            }).ToList(),
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Delete all idempotency records (for testing)
    /// </summary>
    [HttpDelete("clear-all")]
    public async Task<IActionResult> ClearAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var dbContext = await _dbContextProvider.GetDbContextAsync();
            var deletedCount = await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM AppIdempotencyRecords",
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = "All idempotency records deleted",
                deletedCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                message = "Failed to delete idempotency records",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Delete idempotency records for a specific account
    /// </summary>
    [HttpDelete("clear/{accountId}")]
    public async Task<IActionResult> ClearByAccountAsync(
        Guid accountId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var dbContext = await _dbContextProvider.GetDbContextAsync();
            var deletedCount = await dbContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM AppIdempotencyRecords WHERE AccountId = {0}",
                accountId,
                cancellationToken);

            return Ok(new
            {
                success = true,
                message = $"Idempotency records deleted for account {accountId}",
                accountId,
                deletedCount,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return Ok(new
            {
                success = false,
                message = "Failed to delete idempotency records",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }
}

