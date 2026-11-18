namespace StardewAI.Backend.Tests;

public sealed partial class NativePickupInventoryBoundaryTests
{
    [Theory]
    [InlineData("missing-before")]
    [InlineData("missing-after")]
    [InlineData("missing-calls")]
    [InlineData("schema")]
    [InlineData("actor")]
    [InlineData("tick")]
    [InlineData("missing-row")]
    [InlineData("duplicate-row")]
    [InlineData("slot")]
    [InlineData("reference")]
    [InlineData("counter-null")]
    [InlineData("ordinal")]
    [InlineData("failed-call")]
    [InlineData("missing-return")]
    [InlineData("call-actor")]
    [InlineData("call-location")]
    [InlineData("call-reference")]
    [InlineData("call-kind")]
    [InlineData("missing-input")]
    [InlineData("extra-input")]
    [InlineData("result-anchor")]
    [InlineData("result-target")]
    [InlineData("profile")]
    public void WateringMetadataRejectsUnboundRawContextCallsAndNativeResult(string fault)
    {
        var f = WateringMetadataFixture();
        _ = f.Read();
        var receipt = f.Receipt;
        var after = receipt.WateringToolMetadataAfter!;
        var call = receipt.WateringToolMetadataCalls![0];
        switch (fault)
        {
            case "missing-before": receipt.WateringToolMetadataBefore = null; break;
            case "missing-after": receipt.WateringToolMetadataAfter = null; break;
            case "missing-calls": receipt.WateringToolMetadataCalls = null; break;
            case "schema": receipt.WateringToolMetadataBefore!.SchemaVersion = after.SchemaVersion = "unknown"; break;
            case "actor": after.ActorReferenceId = Guid.NewGuid().ToString("N"); break;
            case "tick": after.GameTick++; break;
            case "missing-row": after.Tools = []; break;
            case "duplicate-row": after.Tools = [after.Tools![0], after.Tools[0]]; break;
            case "slot": after.Tools![0].SlotIndex = 0; break;
            case "reference": after.Tools![0].ItemReferenceId = Guid.NewGuid().ToString("N"); break;
            case "counter-null": after.Tools![0].SwingTicker = null; break;
            case "ordinal": call.Ordinal = 99; break;
            case "failed-call": call.Completed = false; break;
            case "missing-return": call.Completed = null; break;
            case "call-actor": call.ActorReferenceId = Guid.NewGuid().ToString("N"); break;
            case "call-location": call.LocationId = "Town"; break;
            case "call-reference": call.ItemReferenceId = Guid.NewGuid().ToString("N"); break;
            case "call-kind": call.Kind = "invented"; break;
            case "missing-input": call.InventoryHistoryValue = null; break;
            case "extra-input": call.Direction = 1; break;
            case "result-anchor": f.Result.ProductAfterStateHash = "wrong"; break;
            case "result-target": f.Result.TargetTileX++; break;
            case "profile": f.Stock.Request.ActionOriginWateringResources = false; break;
        }
        var raw = f.Raw();
        Assert.Throws<InvalidDataException>(() => f.Read());
        Assert.Equal(raw, f.Raw());
    }
}
