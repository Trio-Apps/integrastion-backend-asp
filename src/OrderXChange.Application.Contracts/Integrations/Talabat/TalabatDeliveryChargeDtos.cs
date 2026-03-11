using System.Collections.Generic;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public class TalabatDeliveryChargeDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameLocalized { get; set; }

    public int Type { get; set; }

    public bool IsOpenCharge { get; set; }

    public bool IsAutoApplied { get; set; }

    public bool IsCalculatedUsingSubtotal { get; set; }

    public decimal? Value { get; set; }

    public List<int> OrderTypes { get; set; } = new();
}

public class TalabatDeliveryChargeSettingsDto
{
    public string? ActiveDeliveryChargeId { get; set; }

    public string? ActiveDeliveryChargeName { get; set; }

    public string? Source { get; set; }

    public List<TalabatDeliveryChargeDto> Charges { get; set; } = new();
}

public class UpdateTalabatActiveDeliveryChargeInput
{
    public string? DeliveryChargeId { get; set; }
}
