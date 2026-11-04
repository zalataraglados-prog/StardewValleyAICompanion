using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class FutureRouteDateEvidenceProducerTests
{
    [Theory]
    [InlineData(3, true)]
    [InlineData(4, false)]
    public void RequiredFirstEdgeMustBeTheExactNativeGraphEdge(int x, bool valid)
    {
        var request = Request();
        request.RequiredFirstEdge = new TransparentRouteEdge("LOCKED_DOOR_WARP", "farmhouse", "town", x, 2, 0, 2);
        var result = new FutureRouteDateEvidenceProducer().Produce(RouteGraph(), DateEvidence(12), request, Timing());

        Assert.Equal(valid, result.Status == FutureRouteDateEvidenceProductionStatus.Produced);
        if (!valid) Assert.Contains("future_route_required_first_edge_invalid", result.BlockingReasons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SameMapTargetCannotBypassARequiredFirstEdge(bool requireFirstEdge)
    {
        var request = Request();
        request.TargetLocation = request.StartLocation;
        request.TargetTileX = request.StartTileX;
        request.TargetTileY = request.StartTileY;
        if (requireFirstEdge)
            request.RequiredFirstEdge = new TransparentRouteEdge("locked_door_warp", "FarmHouse", "Town", 3, 2, 0, 2);
        var result = new FutureRouteDateEvidenceProducer().Produce(RouteGraph(), DateEvidence(12), request, Timing());

        Assert.Equal(!requireFirstEdge, result.Status == FutureRouteDateEvidenceProductionStatus.Produced);
        if (requireFirstEdge) Assert.Null(result.Scenario);
    }

    [Fact]
    public void RequiredFirstEdgeAllowsAnActualRoundTripToTheOriginalTile()
    {
        var request = Request();
        request.TargetLocation = request.StartLocation;
        request.TargetTileX = request.StartTileX;
        request.TargetTileY = request.StartTileY;
        request.RequiredFirstEdge = new TransparentRouteEdge("locked_door_warp", "FarmHouse", "Town", 3, 2, 0, 2);
        var graph = JsonSerializer.SerializeToElement(new { edges = new[]
        {
            Edge("locked_door_warp", "FarmHouse", 3, 2, "Town", 0, 2),
            Edge("warp", "Town", 4, 2, "FarmHouse", 0, 2)
        } });
        var result = new FutureRouteDateEvidenceProducer().Produce(graph, DateEvidence(12), request, Timing());

        Assert.Equal(FutureRouteDateEvidenceProductionStatus.Produced, result.Status);
        Assert.Equal(2, Assert.Single(result.Scenario!.ProducedPaths!).Length);
        Assert.True(result.Scenario.EarliestDepartureTime >= request.EarliestDepartureTime);
    }
}
