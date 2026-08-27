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
    private EventCandidate[] DwarfKingStatuePowerCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "current_location", "dwarf_king_statue_power");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var sharedReasons = new List<string>();
        var status = ReadString(projection.Value, "status");
        if (!string.Equals(status, "ready", StringComparison.Ordinal))
        {
            sharedReasons.Add("dwarf_king_statue_not_ready:" + status);
        }
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
        {
            sharedReasons.Add("dwarf_king_statue_menu_must_be_clear");
        }
        var target = SelectDwarfKingStatueTarget(projection.Value, snapshot);
        if (target is null)
        {
            sharedReasons.Add("dwarf_king_statue_no_reachable_adjacent_stand");
        }
        if (!projection.Value.TryGetProperty("offers", out var offers) || offers.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<EventCandidate>();
        }

        var rows = offers.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object)
            .ToArray();
        if (rows.Length != 2 || rows.Select(row => ReadInt(row, "power_id")).Distinct().Count() != 2)
        {
            sharedReasons.Add("dwarf_king_statue_exactly_two_distinct_offers_required");
        }

        return rows.Select(offer =>
        {
            var parameters = DwarfKingStatueCandidateParameters(projection.Value, offer, target);
            var reasons = new List<string>(sharedReasons);
            if (target is not null)
            {
                reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
                {
                    OptionId = "mining.choose_dwarf_statue_power",
                    Parameters = parameters
                }));
            }
            var powerId = ReadInt(offer, "power_id");
            var effect = offer.TryGetProperty("effect", out var effectRow) ? effectRow : default;
            return new EventCandidate
            {
                CandidateId = "dwarf-king-statue:" + ReadString(projection.Value, "location_id") + ":" +
                    target?.TargetX + "," + target?.TargetY + ":power:" + powerId,
                Kind = "choose_dwarf_statue_power",
                Available = reasons.Count == 0,
                LocationId = ReadString(projection.Value, "location_id"),
                TileX = target?.TargetX ?? 0,
                TileY = target?.TargetY ?? 0,
                DisplayName = ReadString(offer, "display_text"),
                ExpectedEffect = "player.active_buff=" + ReadString(offer, "buff_id") +
                    ";effect_kind=" + (effect.ValueKind == JsonValueKind.Object ? ReadString(effect, "kind") : string.Empty) +
                    ";valid_until_day_end=true;fresh_snapshot_replan_required=true",
                EstimatedTicks = target is null ? 120 : Math.Max(120, target.Distance * 60 + 120),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_daily_dwarf_king_power_choice",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            };
        }).ToArray();
    }

    private static SmallModelActionParameter[] DwarfKingStatueCandidateParameters(
        JsonElement projection,
        JsonElement offer,
        DwarfKingStatueTarget? target)
    {
        if (target is null)
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        var effect = offer.GetProperty("effect");
        return new[]
        {
            Parameter("dwarf_statue_power_id", ReadInt(offer, "power_id").ToString()),
            Parameter("dwarf_statue_power_source", "small_model_exact_offered_choice"),
            Parameter("dwarf_statue_menu_index", ReadInt(offer, "menu_index").ToString()),
            Parameter("dwarf_statue_buff_id", ReadString(offer, "buff_id")),
            Parameter("dwarf_statue_display_text", ReadString(offer, "display_text")),
            Parameter("dwarf_statue_effect_kind", ReadString(effect, "kind")),
            Parameter("dwarf_statue_exact_effect", ReadString(effect, "exact_effect")),
            Parameter("dwarf_statue_offered_power_ids_csv", ReadString(projection, "offered_power_ids_csv")),
            Parameter("dwarf_statue_days_played", ReadInt(projection, "days_played").ToString()),
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("target_runtime_type", target.RuntimeType),
            Parameter("qualified_item_id", ReadString(projection, "qualified_item_id")),
            Parameter("expected_menu_type_after", ReadString(projection, "expected_menu_type")),
            Parameter("interaction_kind", "location_object"),
            Parameter("expected_action_type", "StatueOfTheDwarfKing"),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static DwarfKingStatueTarget? SelectDwarfKingStatueTarget(JsonElement projection, SnapshotEnvelope snapshot)
    {
        if (!projection.TryGetProperty("statues", out var statues) || statues.ValueKind != JsonValueKind.Array)
        {
            return null;
        }
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return statues.EnumerateArray()
            .SelectMany(statue => statue.TryGetProperty("stand_tiles", out var stands) && stands.ValueKind == JsonValueKind.Array
                ? stands.EnumerateArray()
                    .Where(stand => ReadBool(stand, "available") == true)
                    .Select(stand => new DwarfKingStatueTarget(
                        ReadInt(statue, "tile_x"), ReadInt(statue, "tile_y"),
                        ReadInt(stand, "tile_x"), ReadInt(stand, "tile_y"),
                        ReadString(statue, "target_runtime_type"),
                        Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
                : Enumerable.Empty<DwarfKingStatueTarget>())
            .OrderBy(row => row.Distance)
            .ThenBy(row => row.TargetY)
            .ThenBy(row => row.TargetX)
            .ThenBy(row => row.StandY)
            .ThenBy(row => row.StandX)
            .FirstOrDefault();
    }

    private sealed record DwarfKingStatueTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        string RuntimeType,
        int Distance);
}
