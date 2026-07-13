using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace OrderXChange.Availability;

public class AvailabilityItemDto
{
    public string FoodicsProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameLocalized { get; set; }
    public string? Sku { get; set; }
    public string? CategoryName { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public bool IsInStock { get; set; }
    /// <summary>"ForADay" or "TillFurtherNotice"; null while in stock.</summary>
    public string? Mode { get; set; }
    public DateTime? RestoreAtUtc { get; set; }
}

public class GetAvailabilityInput : PagedAndSortedResultRequestDto
{
    public string? Search { get; set; }
    public string? VendorCode { get; set; }
}

public class SetAvailabilityInput
{
    public List<string> FoodicsProductIds { get; set; } = new();
    public List<string> VendorCodes { get; set; } = new();
    public bool InStock { get; set; }
    /// <summary>When going out of stock: "ForADay" (auto-restored next day) or "TillFurtherNotice".</summary>
    public string? Mode { get; set; }
}

public interface IAvailabilityAppService : IApplicationService
{
    Task<PagedResultDto<AvailabilityItemDto>> GetItemsAsync(GetAvailabilityInput input);
    Task SetAvailabilityAsync(SetAvailabilityInput input);
}
