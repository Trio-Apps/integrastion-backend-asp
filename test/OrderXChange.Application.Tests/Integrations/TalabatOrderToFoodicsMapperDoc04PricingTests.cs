using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using Xunit;

namespace OrderXChange.Application.Integrations;

/// <summary>
/// Verifies the order → Foodics dispatch mapping prices correctly under doc-04 payload semantics,
/// where paidPrice / unitPrice / selectedToppings[].price are now ORIGINAL (pre-discount) values
/// and per-line discounts are provided in products[].discounts[] / selectedToppings[].discounts[].
/// Covers all six scenarios listed in docs/phase2/04-item-prices-discounts-mapping.md §List of Test Scenarios.
/// </summary>
public class TalabatOrderToFoodicsMapperDoc04PricingTests
{
    private static TalabatOrderToFoodicsMapper CreateMapper()
    {
        var config = new ConfigurationBuilder().Build();
        return new TalabatOrderToFoodicsMapper(config, NullLogger<TalabatOrderToFoodicsMapper>.Instance);
    }

    private static string ProductRemoteCode(Guid id) => $"P_{id}";
    private static string ToppingRemoteCode(Guid id) => $"O_{id}";

    private static TalabatOrderWebhook MakeWebhook(List<TalabatOrderProduct> products, string grandTotal = "0.00") =>
        new()
        {
            Token = "test-token",
            Code = "test-code",
            CreatedAt = DateTime.UtcNow,
            ExpeditionType = "pickup",
            Price = new TalabatOrderPrice { GrandTotal = grandTotal },
            Products = products
        };

    private static TalabatOrderDiscount Discount(string amount, string name = "Discount") =>
        new() { Name = name, Amount = amount };

    private static TalabatOrderDiscount DiscountWithSponsors(string amount, string name,
        string sponsor1, string amount1, string sponsor2, string amount2) =>
        new()
        {
            Name = name,
            Amount = amount,
            Sponsorships = new List<TalabatOrderDiscountSponsorship>
            {
                new() { Sponsor = sponsor1, Amount = amount1 },
                new() { Sponsor = sponsor2, Amount = amount2 }
            }
        };

    /// <summary>
    /// Scenario 1: Normal item without discounts.
    /// paidPrice and unitPrice are original prices; no discounts present → effective price = original price.
    /// </summary>
    [Fact]
    public void Scenario1_NoDiscount_EffectivePriceEqualsOriginalPrice()
    {
        var pid = Guid.NewGuid();
        var webhook = MakeWebhook(new List<TalabatOrderProduct>
        {
            new()
            {
                RemoteCode = ProductRemoteCode(pid),
                PaidPrice = "14.00",
                UnitPrice = "14.00",
                Quantity = "1"
            }
        }, grandTotal: "14.00");

        var result = CreateMapper().MapToCreateOrder(webhook, "branch-1", "vendor-1");
        var product = result.Products[0];

        Assert.Equal(14.00m, product.TotalPrice);
        Assert.Equal(14.00m, product.UnitPrice);
        Assert.Equal(1, product.Quantity);
    }

    /// <summary>
    /// Scenario 2: Base discount only (30% off, 50/50 PLATFORM / VENDOR sponsorship).
    /// paidPrice is original; effective price = paidPrice - discount.amount.
    /// </summary>
    [Fact]
    public void Scenario2_BaseDiscountOnly_EffectivePriceDeductsDiscount()
    {
        var pid = Guid.NewGuid();
        var webhook = MakeWebhook(new List<TalabatOrderProduct>
        {
            new()
            {
                RemoteCode = ProductRemoteCode(pid),
                PaidPrice = "14.00",
                UnitPrice = "14.00",
                Quantity = "1",
                Discounts = new List<TalabatOrderDiscount>
                {
                    DiscountWithSponsors("4.20", "First Order", "PLATFORM", "2.10", "VENDOR", "2.10")
                }
            }
        }, grandTotal: "9.80");

        var result = CreateMapper().MapToCreateOrder(webhook, "branch-1", "vendor-1");
        var product = result.Products[0];

        Assert.Equal(9.80m, product.TotalPrice);   // 14.00 - 4.20
        Assert.Equal(9.80m, product.UnitPrice);
    }

    /// <summary>
    /// Scenario 3: Base discount + booster (both deducted from the original price).
    /// </summary>
    [Fact]
    public void Scenario3_BaseDiscountPlusBooster_AllDiscountsDeducted()
    {
        var pid = Guid.NewGuid();
        var webhook = MakeWebhook(new List<TalabatOrderProduct>
        {
            new()
            {
                RemoteCode = ProductRemoteCode(pid),
                PaidPrice = "14.00",
                UnitPrice = "14.00",
                Quantity = "1",
                Discounts = new List<TalabatOrderDiscount>
                {
                    Discount("4.20", "Base"),
                    Discount("1.40", "Booster")
                }
            }
        }, grandTotal: "8.40");

        var result = CreateMapper().MapToCreateOrder(webhook, "branch-1", "vendor-1");
        var product = result.Products[0];

        Assert.Equal(8.40m, product.TotalPrice);   // 14.00 - 4.20 - 1.40
        Assert.Equal(8.40m, product.UnitPrice);
    }

    /// <summary>
    /// Scenario 4: Variation item where the base product price is 0 and the discount is on the selected topping/choice.
    /// </summary>
    [Fact]
    public void Scenario4_VariationItem_DiscountOnTopping_ToppingEffectivePriceUsed()
    {
        var pid = Guid.NewGuid();
        var tid = Guid.NewGuid();
        var webhook = MakeWebhook(new List<TalabatOrderProduct>
        {
            new()
            {
                RemoteCode = ProductRemoteCode(pid),
                PaidPrice = "0.00",
                UnitPrice = "0.00",
                Quantity = "1",
                SelectedToppings = new List<TalabatOrderTopping>
                {
                    new()
                    {
                        RemoteCode = ToppingRemoteCode(tid),
                        Price = "14.00",   // original price for qty=1 (quantity-based total per doc-04)
                        Quantity = 1,
                        Discounts = new List<TalabatOrderDiscount>
                        {
                            Discount("4.20", "First Order")
                        }
                    }
                }
            }
        }, grandTotal: "9.80");

        var result = CreateMapper().MapToCreateOrder(webhook, "branch-1", "vendor-1");
        var product = result.Products[0];

        Assert.Equal(0.00m, product.TotalPrice);
        Assert.NotNull(product.Options);
        Assert.Single(product.Options!);
        Assert.Equal(9.80m, product.Options![0].UnitPrice);   // 14.00 - 4.20
        Assert.Equal(9.80m, product.Options![0].TotalPrice);
    }

    /// <summary>
    /// Scenario 5: Discounts applied to both the main item and its selected topping.
    /// Both are deducted independently.
    /// </summary>
    [Fact]
    public void Scenario5_DiscountsOnItemAndChoices_BothDeducted()
    {
        var pid = Guid.NewGuid();
        var tid = Guid.NewGuid();
        var webhook = MakeWebhook(new List<TalabatOrderProduct>
        {
            new()
            {
                RemoteCode = ProductRemoteCode(pid),
                PaidPrice = "14.00",
                UnitPrice = "14.00",
                Quantity = "1",
                Discounts = new List<TalabatOrderDiscount>
                {
                    Discount("2.10", "First Order")
                },
                SelectedToppings = new List<TalabatOrderTopping>
                {
                    new()
                    {
                        RemoteCode = ToppingRemoteCode(tid),
                        Price = "3.00",
                        Quantity = 1,
                        Discounts = new List<TalabatOrderDiscount>
                        {
                            Discount("0.90", "First Order")
                        }
                    }
                }
            }
        }, grandTotal: "14.00");

        var result = CreateMapper().MapToCreateOrder(webhook, "branch-1", "vendor-1");
        var product = result.Products[0];
        var option = product.Options![0];

        Assert.Equal(11.90m, product.TotalPrice);   // 14.00 - 2.10
        Assert.Equal(11.90m, product.UnitPrice);
        Assert.Equal(2.10m, option.UnitPrice);        // 3.00 - 0.90
        Assert.Equal(2.10m, option.TotalPrice);
    }

    /// <summary>
    /// Scenario 6: Mixed order — no discount, base discount only, base+booster in the same order.
    /// Each product is priced independently.
    /// </summary>
    [Fact]
    public void Scenario6_MixedOrder_EachProductPricedIndependently()
    {
        var pidA = Guid.NewGuid();
        var pidB = Guid.NewGuid();
        var pidC = Guid.NewGuid();
        var webhook = MakeWebhook(new List<TalabatOrderProduct>
        {
            // Item A: no discount
            new()
            {
                RemoteCode = ProductRemoteCode(pidA),
                PaidPrice = "10.00",
                UnitPrice = "10.00",
                Quantity = "1"
            },
            // Item B: base discount only
            new()
            {
                RemoteCode = ProductRemoteCode(pidB),
                PaidPrice = "20.00",
                UnitPrice = "20.00",
                Quantity = "1",
                Discounts = new List<TalabatOrderDiscount>
                {
                    Discount("6.00", "Base")
                }
            },
            // Item C: base + booster
            new()
            {
                RemoteCode = ProductRemoteCode(pidC),
                PaidPrice = "30.00",
                UnitPrice = "30.00",
                Quantity = "1",
                Discounts = new List<TalabatOrderDiscount>
                {
                    Discount("9.00", "Base"),
                    Discount("3.00", "Booster")
                }
            }
        }, grandTotal: "42.00");

        var result = CreateMapper().MapToCreateOrder(webhook, "branch-1", "vendor-1");

        Assert.Equal(10.00m, result.Products[0].TotalPrice);  // no discount
        Assert.Equal(10.00m, result.Products[0].UnitPrice);

        Assert.Equal(14.00m, result.Products[1].TotalPrice);  // 20.00 - 6.00
        Assert.Equal(14.00m, result.Products[1].UnitPrice);

        Assert.Equal(18.00m, result.Products[2].TotalPrice);  // 30.00 - 9.00 - 3.00
        Assert.Equal(18.00m, result.Products[2].UnitPrice);
    }
}
