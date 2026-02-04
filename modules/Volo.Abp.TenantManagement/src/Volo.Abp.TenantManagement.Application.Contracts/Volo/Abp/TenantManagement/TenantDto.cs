using Foodics;
using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace Volo.Abp.TenantManagement;

public class TenantDto : ExtensibleEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Name { get; set; }

    public string ConcurrencyStamp { get; set; }
    public IList<FoodicsAccountDto>? FoodicsAccounts { get; set; }
}
