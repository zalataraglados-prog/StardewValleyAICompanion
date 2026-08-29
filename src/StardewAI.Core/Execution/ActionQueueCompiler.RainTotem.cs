using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution;

public sealed partial class ActionQueueCompiler
{
    private const string RainTotemNativeContract =
        "Object.performUseAction((O)681)->Object.rainTotem->AllowRainTotem->RainTotemAffectsContext_or_location_context->Default_festival_guard_or_context_WeatherForTomorrow=Rain->Default_Game1.getWeatherModificationsForDate";

    private static CompiledActionStep[] CompileUseRainTotemStep(SmallModelAction action)
    {
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var location = ReadParameter(action, "target_location");
        if (!slot.HasValue || string.IsNullOrWhiteSpace(location))
            return Array.Empty<CompiledActionStep>();
        return new[]
        {
            Step("use_rain_totem", location + ":slot" + slot.Value + ":(O)681",
                "inventory_stack=" + ReadParameter(action, "inventory_stack_after") +
                ";affected_context=" + ReadParameter(action, "affected_location_context_id") +
                ";weather_for_tomorrow=Rain", 210)
        };
    }

    private static string[] ValidateUseRainTotemPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        if (action.OptionId != "executor.use_rain_totem")
            return Array.Empty<string>();

        var reasons = new List<string>();
        var slot = ReadIntParameter(action, "inventory_slot_index");
        var before = ReadIntParameter(action, "inventory_stack_before");
        var after = ReadIntParameter(action, "inventory_stack_after");
        if (!slot.HasValue || !before.HasValue || before < 1 || !after.HasValue ||
            !string.Equals(ReadParameter(action, "item_id"), "681", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "qualified_item_id"), "(O)681", StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal))
            return new[] { "use_rain_totem_typed_fields_required" };
        if (ActionSeesActiveMenuOpen(action, snapshot))
            reasons.Add("use_rain_totem_menu_must_be_clear");
        if (!TargetLocationMatchesCurrent(action, snapshot))
            reasons.Add("use_rain_totem_requires_loaded_target_location");

        var context = ReadStateFieldValue(snapshot, "player", "rain_totem");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
            return reasons.Append("use_rain_totem_projection_unavailable").Distinct(StringComparer.Ordinal).ToArray();
        if (!string.Equals(ReadParameter(action, "rain_totem_projection_fingerprint"),
                ReadString(context.Value, "projection_fingerprint"), StringComparison.Ordinal))
            reasons.Add("use_rain_totem_projection_fingerprint_drifted");
        if (!string.Equals(ReadString(context.Value, "native_use_gate_status"), "ready", StringComparison.Ordinal))
            reasons.Add("use_rain_totem_native_effect_gate_blocked");

        JsonElement? row = null;
        if (context.Value.TryGetProperty("rows", out var rows) && rows.ValueKind == JsonValueKind.Array)
            row = rows.EnumerateArray().FirstOrDefault(value => ReadInt(value, "inventory_slot_index", -1) == slot);
        if (!row.HasValue || !string.Equals(ReadString(row.Value, "item_id"), "681", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "qualified_item_id"), "(O)681", StringComparison.Ordinal) ||
            !string.Equals(ReadString(row.Value, "inventory_runtime_type"), "StardewValley.Object", StringComparison.Ordinal) ||
            ReadBool(row.Value, "temporarily_invisible") == true || after != before - 1 ||
            ReadInt(row.Value, "stack_before", -1) != before || ReadInt(row.Value, "stack_after", -1) != after)
            reasons.Add("use_rain_totem_inventory_identity_drifted");

        var routing = context.Value.GetProperty("context_routing");
        if (!string.Equals(ReadParameter(action, "source_location_context_id"), ReadString(routing, "source_location_context_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "configured_affected_context_id"), ReadString(routing, "configured_affected_context_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "affected_location_context_id"), ReadString(routing, "affected_location_context_id"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "weather_state_owner_context_id"), ReadString(routing, "weather_state_owner_context_id"), StringComparison.Ordinal) ||
            ReadBoolParameter(action, "allow_rain_totem") != ReadBool(routing, "allow_rain_totem"))
            reasons.Add("use_rain_totem_context_routing_drifted");

        var transition = context.Value.GetProperty("weather_transition");
        if (ReadBoolParameter(action, "tomorrow_is_default_festival") != ReadBool(transition, "tomorrow_is_default_festival") ||
            !string.Equals(ReadParameter(action, "affected_weather_before"), ReadString(transition, "affected_weather_before"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "affected_weather_after"), ReadString(transition, "affected_weather_after"), StringComparison.Ordinal) ||
            !string.Equals(ReadString(transition, "affected_weather_after"), "Rain", StringComparison.Ordinal) ||
            ReadIntParameter(action, "tomorrow_total_days") != ReadInt(transition, "tomorrow_total_days") ||
            !string.Equals(ReadParameter(action, "effective_tomorrow_weather"), ReadString(transition, "effective_tomorrow_weather"), StringComparison.Ordinal) ||
            ReadBoolParameter(action, "rain_will_take_effect_tomorrow") != ReadBool(transition, "rain_will_take_effect_tomorrow") ||
            ReadBool(transition, "rain_will_take_effect_tomorrow") != true)
            reasons.Add("use_rain_totem_weather_state_drifted");

        var animation = context.Value.GetProperty("animation_contract");
        if (ReadIntParameter(action, "native_facing_direction") != ReadInt(animation, "facing_direction") ||
            ReadIntParameter(action, "native_animation_duration_ms") != ReadInt(animation, "animation_duration_ms") ||
            ReadIntParameter(action, "native_cloud_sprite_count") != ReadInt(animation, "cloud_sprite_count") ||
            ReadIntParameter(action, "native_item_sprite_count") != ReadInt(animation, "item_sprite_count") ||
            ReadIntParameter(action, "native_cloud_batch_count") != ReadInt(animation, "cloud_batch_count") ||
            ReadIntParameter(action, "native_cloud_delay_step_ms") != ReadInt(animation, "cloud_delay_step_ms") ||
            !string.Equals(ReadParameter(action, "native_initial_sound"), ReadString(animation, "initial_sound"), StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "native_delayed_sound"), ReadString(animation, "delayed_sound"), StringComparison.Ordinal) ||
            ReadIntParameter(action, "native_delayed_sound_ms") != ReadInt(animation, "delayed_sound_ms"))
            reasons.Add("use_rain_totem_animation_contract_drifted");
        if (!string.Equals(ReadParameter(action, "native_contract"), RainTotemNativeContract, StringComparison.Ordinal) ||
            !string.Equals(ReadString(context.Value, "native_contract"), RainTotemNativeContract, StringComparison.Ordinal))
            reasons.Add("use_rain_totem_native_contract_drifted");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
