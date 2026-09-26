using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CommunityCenterBundleRewardMainlineTests
{
    [Theory]
    [InlineData("junimo_note_present_button", false, 10, 10)]
    [InlineData("missed_rewards_chest", true, 22, 10)]
    public void PendingRewardCompilesThroughExactNativeClaimMode(
        string claimMode,
        bool areaComplete,
        int targetX,
        int targetY)
    {
        var snapshot = Snapshot(claimMode, areaComplete, targetX, targetY);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "community_center.donate_bundle_items" },
            true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("claim_community_center_bundle_reward", candidate.Kind);
        Assert.Equal("(O)465", candidate.QualifiedItemId);
        Assert.Equal(20, candidate.Quantity);
        AssertParameter(candidate.Parameters, "reward_claim_mode", claimMode);
        var sourceJson = AssertParameter(
            candidate.Parameters,
            "authoritative_route_sources_json");
        using var document = JsonDocument.Parse(sourceJson);
        var source = Assert.Single(document.RootElement.EnumerateArray());
        Assert.Equal("creates_reward_item", source.GetProperty("route_kind").GetString());
        Assert.Equal("bundle:Pantry/0:reward", source.GetProperty("source_id").GetString());

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("claim_community_center_bundle_reward", step.Kind);
        Assert.Contains(
            "no_direct_bundle_reward_or_inventory_mutation",
            step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.claim_community_center_bundle_reward", item.OptionId);
        Assert.Equal(
            "claim_community_center_bundle_reward",
            Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsRewardWhenExactSourceIdentityDrifts()
    {
        var original = Snapshot("junimo_note_present_button", false, 10, 10);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            original,
            new[] { "community_center.donate_bundle_items" },
            true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            original.StateHash);
        var driftedRoot = JsonSerializer.SerializeToNode(original.State)!.AsObject();
        driftedRoot["world_progress"]!["community_center"]!["value"]!["bundle_rows"]![0]!["reward"]!["authoritative_route_sources"]![0]!["source_id"] =
            "bundle:Pantry/1:reward";
        var drifted = Envelope(driftedRoot);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains(
            "community_center_bundle_reward_projection_drifted",
            Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesNativeRewardMenusWithoutDirectStateMutation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.CommunityCenterReward.cs"));

        Assert.Contains("CommunityCenter.checkBundle", source, StringComparison.Ordinal);
        Assert.Contains("presentButton", source, StringComparison.Ordinal);
        Assert.Contains("performAction", source, StringComparison.Ordinal);
        Assert.Contains("\"MissedRewards\"", source, StringComparison.Ordinal);
        Assert.Contains("ItemGrabMenu", source, StringComparison.Ordinal);
        Assert.Contains("SpecialVariable == active.BundleId", source, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bundleRewards[", source, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventory", source, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string claimMode,
        bool areaComplete,
        int targetX,
        int targetY)
    {
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"CommunityCenter","status":"available"},
            "tile_x":{"value":8,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[],"status":"available"}
          },
          "world_progress": {
            "community_center":{"value":{
              "route_state":"{{{(areaComplete ? "community_center_locked" : "undecided")}}}",
              "community_center_is_current_location":true,
              "can_read_junimo_text":true,
              "bundle_data_row_count":1,
              "projected_bundle_row_count":1,
              "unavailable_bundle_row_count":0,
              "bundle_rows":[{
                "projection_status":"exact",
                "bundle_data_key":"Pantry/0",
                "bundle_id":0,
                "area_id":0,
                "area_name":"Pantry",
                "reward_available":true,
                "area_complete":{{{areaComplete.ToString().ToLowerInvariant()}}},
                "note_appears":{{{(!areaComplete).ToString().ToLowerInvariant()}}},
                "note_tile_x":10,
                "note_tile_y":10,
                "interaction_tile_x":10,
                "interaction_tile_y":10,
                "area_mutex_locked":false,
                "reward":{
                  "projection_status":"exact",
                  "projection_failure":"",
                  "item_id":"465",
                  "qualified_item_id":"(O)465",
                  "runtime_type":"StardewValley.Object",
                  "quality":0,
                  "stack":20,
                  "inventory_item_total_before":0,
                  "inventory_item_total_after":20,
                  "inventory_accepts_reward":true,
                  "claim_mode":"{{{claimMode}}}",
                  "interaction_tile_x":{{{targetX}}},
                  "interaction_tile_y":{{{targetY}}},
                  "action_status":"ready",
                  "authoritative_route_sources":[{
                    "route_kind":"creates_reward_item",
                    "source_id":"bundle:Pantry/0:reward",
                    "qualified_item_id":"(O)465",
                    "source_asset":"Data/Bundles",
                    "source_path":"payload.Pantry/0[reward]",
                    "native_consumer":"JunimoNoteMenu.GetBundleRewards/Utility.getItemFromStandardTextDescription"
                  }]
                },
                "ingredients":[],
                "donation_candidates":[]
              }]
            },"status":"available"}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"CommunityCenter","width":64,"height":64,"notable_tiles":[]},"status":"available"}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}
          }
        }
        """;
        return Envelope(JsonNode.Parse(json)!.AsObject());
    }

    private static SnapshotEnvelope Envelope(JsonObject root)
    {
        var state = root.Deserialize<Dictionary<string, JsonElement>>()!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-09-26T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string? value = null)
    {
        var parameter = Assert.Single(parameters.Where(row => row.Name == name));
        if (value is not null)
            Assert.Equal(value, parameter.Value);
        return parameter.Value;
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }
}
