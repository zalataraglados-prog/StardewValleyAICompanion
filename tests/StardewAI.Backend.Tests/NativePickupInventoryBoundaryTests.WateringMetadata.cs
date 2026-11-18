using System.Text.Json.Nodes;
using System.Xml.Linq;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Theory]
    [InlineData(false, 20, 1, false, false)]
    [InlineData(true, 20, 1, false, false)]
    [InlineData(false, 0, 2, false, false)]
    [InlineData(false, 20, 1, true, false)]
    [InlineData(false, 0, 1, false, true)]
    [InlineData(false, 20, 0, false, false)]
    public void WateringMetadataPublicCompositionSettlesRealAdmissionShapeAndToolInputs(
        bool newSlot, int water, int cycles, bool bottomless, bool enchanted)
    {
        var f = WateringMetadataFixture(newSlot, water, cycles, bottomless: bottomless, enchanted: enchanted, counter: int.MaxValue);
        var raw = f.Raw();
        var summary = f.Read();
        Assert.Single(summary.Inventory.Slots);
        Assert.Equal(6, summary.Inventory.Evidence.ObservedPickupUnits);
        Assert.True(summary.PhysicalInventoryDeltaVerified);
        Assert.True(summary.MaterialGraphAndMirrorsVerified);
        Assert.True(summary.WateringCropCanEnergyAndBowlsVerified);
        Assert.True(summary.PersistedToolMetadataVerified);
        Assert.True(summary.UnrelatedPersistentStateVerified);
        Assert.False(summary.ProducerProvenanceVerified);
        Assert.False(summary.ResourceCreditAuthorized);
        Assert.False(summary.NativeOutcomeVerified);
        Assert.False(summary.FormalProductTrainingAuthorized);
        Assert.Equal(raw, f.Raw());
        if (cycles > 0)
            Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.ReadPickupInventory(
                f.Stock.Request, f.Receipt, f.Stock.Before, f.Stock.After, "Farm"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WateringMetadataBlockedPickupMayReportARefillTileWithoutCreditingACrop(bool refill)
    {
        var f = WateringMetadataFixture(water: refill ? 0 : 20, cycles: refill ? 1 : 0, applied: false, refillOnly: refill);
        f.Result.TargetTileX = 90; f.Result.TargetTileY = 20;
        var raw = f.Raw();
        var summary = f.Read();
        Assert.Equal(6, summary.Inventory.Evidence.ObservedPickupUnits);
        Assert.Equal(0, f.Result.WateredCount);
        Assert.False(summary.NativeOutcomeVerified);
        Assert.Equal(raw, f.Raw());
    }

    [Theory]
    [InlineData("sprite")]
    [InlineData("counter")]
    [InlineData("history-without-call")]
    [InlineData("water")]
    [InlineData("unknown-xml")]
    [InlineData("before-water-baseline")]
    public void WateringMetadataDoesNotUseSelfConsistentAfterXmlAsAnIndependentPrediction(string fault)
    {
        var f = WateringMetadataFixture();
        _ = f.Read();
        if (fault == "before-water-baseline")
        {
            var before = f.Receipt.InventoryPayloadBefore!.Slots![1];
            var altered = XElement.Parse(before.SerializedStateXml!);
            altered.Element("WaterLeft")!.Value = "30"; SetXml(before, altered);
            f.Receipt.WateringToolMetadataBefore!.Tools![0] = WateringMetadataRow(before);
            Assert.Throws<InvalidDataException>(() => f.Read());
            return;
        }
        var slot = f.Receipt.InventoryPayloadAfter!.Slots![1];
        var xml = XElement.Parse(slot.SerializedStateXml!);
        switch (fault)
        {
            case "sprite": xml.Element("currentParentTileIndex")!.Value = "276"; break;
            case "counter": xml.Element("swingTicker")!.Value = "9"; break;
            case "water": xml.Element("WaterLeft")!.Value = "18"; break;
            case "unknown-xml": xml.Add(new XElement("unexplained_tool_state", "changed")); break;
            case "history-without-call":
                f.Receipt.WateringToolMetadataCalls = f.Receipt.WateringToolMetadataCalls!.Where(row => row.Kind != "inventory_history").ToArray();
                for (var i = 0; i < f.Receipt.WateringToolMetadataCalls.Length; i++) f.Receipt.WateringToolMetadataCalls[i].Ordinal = i + 1;
                break;
        }
        SetXml(slot, xml);
        f.Receipt.WateringToolMetadataAfter!.Tools![0] = WateringMetadataRow(slot);
        var raw = f.Raw();
        Assert.Throws<InvalidDataException>(() => f.Read());
        Assert.Equal(raw, f.Raw());
    }

    [Fact]
    public void WateringMetadataDoesNotRelaxTheFinalPhysicalStockGraph()
    {
        var f = WateringMetadataFixture();
        _ = f.Read();
        Edit(f.Stock.After, "farm", "material_inventory_graph", graph => graph["quantity_rows"]![0]!["available_quantity"] = 999);
        Rebind(f.Stock); f.Result.ProductAfterStateHash = f.Stock.After.StateHash;
        Assert.Throws<InvalidDataException>(() => f.Read());
    }
}
