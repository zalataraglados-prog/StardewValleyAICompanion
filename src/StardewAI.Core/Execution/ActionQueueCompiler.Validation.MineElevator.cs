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
    private static string[] ValidateMineElevatorMenuPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var state = ReadStateFieldValue(snapshot, "menus", "menu_specific_state");
        if (!state.HasValue || state.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(state.Value, "kind"), "mine_elevator", StringComparison.Ordinal))
            return new[] { "mine_elevator_transparent_menu_state_missing" };

        var menu = state.Value;
        var reasons = new List<string>();
        var target = ReadIntParameter(action, "expected_mine_level_after") ?? ReadIntParameter(action, "target_depth");
        if (!target.HasValue)
            reasons.Add("mine_elevator_target_depth_required");
        else if (target.Value != 0 && (target.Value < 5 || target.Value > 120 || target.Value % 5 != 0))
            reasons.Add("mine_elevator_target_checkpoint_invalid");
        if (target == 0 && ReadBool(menu, "is_current_location_mineshaft") != true)
            reasons.Add("mine_elevator_floor_zero_requires_loaded_mineshaft");
        if (target == ReadInt(menu, "current_mine_level", -1))
            reasons.Add("mine_elevator_target_is_current_level");

        var identity = ReadString(menu, "menu_identity_sha256");
        if (string.IsNullOrWhiteSpace(identity) ||
            !string.Equals(ReadParameter(action, "mine_elevator_menu_identity_sha256"), identity, StringComparison.Ordinal) ||
            !string.Equals(ReadParameter(action, "target_runtime_identity"), identity, StringComparison.Ordinal))
            reasons.Add("mine_elevator_menu_identity_mismatch");
        if (!string.Equals(ReadParameter(action, "target_runtime_type"), "MineElevatorMenu", StringComparison.Ordinal))
            reasons.Add("mine_elevator_runtime_type_mismatch");
        if (!string.Equals(ReadParameter(action, "target_location_family"), "ordinary_mines", StringComparison.Ordinal))
            reasons.Add("mine_elevator_requires_ordinary_mines_family");

        var selectable = target.HasValue && menu.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array &&
            entries.EnumerateArray().Any(entry => ReadInt(entry, "floor", -1) == target.Value && ReadBool(entry, "selectable") == true);
        if (!selectable)
            reasons.Add("mine_elevator_target_not_selectable_in_live_menu");
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }
}
