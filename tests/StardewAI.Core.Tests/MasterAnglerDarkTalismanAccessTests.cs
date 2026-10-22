using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    [Fact]
    public void DarkTalismanKrobusCandidateCompilesOnlyFromExactProjection()
    {
        var fixture = CreateFixture("BugLand:0", targetLocation: "BugLand");
        try
        {
            var snapshot = DarkTalismanSnapshot(
                fixture.Before,
                "Sewer",
                19,
                21,
                DarkTalismanAccess("ready_to_talk_to_krobus"));
            var access = snapshot.State["locations"]
                .GetProperty("dark_talisman_access")
                .GetProperty("value");
            Assert.Equal("ready_to_talk_to_krobus", access.GetProperty("status").GetString());
            Assert.False(access.GetProperty("krobus_unseal_mail_received").GetBoolean());

            var candidate = EvaluateDarkTalismanCandidate(snapshot, fixture.Parameters);
            Assert.True(
                candidate.Kind == "interact_endpoint",
                candidate.Kind + ":" + string.Join(",", candidate.BlockReasons) + ":" +
                string.Join(",", candidate.Parameters.Select(row => row.Name + "=" + row.Value)));
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "interaction_kind" && row.Value == "npc_check_action");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "expected_action_type" && row.Value == "DarkTalismanKrobus");

            var item = CompileDarkTalismanInteraction(snapshot, fixture.Parameters);
            Assert.Empty(item.BlockingReasons);

            var drifted = RewriteDarkTalismanAccess(snapshot, access =>
                access["krobus"]!["tile_x"] = 22);
            var blocked = CompileDarkTalismanInteraction(snapshot, fixture.Parameters, drifted);
            Assert.Contains("dark_talisman_krobus_not_observed_at_target", blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void DarkTalismanChestReusesStrictNativeSpecialItemInteraction()
    {
        var fixture = CreateFixture("WitchSwamp:0", targetLocation: "WitchSwamp");
        try
        {
            var snapshot = DarkTalismanSnapshot(
                fixture.Before,
                "BugLand",
                30,
                5,
                DarkTalismanAccess("ready_to_claim_dark_talisman"));

            var candidate = EvaluateDarkTalismanCandidate(snapshot, fixture.Parameters);
            Assert.Equal("interact_endpoint", candidate.Kind);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "interaction_kind" && row.Value == "overlay_object");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "expected_action_type" && row.Value == "DarkTalismanChest");

            var item = CompileDarkTalismanInteraction(snapshot, fixture.Parameters);
            Assert.Empty(item.BlockingReasons);

            var drifted = RewriteDarkTalismanAccess(snapshot, access =>
                access["bug_lair"]!["expected_special_item_which"] = 5);
            var blocked = CompileDarkTalismanInteraction(snapshot, fixture.Parameters, drifted);
            Assert.Contains("dark_talisman_chest_not_observed_at_target", blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void DarkTalismanStatueWaitsOnExactRailroadNativeContract()
    {
        var fixture = CreateFixture("WitchSwamp:0", targetLocation: "WitchSwamp");
        try
        {
            var snapshot = DarkTalismanSnapshot(
                fixture.Before,
                "Railroad",
                53,
                35,
                DarkTalismanAccess("ready_to_remove_railroad_statue"));

            var candidate = EvaluateDarkTalismanCandidate(snapshot, fixture.Parameters);
            Assert.Equal("interact_endpoint", candidate.Kind);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "interaction_kind" && row.Value == "location_check_action");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "expected_action_type" && row.Value == "DarkTalismanStatue");

            var item = CompileDarkTalismanInteraction(snapshot, fixture.Parameters);
            Assert.Empty(item.BlockingReasons);

            var drifted = RewriteDarkTalismanAccess(snapshot, access =>
                access["railroad_statue"]!["buildings_tile_index"] = 286);
            var blocked = CompileDarkTalismanInteraction(snapshot, fixture.Parameters, drifted);
            Assert.Contains("dark_talisman_statue_not_observed_at_target", blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void RuntimeDarkTalismanPathUsesNativeInteractionsWithoutDirectProgressMutation()
    {
        var source = RuntimeHarnessSources.All;
        Assert.Contains("StartSpecialItemChestInteraction", source, StringComparison.Ordinal);
        Assert.Contains("active.Location.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("krobus.checkAction(Game1.player, Game1.currentLocation)", source, StringComparison.Ordinal);
        Assert.Contains("exactRailroad.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("Game1.player.freezePause <= 0", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"Game1\.player\.hasDarkTalisman\s*=", source);
        Assert.DoesNotContain("mailReceived.Add(\"krobusUnseal\")", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add(\"witchStatueGone\")", source, StringComparison.Ordinal);
    }

    private static EventCandidate EvaluateDarkTalismanCandidate(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) => Assert.Single(Assert.Single(
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = parameters
                    }
                }).Options).EventCandidates);

    private static StardewAI.Contracts.Execution.ActionQueueItem CompileDarkTalismanInteraction(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters,
        SnapshotEnvelope? compileSnapshot = null)
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = parameters
                }
            });
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(
                new StardewAI.Contracts.Training.BaselineTrainingReport(),
                availability,
                "goal.fishing.complete_master_angler"),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, compileSnapshot ?? snapshot);
        return Assert.Single(queue.Items.Where(item => item.OptionId == "executor.interact"));
    }

    private static SnapshotEnvelope DarkTalismanSnapshot(
        SnapshotEnvelope source,
        string locationId,
        int playerX,
        int playerY,
        JsonObject access)
    {
        var state = JsonSerializer.SerializeToNode(source.State, JsonOptions)!.AsObject();
        state["player"]!["location_id"]!["value"] = locationId;
        state["player"]!["tile_x"]!["value"] = playerX;
        state["player"]!["tile_y"]!["value"] = playerY;
        state["current_location"]!["map"]!["value"] = new JsonObject
        {
            ["location_id"] = locationId,
            ["width"] = 70,
            ["height"] = 70
        };
        state["locations"]!["collision_grid"]!["value"] = new JsonObject
        {
            ["location_id"] = locationId,
            ["width"] = 70,
            ["height"] = 70,
            ["notable_tiles"] = new JsonArray()
        };
        var targetLocation = access["status"]!.GetValue<string>() == "ready_to_talk_to_krobus"
            ? "BugLand"
            : "WitchSwamp";
        state["locations"]!["route_graph"] = Field(new JsonObject
        {
            ["edges"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "warp",
                    ["from_location"] = locationId,
                    ["from_x"] = playerX + 1,
                    ["from_y"] = playerY,
                    ["target_location"] = targetLocation,
                    ["target_x"] = 1,
                    ["target_y"] = 1,
                    ["resolved"] = true
                }
            }
        });
        state["locations"]!["social_route_date_evidence"] = Field(new JsonObject
        {
            ["schema_version"] = "social_route_date_evidence.v2",
            ["capture_total_days"] = 5,
            ["all_location_static_walkability_complete"] = true,
            ["projection_status"] = "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            ["location_count"] = 2,
            ["locations"] = new JsonArray
            {
                RouteLocationEvidence(locationId, 70, 70),
                RouteLocationEvidence(targetLocation, 70, 70)
            }
        });
        state["locations"]!["dark_talisman_access"] = Field(access);
        return SnapshotFromState(source, state);
    }

    private static SnapshotEnvelope RewriteDarkTalismanAccess(
        SnapshotEnvelope source,
        Action<JsonObject> rewrite)
    {
        var state = JsonSerializer.SerializeToNode(source.State, JsonOptions)!.AsObject();
        var access = state["locations"]!["dark_talisman_access"]!["value"]!.AsObject();
        rewrite(access);
        return SnapshotFromState(source, state);
    }

    private static SnapshotEnvelope SnapshotFromState(SnapshotEnvelope source, JsonObject state)
    {
        var fields = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            state.ToJsonString(JsonOptions),
            JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = source.SchemaVersion,
            GameVersion = source.GameVersion,
            StateHash = SnapshotHash.ComputeStateHash(fields),
            GameTick = source.GameTick,
            RealTimestamp = source.RealTimestamp,
            Completeness = source.Completeness,
            State = fields
        };
    }

    private static JsonObject DarkTalismanAccess(string status) => new()
    {
        ["schema_version"] = "dark_talisman_access.v1",
        ["projection_status"] = "complete_locked_base_1.6.15_native_contract",
        ["status"] = status,
        ["community_center_or_warehouse_done"] = true,
        ["railroad_event_seen"] = true,
        ["railroad_event_active"] = false,
        ["railroad_event_eligible"] = false,
        ["dark_talisman_quest_active"] = status == "ready_to_talk_to_krobus",
        ["krobus_unseal_mail_received"] = status is "ready_to_claim_dark_talisman" or "ready_to_remove_railroad_statue",
        ["player_has_dark_talisman"] = status == "ready_to_remove_railroad_statue",
        ["krobus"] = new JsonObject
        {
            ["present"] = true,
            ["location_id"] = "Sewer",
            ["in_sewer"] = true,
            ["tile_x"] = 20,
            ["tile_y"] = 21,
            ["runtime_type"] = "StardewValley.NPC"
        },
        ["bug_lair"] = new JsonObject
        {
            ["location_loaded"] = true,
            ["chest_tile_x"] = 31,
            ["chest_tile_y"] = 5,
            ["overlay_object_present"] = true,
            ["chest_runtime_type"] = "StardewValley.Objects.Chest",
            ["chest_item_count"] = 1,
            ["special_item_ids"] = new JsonArray(6),
            ["contains_exact_dark_talisman"] = true,
            ["expected_special_item_which"] = 6
        },
        ["railroad_statue"] = new JsonObject
        {
            ["location_loaded"] = true,
            ["tile_x"] = 54,
            ["tile_y"] = 35,
            ["buildings_tile_index"] = 287,
            ["expected_buildings_tile_index"] = 287,
            ["expected_tilesheet"] = "untitled tile sheet",
            ["identity_exact"] = true,
            ["gone"] = false
        }
    };
}
