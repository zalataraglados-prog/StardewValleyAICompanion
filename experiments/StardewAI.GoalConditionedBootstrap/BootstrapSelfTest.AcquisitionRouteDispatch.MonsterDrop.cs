using System.Text.Json;
using StardewAI.Contracts.Execution;
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
    private static void VerifyMonsterDropRankingRebuild()
    {
        var snapshot = AcquisitionDispatchMonsterDropSnapshot();
        var ledger = new StrategyCommitmentLedger
        {
            LedgerId = "ledger.dispatch.monster-drop.self-test",
            Revision = 1,
            SourceStateHash = snapshot.StateHash
        };
        var requirement = BushRequirement() with
        {
            QualifiedItemId = "(O)766",
            RouteKind = "native_monster_drop_table",
            SourceId = "monster:Green Slime"
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
        Require(ranked.Length == 1,
            "Exact monster-drop intent emitted no unique live ranking candidate.");

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
            "Exact monster-drop ranking did not reproduce from the transparent snapshot: " +
            string.Join(",", reasons));

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
            "Exact rebuilt monster-drop intent was rejected by dispatch selection.");

        var pickupPlan = new SmallModelPlanEnvelope
        {
            Steps = Array.Empty<SmallModelPlanStep>()
        };
        var pickupReasons = AcquisitionRouteDispatchCompilationBuilder
            .AppendDeferredNativeDropPickup(
                pickupPlan,
                requirement,
                rebuilt[0],
                snapshot);
        Require(pickupPlan.Steps.Length == 1,
            "Exact monster-drop route did not append one shared debris pickup.");
        var pickup = pickupPlan.Steps[0];
        Require(pickupReasons.Length == 0 &&
                pickup.Kind == "pickup_debris" &&
                pickup.Parameters.Any(parameter =>
                    parameter.Name == "deferred_pickup_source_kind" &&
                    parameter.Value == "native_monster_drop_table"),
            "Exact monster-drop route emitted an invalid deferred pickup.");

        var injected = rebuilt.Select(CloneCandidate).ToArray();
        injected[0].Parameters = injected[0].Parameters.Concat(new[]
        {
            Parameter("acquisition_source_id", requirement.SourceId)
        }).ToArray();
        Require(AcquisitionRouteDispatchCompilationBuilder.SelectCandidates(
                    requirement,
                    lowering,
                    snapshot,
                    injected).Length == 0,
            "A monster-drop candidate with extra acquisition lineage was admitted.");
    }

    private static SnapshotEnvelope AcquisitionDispatchMonsterDropSnapshot()
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
                "current_mine": {"value":{"location_id":"UndergroundMine45","mine_level":45,"mine_kind":"ordinary_mines"},"status":"available"},
                "tiles": {"value":{"player_tile":{"tile_x":1,"tile_y":2},"map":{"width":6,"height":5},"collision_context":{"status":"available","encoding":"row_major_strings_1_blocked_0_passable","width":6,"height":5,"blocked_rows":["111111","100001","100001","100001","111111"]},"exits":[],"ladders":[],"shafts":[],"elevators":[],"staircase_placement":{}},"status":"available"},
                "objects": {"value":[{"tile_x":3,"tile_y":2,"qualified_item_id":"(O)32","is_breakable_stone":true,"best_pickaxe_hits_remaining":2}],"status":"available"},
                "resource_clumps": {"value":[],"status":"available"},
                "monsters": {"value":[{"runtime_identity":"slime-1","runtime_type":"StardewValley.Monsters.GreenSlime","name":"Green Slime","tile_x":4,"tile_y":2,"health":20,"possible_drop_qualified_item_ids":["(O)766"],"guaranteed_drop_qualified_item_ids":["(O)766"],"authoritative_route_sources":[{"route_kind":"native_monster_drop_table","source_id":"monster:Green Slime","qualified_item_id":"(O)766"}],"melee_attack_projections":[{"slot_index":1,"expected_attacks_to_defeat":2.0,"expected_active_damage_duration_ms":600.0,"terminal_effect":"defeat"}]}],"status":"available"},
                "floor_objectives": {"value":{"must_kill_all_monsters_to_advance":false},"status":"available"},
                "reward_chests": {"value":[],"status":"available"},
                "player_resources": {"value":{"health":100,"max_health":100,"energy":220,"max_energy":270,"current_time":1200,"deepest_mine_level":45,"selected_slot_index":1,"food_slots":[],"cardinal_movement":{"tile_duration_ms":100.0}},"status":"available"},
                "completeness": {"value":{"status":"complete"},"status":"available"}
              }
            }
            """) ?? throw new InvalidDataException(
                "Monster-drop dispatch snapshot did not deserialize.");
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-11T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }
}
