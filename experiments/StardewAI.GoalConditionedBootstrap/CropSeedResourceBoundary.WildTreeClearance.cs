using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.WorldModel;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    internal static void VerifyWildTreeClearance(SnapshotEnvelope before, SnapshotEnvelope after,
        TrainingExecutionRequest request, NativeClearanceTerrainSummary terrain)
    {
        Require(terrain.RuntimeType == "StardewValley.TerrainFeatures.Tree" && request.ClearCompletionMode == "wild_tree_removed" &&
                request.TreeChopProjectionStatus == "exact_live_tree_and_locked_wild_tree_chop_domain" &&
                request.TreeChopDataContractStatus == "exact_locked_base_1.6.15_chop" && request.TreeChopProtectionStatus == "unprotected",
            "Wild-tree clearance requires its original complete native chop contract.");
        var evidence = terrain.Consumption.ResourceEvidence;
        Require(evidence.ObservedPickupUnits == 0,
            "Wild-tree clearance pickup changes still require their combined inventory settlement.");
        var calls = evidence.ClearanceToolInputs!.Calls;
        Require(calls.Count > 0 && calls.All(call => call.Debit.Input.Tool == NativeClearanceToolKind.Axe &&
                call.X >= 0 && call.Y >= 0 && call.X / 64 == terrain.TileX && call.Y / 64 == terrain.TileY),
            "Wild-tree native tool calls differ from the original cleared target.");
        VerifyWildTreeSkillEffects(before, after, request);
        VerifyWildTreeStatEffects(before, after, request);
        var energy = Field(before, "player", "energy")!;
        energy["value"] = Value(Field(after, "player", "energy")!).DeepClone();
        EqualField(energy, Field(after, "player", "energy"), "wild-tree energy envelope");
        var bowls = VerifyWateringBowlMirrors(before, after);
        var state = new Dictionary<string, JsonElement>(after.State, StringComparer.Ordinal);
        foreach (var (domain, names) in new[]
        {
            ("farm", bowls ? new[] { "terrain_features", "pet_bowls" } : new[] { "terrain_features" }),
            ("player", new[] { "energy", "skills_detail", "level" }),
            ("world_progress", new[] { "game_state_query_unlock_state" })
        })
        {
            var fields = JsonNode.Parse(after.State[domain].GetRawText())!.AsObject();
            foreach (var name in names) fields[name]!["value"] = Value(Field(before, domain, name)!).DeepClone();
            state[domain] = JsonSerializer.SerializeToElement(fields);
        }
        var view = new SnapshotEnvelope { SaveId = after.SaveId, PlayerId = after.PlayerId, GameVersion = after.GameVersion, State = state };
        Require(FullShipmentRecoverySettlementBoundaryProjector.TryProject(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProject(view, out var next) && first == next,
            "Wild-tree clearance changed other persistent resources/state.");
    }
}
