using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class ArtifactSpotExcavationMainlineTests
{
    [Fact]
    public void HighLevelCandidateSelectsOnlyNativeArtifactSpotsAndReusesClearObstacleQueue()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.excavate_artifact_spots" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("clear:Farm:11,10:artifact_spot", candidate.CandidateId);
        Assert.Equal("clear_obstacle_tile", candidate.Kind);
        Assert.Contains("source=(O)590", candidate.ExpectedEffect, StringComparison.Ordinal);
        Assert.DoesNotContain("SeedSpot", candidate.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains(candidate.Parameters, value =>
            value.Name == "clear_output_projection_status" && value.Value == "exact");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("clear_obstacle", Assert.Single(plan.Steps).Kind);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.clear_obstacle", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("clear_obstacle", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CapabilityIsAdmittedByTheVerifiedHighLevelNativeRuntimeChain()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "foraging.excavate_artifact_spots");

        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, declaration.CompilerStatus);
        Assert.True(declaration.InternalExecutionPipelineSupported);
        Assert.True(declaration.AutonomousCandidateEnabled);
        Assert.True(DailyPlanCompiler.HasOptionCompiler(declaration.OptionId));
        Assert.False(ActionQueueCompiler.HasStepCompiler(declaration.OptionId));
        Assert.Equal(OptionRuntimeStatus.RuntimeVerified, declaration.RuntimeEvidenceStatus);
        Assert.Equal(OptionTrainingEligibility.Eligible, declaration.TrainingEligibility);
        Assert.Equal(new[] { "EVD-334" }, declaration.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-334" }, declaration.CandidateEvidenceIds);
        Assert.Equal(new[] { "EVD-334" }, declaration.CompilerEvidenceIds);
        Assert.Equal(new[] { "EVD-334" }, declaration.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-334" }, declaration.OutputEvidenceIds);
        Assert.Contains(
            declaration.OptionId,
            OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    private static SnapshotEnvelope Snapshot()
    {
        const string outputItems = "[{\"qualifiedItemId\":\"(O)100\",\"quantity\":1}]";
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "energy":{"value":270,"status":"available"},
            "inventory":{"value":[],"status":"available"}
          },
          "time":{"time":{"value":900,"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
          "current_location":{
            "map":{"value":{"id":"Farm"},"status":"available"},
            "objects":{"value":[
              {
                "tile_x":11,"tile_y":10,"qualified_item_id":"(O)590","name":"Artifact Spot","clear_kind":"artifact_spot",
                "clear_obstacle_executor_status":"ready","required_tool_kind":"hoe","tool_slot_index":2,"expected_tool_hits_to_clear":1,
                "harvest_experience_skill_id":"foraging","harvest_experience_on_success_min":15,"harvest_experience_on_success_max":15,
                "harvest_experience_condition":"native_hoe_digs_artifact_spot","harvest_experience_projection_status":"exact",
                "clear_output_projection_status":"exact","clear_output_items_json":{{{JsonSerializer.Serialize(outputItems)}}},
                "clear_output_qualified_item_id":"(O)100","clear_output_quantity_min":1,"clear_output_quantity_max":1,
                "clear_bonus_output_qualified_item_id":"(O)Book_Defense","clear_bonus_output_quantity_min":0,"clear_bonus_output_quantity_max":0,
                "artifact_spots_dug_before":4,"artifact_spots_dug_delta":1,"artifact_spots_dug_expected_after":5,
                "clear_terrain_feature_expected_after":"HoeDirt","defense_book_mail_before":0,"defense_book_mail_expected_after":0
              },
              {"tile_x":12,"tile_y":10,"qualified_item_id":"(O)SeedSpot","name":"Seed Spot","clear_kind":"artifact_spot"},
              {"tile_x":9,"tile_y":10,"qualified_item_id":"(O)294","name":"Twig","clear_kind":"twig"}
            ],"status":"available"},
            "terrain_features":{"value":[],"status":"available"}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"},
            "route_action_branch_coverage":{"value":{"rows":[]},"status":"available"}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-08T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
