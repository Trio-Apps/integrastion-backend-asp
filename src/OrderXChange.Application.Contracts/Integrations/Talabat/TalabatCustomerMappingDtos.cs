using System;
using System.Collections.Generic;

namespace OrderXChange.Application.Contracts.Integrations.Talabat;

public class TalabatCustomerMappingAccountDto
{
    public Guid TalabatAccountId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string VendorCode { get; set; } = string.Empty;
    public Guid? FoodicsAccountId { get; set; }
    public string? FoodicsAccountName { get; set; }
    public string? DefaultCustomerId { get; set; }
    public string? DefaultCustomerName { get; set; }
    public string? DefaultCustomerAddressId { get; set; }
    public string? DefaultCustomerAddressName { get; set; }
}

public class TalabatCustomerMappingSettingsDto
{
    public List<TalabatCustomerMappingAccountDto> Accounts { get; set; } = [];
}

public class FoodicsCustomerLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public int? DialCode { get; set; }
}

public class FoodicsAddressLookupDto
{
    public string Id { get; set; } = string.Empty;
    public string? Name { get; set; }
    public string? Description { get; set; }
}

public class UpdateTalabatDefaultCustomerMappingInput
{
    public Guid TalabatAccountId { get; set; }
    public string? CustomerId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerAddressId { get; set; }
    public string? CustomerAddressName { get; set; }
}
