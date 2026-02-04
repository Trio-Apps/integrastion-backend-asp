using OrderXChange.Localization;
using Volo.Abp.AspNetCore.Mvc;

namespace OrderXChange.Controllers;

/* Inherit your controllers from this class.
 */
public abstract class OrderXChangeController : AbpControllerBase
{
    protected OrderXChangeController()
    {
        LocalizationResource = typeof(OrderXChangeResource);
    }
}
