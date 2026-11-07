using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class FullShipmentRecoverySettlementBoundaryProjector
{
    private static bool HasBoundaryProjectionSchema(JsonNode value, string expected)
        => value is JsonObject projection &&
           projection["schema_version"] is JsonValue schema &&
           schema.TryGetValue<string>(out var actual) && actual == expected;

    private static void NormalizeToolUpgradeRuntimeState(JsonNode value)
    {
        if (!HasBoundaryProjectionSchema(value, "tool_upgrade.v1"))
            return;
        RemoveFields(value,
            "current_location_id",
            "actor_control_ready",
            "service_status",
            "lifecycle_stage",
            "projection_fingerprint");
        if (value["clint"] is JsonObject clint)
            RemoveFields(clint, "present_at_blacksmith", "current_location",
                "tile_x", "tile_y");
    }

    private static void NormalizeToolUpgradeResourceProjection(JsonNode value)
    {
        if (!HasBoundaryProjectionSchema(value, "tool_upgrade.v1"))
            return;
        // These duplicate the inventory/currency changes proved by the caller.
        RemoveFields(value, "free_inventory_slots", "money_before");
        if (value["pending_tool"] is JsonObject pending)
            RemoveFields(pending, "claim_capacity_satisfied");
        if (value["next_axe_upgrade"] is JsonObject next)
            RemoveFields(next, "money_available", "trade_item_available",
                "requirements_satisfied");
    }

    private static void NormalizeCommunityCenterLifecycleRuntimeState(JsonNode value)
    {
        if (value is not JsonObject center ||
            center["lifecycle"] is not JsonObject lifecycle ||
            !HasBoundaryProjectionSchema(lifecycle, "community_center_lifecycle.v1"))
            return;
        // Stage mixes persistent progress with an event's in-flight animation.
        // Keep all underlying mail, seen/completion flags, assets and unknowns.
        RemoveFields(lifecycle, "stage");
        foreach (var name in new[]
                 { "initial_unlock_event", "junimo_text_event", "final_ceremony_event" })
        {
            if (lifecycle[name] is JsonObject asset)
                RemoveFields(asset, "event_active");
        }
    }
}
