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
    }
}
