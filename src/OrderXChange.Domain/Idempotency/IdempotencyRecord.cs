using System;
using Volo.Abp.Domain.Entities;

namespace OrderXChange.Idempotency;

/// <summary>
/// Idempotency tracking entity as per SDD Section 7 - Idempotency Strategy
/// Composite key: AccountId + IdempotencyKey
/// </summary>
public class IdempotencyRecord : Entity
{
    /// <summary>
    /// FoodicsAccount ID for multi-tenant isolation
    /// </summary>
    public Guid AccountId { get; set; }
    
    /// <summary>
    /// Unique idempotency key for the operation
    /// Format depends on operation type (e.g., "menu:hash:sha256")
    /// </summary>
    public string IdempotencyKey { get; set; } = null!;
    
    /// <summary>
    /// When the operation was first seen/started
    /// </summary>
    public DateTime FirstSeenUtc { get; set; }
    
    /// <summary>
    /// When the operation was last processed
    /// </summary>
    public DateTime LastProcessedUtc { get; set; }
    
    /// <summary>
    /// Current status of the operation
    /// </summary>
    public IdempotencyStatus Status { get; set; }
    
    /// <summary>
    /// Hash of the result for duplicate detection
    /// </summary>
    public string? ResultHash { get; set; }
    
    /// <summary>
    /// When this record expires (TTL: 14-30 days as per SDD 7.2)
    /// </summary>
    public DateTime? ExpiresAt { get; set; }
    
    protected IdempotencyRecord()
    {
    }
    
    public IdempotencyRecord(
        Guid accountId,
        string idempotencyKey,
        IdempotencyStatus status = IdempotencyStatus.Started,
        int retentionDays = 30)
    {
        AccountId = accountId;
        IdempotencyKey = idempotencyKey;
        Status = status;
        FirstSeenUtc = DateTime.UtcNow;
        LastProcessedUtc = DateTime.UtcNow;
        ExpiresAt = DateTime.UtcNow.AddDays(retentionDays);
    }
    
    public void MarkSucceeded(string? resultHash = null)
    {
        Status = IdempotencyStatus.Succeeded;
        ResultHash = resultHash;
        LastProcessedUtc = DateTime.UtcNow;
    }
    
    public void MarkFailed()
    {
        Status = IdempotencyStatus.FailedPermanent;
        LastProcessedUtc = DateTime.UtcNow;
    }
    
    public override object[] GetKeys()
    {
        return new object[] { AccountId, IdempotencyKey };
    }
}

/// <summary>
/// Idempotency operation status as per SDD Section 7.3
/// </summary>
public enum IdempotencyStatus
{
    /// <summary>
    /// Operation has started but not yet completed
    /// </summary>
    Started = 1,
    
    /// <summary>
    /// Operation completed successfully
    /// </summary>
    Succeeded = 2,
    
    /// <summary>
    /// Operation failed permanently (non-retryable)
    /// </summary>
    FailedPermanent = 3
}

