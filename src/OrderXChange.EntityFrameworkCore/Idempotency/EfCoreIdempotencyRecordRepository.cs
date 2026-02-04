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
            .FirstOrDefaultAsync(
                x => x.AccountId == accountId && x.IdempotencyKey == idempotencyKey,
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

