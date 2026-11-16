using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed class NativeTillingObservationContractTests
{
    [Fact]
    public void TillingFlagIsOmittedFromOrdinaryWire()
    {
        var request = new TrainingExecutionRequest();
        Assert.DoesNotContain("action_origin_tilling_resources", JsonSerializer.Serialize(request));
    }

    [Fact]
    public void ExplicitTillingRequestReusesOriginalObservationWithoutProductionCredit()
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture();
        Enable(request);
        Assert.True(NativeTillingObservationContract.IsCompleteRequest(request));
        var summary = NativeOutputObservationBoundary.Read(request, receipt, 100, 110, "Farm");
        Assert.Equal(16, summary.Inventory.Single().InventoryAfter);
        Assert.False(summary.ResourceCreditAuthorized); Assert.False(summary.NativeOutcomeVerified);
        Assert.False(summary.FormalProductTrainingAuthorized);
        Assert.True(JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(request))!.ActionOriginTillingResources);
    }

    [Theory]
    [InlineData("flag")]
    [InlineData("mode")]
    [InlineData("option")]
    [InlineData("source_option")]
    [InlineData("scope")]
    [InlineData("anchor")]
    [InlineData("tools")]
    [InlineData("payload")]
    [InlineData("transients")]
    [InlineData("graph")]
    [InlineData("debris")]
    public void TillingCannotBorrowTheOldScopeOrOmitAWitnessTier(string fault)
    {
        var (request, receipt) = NativeOutputObservationBoundaryTests.Fixture(); Enable(request);
        switch (fault)
        {
            case "flag": request.ActionOriginTillingResources = false; break;
            case "mode": request.ExecutionMode = "coop_companion"; break;
            case "option": request.OptionId = "executor.clear_obstacle"; break;
            case "source_option": request.SourcePlanningOptionId = "executor.move_to_tile"; break;
            case "scope": request.ActionOriginObservationScopeId = null; break;
            case "anchor": request.ActionOriginSnapshotAnchor = false; break;
            case "tools": request.ActionOriginToolInputs = false; break;
            case "payload": request.ActionOriginInventoryPayload = false; break;
            case "transients": request.ActionOriginInventoryTransients = false; break;
            case "graph": request.ActionOriginInventoryGraph = false; break;
            case "debris": request.ActionOriginDebrisState = false; break;
        }
        Assert.False(NativeTillingObservationContract.IsCompleteRequest(request));
        Assert.Throws<InvalidDataException>(() => NativeOutputObservationBoundary.Read(request, receipt, 100, 110, "Farm"));
    }

    private static void Enable(TrainingExecutionRequest request)
    {
        request.OptionId = request.SourcePlanningOptionId = "executor.till_soil";
        request.ActionOriginTillingResources = request.ActionOriginSnapshotAnchor = request.ActionOriginToolInputs =
            request.ActionOriginInventoryPayload = request.ActionOriginInventoryTransients = request.ActionOriginInventoryGraph =
                request.ActionOriginDebrisState = true;
    }
}
