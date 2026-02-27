using System.Collections.Generic;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public class TalabatPaymentMethodDto
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? NameLocalized { get; set; }

    public string? Code { get; set; }

    public int? Type { get; set; }

    public bool IsActive { get; set; }
}

public class TalabatPaymentMethodSettingsDto
{
    public string? ActivePaymentMethodId { get; set; }

    public string? ActivePaymentMethodName { get; set; }

    public string? ActivePaymentMethodCode { get; set; }

    public string? Source { get; set; }

    public List<TalabatPaymentMethodDto> PaymentMethods { get; set; } = new();
}

public class UpdateTalabatActivePaymentMethodInput
{
    public string? PaymentMethodId { get; set; }
}
