using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRadioactiveOreRankingRebuild()
    {
        var snapshot = AcquisitionDispatchRadioactiveOreSnapshot();
        VerifyRadioactiveOreCurrentRouteCandidateIndex(snapshot);
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.radioactive-ore.self-test",
            Revision = 1,
            SourceStateHash = snapshot.StateHash
        };
        var requirement = BushRequirement() with
        {
            QualifiedItemId = "(O)909",
            RouteKind = "native_radioactive_ore_node",
            SourceId = "GameLocation.breakStone"
        };
        var intent = new OptionAvailabilityCandidate
        {
            OptionId = "mining.reach_depth",
            Parameters = AcquisitionCurrentRouteCandidateIndex
                .BuildRollingMiningParameters(
                    snapshot,
                    requirement.RouteKind,
                    requirement.SourceId,
                    requirement.QualifiedItemId)
        };
        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[] { intent },
                includeExecutorCalibrationOptions: true,
                commitmentLedger: ledger),
            "grandpa.stage1.21_points");
        Require(ranked.Length == 1 &&
                ranked[0].CandidateId.Contains(
                    ":acquisition:native_radioactive_ore_node:",
                    StringComparison.Ordinal) &&
                ranked[0].Parameters.Any(parameter =>
                    parameter.Name == "execution_option_id" &&
                    parameter.Value == "executor.mine_stone"),
            "Exact radioactive-ore intent did not preempt the live ladder.");

        var rebuilt = AcquisitionRouteDispatchCompilationBuilder
            .RebuildVerifiedCurrentCandidates(
                snapshot,
                ledger,
                "grandpa.stage1.21_points",
                requirement,
                new[] { "mining.reach_depth" },
                ranked,
                out var reasons);
        Require(rebuilt.Length == ranked.Length && reasons.Length == 0,
            "Exact radioactive-ore ranking did not reproduce from the " +
            "transparent snapshot: " + string.Join(",", reasons));

        var lowering = BushLowering() with
        {
            RouteKind = requirement.RouteKind,
            SourceId = requirement.SourceId,
            EndpointOptionIds = new[] { "mining.reach_depth" }
        };
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    requirement,
                    lowering,
                    snapshot,
                    rebuilt).Length == 1,
            "Exact rebuilt radioactive-ore intent was rejected by dispatch " +
            "selection.");
    }

    private static void VerifyRadioactiveOreCurrentRouteCandidateIndex(
        SnapshotEnvelope snapshot)
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "stardewai-radioactive-ore-index-" + Guid.NewGuid() + ".json");
        try
        {
            File.WriteAllText(
                path,
                JsonSerializer.Serialize(snapshot, JsonDefaults.Options));
            var index = AcquisitionCurrentRouteCandidateIndex.Read(path);
            Require(index.TryFind(
                        "native_radioactive_ore_node",
                        "GameLocation.breakStone",
                        "(O)909",
                        out var candidates,
                        out var blockingReasons) &&
                    blockingReasons.Length == 0 &&
                    candidates.Length == 1 &&
                    candidates[0].OptionId == "mining.reach_depth" &&
                    candidates[0].TargetTileX == 3 &&
                    candidates[0].TargetTileY == 2 &&
                    candidates[0].CandidateId.Contains(
                        ":acquisition:native_radioactive_ore_node:",
                        StringComparison.Ordinal),
                "The exact radioactive-ore source did not enter the current " +
                "candidate index ahead of the reachable ladder.");
            Require(index.TryFind(
                        "native_radioactive_ore_node",
                        "spoofed.breakStone",
                        "(O)909",
                        out var spoofed,
                        out var spoofedReasons) &&
                    spoofedReasons.Length == 0 &&
                    spoofed.Length == 0,
                "A mismatched radioactive-ore source entered the current " +
                "candidate index.");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static SnapshotEnvelope AcquisitionDispatchRadioactiveOreSnapshot()
    {
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            """
            {
              "player": {
                "inventory": {"value":[],"status":"available"},
                "inventory_capacity": {"value":{"has_empty_slot":true,"empty_slots":12},"status":"available"}
              },
              "current_location": {
                "debris": {"value":[],"status":"available"}
              },
              "mining": {
                "current_mine": {"value":{"location_id":"UndergroundMine99","mine_level":99,"mine_kind":"ordinary_mines"},"status":"available"},
                "tiles": {"value":{"player_tile":{"tile_x":1,"tile_y":2},"map":{"width":6,"height":5},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":["111111","100001","100001","100001","111111"]},"exits":[],"ladders":[{"tile_x":4,"tile_y":2}],"shafts":[],"elevators":[],"staircase_placement":{}},"status":"available"},
                "objects": {"value":[{"tile_x":3,"tile_y":2,"item_id":"95","qualified_item_id":"(O)95","is_breakable_stone":true,"best_pickaxe_hits_remaining":1,"drop_rule_branch":"game_location_break_stone_direct_node","guaranteed_drop_qualified_item_ids":["(O)909"],"authoritative_route_sources":[{"route_kind":"native_radioactive_ore_node","source_id":"GameLocation.breakStone","qualified_item_id":"(O)909"}]}],"status":"available"},
                "resource_clumps": {"value":[],"status":"available"},
                "monsters": {"value":[],"status":"available"},
                "floor_objectives": {"value":{"must_kill_all_monsters_to_advance":false},"status":"available"},
                "reward_chests": {"value":[],"status":"available"},
                "player_resources": {"value":{"health":100,"max_health":100,"energy":220,"max_energy":270,"current_time":600,"deepest_mine_level":120,"selected_slot_index":1,"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0}},"status":"available"},
                "completeness": {"value":{"status":"complete"},"status":"available"}
              }
            }
            """) ?? throw new InvalidDataException(
                "Radioactive-ore dispatch snapshot did not deserialize.");
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-15T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
