using Volo.Abp.Settings;

namespace OrderXChange;

public sealed class OrderXChangeSettingDefinitionProvider : SettingDefinitionProvider
{
    public override void Define(ISettingDefinitionContext context)
    {
        AddIfMissing(context, "Abp.Account.EnableLocalLogin", "true");
        AddIfMissing(context, "Abp.Account.IsSelfRegistrationEnabled", "true");
    }

    private static void AddIfMissing(ISettingDefinitionContext context, string name, string defaultValue)
    {
        if (context.GetOrNull(name) is null)
        {
            context.Add(new SettingDefinition(name, defaultValue, isVisibleToClients: true));
        }
    }
}
