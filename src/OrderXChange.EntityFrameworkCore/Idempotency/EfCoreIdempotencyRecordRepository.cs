using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using OrderXChange.EntityFrameworkCore;
using OrderXChange.Idempotency;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace OrderXChange.EntityFrameworkCore.Idempotency;

/// <summary>
/// EF Core implementation of IdempotencyRecord repository
/// </summary>
public class EfCoreIdempotencyRecordRepository
    : EfCoreRepository<OrderXChangeDbContext, IdempotencyRecord>,
      IIdempotencyRecordRepository
{
    public EfCoreIdempotencyRecordRepository(
        IDbContextProvider<OrderXChangeDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<IdempotencyRecord?> FindByKeyAsync(
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey,
                GetCancellationToken(cancellationToken));
    }

    public async Task UpsertAsync(
        IdempotencyRecord record,
        bool updateFirstSeen = false,
        CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();

        var sql = updateFirstSeen
            ? @"INSERT INTO AppIdempotencyRecords
                (AccountId, IdempotencyKey, Status, FirstSeenUtc, LastProcessedUtc, ExpiresAt, ResultHash)
               VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})
               ON DUPLICATE KEY UPDATE
                Status = VALUES(Status),
                FirstSeenUtc = VALUES(FirstSeenUtc),
                LastProcessedUtc = VALUES(LastProcessedUtc),
                ExpiresAt = VALUES(ExpiresAt),
                ResultHash = VALUES(ResultHash);"
            : @"INSERT INTO AppIdempotencyRecords
                (AccountId, IdempotencyKey, Status, FirstSeenUtc, LastProcessedUtc, ExpiresAt, ResultHash)
               VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})
               ON DUPLICATE KEY UPDATE
                Status = VALUES(Status),
                LastProcessedUtc = VALUES(LastProcessedUtc),
                ExpiresAt = VALUES(ExpiresAt),
                ResultHash = VALUES(ResultHash);";

        await dbContext.Database.ExecuteSqlRawAsync(
            sql,
            new object?[]
            {
                record.AccountId,
                record.IdempotencyKey,
                (int)record.Status,
                record.FirstSeenUtc,
                record.LastProcessedUtc,
                record.ExpiresAt,
                record.ResultHash
            },
            GetCancellationToken(cancellationToken));
    }

    public async Task<bool> ExistsAsync(
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .AnyAsync(
                x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey,
                GetCancellationToken(cancellationToken));
    }

    public async Task<int> DeleteExpiredAsync(CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var now = DateTime.UtcNow;
        
        return await dbContext.Database.ExecuteSqlRawAsync(
            "DELETE FROM IdempotencyRecords WHERE ExpiresAt < {0}",
            now,
            GetCancellationToken(cancellationToken));
    }
}

