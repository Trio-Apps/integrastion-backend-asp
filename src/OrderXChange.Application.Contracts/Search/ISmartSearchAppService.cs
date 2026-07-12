using System.Threading.Tasks;
using Volo.Abp.Application.Services;

namespace OrderXChange.Search;

/// <summary>
/// Unified "Smart Search" (BRS §4): one keyword / partial-text query fanned out across orders,
/// catalog items &amp; modifiers, and branches. Results are grouped, branch-scoped (§1) and
/// permission-trimmed (Orders / Availability).
/// </summary>
public interface ISmartSearchAppService : IApplicationService
{
    Task<SmartSearchResultDto> SearchAsync(SmartSearchInput input);
}
