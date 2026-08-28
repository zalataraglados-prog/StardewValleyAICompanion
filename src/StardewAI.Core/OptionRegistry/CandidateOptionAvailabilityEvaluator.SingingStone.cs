using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] SingingStoneCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var safeContext = ReadNativeObjectSafeItemContext(snapshot);
        var safeSlotKind = safeContext.SafeSlotKind;
        var safeSlot = safeContext.AllowsEmptyOrTool ? safeContext.SafeSlotIndex : -1;
        var restoreSlot = safeContext.RestoreSlotIndex;
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");

        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("singing_stone_interaction", out var interaction) &&
                interaction.ValueKind == JsonValueKind.Object)
            .Select(row => BuildSingingStoneCandidate(
                snapshot, row, row.GetProperty("singing_stone_interaction"), locationId,
                playerX, playerY, safeSlot, restoreSlot, safeSlotKind))
            .OrderBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildSingingStoneCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        JsonElement interaction,
        string locationId,
        int playerX,
        int playerY,
        int safeSlot,
        int restoreSlot,
        string safeSlotKind)
    {
        var reasons = new List<string>();
        if (!string.Equals(ReadString(interaction, "status"), "ready", StringComparison.Ordinal))
            reasons.Add("singing_stone_not_ready:" + ReadString(interaction, "status"));
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
            reasons.Add("singing_stone_menu_must_be_clear");
        if (safeSlot is < 0 or > 11 || restoreSlot is < 0 or > 11 ||
            (safeSlotKind != "empty" && safeSlotKind != "tool"))
        {
            reasons.Add("singing_stone_safe_toolbar_slot_required");
        }

        var targetX = ReadInt(row, "tile_x");
        var targetY = ReadInt(row, "tile_y");
        var stand = SelectNearestAvailableNativeObjectStand(interaction, playerX, playerY);
        if (stand is null)
            reasons.Add("singing_stone_no_reachable_adjacent_stand");
        var parameters = SingingStoneCandidateParameters(
            row, interaction, locationId, stand, safeSlot, restoreSlot, safeSlotKind);
        if (stand is not null && safeSlot >= 0 && restoreSlot >= 0)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "world.play_singing_stone",
                Parameters = parameters
            }));
        }

        return new EventCandidate
        {
            CandidateId = "singing-stone:" + locationId + ":" + targetX + "," + targetY,
            Kind = "play_singing_stone",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = targetX,
            TileY = targetY,
            DisplayName = "Play Singing Stone",
            ExpectedEffect = "native_sound=crystal;pitch_distribution=uniform_0_2300_step_100" +
                ";current_location.objects[" + targetX + "," + targetY + "].shake_timer=100" +
                ";item_identity_unchanged=true;fresh_snapshot_replan_required=true",
            EstimatedTicks = stand is null ? 90 : Math.Max(90, stand.Distance * 60 + 90),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_player_command_singing_stone",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] SingingStoneCandidateParameters(
        JsonElement row,
        JsonElement interaction,
        string locationId,
        NativeObjectStand? stand,
        int safeSlot,
        int restoreSlot,
        string safeSlotKind)
    {
        if (stand is null || safeSlot < 0 || restoreSlot < 0 ||
            (safeSlotKind != "empty" && safeSlotKind != "tool"))
            return Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", ReadInt(row, "tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(row, "tile_y").ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("target_runtime_type", ReadString(interaction, "target_runtime_type")),
            Parameter("item_id", ReadString(interaction, "canonical_item_id")),
            Parameter("qualified_item_id", ReadString(interaction, "canonical_qualified_item_id")),
            Parameter("safe_slot_index", safeSlot.ToString()),
            Parameter("safe_slot_kind", safeSlotKind),
            Parameter("restore_slot_index", restoreSlot.ToString()),
            Parameter("singing_stone_sound_name", ReadString(interaction, "sound_name")),
            Parameter("singing_stone_pitch_rng_source", ReadString(interaction, "pitch_rng_source")),
            Parameter("singing_stone_exact_next_pitch_status", ReadString(interaction, "exact_next_pitch_status")),
            Parameter("singing_stone_pitch_min", ReadInt(interaction, "pitch_min_inclusive").ToString()),
            Parameter("singing_stone_pitch_max", ReadInt(interaction, "pitch_max_inclusive").ToString()),
            Parameter("singing_stone_pitch_step", ReadInt(interaction, "pitch_step").ToString()),
            Parameter("singing_stone_pitch_outcome_count", ReadInt(interaction, "pitch_outcome_count").ToString()),
            Parameter("singing_stone_expected_shake_timer", ReadInt(interaction, "expected_shake_timer_immediately_after_action").ToString()),
            Parameter("singing_stone_expected_location_action_return", ReadBool(interaction, "expected_native_location_action_return") == true ? "true" : "false"),
            Parameter("interaction_kind", ReadString(interaction, "interaction_kind")),
            Parameter("expected_action_type", ReadString(interaction, "expected_action_type")),
            Parameter("native_contract", ReadString(interaction, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

}
