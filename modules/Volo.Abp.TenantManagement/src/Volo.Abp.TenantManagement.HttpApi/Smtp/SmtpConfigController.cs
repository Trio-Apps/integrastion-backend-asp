using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.TenantManagement;

namespace Volo.Abp.TenantManagement.Smtp;

[Controller]
[RemoteService(Name = "SmtpConfig")]
[Area(TenantManagementRemoteServiceConsts.ModuleName)]
[Route("api/smtp-config")]
public class SmtpConfigController : AbpControllerBase, ISmtpConfigAppService
{
    private readonly ISmtpConfigAppService _smtpConfigAppService;

    public SmtpConfigController(ISmtpConfigAppService smtpConfigAppService)
    {
        _smtpConfigAppService = smtpConfigAppService;
    }

    [HttpGet]
    public Task<SmtpConfigDto?> GetAsync()
    {
        return _smtpConfigAppService.GetAsync();
    }

    [HttpPut]
    public Task<SmtpConfigDto> SaveAsync(CreateUpdateSmtpConfigDto input)
    {
        return _smtpConfigAppService.SaveAsync(input);
    }

    [HttpPost("test")]
    public Task<SmtpTestResultDto> TestAsync(CreateUpdateSmtpConfigDto input)
    {
        return _smtpConfigAppService.TestAsync(input);
    }
}
