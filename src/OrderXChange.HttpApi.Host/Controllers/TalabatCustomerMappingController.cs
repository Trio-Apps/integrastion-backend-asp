using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

[Authorize("OrderXChange.Dashboard.Tenant")]
[Route("api/app/talabat-customer-mapping")]
public class TalabatCustomerMappingController : AbpController
{
    private readonly TalabatCustomerMappingService _service;

    public TalabatCustomerMappingController(TalabatCustomerMappingService service)
    {
        _service = service;
    }

    [HttpGet]
    public Task<TalabatCustomerMappingSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        return _service.GetSettingsAsync(cancellationToken);
    }

    [HttpGet("customers")]
    public Task<List<FoodicsCustomerLookupDto>> SearchCustomersAsync(
        [FromQuery] Guid talabatAccountId,
        [FromQuery] string? filter = null,
        CancellationToken cancellationToken = default)
    {
        return _service.SearchCustomersAsync(talabatAccountId, filter, cancellationToken);
    }

    [HttpGet("addresses")]
    public Task<List<FoodicsAddressLookupDto>> GetAddressesAsync(
        [FromQuery] Guid talabatAccountId,
        [FromQuery] string customerId,
        CancellationToken cancellationToken = default)
    {
        return _service.GetAddressesAsync(talabatAccountId, customerId, cancellationToken);
    }

    [HttpPut]
    public Task<TalabatCustomerMappingSettingsDto> UpdateAsync(
        [FromBody] UpdateTalabatDefaultCustomerMappingInput input,
        CancellationToken cancellationToken = default)
    {
        return _service.UpdateAsync(input, cancellationToken);
    }
}
