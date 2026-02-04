using System;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace OrderXChange.Idempotency;

/// <summary>
/// Repository for managing idempotency records
/// </summary>
public interface IIdempotencyRecordRepository : IRepository<IdempotencyRecord>
{
    /// <summary>
    /// Get an idempotency record by account and key
    /// </summary>
    Task<IdempotencyRecord?> FindByKeyAsync(
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Check if an idempotency key exists
    /// </summary>
    Task<bool> ExistsAsync(
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Delete expired idempotency records (cleanup job)
    /// </summary>
    Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default);
}

