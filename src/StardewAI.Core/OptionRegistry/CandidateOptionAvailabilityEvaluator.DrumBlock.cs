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
    private EventCandidate[] DrumBlockCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();
        var safe = ReadNativeObjectSafeItemContext(snapshot);
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("drum_block_tuning", out var projection) && projection.ValueKind == JsonValueKind.Object)
            .Select(row => BuildDrumBlockCandidate(snapshot, row, row.GetProperty("drum_block_tuning"), locationId, playerX, playerY, safe))
            .OrderBy(row => row.TileY).ThenBy(row => row.TileX).ToArray();
    }

    private EventCandidate BuildDrumBlockCandidate(SnapshotEnvelope snapshot, JsonElement row, JsonElement projection,
        string locationId, int playerX, int playerY, NativeObjectSafeItemContext safe)
    {
        var reasons = new List<string>();
        if (ReadString(projection, "status") != "ready") reasons.Add("drum_block_not_ready:" + ReadString(projection, "status"));
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase)) reasons.Add("drum_block_menu_must_be_clear");
        if (!safe.AllowsEmptyOrTool) reasons.Add("drum_block_safe_toolbar_slot_required");
        var stand = SelectNearestAvailableNativeObjectStand(projection, playerX, playerY);
        if (stand is null) reasons.Add("drum_block_no_reachable_adjacent_stand");
        var parameters = DrumBlockParameters(row, projection, locationId, stand, safe);
        if (stand is not null && safe.AllowsEmptyOrTool)
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate { OptionId = "world.tune_drum_block", Parameters = parameters }));
        var x = ReadInt(row, "tile_x");
        var y = ReadInt(row, "tile_y");
        return new EventCandidate
        {
            CandidateId = $"drum-block:{locationId}:{x},{y}", Kind = "tune_drum_block", Available = reasons.Count == 0,
            LocationId = locationId, TileX = x, TileY = y, DisplayName = "Tune Drum Block",
            ExpectedEffect = $"tone={ReadInt(projection, "next_tone")};sound={ReadString(projection, "sound_cue")};shake_timer=200;scale_y=1.3;selected_slot_restored=true",
            EstimatedTicks = stand is null ? 90 : Math.Max(90, stand.Distance * 60 + 90), EnergyCost = 0,
            AvailabilityClass = "transparent_native_player_command_drum_block", BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(), Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] DrumBlockParameters(JsonElement row, JsonElement p, string locationId,
        NativeObjectStand? stand, NativeObjectSafeItemContext safe)
    {
        if (stand is null || !safe.AllowsEmptyOrTool) return Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            Parameter("target_location", locationId), Parameter("target_tile_x", ReadInt(row, "tile_x").ToString()), Parameter("target_tile_y", ReadInt(row, "tile_y").ToString()),
            Parameter("stand_tile_x", stand.X.ToString()), Parameter("stand_tile_y", stand.Y.ToString()), Parameter("target_runtime_type", ReadString(p, "target_runtime_type")),
            Parameter("item_id", ReadString(p, "canonical_item_id")), Parameter("qualified_item_id", ReadString(p, "canonical_qualified_item_id")),
            Parameter("safe_slot_index", safe.SafeSlotIndex.ToString()), Parameter("safe_slot_kind", safe.SafeSlotKind), Parameter("restore_slot_index", safe.RestoreSlotIndex.ToString()),
            Parameter("drum_block_current_tone_raw", ReadString(p, "current_tone_raw")), Parameter("drum_block_current_tone", ReadInt(p, "current_tone_parsed").ToString()),
            Parameter("drum_block_next_tone", ReadInt(p, "next_tone").ToString()), Parameter("drum_block_tone_min", ReadInt(p, "tone_min_inclusive").ToString()),
            Parameter("drum_block_tone_max", ReadInt(p, "tone_max_inclusive").ToString()), Parameter("drum_block_tone_step", ReadInt(p, "tone_step").ToString()),
            Parameter("drum_block_tone_state_count", ReadInt(p, "tone_state_count").ToString()), Parameter("drum_block_sound_cue", ReadString(p, "sound_cue")),
            Parameter("drum_block_expected_shake_timer", ReadInt(p, "expected_shake_timer_immediately_after_action").ToString()),
            Parameter("drum_block_expected_scale_y", ReadDouble(p, "expected_scale_y_immediately_after_action").ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            Parameter("drum_block_expected_location_action_return", ReadBool(p, "expected_native_location_action_return") == true ? "true" : "false"),
            Parameter("interaction_kind", ReadString(p, "interaction_kind")), Parameter("expected_action_type", ReadString(p, "expected_action_type")),
            Parameter("native_contract", ReadString(p, "native_contract")), Parameter("max_movement_tiles", "512")
        };
    }
}
