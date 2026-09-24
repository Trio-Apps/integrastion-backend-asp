using System;
using OrderXChange.Application.Contracts.Integrations.Talabat;

namespace OrderXChange.Application.Integrations.Talabat;

/// <summary>
/// Classifies a Talabat delivery order as TGO (talabat GO — a Talabat rider collects
/// the order) or TMP (talabat Marketplace — the vendor delivers it).
/// </summary>
/// <remarks>
/// Talabat sends <c>expeditionType = "delivery"</c> for both models; nothing in the
/// payload names the model. They differ in the delivery block: TGO orders carry a
/// <c>riderPickupTime</c> and no address (the vendor never needs it), TMP orders carry
/// the customer's full address. Non-delivery orders (e.g. "pickup") are returned as-is.
/// </remarks>
public static class TalabatDeliveryModel
{
    public const string Tgo = "TGO";
    public const string Tmp = "TMP";

    public static string? Resolve(TalabatOrderWebhook? webhook)
    {
        var expeditionType = webhook?.ExpeditionType;
        if (!string.Equals(expeditionType, "delivery", StringComparison.OrdinalIgnoreCase))
        {
            return expeditionType;
        }

        var delivery = webhook!.Delivery;
        if (delivery?.RiderPickupTime != null || delivery?.Address == null)
        {
            return Tgo;
        }

        return Tmp;
    }
}
