using System;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using Xunit;

namespace OrderXChange.Application.Integrations;

/// <summary>
/// Talabat sends expeditionType "delivery" for both delivery models; the model is read
/// from the delivery block (shapes taken from live PICK orders, 2026-09-24).
/// </summary>
public class TalabatDeliveryModelTests
{
    [Fact]
    public void Talabat_rider_order_without_address_is_TGO()
    {
        var webhook = new TalabatOrderWebhook
        {
            ExpeditionType = "delivery",
            Delivery = new TalabatOrderDelivery { RiderPickupTime = DateTime.UtcNow, Address = null }
        };

        Assert.Equal("TGO", TalabatDeliveryModel.Resolve(webhook));
    }

    [Fact]
    public void Vendor_delivery_order_with_address_is_TMP()
    {
        var webhook = new TalabatOrderWebhook
        {
            ExpeditionType = "delivery",
            Delivery = new TalabatOrderDelivery
            {
                RiderPickupTime = null,
                Address = new TalabatOrderAddress { Street = "شارع 20", City = "Ahmadi" }
            }
        };

        Assert.Equal("TMP", TalabatDeliveryModel.Resolve(webhook));
    }

    [Fact]
    public void Delivery_order_with_no_delivery_block_is_TGO()
    {
        var webhook = new TalabatOrderWebhook { ExpeditionType = "Delivery", Delivery = null };

        Assert.Equal("TGO", TalabatDeliveryModel.Resolve(webhook));
    }

    [Theory]
    [InlineData("pickup")]
    [InlineData(null)]
    public void Non_delivery_expedition_type_is_kept_as_sent(string? expeditionType)
    {
        var webhook = new TalabatOrderWebhook
        {
            ExpeditionType = expeditionType,
            Delivery = new TalabatOrderDelivery { Address = new TalabatOrderAddress { City = "Kuwait" } }
        };

        Assert.Equal(expeditionType, TalabatDeliveryModel.Resolve(webhook));
    }

    [Fact]
    public void Missing_webhook_resolves_to_null()
    {
        Assert.Null(TalabatDeliveryModel.Resolve(null));
    }
}
