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
}
