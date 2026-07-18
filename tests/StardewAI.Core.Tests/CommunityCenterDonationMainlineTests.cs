using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CommunityCenterDonationMainlineTests
{
    [Fact]
    public void ExactBundleDonationFlowsThroughCandidatePlanAndActionQueue()
    {
        var snapshot = Snapshot(completedBefore: 1);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "community_center.donate_bundle_items" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("donate_community_center_item", candidate.Kind);
        Assert.Equal("(O)24", candidate.QualifiedItemId);
        AssertParameter(candidate.Parameters, "bundle_id", "0");
        AssertParameter(candidate.Parameters, "bundle_ingredient_index", "1");
        AssertParameter(candidate.Parameters, "required_stack", "1");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("donate_community_center_item", step.Kind);
        Assert.Contains("native_CommunityCenter_checkBundle_only", step.SafetyConstraints);
        Assert.Contains("no_direct_bundle_inventory_reward_mail_or_route_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.donate_community_center_item", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("donate_community_center_item", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CompilerRejectsBundleDonationWhenCompletionProjectionDrifts()
    {
        var original = Snapshot(completedBefore: 1);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "community_center.donate_bundle_items" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            original.StateHash);
        var drifted = Snapshot(completedBefore: 2);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("community_center_donation_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    private static SnapshotEnvelope Snapshot(int completedBefore)
    {
        var completedAfter = completedBefore + 1;
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"CommunityCenter","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":0,"qualified_item_id":"(O)24","stack":3}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "community_center":{"value":{
              "route_state":"undecided",
              "community_center_is_current_location":true,
              "bundle_data_row_count":1,
              "projected_bundle_row_count":1,
              "unavailable_bundle_row_count":0,
              "bundle_rows":[{
                "projection_status":"exact",
                "projection_failure":"",
                "bundle_data_key":"Pantry/0",
                "bundle_id":0,
                "area_id":0,
                "area_name":"Pantry",
                "required_slot_count":4,
                "completed_ingredient_count":{{{completedBefore}}},
                "complete":false,
                "note_appears":true,
                "note_tile_x":10,
                "note_tile_y":10,
                "area_mutex_locked":false,
                "ingredients":[
                  {"ingredient_index":0,"item_id_or_category":"16","required_stack":1,"minimum_quality":0,"completed":true},
                  {"ingredient_index":1,"item_id_or_category":"24","required_stack":1,"minimum_quality":0,"completed":false},
                  {"ingredient_index":2,"item_id_or_category":"188","required_stack":1,"minimum_quality":0,"completed":false},
                  {"ingredient_index":3,"item_id_or_category":"190","required_stack":1,"minimum_quality":0,"completed":false},
                  {"ingredient_index":4,"item_id_or_category":"192","required_stack":1,"minimum_quality":0,"completed":false},
                  {"ingredient_index":5,"item_id_or_category":"250","required_stack":1,"minimum_quality":0,"completed":false}
                ],
                "donation_candidates":[{
                  "inventory_slot_index":0,
                  "ingredient_index":1,
                  "item_id":"24",
                  "qualified_item_id":"(O)24",
                  "runtime_type":"StardewValley.Object",
                  "quality":0,
                  "stack_before":3,
                  "stack_after":2,
                  "required_stack":1,
                  "inventory_item_total_before":3,
                  "inventory_item_total_after":2,
                  "completed_ingredient_count_before":{{{completedBefore}}},
                  "completed_ingredient_count_after":{{{completedAfter}}},
                  "completes_bundle":false,
                  "action_status":"ready"
                }]
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"CommunityCenter","width":64,"height":64,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-18T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    [Fact]
    public void RuntimeUsesOnlyNativeJunimoNoteLifecycleForProgressMutation()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.CommunityCenter.cs"));

        Assert.Contains("CommunityCenter.checkBundle", source, StringComparison.Ordinal);
        Assert.Contains("JunimoNoteMenu", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("menu.exitThisMenu", source, StringComparison.Ordinal);
        Assert.Contains("GetBundleIngredientDescriptionIndexForItem", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bundles.FieldDict", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ConsumeStack", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", source, StringComparison.Ordinal);
    }

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string value)
    {
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
