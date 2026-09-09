using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class SpringOnionHarvestMainlineTests
{
    [Fact]
    public void HighLevelCandidateSelectsOnlyExactNativeSpringOnionsAndReusesCropHarvestQueue()
    {
        var spec = new StardewAI.Core.OptionRegistry.OptionRegistry()
            .GetRequired("foraging.harvest_spring_onions");
        Assert.Contains(
            "vanilla_1_6_current_location_crops",
            spec.RequiredFactPolicy.DefaultRule.AllowedAdapterIds);
        Assert.Equal(new[] { "EVD-335" }, spec.RuntimeEvidenceIds);
        Assert.Equal(OptionRuntimeStatus.RuntimeVerified, spec.RuntimeStatus);
        Assert.Equal(OptionTrainingEligibility.Eligible, spec.TrainingEligibility);
        Assert.Contains(spec.OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);

        var snapshot = Snapshot(SpringOnionCrop() + "," + OrdinaryCrop() + "," + GingerCrop());
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.harvest_spring_onions" },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("harvest:Forest:11,10", candidate.CandidateId);
        Assert.Equal("harvest_crop_tile", candidate.Kind);
        Assert.Equal("399", candidate.ItemId);
        Assert.Equal("(O)399", candidate.QualifiedItemId);
        Assert.Contains("forage_crop=true", candidate.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("forage_crop_id=1", candidate.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains(
            "harvest_item_projection_status=exact_from_decompiled_native_spring_onion_branch",
            candidate.ExpectedEffect,
            StringComparison.Ordinal);
        Assert.Contains("skill_experience_skill_id=foraging", candidate.ExpectedEffect, StringComparison.Ordinal);
        Assert.Contains("skill_experience_on_success_min=3", candidate.ExpectedEffect, StringComparison.Ordinal);

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        Assert.Equal("harvest_crop", Assert.Single(plan.Steps).Kind);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.harvest_crop", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("harvest_crop", Assert.Single(item.NormalizedCommand.Steps).StepType);
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "harvest_item_qualified_id" && value.Value == "(O)399");
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "harvest_item_projection_status" &&
            value.Value == "exact_from_decompiled_native_spring_onion_branch");
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "forage_crop" && value.Value == "true");
        Assert.Contains(item.NormalizedCommand.Parameters, value =>
            value.Name == "forage_crop_id" && value.Value == "1");
    }

    [Fact]
    public void HighLevelCandidateFailsClosedForOtherOrInexactCrops()
    {
        var snapshot = Snapshot(
            OrdinaryCrop() + "," +
            GingerCrop() + "," +
            SpringOnionCrop(skillStatus: "unavailable"));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "foraging.harvest_spring_onions" },
            includeExecutorCalibrationOptions: true);
        var option = Assert.Single(availability.Options);

        Assert.False(option.Available);
        Assert.Empty(option.EventCandidates);
        Assert.Contains("no_spring_onion_harvest_candidates", option.BlockingReasons);
    }

    [Fact]
    public void FreshQueueCompilationRejectsSpringOnionIdentityDrift()
    {
        var selectedSnapshot = Snapshot(SpringOnionCrop());
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            selectedSnapshot,
            new[] { "foraging.harvest_spring_onions" },
            includeExecutorCalibrationOptions: true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            selectedSnapshot.StateHash);
        var driftedSnapshot = Snapshot(
            OrdinaryCrop(tileX: 11, tileY: 10),
            selectedSnapshot.StateHash);

        var queue = new ActionQueueCompiler().Compile(plan, driftedSnapshot);
        var item = Assert.Single(queue.Items);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("harvest_crop_item_identity_drifted", item.BlockingReasons);
        Assert.Contains("harvest_crop_item_projection_drifted", item.BlockingReasons);
        Assert.Contains("harvest_crop_forage_identity_drifted", item.BlockingReasons);
    }

    private static SnapshotEnvelope Snapshot(string crops, string? stateHash = null)
    {
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"Forest","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[],"status":"available"},
            "inventory_capacity":{"value":{"has_empty_slot":true,"empty_slots":12},"status":"available"}
          },
          "time":{"time":{"value":900,"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}},
          "current_location":{"crops":{"value":[{{{crops}}}],"status":"available"}},
          "locations":{"collision_grid":{"value":{"location_id":"Forest","width":120,"height":120,"notable_tiles":[]},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            StateHash = stateHash ?? SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-08T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string SpringOnionCrop(
        int tileX = 11,
        int tileY = 10,
        string skillStatus = "exact_from_decompiled_native_harvest")
    {
        return $$$"""
        {
          "tile_x":{{{tileX}}},"tile_y":{{{tileY}}},"ready_for_harvest":true,
          "harvest_item_id":"399","harvest_item_qualified_id":"(O)399","harvest_item_category":-81,
          "harvest_item_projection_status":"exact_from_decompiled_native_spring_onion_branch",
          "harvest_context_tags":["forage_item"],"harvest_min_stack":1,"harvest_method":"Grab",
          "forage_crop":true,"forage_crop_id":"1",
          "harvest_experience_skill_id":"foraging","harvest_experience_on_success_min":3,
          "harvest_experience_on_success_max":3,"harvest_experience_condition":"native_player_harvest_of_spring_onion_crop",
          "harvest_experience_projection_status":"{{{skillStatus}}}"
        }
        """;
    }

    private static string OrdinaryCrop(int tileX = 12, int tileY = 10)
    {
        return $$$"""
        {
          "tile_x":{{{tileX}}},"tile_y":{{{tileY}}},"ready_for_harvest":true,
          "harvest_item_id":"24","harvest_item_qualified_id":"(O)24","harvest_item_category":-75,
          "harvest_item_projection_status":"exact_from_live_index_of_harvest",
          "harvest_context_tags":[],"harvest_min_stack":1,"harvest_method":"Grab",
          "forage_crop":false,"forage_crop_id":"",
          "harvest_experience_skill_id":"farming","harvest_experience_on_success_min":8,
          "harvest_experience_on_success_max":8,"harvest_experience_projection_status":"exact"
        }
        """;
    }

    private static string GingerCrop()
    {
        return """
        {
          "tile_x":13,"tile_y":10,"ready_for_harvest":true,
          "harvest_item_id":"829","harvest_item_qualified_id":"(O)829","harvest_item_category":-81,
          "harvest_item_projection_status":"unavailable_no_live_harvest_item_id",
          "harvest_context_tags":["forage_item"],"harvest_min_stack":1,"harvest_method":"Grab",
          "forage_crop":true,"forage_crop_id":"2",
          "harvest_experience_skill_id":"foraging","harvest_experience_on_success_min":7,
          "harvest_experience_on_success_max":7,"harvest_experience_projection_status":"unavailable"
        }
        """;
    }
}
