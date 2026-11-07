using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyContextualResourceBoundary(SnapshotEnvelope source)
    {
        var before = MutateContextBoundary(source, state =>
        {
            state["player"]!["tool_upgrade"] = JsonNode.Parse("""
                {"status":"available","value":{"schema_version":"tool_upgrade.v1",
                "current_location_id":"FarmHouse","actor_control_ready":true,
                "service_status":"route","lifecycle_stage":"route","projection_fingerprint":"old",
                "clint":{"present_at_blacksmith":true,"current_location":"Blacksmith","tile_x":3,"tile_y":13},
                "free_inventory_slots":7,"money_before":450,
                "pending_tool":{"present":false,"days_left":0,"claim_capacity_satisfied":true},
                "current_axe":{"qualified_item_id":"(T)Axe","upgrade_level":0,"slot_index":0},
                "next_axe_upgrade":{"price_gold":2000,"trade_item_amount":5,
                "money_available":450,"trade_item_available":0,"requirements_satisfied":false}}}
                """);
            state["world_progress"]!["island_gourmand"] = JsonNode.Parse("""
                {"status":"available","value":{"schema_version":"island_gourmand_progress.v1",
                "menu_clear":true,"requests_fulfilled":0,"walnut_debris_count":0,"mutex_locked":false}}
                """);
            state["world_progress"]!["community_center"] = JsonNode.Parse("""
                {"status":"available","value":{"completed":false,"lifecycle":{
                "schema_version":"community_center_lifecycle.v1","stage":"initial_unlock_pending",
                "door_unlock_received":false,"initial_unlock_event":{
                "event_active":false,"event_seen":false,"event_script_sha256":"locked"}}}}
                """);
        });
        var runtime = MutateContextBoundary(before, state =>
        {
            var tool = state["player"]!["tool_upgrade"]!["value"]!;
            tool["current_location_id"] = "Town";
            tool["actor_control_ready"] = false;
            tool["service_status"] = "blocked";
            tool["lifecycle_stage"] = "blocked";
            tool["projection_fingerprint"] = "new";
            tool["clint"]!["tile_x"] = 9;
            state["world_progress"]!["island_gourmand"]!["value"]!["menu_clear"] = false;
            var lifecycle = state["world_progress"]!["community_center"]!["value"]!["lifecycle"]!;
            lifecycle["stage"] = "initial_unlock_event_active";
            lifecycle["initial_unlock_event"]!["event_active"] = true;
        });
        Require(ContextBoundariesMatch(before, runtime, false) &&
                ContextBoundariesMatch(before, runtime, true),
            "Known route/menu/event context changed persistent resource authority.");

        var resourceOnly = MutateContextBoundary(before, state =>
        {
            var tool = state["player"]!["tool_upgrade"]!["value"]!;
            tool["money_before"] = 370;
            tool["free_inventory_slots"] = 6;
            tool["pending_tool"]!["claim_capacity_satisfied"] = false;
            tool["next_axe_upgrade"]!["money_available"] = 370;
            tool["next_axe_upgrade"]!["trade_item_available"] = 1;
            tool["next_axe_upgrade"]!["requirements_satisfied"] = true;
        });
        Require(!ContextBoundariesMatch(before, resourceOnly, false) &&
                ContextBoundariesMatch(before, resourceOnly, true),
            "Tool resource duplicates were ignored outside exact resource mutation.");

        var mutations = new (string Name, Action<JsonObject> Mutate)[]
        {
            ("pending-tool", state => state["player"]!["tool_upgrade"]!["value"]!["pending_tool"]!["present"] = true),
            ("days-left", state => state["player"]!["tool_upgrade"]!["value"]!["pending_tool"]!["days_left"] = 1),
            ("axe-level", state => state["player"]!["tool_upgrade"]!["value"]!["current_axe"]!["upgrade_level"] = 1),
            ("axe-slot", state => state["player"]!["tool_upgrade"]!["value"]!["current_axe"]!["slot_index"] = 2),
            ("price", state => state["player"]!["tool_upgrade"]!["value"]!["next_axe_upgrade"]!["price_gold"] = 1000),
            ("trade-cost", state => state["player"]!["tool_upgrade"]!["value"]!["next_axe_upgrade"]!["trade_item_amount"] = 1),
            ("tool-unknown", state => state["player"]!["tool_upgrade"]!["value"]!["future_progress"] = 1),
            ("clint-unknown", state => state["player"]!["tool_upgrade"]!["value"]!["clint"]!["future_progress"] = 1),
            ("gourmand-progress", state => state["world_progress"]!["island_gourmand"]!["value"]!["requests_fulfilled"] = 1),
            ("gourmand-reward", state => state["world_progress"]!["island_gourmand"]!["value"]!["walnut_debris_count"] = 5),
            ("gourmand-lock", state => state["world_progress"]!["island_gourmand"]!["value"]!["mutex_locked"] = true),
            ("gourmand-unknown", state => state["world_progress"]!["island_gourmand"]!["value"]!["future_progress"] = 1),
            ("center-complete", state => state["world_progress"]!["community_center"]!["value"]!["completed"] = true),
            ("center-mail", state => state["world_progress"]!["community_center"]!["value"]!["lifecycle"]!["door_unlock_received"] = true),
            ("center-seen", state => state["world_progress"]!["community_center"]!["value"]!["lifecycle"]!["initial_unlock_event"]!["event_seen"] = true),
            ("center-asset", state => state["world_progress"]!["community_center"]!["value"]!["lifecycle"]!["initial_unlock_event"]!["event_script_sha256"] = "modified"),
            ("center-unknown", state => state["world_progress"]!["community_center"]!["value"]!["lifecycle"]!["future_progress"] = 1)
        };
        foreach (var mutation in mutations)
        {
            var after = MutateContextBoundary(before, mutation.Mutate);
            Require(!ContextBoundariesMatch(before, after, false) &&
                    !ContextBoundariesMatch(before, after, true),
                "Persistent boundary ignored " + mutation.Name + ".");
        }

        foreach (var field in new[] { "tool_upgrade", "island_gourmand" })
        {
            var domain = field == "tool_upgrade" ? "player" : "world_progress";
            var unsupported = MutateContextBoundary(before, state =>
                state[domain]![field]!["value"]!["schema_version"] = "unknown.v2");
            var changed = MutateContextBoundary(unsupported, state =>
                state[domain]![field]!["value"]![field == "tool_upgrade"
                    ? "current_location_id" : "menu_clear"] = "unknown-context");
            Require(!ContextBoundariesMatch(unsupported, changed, false) &&
                    !ContextBoundariesMatch(unsupported, changed, true),
                "Unsupported projection schema received runtime normalization.");
        }
        Console.WriteLine("PASS: 21 contextual boundary cases across strict and resource-mutation views.");
    }

    private static SnapshotEnvelope MutateContextBoundary(
        SnapshotEnvelope source, Action<JsonObject> mutate)
    {
        var snapshot = CloneSnapshot(source);
        var state = JsonSerializer.SerializeToNode(snapshot.State, JsonDefaults.Options)!.AsObject();
        mutate(state);
        foreach (var domain in state)
            snapshot.State[domain.Key] = JsonSerializer.SerializeToElement(domain.Value, JsonDefaults.Options);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    private static bool ContextBoundariesMatch(
        SnapshotEnvelope before, SnapshotEnvelope after, bool resourceMutation)
    {
        if (resourceMutation)
            return FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(before, out var first) &&
                FullShipmentRecoverySettlementBoundaryProjector.TryProjectExpectedResourceMutationInvariant(after, out var second) && first == second;
        return FullShipmentRecoverySettlementBoundaryProjector.TryProject(before, out var initial) &&
            FullShipmentRecoverySettlementBoundaryProjector.TryProject(after, out var final) && initial == final;
    }
}
