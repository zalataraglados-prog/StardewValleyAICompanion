using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeWateringObservationContractTests
{
    [Fact]
    public void OrdinaryWireDoesNotRequestOrContainWateringMetadata()
    {
        Assert.DoesNotContain("action_origin_watering_resources", JsonSerializer.Serialize(new TrainingExecutionRequest()));
        var wire = JsonSerializer.Serialize(new ActionOriginObservationReceipt());
        Assert.DoesNotContain("watering_tool_metadata", wire);
    }

    [Fact]
    public void ExplicitWateringOnlyAdmitsRawObservationNotResourceOrOutcomeCredit()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        Enable(request);
        Assert.True(NativeWateringObservationContract.IsCompleteRequest(request));
        var summary = NativeOutputObservationBoundary.Read(request, receipt, 100, 110, "Farm");
        Assert.False(summary.ResourceCreditAuthorized);
        Assert.False(summary.NativeOutcomeVerified);
        Assert.False(summary.FormalProductTrainingAuthorized);
        Assert.True(JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(request))!.ActionOriginWateringResources);
    }

    [Theory]
    [InlineData("flag")]
    [InlineData("mode")]
    [InlineData("option")]
    [InlineData("source")]
    [InlineData("scope")]
    [InlineData("anchor")]
    [InlineData("tools")]
    [InlineData("payload")]
    [InlineData("transients")]
    [InlineData("graph")]
    [InlineData("debris")]
    [InlineData("mixed")]
    public void WateringCannotBorrowAnOldScopeOrOmitAnExplicitWitnessTier(string fault)
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        Enable(request);
        switch (fault)
        {
            case "flag": request.ActionOriginWateringResources = false; break;
            case "mode": request.ExecutionMode = "coop_companion"; break;
            case "option": request.OptionId = "executor.clear_obstacle"; break;
            case "source": request.SourcePlanningOptionId = "executor.move_to_tile"; break;
            case "scope": request.ActionOriginObservationScopeId = null; break;
            case "anchor": request.ActionOriginSnapshotAnchor = false; break;
            case "tools": request.ActionOriginToolInputs = false; break;
            case "payload": request.ActionOriginInventoryPayload = false; break;
            case "transients": request.ActionOriginInventoryTransients = false; break;
            case "graph": request.ActionOriginInventoryGraph = false; break;
            case "debris": request.ActionOriginDebrisState = false; break;
            case "mixed": request.ActionOriginTillingResources = true; break;
        }
        Assert.False(NativeWateringObservationContract.IsCompleteRequest(request));
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.Read(request, receipt, 100, 110, "Farm"));
    }

    [Fact]
    public void MixedFlagsCannotBeReadThroughTheOldTillingProfile()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        Enable(request);
        request.OptionId = request.SourcePlanningOptionId = "executor.till_soil";
        request.ActionOriginTillingResources = true;
        Assert.False(NativeTillingObservationContract.IsCompleteRequest(request));
        Assert.False(NativeWateringObservationContract.IsCompleteRequest(request));
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.Read(request, receipt, 100, 110, "Farm"));
    }

    [Fact]
    public void RawWateringMetadataRoundTripsNegativeWaterWrapCountersAndMultipleCallsWithoutCertifyingThem()
    {
        var original = new ActionOriginObservationReceipt
        {
            WateringToolMetadataBefore = new() { SchemaVersion = "native_watering_tool_metadata.v1", GameTick = 100,
                ActorReferenceId = "actor", Tools = [new() { SlotIndex = 1, ItemReferenceId = "can",
                    RuntimeType = "StardewValley.Tools.WateringCan", QualifiedItemId = "(T)WateringCan",
                    InitialParentTileIndex = 273, CurrentParentTileIndex = 275, SwingTicker = int.MaxValue,
                    HasBeenInInventory = false, WaterLeft = 0 }] },
            WateringToolMetadataAfter = new() { SchemaVersion = "native_watering_tool_metadata.v1", GameTick = 110,
                ActorReferenceId = "actor", Tools = [new() { SlotIndex = 1, ItemReferenceId = "can",
                    RuntimeType = "StardewValley.Tools.WateringCan", QualifiedItemId = "(T)WateringCan",
                    InitialParentTileIndex = 273, CurrentParentTileIndex = 273, SwingTicker = int.MinValue,
                    HasBeenInInventory = true, WaterLeft = -1 }] },
            WateringToolMetadataCalls = [new() { Ordinal = 1, Kind = "inventory_history", InventoryHistoryValue = true,
                    Completed = true, GameTick = 100, ItemReferenceId = "can" },
                new() { Ordinal = 2, Kind = "update", Completed = false, GameTick = 101, ItemReferenceId = "can", Direction = 0, FarmerMotionFrame = 0 },
                new() { Ordinal = 3, Kind = "end_using", GameTick = 102, ItemReferenceId = "can" }]
        };
        var copy = JsonSerializer.Deserialize<ActionOriginObservationReceipt>(JsonSerializer.Serialize(original))!;
        Assert.Equal(int.MaxValue, copy.WateringToolMetadataBefore!.Tools![0].SwingTicker);
        Assert.False(copy.WateringToolMetadataBefore.Tools[0].HasBeenInInventory);
        Assert.Equal(int.MinValue, copy.WateringToolMetadataAfter!.Tools![0].SwingTicker);
        Assert.Equal(-1, copy.WateringToolMetadataAfter.Tools[0].WaterLeft);
        Assert.Equal(3, copy.WateringToolMetadataCalls!.Length);
        Assert.Null(copy.WateringToolMetadataCalls[0].Direction);
        Assert.True(copy.WateringToolMetadataCalls[0].Completed);
        Assert.True(copy.WateringToolMetadataCalls[0].InventoryHistoryValue);
        Assert.Null(copy.WateringToolMetadataCalls[1].InventoryHistoryValue);
        Assert.False(copy.WateringToolMetadataCalls[1].Completed);
        Assert.Null(copy.WateringToolMetadataCalls[2].Completed);
        Assert.Equal(0, copy.WateringToolMetadataCalls[1].FarmerMotionFrame);
    }

    internal static void Enable(TrainingExecutionRequest request)
    {
        request.OptionId = request.SourcePlanningOptionId = "executor.water_crop";
        request.ActionOriginWateringResources = request.ActionOriginSnapshotAnchor = request.ActionOriginToolInputs =
            request.ActionOriginInventoryPayload = request.ActionOriginInventoryTransients = request.ActionOriginInventoryGraph =
                request.ActionOriginDebrisState = true;
    }
}
