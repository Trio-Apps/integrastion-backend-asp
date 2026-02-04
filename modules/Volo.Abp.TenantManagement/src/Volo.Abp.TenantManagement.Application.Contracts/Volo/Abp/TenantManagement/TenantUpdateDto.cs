using Foodics;
using System.Collections;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace Volo.Abp.TenantManagement;

public class TenantUpdateDto : TenantCreateOrUpdateDtoBase, IHasConcurrencyStamp
{
    public string ConcurrencyStamp { get; set; }
    public IList<FoodicsAccountDto> FoodicsAccounts { get; set; }
}
