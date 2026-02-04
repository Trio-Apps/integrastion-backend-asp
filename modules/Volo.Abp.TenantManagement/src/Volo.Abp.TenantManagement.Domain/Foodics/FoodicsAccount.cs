using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace Foodics
{
    public class FoodicsAccount : FullAuditedEntity<Guid> , IMultiTenant
    {
        public string OAuthClientId { get; set; }
        public string OAuthClientSecret { get; set; }
        public string BrandName { get; set; }
        public string AccessToken { get; set; }

        public Guid? TenantId { get; set; }
    }
}
