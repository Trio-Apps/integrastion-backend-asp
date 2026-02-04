using OrderXChange.Localization;
using Volo.Abp.Application.Services;

namespace OrderXChange;

/* Inherit your application services from this class.
 */
public abstract class OrderXChangeAppService : ApplicationService
{
    protected OrderXChangeAppService()
    {
        LocalizationResource = typeof(OrderXChangeResource);
    }
}
