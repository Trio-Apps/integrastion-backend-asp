using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Permissions;
using OrderXChange.Talabat;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

/// <summary>
/// File-download endpoint for exporting the Talabat orders list to Excel.
/// Kept as an explicit controller (not app-service auto-API) so it can return a
/// binary .xlsx with a proper Content-Disposition instead of a JSON body.
/// </summary>
[Authorize(OrderXChangePermissions.Orders.Default)]
[Route("api/app/talabat-order-log")]
public class TalabatOrderLogExportController : AbpController
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly TalabatOrderLogAppService _appService;

    public TalabatOrderLogExportController(TalabatOrderLogAppService appService)
    {
        _appService = appService;
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportAsync([FromQuery] GetTalabatOrderLogsInput input)
    {
        var bytes = await _appService.ExportToExcelAsync(input);
        var fileName = $"talabat-orders-{DateTime.UtcNow:yyyyMMdd-HHmmss}.xlsx";
        return File(bytes, XlsxContentType, fileName);
    }
}
