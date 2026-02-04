using Microsoft.Extensions.Localization;
using OrderXChange.Localization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Ui.Branding;

namespace OrderXChange;

[Dependency(ReplaceServices = true)]
public class OrderXChangeBrandingProvider : DefaultBrandingProvider
{
    private IStringLocalizer<OrderXChangeResource> _localizer;

    public OrderXChangeBrandingProvider(IStringLocalizer<OrderXChangeResource> localizer)
    {
        _localizer = localizer;
    }

    public override string AppName => _localizer["AppName"];
}
