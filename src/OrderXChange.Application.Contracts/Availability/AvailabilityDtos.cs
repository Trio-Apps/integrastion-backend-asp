using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace OrderXChange.Availability;

public class AvailabilityBranchStateDto
{
    public string VendorCode { get; set; } = string.Empty;
    public string BranchName { get; set; } = string.Empty;
    public bool IsInStock { get; set; } = true;
    /// <summary>"ForADay" or "TillFurtherNotice"; null while in stock.</summary>
    public string? Mode { get; set; }
    public DateTime? RestoreAtUtc { get; set; }
}

public class AvailabilityItemDto
{
    public string FoodicsProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? NameLocalized { get; set; }
    public string? Sku { get; set; }
    public string? CategoryName { get; set; }

    /// <summary>Per-branch stock state for this item (branches that carry it).</summary>
    public List<AvailabilityBranchStateDto> Branches { get; set; } = new();

    public int BranchCount { get; set; }
    public int OutOfStockCount { get; set; }
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

public class AvailabilitySettingsDto
{
    /// <summary>Local time of day ("HH:mm") the business day ends / a new day begins.</summary>
    public string DayEndTime { get; set; } = "05:00";

    /// <summary>Timezone id used to interpret <see cref="DayEndTime"/>.</summary>
    public string TimeZone { get; set; } = "Asia/Kuwait";

    /// <summary>
    /// Preview: the UTC instant an item marked "out for a day" right now would be restored at.
    /// </summary>
    public DateTime NextRestoreAtUtc { get; set; }
}

public class UpdateAvailabilitySettingsInput
{
    public string DayEndTime { get; set; } = "05:00";
    public string TimeZone { get; set; } = "Asia/Kuwait";
}

public interface IAvailabilityAppService : IApplicationService
{
    Task<PagedResultDto<AvailabilityItemDto>> GetItemsAsync(GetAvailabilityInput input);
    Task SetAvailabilityAsync(SetAvailabilityInput input);
    Task<AvailabilitySettingsDto> GetSettingsAsync();
    Task<AvailabilitySettingsDto> UpdateSettingsAsync(UpdateAvailabilitySettingsInput input);
}
