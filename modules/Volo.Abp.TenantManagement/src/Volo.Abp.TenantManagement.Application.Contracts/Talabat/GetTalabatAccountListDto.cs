using Volo.Abp.Application.Dtos;

namespace Volo.Abp.TenantManagement.Talabat
{
    public class GetTalabatAccountListDto : PagedAndSortedResultRequestDto
    {
        public string? Filter { get; set; }
    }
}
