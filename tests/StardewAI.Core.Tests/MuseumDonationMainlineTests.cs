using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MuseumDonationMainlineTests
{
    [Fact]
    public void ExactDonationFlowsFromTransparentCandidateToNativeExecutor()
    {
        var snapshot = Snapshot(donatedBefore: 59, total: 95);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "museum.donate_items" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("donate_museum_item", candidate.Kind);
        Assert.Equal("(O)96", candidate.QualifiedItemId);
        AssertParameter(candidate.Parameters, "expected_donated_count_before", "59");
        AssertParameter(candidate.Parameters, "expected_donated_count_after", "60");
        AssertParameter(candidate.Parameters, "reaches_rusty_key_threshold", "true");
        AssertParameter(candidate.Parameters, "rusty_key_reward_action", "MarkEventSeen Host 295672");
        AssertParameter(candidate.Parameters, "auto_applied_reward_ids_json", "[\"museum60\"]");
        AssertParameter(candidate.Parameters, "auto_applied_reward_actions_json", "[\"MarkEventSeen Host 295672\"]");

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("donate_museum_item", step.Kind);
        Assert.Contains("native_MuseumMenu_receiveLeftClick_only", step.SafetyConstraints);
        Assert.Contains("no_direct_museum_inventory_achievement_mail_or_event_mutation", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.donate_museum_item", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("donate_museum_item", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void FinalDynamicCollectionDonationCarriesAchievementPostcondition()
    {
        var snapshot = Snapshot(donatedBefore: 94, total: 95);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "museum.donate_items" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        AssertParameter(candidate.Parameters, "expected_collection_complete_after", "true");
        AssertParameter(candidate.Parameters, "expected_complete_collection_achievement_after", "true");
        AssertParameter(candidate.Parameters, "newly_pending_reward_ids_json", "[\"museumComplete\"]");
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal("pending", queue.Status);
        Assert.Contains("collection_complete=true", Assert.Single(queue.Items).NormalizedCommand.Steps.Single().ExpectedEffect);
    }

    [Fact]
    public void CompilerRejectsDonationWhenTransparentCountDrifts()
    {
        var original = Snapshot(donatedBefore: 59, total: 95);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "museum.donate_items" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            original.StateHash);
        var drifted = Snapshot(donatedBefore: 60, total: 95);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("museum_donation_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RemoteDonationUsesOneFreshConnectorAndLocksExactItem()
    {
        var snapshot = RemoteSnapshot(Snapshot(donatedBefore: 59, total: 95));
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "museum.donate_items" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal("(O)96", candidate.QualifiedItemId);
        AssertParameter(candidate.Parameters, "continuation.option_id", "museum.donate_items");
        AssertParameter(candidate.Parameters, "continuation.inventory_slot_index", "0");
        AssertParameter(candidate.Parameters, "continuation.qualified_item_id", "(O)96");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var routeItem = Assert.Single(queue.Items);
        Assert.Equal("executor.traverse_connector", routeItem.OptionId);

    }

    [Fact]
    public void RuntimeDonationUsesNativeMenuLifecycleWithoutDirectProgressWrites()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Museum.cs"));

        Assert.Contains("LibraryMuseum.OpenDonationMenu", source, StringComparison.Ordinal);
        Assert.Contains("MuseumMenu", source, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("MuseumMenu.placingInMuseumState", source, StringComparison.Ordinal);
        Assert.Contains("menu.fadeTimer > 0", source, StringComparison.Ordinal);
        Assert.Contains("menu.okButton.bounds.Center", source, StringComparison.Ordinal);
        Assert.Contains("museum_donation_native_settlement_timeout_or_mismatch", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.exitActiveMenu()", source, StringComparison.Ordinal);
        Assert.DoesNotContain("museumPieces.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("hasRustyKey =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("achievements.Add", source, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(int donatedBefore, int total)
    {
        var donatedAfter = donatedBefore + 1;
        var reachesThreshold = donatedBefore < 60 && donatedAfter >= 60;
        var completesCollection = donatedAfter >= total;
        var pendingAfterJson = completesCollection ? "[\"museumComplete\"]" : "[]";
        var autoRewardIdsJson = reachesThreshold ? "[\"museum60\"]" : "[]";
        var autoRewardActionsJson = reachesThreshold ? "[\"MarkEventSeen Host 295672\"]" : "[]";
        var json = $$$"""
        {
          "player": {
            "location_id":{"value":"ArchaeologyHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":0,"qualified_item_id":"(O)96","stack":2}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress": {
            "museum":{"value":{
              "pieces":[],
              "donated_count":{{{donatedBefore}}},
              "total_donatable_items":{{{total}}},
              "collection_complete":false,
              "complete_collection_achievement_received":false,
              "field_guide_quest_present":false,
              "field_guide_quest_completed":false,
              "rusty_key_donation_threshold":60,
              "rusty_key_reward_id":"museum60",
              "rusty_key_reward_action":"MarkEventSeen Host 295672",
              "rusty_key_reward_claimed":false,
              "rusty_key_prerequisite_event_seen":true,
              "rusty_key_event_seen":false,
              "has_rusty_key":false,
              "museum_location_id":"ArchaeologyHouse",
              "museum_is_current_location":true,
              "museum_mutex_locked":false,
              "gunther_action_tile_x":10,
              "gunther_action_tile_y":9,
              "gunther_action_raw":"Gunther",
              "free_donation_tile_x":4,
              "free_donation_tile_y":4,
              "free_donation_tile_count":1,
              "pending_reward_ids":[],
              "donation_candidates":[{
                "slot_index":0,
                "item_id":"96",
                "qualified_item_id":"(O)96",
                "display_name":"Dwarf Scroll I",
                "runtime_type":"StardewValley.Object",
                "stack_before":2,
                "stack_after":1,
                "donated_count_before":{{{donatedBefore}}},
                "donated_count_after":{{{donatedAfter}}},
                "completes_collection":{{{completesCollection.ToString().ToLowerInvariant()}}},
                "reaches_rusty_key_threshold":{{{reachesThreshold.ToString().ToLowerInvariant()}}},
                "expected_complete_collection_achievement_after":{{{completesCollection.ToString().ToLowerInvariant()}}},
                "field_guide_quest_present_before":false,
                "field_guide_quest_completed_before":false,
                "expected_field_guide_quest_completed_after":false,
                "pending_reward_ids_before":[],
                "pending_reward_ids_after":{{{pendingAfterJson}}},
                "newly_pending_reward_ids":{{{pendingAfterJson}}},
                "auto_applied_reward_ids":{{{autoRewardIdsJson}}},
                "auto_applied_reward_actions":{{{autoRewardActionsJson}}},
                "reward_projection_status":"ready",
                "action_status":"ready"
              }]
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid":{"value":{"location_id":"ArchaeologyHouse","width":64,"height":64,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
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

    private static SnapshotEnvelope RemoteSnapshot(SnapshotEnvelope snapshot)
    {
        var root = JsonSerializer.SerializeToNode(snapshot.State, JsonOptions)!.AsObject();
        root["player"]!["location_id"]!["value"] = "Town";
        root["player"]!["tile_x"]!["value"] = 9;
        root["player"]!["tile_y"]!["value"] = 10;
        root["player"]!.AsObject()["energy"] = FieldNode(270);
        root["time"] = new JsonObject { ["time"] = FieldNode(900) };
        root["world_progress"]!["museum"]!["value"]!["museum_is_current_location"] = false;
        root["world_progress"]!["museum"]!["value"]!["donation_candidates"]![0]!["action_status"] =
            "museum_not_current_location";
        var locations = root["locations"]!.AsObject();
        locations["collision_grid"]!["value"]!["location_id"] = "Town";
        locations["route_graph"] = FieldNode(new
        {
            status = "complete",
            edges = new[]
            {
                new
                {
                    kind = "warp", from_location = "Town", from_x = 10, from_y = 10,
                    target_location = "ArchaeologyHouse", target_x = 5, target_y = 12,
                    resolved = true
                }
            }
        });
        locations["route_connectors"] = FieldNode(new
        {
            location_id = "Town",
            connectors = new[]
            {
                new
                {
                    kind = "warp", tile_x = 10, tile_y = 10,
                    target_location = "ArchaeologyHouse", target_x = 5, target_y = 12,
                    resolved = true
                }
            }
        });
        locations["route_action_branch_coverage"] = FieldNode(new { rows = Array.Empty<object>() });
        var state = root.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state), GameTick = snapshot.GameTick,
            RealTimestamp = snapshot.RealTimestamp, Completeness = snapshot.Completeness, State = state
        };
    }

    private static JsonNode FieldNode(object value) => JsonSerializer.SerializeToNode(new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    }, JsonOptions)!;

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
