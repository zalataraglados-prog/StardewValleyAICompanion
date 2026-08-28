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
    private EventCandidate[] FarmComputerReportCandidates(SnapshotEnvelope snapshot)
    {
        var objects = ReadStateFieldValue(snapshot, "current_location", "objects");
        if (!objects.HasValue || objects.Value.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();

        var safeContext = ReadNativeObjectSafeItemContext(snapshot);
        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return objects.Value.EnumerateArray()
            .Where(row => row.TryGetProperty("farm_computer_report", out var report) &&
                report.ValueKind == JsonValueKind.Object)
            .Select(row => BuildFarmComputerReportCandidate(
                snapshot, row, row.GetProperty("farm_computer_report"), locationId,
                playerX, playerY, safeContext))
            .OrderBy(candidate => candidate.TileY)
            .ThenBy(candidate => candidate.TileX)
            .ToArray();
    }

    private EventCandidate BuildFarmComputerReportCandidate(
        SnapshotEnvelope snapshot,
        JsonElement row,
        JsonElement report,
        string locationId,
        int playerX,
        int playerY,
        NativeObjectSafeItemContext safeContext)
    {
        var reasons = new List<string>();
        if (!string.Equals(ReadString(report, "status"), "ready", StringComparison.Ordinal))
            reasons.Add("farm_computer_not_ready:" + ReadString(report, "status"));
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
            reasons.Add("farm_computer_menu_must_be_clear");
        if (!safeContext.AllowsEmptyOrTool)
            reasons.Add("farm_computer_safe_toolbar_slot_required");

        var stand = SelectNearestAvailableNativeObjectStand(report, playerX, playerY);
        if (stand is null)
            reasons.Add("farm_computer_no_reachable_adjacent_stand");
        var parameters = FarmComputerCandidateParameters(
            row, report, locationId, stand, safeContext);
        if (stand is not null && safeContext.AllowsEmptyOrTool)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "farming.read_farm_computer_report",
                Parameters = parameters
            }));
        }

        var targetX = ReadInt(row, "tile_x");
        var targetY = ReadInt(row, "tile_y");
        return new EventCandidate
        {
            CandidateId = "farm-computer:" + locationId + ":" + targetX + "," + targetY,
            Kind = "read_farm_computer_report",
            Available = reasons.Count == 0,
            LocationId = locationId,
            TileX = targetX,
            TileY = targetY,
            DisplayName = "Read Farm Computer Report",
            ExpectedEffect = "native_dialogue=FarmComputer;report_sha256=" + ReadString(report, "report_sha256") +
                ";structured_information_already_transparent=true;selected_slot_restored=true",
            EstimatedTicks = stand is null ? 630 : Math.Max(630, stand.Distance * 60 + 630),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_player_command_farm_computer",
            BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
            Parameters = parameters
        };
    }

    private static SmallModelActionParameter[] FarmComputerCandidateParameters(
        JsonElement row,
        JsonElement report,
        string locationId,
        NativeObjectStand? stand,
        NativeObjectSafeItemContext safeContext)
    {
        if (stand is null || !safeContext.AllowsEmptyOrTool)
            return Array.Empty<SmallModelActionParameter>();
        return new[]
        {
            Parameter("target_location", locationId),
            Parameter("target_tile_x", ReadInt(row, "tile_x").ToString()),
            Parameter("target_tile_y", ReadInt(row, "tile_y").ToString()),
            Parameter("stand_tile_x", stand.X.ToString()),
            Parameter("stand_tile_y", stand.Y.ToString()),
            Parameter("target_runtime_type", ReadString(report, "target_runtime_type")),
            Parameter("item_id", ReadString(report, "canonical_item_id")),
            Parameter("qualified_item_id", ReadString(report, "canonical_qualified_item_id")),
            Parameter("safe_slot_index", safeContext.SafeSlotIndex.ToString()),
            Parameter("safe_slot_kind", safeContext.SafeSlotKind),
            Parameter("restore_slot_index", safeContext.RestoreSlotIndex.ToString()),
            Parameter("farm_computer_root_location_id", ReadString(report, "root_location_id")),
            Parameter("farm_computer_includes_hay", BoolWire(ReadBool(report, "includes_hay"))),
            Parameter("farm_computer_pieces_of_hay", NullableIntWire(ReadNullableInt(report, "pieces_of_hay"))),
            Parameter("farm_computer_hay_capacity", NullableIntWire(ReadNullableInt(report, "hay_capacity"))),
            Parameter("farm_computer_total_crops", ReadInt(report, "total_crops").ToString()),
            Parameter("farm_computer_crops_ready", ReadInt(report, "crops_ready_for_harvest").ToString()),
            Parameter("farm_computer_unwatered_crops", ReadInt(report, "unwatered_crops").ToString()),
            Parameter("farm_computer_greenhouse_crops_ready", NullableIntWire(ReadNullableInt(report, "greenhouse_crops_ready_for_harvest"))),
            Parameter("farm_computer_open_hoe_dirt", ReadInt(report, "total_open_hoe_dirt").ToString()),
            Parameter("farm_computer_total_forage", NullableIntWire(ReadNullableInt(report, "total_forage_items"))),
            Parameter("farm_computer_machines_ready", ReadInt(report, "machines_ready_for_harvest").ToString()),
            Parameter("farm_computer_farm_cave_ready", NullableBoolWire(ReadNullableBool(report, "farm_cave_needs_harvesting"))),
            Parameter("farm_computer_report_sha256", ReadString(report, "report_sha256")),
            Parameter("farm_computer_expected_delay_ms", ReadInt(report, "expected_delay_milliseconds").ToString()),
            Parameter("farm_computer_expected_shake_timer", ReadInt(report, "expected_shake_timer_immediately_after_action").ToString()),
            Parameter("farm_computer_expected_freeze_ms", ReadInt(report, "expected_player_freeze_milliseconds").ToString()),
            Parameter("farm_computer_expected_location_action_return", BoolWire(ReadBool(report, "expected_native_location_action_return"))),
            Parameter("interaction_kind", ReadString(report, "interaction_kind")),
            Parameter("expected_action_type", ReadString(report, "expected_action_type")),
            Parameter("native_contract", ReadString(report, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static string NullableIntWire(int? value) => value?.ToString() ?? string.Empty;
    private static string NullableBoolWire(bool? value) => value.HasValue ? BoolWire(value) : string.Empty;
    private static string BoolWire(bool? value) => value == true ? "true" : "false";
}
