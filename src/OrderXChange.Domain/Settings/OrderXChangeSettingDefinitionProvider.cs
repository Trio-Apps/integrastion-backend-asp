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

        if (context.GetOrNull(OrderXChangeSettings.AvailabilityDayEndTime) is null)
        {
            context.Add(new SettingDefinition(OrderXChangeSettings.AvailabilityDayEndTime, "05:00"));
        }

        if (context.GetOrNull(OrderXChangeSettings.AvailabilityTimeZone) is null)
        {
            context.Add(new SettingDefinition(OrderXChangeSettings.AvailabilityTimeZone, "Asia/Kuwait"));
        }

        if (context.GetOrNull(OrderXChangeSettings.FailedOrdersReportEmail) is null)
        {
            context.Add(new SettingDefinition(OrderXChangeSettings.FailedOrdersReportEmail));
        }

        if (context.GetOrNull(OrderXChangeSettings.FailedOrdersReportTime) is null)
        {
            context.Add(new SettingDefinition(OrderXChangeSettings.FailedOrdersReportTime, "23:30"));
        }

        if (context.GetOrNull(OrderXChangeSettings.FailedOrdersReportLastSentDate) is null)
        {
            context.Add(new SettingDefinition(OrderXChangeSettings.FailedOrdersReportLastSentDate));
        }
    }
}
