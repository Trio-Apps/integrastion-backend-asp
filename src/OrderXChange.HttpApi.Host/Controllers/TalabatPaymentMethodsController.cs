using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

[Authorize("OrderXChange.Dashboard.Tenant")]
[Route("api/app/talabat-payment-methods")]
public class TalabatPaymentMethodsController : AbpController
{
    private readonly TalabatPaymentMethodSettingsService _talabatPaymentMethodSettingsService;

    public TalabatPaymentMethodsController(TalabatPaymentMethodSettingsService talabatPaymentMethodSettingsService)
    {
        _talabatPaymentMethodSettingsService = talabatPaymentMethodSettingsService;
    }

    [HttpGet]
    public Task<TalabatPaymentMethodSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        return _talabatPaymentMethodSettingsService.GetSettingsAsync(cancellationToken: cancellationToken);
    }

    [HttpPut("active")]
    public Task<TalabatPaymentMethodSettingsDto> UpdateActiveAsync(
        [FromBody] UpdateTalabatActivePaymentMethodInput input,
        CancellationToken cancellationToken = default)
    {
        return _talabatPaymentMethodSettingsService.UpdateActivePaymentMethodAsync(input, cancellationToken);
    }
}
