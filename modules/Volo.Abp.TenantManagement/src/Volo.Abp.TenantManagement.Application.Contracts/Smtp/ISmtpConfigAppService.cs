using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace Volo.Abp.TenantManagement.Smtp;

public interface ISmtpConfigAppService : IApplicationService
{
    Task<SmtpConfigDto?> GetAsync();
    Task<SmtpConfigDto> SaveAsync(CreateUpdateSmtpConfigDto input);
    Task<SmtpTestResultDto> TestAsync(CreateUpdateSmtpConfigDto input);
}
