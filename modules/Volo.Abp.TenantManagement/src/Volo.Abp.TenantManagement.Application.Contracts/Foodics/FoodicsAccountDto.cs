using System;
using System.Collections.Generic;
using System.Text;
using Volo.Abp.Application.Dtos;

namespace Foodics
{
    public class FoodicsAccountDto : FullAuditedEntityDto<Guid>
    {
        public string OAuthClientId { get; set; }
        public string OAuthClientSecret { get; set; }
        public string BrandName { get; set; }
        public string AccessToken { get; set; }
    }
}
