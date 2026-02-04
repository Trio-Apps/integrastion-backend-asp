using Volo.Abp.Settings;

namespace OrderXChange.Settings;

public class OrderXChangeSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        //Define your own settings here. Example:
        //context.Add(new SettingDefinition(OrderXChangeSettings.MySetting1));
    }
}
