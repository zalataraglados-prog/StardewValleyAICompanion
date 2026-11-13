using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static void EqualExpectedResourceField(string domain, string name, JsonObject? expected, JsonObject? actual, string label)
    {
        if (expected is null || actual is null) { EqualField(expected, actual, domain + "." + name); return; }
        var first = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope(domain, name, JsonSerializer.SerializeToElement(expected));
        var next = FullShipmentRecoverySettlementBoundaryProjector.ProjectExpectedResourceEnvelope(domain, name, JsonSerializer.SerializeToElement(actual));
        Require(SnapshotHash.CanonicalizeSemantic(first) == SnapshotHash.CanonicalizeSemantic(next),
            label + " changed unexpected " + domain + "." + name + ".");
    }
}
