using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

[Authorize("OrderXChange.Dashboard.Tenant")]
[Route("api/app/talabat-delivery-charges")]
public class TalabatDeliveryChargesController : AbpController
{
    private readonly TalabatDeliveryChargeSettingsService _talabatDeliveryChargeSettingsService;

    public TalabatDeliveryChargesController(TalabatDeliveryChargeSettingsService talabatDeliveryChargeSettingsService)
    {
        _talabatDeliveryChargeSettingsService = talabatDeliveryChargeSettingsService;
    }

    [HttpGet]
    public Task<TalabatDeliveryChargeSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        return _talabatDeliveryChargeSettingsService.GetSettingsAsync(cancellationToken: cancellationToken);
    }

    [HttpPut("active")]
    public Task<TalabatDeliveryChargeSettingsDto> UpdateActiveAsync(
        [FromBody] UpdateTalabatActiveDeliveryChargeInput input,
        CancellationToken cancellationToken = default)
    {
        return _talabatDeliveryChargeSettingsService.UpdateActiveDeliveryChargeAsync(input, cancellationToken);
    }
}
