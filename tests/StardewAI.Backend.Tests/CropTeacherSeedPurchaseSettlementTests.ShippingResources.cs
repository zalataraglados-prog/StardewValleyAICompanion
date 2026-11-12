using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void ShippingResources_NonemptyStockAndTwoPhysicalBankEchoes()
    {
        using var fixture = new ShippingResourcesFixture();
        var effect = fixture.Verify();
        Assert.Equal(fixture.Index, effect.SlotIndex); Assert.Equal(0, effect.Quality);
        Assert.Equal(50, effect.UnitPrice); Assert.Equal(71, effect.BinTileX); Assert.Equal(14, effect.BinTileY);
        Assert.Equal(0, effect.PendingValueBefore); Assert.Equal(50, effect.PendingValueAfter);
        Assert.True(effect.SpecificPersistentResourceBoundaryVerified);
        Assert.False(effect.NativeOutcomeVerified); Assert.False(effect.ResourceCreditAuthorized); Assert.False(effect.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void ShippingResources_FinalUnitRetainsEmptyRawRowAndRemovesGraphAggregate()
    {
        using var fixture = new ShippingResourcesFixture(stack: 1);
        Assert.Equal(50, fixture.Verify().PendingValueAfter);
    }

    [Fact]
    public void ShippingResources_ExistingVariantIncrementsWithoutCreditingMoney()
    {
        using var fixture = new ShippingResourcesFixture(bankCount: 2);
        var effect = fixture.Verify();
        Assert.Equal(100, effect.PendingValueBefore); Assert.Equal(150, effect.PendingValueAfter);
        Assert.False(effect.ResourceCreditAuthorized);
    }

    [Fact]
    public void ShippingResources_RejectsRawMetadataAndOtherSlotDrift()
    {
        foreach (var selected in new[] { true, false })
        {
            using var fixture = new ShippingResourcesFixture();
            fixture.Mutate("player", "inventory", rows => rows.AsArray().OfType<JsonObject>()
                .First(row => (row["slot_index"]!.GetValue<int>() == fixture.Index) == selected)["shipping_unknown"] = "drift");
            Assert.Throws<InvalidDataException>(() => fixture.Verify());
        }
    }

    [Fact]
    public void ShippingResources_RejectsForeignGraphQuantityAndUnknownMetadataDrift()
    {
        foreach (var kind in new[] { "foreign", "quantity", "unknown" })
        {
            using var fixture = new ShippingResourcesFixture();
            fixture.Mutate("farm", "material_inventory_graph", graph => {
                if (kind == "foreign") graph["inventory_nodes"]!.AsArray().Last()!["shipping_unknown"] = "drift";
                else if (kind == "quantity") graph["quantity_rows"]![0]!["source_slot_count"] = 999;
                else graph["shipping_unknown"] = "drift";
            });
            Assert.Throws<InvalidDataException>(() => fixture.Verify());
        }
    }

    [Fact]
    public void ShippingResources_RejectsBinEchoSignatureOtherItemAndMetadataDrift()
    {
        foreach (var kind in new[] { "echo", "signature", "other_item", "metadata", "scope" })
        {
            using var fixture = new ShippingResourcesFixture();
            fixture.Mutate("farm", "shipping_bins", bins => {
                var bin = bins.AsArray()[1]!;
                if (kind == "echo") bin["contents_total_count"] = 2;
                else if (kind == "signature") bin["contents_signature"] = "unverified";
                else if (kind == "other_item") bin["contents"]![0]!["qualified_item_id"] = "(O)390";
                else if (kind == "scope") bin["bin_scope"] = "personal";
                else bin["shipping_unknown"] = "drift";
            });
            Assert.Throws<InvalidDataException>(() => fixture.Verify());
        }
    }

    [Fact]
    public void ShippingResources_RejectsCurrencySeedAndWorldDrift()
    {
        foreach (var kind in new[] { "money", "seed", "world" })
        {
            using var fixture = new ShippingResourcesFixture();
            if (kind == "money") fixture.Mutate("player", "money", _ => { });
            if (kind == "money")
            {
                var fields = JsonNode.Parse(fixture.After.State["player"].GetRawText())!;
                fields["money"]!["value"] = 999999;
                fixture.After.State["player"] = System.Text.Json.JsonSerializer.SerializeToElement(fields); fixture.Rebind();
            }
            else if (kind == "seed") fixture.Mutate("player", "seed_inventory", rows => rows.AsArray()[0]!["stack"] = 999);
            else
            {
                fixture.After.State["shipping_unknown_world"] = System.Text.Json.JsonSerializer.SerializeToElement(new { changed = true });
                fixture.Rebind();
            }
            Assert.Throws<InvalidDataException>(() => fixture.Verify());
        }
    }

    [Fact]
    public void ShippingResources_RejectsReceiptSourceIdentityDayAndTickDrift()
    {
        foreach (var kind in new[] { "run", "candidate", "source", "actor", "day", "tick" })
        {
            using var fixture = new ShippingResourcesFixture();
            switch (kind)
            {
                case "run": fixture.Receipt.RunId = "wrong"; break;
                case "candidate": fixture.Receipt.SelectedCandidateId = "wrong"; break;
                case "source": fixture.Queue.StateHash = "wrong"; break;
                case "actor": fixture.After.PlayerId = new FieldEnvelope<string?> { Value = "456", Status = FieldStatus.Available }; break;
                case "day": fixture.Transition = fixture.Transition with { TotalDay = fixture.Transition.TotalDay + 1 }; break;
                case "tick": fixture.Receipt.StepResults[0].AfterGameTick++; break;
            }
            Assert.Throws<InvalidDataException>(() => fixture.Verify());
        }
    }
}
