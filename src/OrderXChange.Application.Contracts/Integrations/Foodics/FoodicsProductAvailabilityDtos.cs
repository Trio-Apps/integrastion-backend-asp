using System.Collections.Generic;

namespace OrderXChange.Application.Integrations.Foodics;

public class ProductAvailabilitySyncResultDto
{
    public int TotalProducts { get; set; }
    public int TotalBranches { get; set; }
    public int AvailableProducts { get; set; }
    public int UnavailableProducts { get; set; }
    public List<TalabatProductAvailabilityDto> Products { get; set; } = [];
}

public class TalabatProductAvailabilityDto
{
    public string ProductId { get; set; } = string.Empty;
    public string? ProductName { get; set; }
    public string? ProductSku { get; set; }
    public string BranchId { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string? BranchReference { get; set; }
    public decimal? Price { get; set; }
    public bool IsAvailable { get; set; }
    public bool IsActive { get; set; }
    public List<string> PriceTagIds { get; set; } = [];
    public List<string> PriceTagNames { get; set; } = [];
}

