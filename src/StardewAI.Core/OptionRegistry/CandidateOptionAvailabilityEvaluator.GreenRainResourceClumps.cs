using System;
using System.Linq;
using System.Text.Json;
using System.Globalization;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] GreenRainResourceClumpCandidates(SnapshotEnvelope snapshot)
    {
        var clumps = ReadStateFieldValue(snapshot, "current_location", "resource_clumps");
        if (!clumps.HasValue || clumps.Value.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var locationId = ReadStateFieldString(snapshot, "player", "location_id");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return clumps.Value.EnumerateArray()
            .Where(clump => clump.ValueKind == JsonValueKind.Object &&
                string.Equals(ReadString(clump, "clear_kind"), "green_rain_bush", StringComparison.Ordinal))
            .Select(clump =>
            {
                var x = ReadInt(clump, "tile_x");
                var y = ReadInt(clump, "tile_y");
                var width = Math.Max(1, ReadInt(clump, "width"));
                var height = Math.Max(1, ReadInt(clump, "height"));
                var hits = Math.Max(1, ReadInt(clump, "expected_tool_hits_to_clear"));
                var standSelection = FindBestResourceClumpStandTile(snapshot, x, y, width, height);
                var stand = standSelection?.Stand;
                var hit = standSelection?.Hit;
                var parameters = new[]
                {
                    Parameter("target_tile_x", (hit?.X ?? x).ToString()),
                    Parameter("target_tile_y", (hit?.Y ?? y).ToString()),
                    Parameter("stand_tile_x", (stand?.X ?? x).ToString()),
                    Parameter("stand_tile_y", (stand?.Y ?? y).ToString()),
                    Parameter("resource_clump_tile_x", x.ToString()),
                    Parameter("resource_clump_tile_y", y.ToString()),
                    Parameter("resource_clump_width", width.ToString()),
                    Parameter("resource_clump_height", height.ToString()),
                    Parameter("resource_clump_parent_sheet_index", ReadInt(clump, "parent_sheet_index").ToString()),
                    Parameter("target_runtime_type", ReadString(clump, "runtime_type")),
                    Parameter("tool_slot_index", ReadInt(clump, "tool_slot_index").ToString()),
                    Parameter("required_tool_kind", "axe"),
                    Parameter("max_tool_swings", hits.ToString()),
                    Parameter("max_movement_tiles", "512"),
                    Parameter("expected_output_items_json", ReadString(clump, "expected_core_output_items_json")),
                    Parameter("expected_output_context_tag_sets_json", ReadString(clump, "expected_core_output_context_tag_sets_json")),
                    Parameter("expected_foraging_experience_delta", ReadInt(clump, "expected_foraging_experience_delta").ToString()),
                    Parameter("output_distribution_status", ReadString(clump, "output_distribution_status")),
                    Parameter("possible_secret_note_qualified_item_id", ReadString(clump, "possible_secret_note_qualified_item_id")),
                    Parameter("unseen_secret_note_count", ReadInt(clump, "unseen_secret_note_count").ToString(CultureInfo.InvariantCulture)),
                    Parameter("total_secret_note_count", ReadInt(clump, "total_secret_note_count").ToString(CultureInfo.InvariantCulture)),
                    Parameter("secret_note_outer_roll_probability", ReadDouble(clump, "secret_note_outer_roll_probability").ToString("R", CultureInfo.InvariantCulture)),
                    Parameter("secret_note_inner_roll_probability", ReadDouble(clump, "secret_note_inner_roll_probability").ToString("R", CultureInfo.InvariantCulture)),
                    Parameter("secret_note_combined_probability", ReadDouble(clump, "secret_note_combined_probability").ToString("R", CultureInfo.InvariantCulture)),
                    Parameter("secret_note_projection_status", ReadString(clump, "secret_note_projection_status")),
                    Parameter("native_contract", ReadString(clump, "native_contract")),
                    Parameter("skill_experience_skill_id", "foraging"),
                    Parameter("skill_experience_on_success_min", "15"),
                    Parameter("skill_experience_on_success_max", "15"),
                    Parameter("skill_experience_condition", "native_axe_destroys_exact_green_rain_resource_clump"),
                    Parameter("skill_experience_projection_status", "exact")
                };
                var blocks = CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                {
                    OptionId = "executor.break_current_location_resource_clump",
                    Parameters = parameters
                }).ToList();
                var status = ReadString(clump, "clear_obstacle_executor_status");
                if (!string.Equals(status, "ready", StringComparison.Ordinal))
                {
                    blocks.Add(string.IsNullOrWhiteSpace(status) ? "green_rain_resource_clump_projection_unavailable" : status);
                }
                if (stand is null)
                {
                    blocks.Add("green_rain_resource_clump_no_reachable_perimeter_stand");
                }

                var distance = stand is null ? 0 : Math.Abs(playerX - stand.X) + Math.Abs(playerY - stand.Y);
                var effect = (stand is null ? string.Empty : "resource_clump_stand_tile=" + stand.X + "," + stand.Y + ";") +
                    (hit is null ? string.Empty : "resource_clump_hit_tile=" + hit.X + "," + hit.Y + ";") +
                    "current_location.resource_clumps[" + x + "," + y + "].present=false" +
                    ";resource_clump_tile=" + x + "," + y +
                    ";resource_clump_width=" + width +
                    ";resource_clump_height=" + height +
                    ";resource_clump_parent_sheet_index=" + ReadInt(clump, "parent_sheet_index") +
                    ";tool_slot_index=" + ReadInt(clump, "tool_slot_index") +
                    ";required_tool_kind=axe" +
                    ";max_tool_swings=" + hits +
                    ";max_movement_tiles=512" +
                    ";expected_foraging_experience_delta=15";
                return new EventCandidate
                {
                    CandidateId = "clear-green-rain-clump:" + locationId + ":" + x + "," + y,
                    Kind = "clear_green_rain_resource_clump",
                    Available = blocks.Count == 0,
                    LocationId = locationId,
                    TileX = x,
                    TileY = y,
                    ExpectedEffect = effect,
                    EstimatedTicks = Math.Max(60, distance * 60 + hits * 60),
                    EnergyCost = hits * 2,
                    AvailabilityClass = "transparent_loaded_green_rain_resource_clump",
                    BlockReasons = blocks.Distinct(StringComparer.Ordinal).ToArray(),
                    Parameters = parameters
                };
            })
            .ToArray();
    }
}
