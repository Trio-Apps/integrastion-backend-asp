using System;
using Volo.Abp.Application.Dtos;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public class GetTalabatOrderLogsInput : PagedAndSortedResultRequestDto
{
    public string? VendorCode { get; set; }
    public string? Status { get; set; }
    public bool? IsTestOrder { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}

public class TalabatOrderLogDto
{
    public Guid Id { get; set; }
    public Guid FoodicsAccountId { get; set; }
    public string? VendorCode { get; set; }
    public string? PlatformRestaurantId { get; set; }
    public string? OrderToken { get; set; }
    public string? OrderCode { get; set; }
    public string? ShortCode { get; set; }
    public string Status { get; set; } = string.Empty;
    public bool IsTestOrder { get; set; }
    public int ProductsCount { get; set; }
    public int CategoriesCount { get; set; }
    public DateTime? OrderCreatedAt { get; set; }
    public DateTime? ReceivedAt { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public int Attempts { get; set; }
    public string? LastError { get; set; }
    public DateTime CreationTime { get; set; }
}
