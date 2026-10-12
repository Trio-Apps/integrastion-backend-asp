using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using OrderXChange.Application.Contracts.Integrations.Talabat;
using OrderXChange.Application.Integrations.Talabat;
using Xunit;

namespace OrderXChange.Application.Integrations;

/// <summary>
/// Foodics rejects an order (422) whose customer_notes or kitchen_notes exceed 512 characters.
/// Talabat customer comments of ~500 characters plus our "Talabat Order ID" reference line did
/// exactly that (PH-ARD-002, 2026-10-05/06).
/// </summary>
public class TalabatOrderToFoodicsMapperNotesTests
{
    private const int FoodicsNotesMaxLength = 512;

    private static TalabatOrderToFoodicsMapper CreateMapper() =>
        new(new ConfigurationBuilder().Build(), NullLogger<TalabatOrderToFoodicsMapper>.Instance);

    private static TalabatOrderWebhook MakeWebhook(string? customerComment, string? vendorComment = null) =>
        new()
        {
            Token = "test-token",
            Code = "TB_KW-ABC123",
            ShortCode = "4926",
            CreatedAt = DateTime.UtcNow,
            ExpeditionType = "pickup",
            Comments = new TalabatOrderComments { CustomerComment = customerComment, VendorComment = vendorComment },
            Price = new TalabatOrderPrice { GrandTotal = "5.00" },
            Products = new List<TalabatOrderProduct>
            {
                new() { RemoteCode = $"P_{Guid.NewGuid()}", PaidPrice = "5.00", UnitPrice = "5.00", Quantity = "1" }
            }
        };

    [Fact]
    public void Long_customer_comment_is_cut_to_the_Foodics_limit_keeping_the_order_reference()
    {
        var comment = new string('x', 500);

        var notes = CreateMapper().MapToCreateOrder(MakeWebhook(comment), "branch-1", "vendor-1").CustomerNotes!;

        Assert.Equal(FoodicsNotesMaxLength, notes.Length);
        Assert.StartsWith("Talabat Order ID: TB_KW-ABC123 | Talabat Short Code: 4926", notes);
        Assert.EndsWith("…", notes);
    }

    [Fact]
    public void Short_customer_comment_is_sent_unchanged()
    {
        var notes = CreateMapper().MapToCreateOrder(MakeWebhook("No onions please"), "branch-1", "vendor-1").CustomerNotes!;

        Assert.EndsWith("No onions please", notes);
        Assert.DoesNotContain("…", notes);
    }

    [Fact]
    public void Long_vendor_comment_is_cut_in_kitchen_notes_too()
    {
        var notes = CreateMapper().MapToCreateOrder(MakeWebhook(null, new string('y', 600)), "branch-1", "vendor-1").KitchenNotes!;

        Assert.Equal(FoodicsNotesMaxLength, notes.Length);
    }

    [Fact]
    public void Cut_never_splits_an_emoji()
    {
        // Pad so the cut lands right between the two halves of the emoji's surrogate pair.
        var reference = "Talabat Order ID: TB_KW-ABC123 | Talabat Short Code: 4926" + Environment.NewLine;
        var comment = new string('x', FoodicsNotesMaxLength - 2 - reference.Length) + "😀" + new string('z', 50);

        var notes = CreateMapper().MapToCreateOrder(MakeWebhook(comment), "branch-1", "vendor-1").CustomerNotes!;

        Assert.True(notes.Length <= FoodicsNotesMaxLength);
        Assert.False(char.IsHighSurrogate(notes[^2]), "a lone high surrogate was left before the ellipsis");
        Assert.EndsWith("…", notes);
    }
}
