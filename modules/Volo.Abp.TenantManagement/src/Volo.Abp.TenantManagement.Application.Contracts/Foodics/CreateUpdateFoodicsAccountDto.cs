using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace Foodics
{
    public class CreateUpdateFoodicsAccountDto : EntityDto
    {
        public string OAuthClientId { get; set; }
        public string OAuthClientSecret { get; set; }
        public string AccessToken { get; set; }
        public string BrandName { get; set; }
    }
}
