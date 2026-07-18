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
    private static CompiledActionStep[] CompilePetInteractStep(SmallModelAction action)
    {
        var petId = ReadParameter(action, "target_runtime_identity");
        if (string.IsNullOrWhiteSpace(petId))
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "pet_interact",
                "pet:" + petId + ":native_checkAction",
                "farm.pets[" + petId + "].last_pet_day=current_day;friendship=" + ReadParameter(action, "expected_friendship_after") + ";times_pet=" + ReadParameter(action, "expected_times_pet_after") + ";quests.mail_received.petLoveMessage=" + ReadParameter(action, "expected_pet_love_mail_after") + ";quests.mail_received_or_pending.MarniePetAdoption=" + ReadParameter(action, "expected_marnie_pet_adoption_mail_after_or_pending"),
                120)
        };
    }

    private static CompiledActionStep[] CompileFillPetBowlStep(SmallModelAction action)
    {
        var x = ReadIntParameter(action, "target_tile_x");
        var y = ReadIntParameter(action, "target_tile_y");
        if (!x.HasValue || !y.HasValue)
        {
            return Array.Empty<CompiledActionStep>();
        }
        return new[]
        {
            Step(
                "fill_pet_bowl",
                "pet_bowl:" + x.Value + "," + y.Value + ":native_watering_can",
                "farm.pet_bowls[" + x.Value + "," + y.Value + "].watered=true;friendship_settlement=next_day:" + ReadParameter(action, "expected_next_day_friendship_after") + ";next_day_MarniePetAdoption=" + ReadParameter(action, "expected_next_day_marnie_pet_adoption_mail"),
                120)
        };
    }

    private static string[] ValidatePetCarePlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        return action.OptionId switch
        {
            "executor.pet_interact" => ValidatePetInteractionPlan(action, snapshot),
            "executor.fill_pet_bowl" => ValidateFillPetBowlPlan(action, snapshot),
            _ => Array.Empty<string>()
        };
    }

    private static string[] ValidatePetInteractionPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var reasons = new List<string>();
        var petId = ReadParameter(action, "target_runtime_identity");
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var safeSlot = ReadIntParameter(action, "safe_slot_index");
        var friendshipBefore = ReadIntParameter(action, "expected_friendship_before");
        var friendshipAfter = ReadIntParameter(action, "expected_friendship_after");
        var lastDayBeforeText = ReadParameter(action, "expected_last_pet_day_before");
        var lastDayBefore = ReadIntParameter(action, "expected_last_pet_day_before");
        var lastDayAfter = ReadIntParameter(action, "expected_last_pet_day_after");
        var timesBefore = ReadIntParameter(action, "expected_times_pet_before");
        var timesAfter = ReadIntParameter(action, "expected_times_pet_after");
        if (!Guid.TryParse(petId, out _) || !targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue ||
            !safeSlot.HasValue || safeSlot.Value is < 0 or > 11 || !friendshipBefore.HasValue || !friendshipAfter.HasValue ||
            (lastDayBeforeText != "missing" && !lastDayBefore.HasValue) ||
            !lastDayAfter.HasValue || !timesBefore.HasValue || !timesAfter.HasValue || timesAfter.Value != timesBefore.Value + 1 ||
            !TryBoolParameter(action, "expected_granted_friendship_before", out var grantedBefore) || grantedBefore ||
            !TryBoolParameter(action, "expected_granted_friendship_after", out var grantedAfter) || !grantedAfter ||
            !TryBoolParameter(action, "expected_pet_love_mail_before", out var mailBefore) ||
            !TryBoolParameter(action, "expected_pet_love_mail_after", out var mailAfter) ||
            !TryBoolParameter(action, "expected_marnie_pet_adoption_mail_before_or_pending", out var adoptionMailBefore) ||
            !TryBoolParameter(action, "expected_marnie_pet_adoption_mail_after_or_pending", out var adoptionMailAfter) ||
            !TryBoolParameter(action, "pet_gift_trigger_expected", out var giftTrigger) ||
            friendshipAfter.Value != Math.Min(1000, friendshipBefore.Value + 12) ||
            mailAfter != (mailBefore || friendshipAfter.Value >= 1000) ||
            adoptionMailAfter != (adoptionMailBefore || friendshipAfter.Value >= 1000) ||
            ReadParameter(action, "pet_gift_selection_status") != (giftTrigger ? "runtime_observed_global_rng_selection" : "not_triggered"))
        {
            return new[] { "pet_interaction_typed_projection_required" };
        }
        if (Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
        {
            reasons.Add("pet_interaction_stand_tile_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("pet_interaction_menu_must_be_clear");
        }
        var targetLocation = ReadParameter(action, "target_location");
        if (!string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("pet_interaction_target_location_mismatch");
        }

        var row = FindPetRow(snapshot, petId);
        if (!row.HasValue || row.Value.ValueKind != JsonValueKind.Object || ReadString(row.Value, "action_status") != "ready" ||
            ReadInt(row.Value, "tile_x") != targetX.Value || ReadInt(row.Value, "tile_y") != targetY.Value ||
            !string.Equals(ReadString(row.Value, "location_id"), targetLocation, StringComparison.OrdinalIgnoreCase) ||
            ReadString(row.Value, "runtime_type") != ReadParameter(action, "target_runtime_type") ||
            ReadInt(row.Value, "friendship_toward_farmer") != friendshipBefore.Value ||
            ReadInt(row.Value, "friendship_after_daily_interaction") != friendshipAfter.Value ||
            NullableReadInt(row.Value, "last_pet_day_for_player") != lastDayBefore ||
            ReadInt(row.Value, "times_pet_before") != timesBefore.Value ||
            ReadInt(row.Value, "times_pet_after_daily_interaction") != timesAfter.Value ||
            ReadInt(row.Value, "current_total_days") != lastDayAfter.Value ||
            ReadInt(row.Value, "safe_slot_index") != safeSlot.Value ||
            ReadBool(row.Value, "granted_friendship_for_pet") != grantedBefore ||
            ReadBool(row.Value, "granted_friendship_after_daily_interaction") != grantedAfter ||
            ReadBool(row.Value, "pet_love_mail_before") != mailBefore ||
            ReadBool(row.Value, "pet_love_mail_after_daily_interaction") != mailAfter ||
            ReadBool(row.Value, "gift_trigger_will_succeed") != giftTrigger ||
            ReadBool(row.Value, "marnie_pet_adoption_mail_before_or_pending") != adoptionMailBefore ||
            ReadBool(row.Value, "marnie_pet_adoption_mail_after_daily_interaction") != adoptionMailAfter)
        {
            reasons.Add("pet_interaction_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static string[] ValidateFillPetBowlPlan(SmallModelAction action, SnapshotEnvelope snapshot)
    {
        var reasons = new List<string>();
        var targetX = ReadIntParameter(action, "target_tile_x");
        var targetY = ReadIntParameter(action, "target_tile_y");
        var standX = ReadIntParameter(action, "stand_tile_x");
        var standY = ReadIntParameter(action, "stand_tile_y");
        var buildingX = ReadIntParameter(action, "building_tile_x");
        var buildingY = ReadIntParameter(action, "building_tile_y");
        var toolSlot = ReadIntParameter(action, "tool_slot_index");
        var expectedEnergyCost = ReadDoubleParameter(action, "expected_energy_cost");
        var expectedWaterBefore = ReadIntParameter(action, "expected_water_before");
        var expectedWaterAfter = ReadIntParameter(action, "expected_water_after");
        var friendshipBefore = ReadIntParameter(action, "expected_friendship_before");
        var friendshipAfter = ReadIntParameter(action, "expected_next_day_friendship_after");
        if (!targetX.HasValue || !targetY.HasValue || !standX.HasValue || !standY.HasValue || !buildingX.HasValue || !buildingY.HasValue ||
            !toolSlot.HasValue || toolSlot.Value < 0 || !expectedEnergyCost.HasValue || expectedEnergyCost.Value < 0d ||
            !expectedWaterBefore.HasValue || !expectedWaterAfter.HasValue ||
            !TryBoolParameter(action, "expected_watering_can_bottomless", out var bottomless) ||
            expectedWaterAfter.Value != (bottomless ? expectedWaterBefore.Value : expectedWaterBefore.Value - 1) ||
            ReadParameter(action, "required_tool_kind") != "Watering Can" ||
            !friendshipBefore.HasValue || !friendshipAfter.HasValue || friendshipAfter.Value != Math.Min(1000, friendshipBefore.Value + 6) ||
            !TryBoolParameter(action, "expected_bowl_watered_before", out var wateredBefore) || wateredBefore ||
            !TryBoolParameter(action, "expected_bowl_watered_after", out var wateredAfter) || !wateredAfter ||
            !TryBoolParameter(action, "expected_pet_love_mail_before", out var mailBefore) ||
            !TryBoolParameter(action, "expected_next_day_pet_love_mail", out var mailAfter) || mailAfter != (mailBefore || friendshipAfter.Value >= 1000) ||
            !TryBoolParameter(action, "expected_marnie_pet_adoption_mail_before_or_pending", out var adoptionMailBefore) ||
            !TryBoolParameter(action, "expected_next_day_marnie_pet_adoption_mail", out var adoptionMailAfter) ||
            adoptionMailAfter != (adoptionMailBefore || friendshipAfter.Value >= 1000) ||
            ReadParameter(action, "delayed_settlement") != "Pet.dayUpdate consumes watered=true and applies min(1000,friendship+6)")
        {
            return new[] { "fill_pet_bowl_typed_projection_required" };
        }
        if (Math.Abs(targetX.Value - standX.Value) + Math.Abs(targetY.Value - standY.Value) != 1)
        {
            reasons.Add("fill_pet_bowl_stand_tile_not_adjacent");
        }
        if (ActionSeesActiveMenuOpen(action, snapshot))
        {
            reasons.Add("fill_pet_bowl_menu_must_be_clear");
        }
        var targetLocation = ReadParameter(action, "target_location");
        if (!string.Equals(targetLocation, ReadStateFieldString(snapshot, "player", "location_id"), StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("fill_pet_bowl_target_location_mismatch");
        }

        var row = FindPetBowlRow(snapshot, targetLocation, buildingX.Value, buildingY.Value);
        if (!row.HasValue || row.Value.ValueKind != JsonValueKind.Object || ReadString(row.Value, "action_status") != "ready" ||
            NullableReadInt(row.Value, "action_tile_x") != targetX || NullableReadInt(row.Value, "action_tile_y") != targetY ||
            ReadInt(row.Value, "watering_can_slot_index") != toolSlot.Value || ReadBool(row.Value, "watered") != false ||
            Math.Abs(ReadDouble(row.Value, "watering_energy_cost") - expectedEnergyCost.Value) > 0.001d ||
            ReadInt(row.Value, "watering_can_water_left") != expectedWaterBefore.Value ||
            ReadInt(row.Value, "expected_watering_can_water_after") != expectedWaterAfter.Value ||
            ReadBool(row.Value, "watering_can_bottomless") != bottomless ||
            ReadString(row.Value, "runtime_type") != ReadParameter(action, "target_runtime_type") ||
            ReadString(row.Value, "assigned_pet_id") != ReadParameter(action, "target_runtime_identity") ||
            NullableReadInt(row.Value, "friendship_before_next_day") != friendshipBefore ||
            NullableReadInt(row.Value, "friendship_after_fill_and_next_day_update") != friendshipAfter ||
            ReadBool(row.Value, "pet_love_mail_before") != mailBefore ||
            ReadBool(row.Value, "pet_love_mail_after_fill_and_next_day_update") != mailAfter ||
            ReadBool(row.Value, "marnie_pet_adoption_mail_before_or_pending") != adoptionMailBefore ||
            ReadBool(row.Value, "marnie_pet_adoption_mail_after_fill_and_next_day_update") != adoptionMailAfter)
        {
            reasons.Add("fill_pet_bowl_projection_drifted");
        }
        return reasons.Distinct(StringComparer.Ordinal).ToArray();
    }

    private static JsonElement? FindPetRow(SnapshotEnvelope snapshot, string petId)
    {
        var rows = ReadStateFieldValue(snapshot, "farm", "pets");
        return rows.HasValue && rows.Value.ValueKind == JsonValueKind.Array
            ? rows.Value.EnumerateArray().FirstOrDefault(row => ReadString(row, "pet_id") == petId)
            : null;
    }

    private static JsonElement? FindPetBowlRow(SnapshotEnvelope snapshot, string locationId, int buildingX, int buildingY)
    {
        var rows = ReadStateFieldValue(snapshot, "farm", "pet_bowls");
        return rows.HasValue && rows.Value.ValueKind == JsonValueKind.Array
            ? rows.Value.EnumerateArray().FirstOrDefault(row =>
                string.Equals(ReadString(row, "location_id"), locationId, StringComparison.OrdinalIgnoreCase) &&
                ReadInt(row, "building_tile_x") == buildingX && ReadInt(row, "building_tile_y") == buildingY)
            : null;
    }

    private static bool TryBoolParameter(SmallModelAction action, string name, out bool value)
    {
        return bool.TryParse(ReadParameter(action, name), out value);
    }
}
