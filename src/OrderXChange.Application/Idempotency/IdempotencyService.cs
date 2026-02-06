using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OrderXChange.Idempotency;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Uow;

namespace OrderXChange.Application.Idempotency;

/// <summary>
/// Service for managing idempotency checks and tracking
/// Implements SDD Section 7 - Idempotency Strategy
/// </summary>
public class IdempotencyService : ITransientDependency
{
    private readonly IIdempotencyRecordRepository _idempotencyRepository;
    private readonly ILogger<IdempotencyService> _logger;
    private readonly IUnitOfWorkManager _unitOfWorkManager;

    public IdempotencyService(
        IIdempotencyRecordRepository idempotencyRepository,
        ILogger<IdempotencyService> logger,
        IUnitOfWorkManager unitOfWorkManager)
    {
        _idempotencyRepository = idempotencyRepository;
        _logger = logger;
        _unitOfWorkManager = unitOfWorkManager;
    }

    /// <summary>
    /// Check if operation has been processed and mark as started if not
    /// Returns: (canProcess, existingRecord)
    /// Includes retry logic to handle concurrent idempotency record creation/updates
    /// </summary>
    public async Task<(bool CanProcess, IdempotencyRecord? ExistingRecord)> CheckAndMarkStartedAsync(
        Guid accountId,
        string idempotencyKey,
        int retentionDays = 30,
        CancellationToken cancellationToken = default,
        TimeSpan? staleAfter = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        const int maxRetries = 3;
        var retryCount = 0;
        var isMenuKey = IsMenuIdempotencyKey(idempotencyKey);
        
        while (retryCount < maxRetries)
        {
            try
            {
                using var uow = _unitOfWorkManager.Begin(requiresNew: true, isTransactional: true);
                
                var existing = await _idempotencyRepository.FindByKeyAsync(
                    accountId,
                    idempotencyKey,
                    cancellationToken);

                if (existing != null)
                {
                    var isMenuLockKey = IsMenuLockKey(idempotencyKey);

                    // Record exists - check status
                    switch (existing.Status)
                    {
                        case IdempotencyStatus.Succeeded:
                            if (isMenuLockKey)
                            {
                                _logger.LogInformation(
                                    "Idempotency lock: Previous lock already succeeded for AccountId={AccountId}, Key={Key}. Releasing for new run.",
                                    accountId, idempotencyKey);
                                await _idempotencyRepository.DeleteAsync(existing, cancellationToken: cancellationToken);
                                existing = null;
                                break;
                            }

                            _logger.LogInformation(
                                "Idempotency check: Operation already succeeded for AccountId={AccountId}, Key={Key}",
                                accountId, idempotencyKey);
                            return (false, existing);

                        case IdempotencyStatus.Started:
                            if (isMenuLockKey)
                            {
                                if (staleAfter.HasValue)
                                {
                                    var staleCutoff = DateTime.UtcNow.Subtract(staleAfter.Value);
                                    if (existing.LastProcessedUtc <= staleCutoff)
                                    {
                                        _logger.LogWarning(
                                            "Idempotency lock: Stale operation detected for AccountId={AccountId}, Key={Key}. " +
                                            "LastProcessedUtc={LastProcessedUtc}, StaleAfter={StaleAfter}. Releasing lock.",
                                            accountId, idempotencyKey, existing.LastProcessedUtc, staleAfter.Value);

                                        await _idempotencyRepository.DeleteAsync(existing, cancellationToken: cancellationToken);
                                        existing = null;
                                        break;
                                    }
                                }

                                _logger.LogWarning(
                                    "Idempotency lock: Operation already in progress for AccountId={AccountId}, Key={Key}",
                                    accountId, idempotencyKey);
                                throw new BusinessException("OPERATION_IN_PROGRESS")
                                    .WithData("AccountId", accountId)
                                    .WithData("IdempotencyKey", idempotencyKey);
                            }

                            if (staleAfter.HasValue)
                            {
                                var staleCutoff = DateTime.UtcNow.Subtract(staleAfter.Value);
                                if (existing.LastProcessedUtc <= staleCutoff)
                                {
                                    _logger.LogWarning(
                                        "Idempotency check: Stale operation detected for AccountId={AccountId}, Key={Key}. " +
                                        "LastProcessedUtc={LastProcessedUtc}, StaleAfter={StaleAfter}. Taking over.",
                                        accountId, idempotencyKey, existing.LastProcessedUtc, staleAfter.Value);

                                    existing.Status = IdempotencyStatus.Started;
                                    existing.FirstSeenUtc = DateTime.UtcNow;
                                    existing.LastProcessedUtc = DateTime.UtcNow;
                                    existing.ExpiresAt = DateTime.UtcNow.AddDays(retentionDays);

                                    await _idempotencyRepository.UpdateAsync(existing, cancellationToken: cancellationToken);
                                    await uow.CompleteAsync(cancellationToken);

                                    return (true, existing);
                                }
                            }

                            _logger.LogWarning(
                                "Idempotency check: Operation already in progress for AccountId={AccountId}, Key={Key}",
                                accountId, idempotencyKey);
                            throw new BusinessException("OPERATION_IN_PROGRESS")
                                .WithData("AccountId", accountId)
                                .WithData("IdempotencyKey", idempotencyKey);

                        case IdempotencyStatus.FailedPermanent:
                            if (isMenuLockKey)
                            {
                                _logger.LogInformation(
                                    "Idempotency lock: Previous lock failed permanently for AccountId={AccountId}, Key={Key}. Releasing for new run.",
                                    accountId, idempotencyKey);
                                await _idempotencyRepository.DeleteAsync(existing, cancellationToken: cancellationToken);
                                existing = null;
                                break;
                            }

                            _logger.LogWarning(
                                "Idempotency check: Operation previously failed permanently for AccountId={AccountId}, Key={Key}",
                                accountId, idempotencyKey);
                            return (false, existing);
                    }
                }

                if (existing == null)
                {
                    // No existing record - create new one
                    var newRecord = new IdempotencyRecord(
                        accountId,
                        idempotencyKey,
                        IdempotencyStatus.Started,
                        retentionDays);

                    await _idempotencyRepository.InsertAsync(newRecord, cancellationToken: cancellationToken);
                    await uow.CompleteAsync(cancellationToken);

                    if (isMenuKey)
                    {
                        _logger.LogInformation(
                            "Idempotency created: AccountId={AccountId}, Key={Key}, Status={Status}, Caller={Caller} ({CallerFile}:{CallerLine})",
                            accountId, idempotencyKey, newRecord.Status, callerMember, callerFile, callerLine);
                    }

                    _logger.LogInformation(
                        "Idempotency check: Operation marked as started for AccountId={AccountId}, Key={Key}",
                        accountId, idempotencyKey);

                    return (true, newRecord);
                }
            }
            catch (DbUpdateConcurrencyException ex)
            {
                retryCount++;
                
                if (retryCount >= maxRetries)
                {
                    // After max retries, assume another job is processing
                    _logger.LogWarning(
                        ex,
                        "Concurrency exception in idempotency check after {Retries} retries. " +
                        "Assuming another job is processing. AccountId={AccountId}, Key={Key}",
                        maxRetries,
                        accountId,
                        idempotencyKey);
                    
                    // Try one final read to get current state
                    try
                    {
                        var finalCheck = await _idempotencyRepository.FindByKeyAsync(
                            accountId,
                            idempotencyKey,
                            cancellationToken);
                        
                        if (finalCheck?.Status == IdempotencyStatus.Started)
                        {
                            throw new BusinessException("OPERATION_IN_PROGRESS")
                                .WithData("AccountId", accountId)
                                .WithData("IdempotencyKey", idempotencyKey);
                        }
                        
                        return (false, finalCheck);
                    }
                    catch (BusinessException)
                    {
                        throw;
                    }
                    catch
                    {
                        // If we can't read, assume operation cannot proceed
                        return (false, null);
                    }
                }
                
                _logger.LogDebug(
                    "Concurrency conflict in idempotency check (attempt {Attempt}/{Max}). " +
                    "Retrying... AccountId={AccountId}, Key={Key}",
                    retryCount,
                    maxRetries,
                    accountId,
                    idempotencyKey);
                
                // Wait before retry (exponential backoff: 50ms, 100ms, 150ms)
                await Task.Delay(50 * retryCount, cancellationToken);
            }
            catch (DbUpdateException ex) when (IsDuplicateKeyException(ex))
            {
                // Another job created the record first - this is OK!
                _logger.LogDebug(
                    "Duplicate key detected in idempotency check. Another job created the record first. " +
                    "AccountId={AccountId}, Key={Key}. Checking status...",
                    accountId,
                    idempotencyKey);
                
                // Wait a bit for the other transaction to complete
                await Task.Delay(100, cancellationToken);
                
                // Read the existing record
                var existing = await _idempotencyRepository.FindByKeyAsync(
                    accountId,
                    idempotencyKey,
                    cancellationToken);
                
                if (existing?.Status == IdempotencyStatus.Started)
                {
                    throw new BusinessException("OPERATION_IN_PROGRESS")
                        .WithData("AccountId", accountId)
                        .WithData("IdempotencyKey", idempotencyKey);
                }
                
                return (false, existing);
            }
        }
        
        // This shouldn't be reached, but just in case
        _logger.LogWarning(
            "Idempotency check exceeded max retries without resolution. " +
            "Assuming operation cannot proceed. AccountId={AccountId}, Key={Key}",
            accountId,
            idempotencyKey);
        
        return (false, null);
    }
    
    /// <summary>
    /// Check if exception is a duplicate key exception
    /// </summary>
    private bool IsDuplicateKeyException(DbUpdateException ex)
    {
        var message = ex.InnerException?.Message ?? ex.Message;
        
        return message.Contains("Duplicate entry", StringComparison.OrdinalIgnoreCase)
            || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase)
            || message.Contains("UK_", StringComparison.OrdinalIgnoreCase)
            || message.Contains("cannot insert duplicate", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Mark operation as succeeded
    /// </summary>
    public async Task MarkSucceededAsync(
        Guid accountId,
        string idempotencyKey,
        object? result = null,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        string? resultHash = null;
        if (result != null)
        {
            resultHash = ComputeHash(result);
        }

        var record = await _idempotencyRepository.FindByKeyAsync(
            accountId,
            idempotencyKey,
            cancellationToken);

        if (record == null)
        {
            if (IsMenuLockKey(idempotencyKey))
            {
                _logger.LogInformation(
                    "Idempotency lock already released (succeeded). AccountId={AccountId}, Key={Key}",
                    accountId, idempotencyKey);
                return;
            }

            var newRecord = new IdempotencyRecord(
                accountId,
                idempotencyKey,
                IdempotencyStatus.Succeeded,
                retentionDays: 30)
            {
                ResultHash = resultHash
            };
            newRecord.LastProcessedUtc = DateTime.UtcNow;

            await _idempotencyRepository.InsertAsync(newRecord, cancellationToken: cancellationToken);

            if (IsMenuIdempotencyKey(idempotencyKey))
            {
                _logger.LogInformation(
                    "Idempotency created (succeeded): AccountId={AccountId}, Key={Key}, Status={Status}, Caller={Caller} ({CallerFile}:{CallerLine})",
                    accountId, idempotencyKey, newRecord.Status, callerMember, callerFile, callerLine);
            }

            _logger.LogInformation(
                "Idempotency: Operation marked as succeeded for AccountId={AccountId}, Key={Key}",
                accountId, idempotencyKey);
            return;
        }

        if (IsMenuLockKey(idempotencyKey))
        {
            await _idempotencyRepository.DeleteAsync(record, cancellationToken: cancellationToken);
            if (IsMenuIdempotencyKey(idempotencyKey))
            {
                _logger.LogInformation(
                    "Idempotency deleted (menu lock): AccountId={AccountId}, Key={Key}, Caller={Caller} ({CallerFile}:{CallerLine})",
                    accountId, idempotencyKey, callerMember, callerFile, callerLine);
            }
            _logger.LogInformation(
                "Idempotency lock released (succeeded) for AccountId={AccountId}, Key={Key}",
                accountId, idempotencyKey);
            return;
        }

        record.MarkSucceeded(resultHash);
        await _idempotencyRepository.UpdateAsync(record, cancellationToken: cancellationToken);

        if (IsMenuIdempotencyKey(idempotencyKey))
        {
            _logger.LogInformation(
                "Idempotency updated (succeeded): AccountId={AccountId}, Key={Key}, Status={Status}, Caller={Caller} ({CallerFile}:{CallerLine})",
                accountId, idempotencyKey, record.Status, callerMember, callerFile, callerLine);
        }

        _logger.LogInformation(
            "Idempotency: Operation marked as succeeded for AccountId={AccountId}, Key={Key}",
            accountId, idempotencyKey);
    }

    /// <summary>
    /// Mark operation as permanently failed
    /// </summary>
    public async Task MarkFailedAsync(
        Guid accountId,
        string idempotencyKey,
        CancellationToken cancellationToken = default,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFile = null,
        [CallerLineNumber] int callerLine = 0)
    {
        var record = await _idempotencyRepository.FindByKeyAsync(
            accountId,
            idempotencyKey,
            cancellationToken);

        if (record == null)
        {
            if (IsMenuLockKey(idempotencyKey))
            {
                _logger.LogInformation(
                    "Idempotency lock already released (failed). AccountId={AccountId}, Key={Key}",
                    accountId, idempotencyKey);
                return;
            }

            var newRecord = new IdempotencyRecord(
                accountId,
                idempotencyKey,
                IdempotencyStatus.FailedPermanent,
                retentionDays: 30);
            newRecord.LastProcessedUtc = DateTime.UtcNow;

            await _idempotencyRepository.InsertAsync(newRecord, cancellationToken: cancellationToken);

            if (IsMenuIdempotencyKey(idempotencyKey))
            {
                _logger.LogInformation(
                    "Idempotency created (failed): AccountId={AccountId}, Key={Key}, Status={Status}, Caller={Caller} ({CallerFile}:{CallerLine})",
                    accountId, idempotencyKey, newRecord.Status, callerMember, callerFile, callerLine);
            }

            _logger.LogWarning(
                "Idempotency: Operation marked as permanently failed for AccountId={AccountId}, Key={Key}",
                accountId, idempotencyKey);
            return;
        }

        if (IsMenuLockKey(idempotencyKey))
        {
            await _idempotencyRepository.DeleteAsync(record, cancellationToken: cancellationToken);
            if (IsMenuIdempotencyKey(idempotencyKey))
            {
                _logger.LogInformation(
                    "Idempotency deleted (menu lock failed): AccountId={AccountId}, Key={Key}, Caller={Caller} ({CallerFile}:{CallerLine})",
                    accountId, idempotencyKey, callerMember, callerFile, callerLine);
            }
            _logger.LogWarning(
                "Idempotency lock released (failed) for AccountId={AccountId}, Key={Key}",
                accountId, idempotencyKey);
            return;
        }

        record.MarkFailed();
        await _idempotencyRepository.UpdateAsync(record, cancellationToken: cancellationToken);

        if (IsMenuIdempotencyKey(idempotencyKey))
        {
            _logger.LogInformation(
                "Idempotency updated (failed): AccountId={AccountId}, Key={Key}, Status={Status}, Caller={Caller} ({CallerFile}:{CallerLine})",
                accountId, idempotencyKey, record.Status, callerMember, callerFile, callerLine);
        }

        _logger.LogWarning(
            "Idempotency: Operation marked as permanently failed for AccountId={AccountId}, Key={Key}",
            accountId, idempotencyKey);
    }

    private static bool IsMenuLockKey(string idempotencyKey)
    {
        return idempotencyKey.StartsWith("menu:lock:", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsMenuIdempotencyKey(string idempotencyKey)
    {
        return idempotencyKey.StartsWith("menu:lock:", StringComparison.OrdinalIgnoreCase)
               || idempotencyKey.StartsWith("menu:hash:", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generate idempotency key for menu sync operation used as a lightweight
    /// concurrency lock (NOT snapshot based).
    /// This is primarily used for background job scheduling to prevent
    /// multiple jobs for the same account/branch from running in parallel.
    /// </summary>
    public string GenerateMenuSyncKey(Guid accountId, string? branchId, DateTime timestamp)
    {
        // Keep the existing time-bucket based key for job-level locking.
        // Prefix is different from the snapshot-based key to avoid confusion.
        var data = $"{accountId}:{branchId ?? "all"}:{timestamp:yyyyMMddHH}";
        var hash = ComputeHash(data);
        return $"menu:lock:{hash}";
    }

    /// <summary>
    /// Generate idempotency key for a specific menu snapshot according to
    /// SDD Section 7.1:
    /// Key = "menu:hash:" + &lt;sha256(menuSnapshot)&gt;
    /// The snapshot object can be any serializable representation of the
    /// current menu (e.g. list of products with includes).
    /// </summary>
    public string GenerateMenuSnapshotKey(Guid accountId, string? branchId, object snapshot)
    {
        if (snapshot is null)
        {
            throw new ArgumentNullException(nameof(snapshot));
        }

        var data = new
        {
            AccountId = accountId,
            BranchId = branchId ?? "all",
            Snapshot = snapshot
        };

        var hash = ComputeHash(data);
        return $"menu:hash:{hash}";
    }

    /// <summary>
    /// Compute SHA256 hash of data
    /// </summary>
    private string ComputeHash(object data)
    {
        var json = JsonSerializer.Serialize(data);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}

