using System;

namespace OrderXChange.Integrations.Talabat;

public class OrderDispatchEto
{
	public string Schema { get; set; } = "order.dispatch.v1";
	public string CorrelationId { get; set; } = Guid.NewGuid().ToString();
	public Guid AccountId { get; set; }
	public Guid FoodicsAccountId { get; set; }
	public string VendorCode { get; set; } = string.Empty;
	public Guid? TenantId { get; set; }
	public Guid OrderLogId { get; set; }
	public string IdempotencyKey { get; set; } = string.Empty;
	public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
}
