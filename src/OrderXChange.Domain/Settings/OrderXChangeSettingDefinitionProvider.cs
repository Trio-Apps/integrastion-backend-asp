using Volo.Abp.Settings;

namespace OrderXChange.Settings;

public class OrderXChangeSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        if (context.GetOrNull(OrderXChangeSettings.TalabatActivePaymentMethodId) is null)
        {
            context.Add(new SettingDefinition(OrderXChangeSettings.TalabatActivePaymentMethodId));
        }

        if (context.GetOrNull(OrderXChangeSettings.TalabatActiveDeliveryChargeId) is null)
        {
            context.Add(new SettingDefinition(OrderXChangeSettings.TalabatActiveDeliveryChargeId));
        }
    }
}
