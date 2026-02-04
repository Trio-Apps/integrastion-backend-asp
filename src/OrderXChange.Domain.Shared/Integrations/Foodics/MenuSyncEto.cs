using System;

namespace OrderXChange.Integrations.Foodics;

/// <summary>
/// Event Transfer Object for Menu Synchronization via Kafka.
/// As per SDD Section 5.2 Message Envelope.
/// </summary>
[Serializable]
public class MenuSyncEto
{
    /// <summary>
    /// Schema version for message evolution
    /// </summary>
    public string Schema { get; set; } = "menu.sync.v1";
    
    /// <summary>
    /// Correlation ID for distributed tracing (SDD Section 11)
    /// </summary>
    public string CorrelationId { get; set; } = null!;
    
    /// <summary>
    /// FoodicsAccount ID (accountId from SDD Section 4)
    /// </summary>
    public Guid AccountId { get; set; }
    
    /// <summary>
    /// Optional specific FoodicsAccount to sync (null = auto-select by job logic)
    /// </summary>
    public Guid? FoodicsAccountId { get; set; }
    
    /// <summary>
    /// Optional Foodics Branch ID filter
    /// </summary>
    public string? BranchId { get; set; }
    
    /// <summary>
    /// Tenant context for multi-tenancy (SDD Section 4)
    /// </summary>
    public Guid? TenantId { get; set; }
    
    /// <summary>
    /// Idempotency key (SDD Section 7.1)
    /// Format: "menu:hash:" + sha256(accountId + branchId + timestamp)
    /// </summary>
    public string IdempotencyKey { get; set; } = null!;
    
    /// <summary>
    /// When the event occurred
    /// </summary>
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}

