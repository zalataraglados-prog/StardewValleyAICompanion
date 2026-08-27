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
    private EventCandidate[] StatueBlessingCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "current_location", "statue_blessing");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<EventCandidate>();
        }

        var reasons = new List<string>();
        var status = ReadString(projection.Value, "status");
        if (!string.Equals(status, "ready", StringComparison.Ordinal))
        {
            reasons.Add("statue_blessing_not_ready:" + status);
        }
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("statue_blessing_menu_must_be_clear");
        }
        var target = SelectStatueBlessingTarget(projection.Value, snapshot);
        if (target is null)
        {
            reasons.Add("statue_blessing_no_reachable_adjacent_stand");
        }
        var parameters = StatueBlessingCandidateParameters(projection.Value, target);
        if (target is not null)
        {
            reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
            {
                OptionId = "rewards.claim_statue_blessing",
                Parameters = parameters
            }));
        }

        var blessing = projection.Value.TryGetProperty("blessing", out var row) ? row : default;
        var blessingId = ReadInt(projection.Value, "blessing_id");
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "statue-blessing:" + ReadString(projection.Value, "location_id") + ":" +
                    target?.TargetX + "," + target?.TargetY + ":day:" + ReadInt(projection.Value, "days_played"),
                Kind = "claim_statue_blessing",
                Available = reasons.Count == 0,
                LocationId = ReadString(projection.Value, "location_id"),
                TileX = target?.TargetX ?? 0,
                TileY = target?.TargetY ?? 0,
                DisplayName = "Statue blessing " + blessingId,
                ExpectedEffect = "player.active_buff=" + ReadString(projection.Value, "buff_id") +
                    ";effect_kind=" + (blessing.ValueKind == JsonValueKind.Object ? ReadString(blessing, "kind") : string.Empty) +
                    ";has_been_blessed_today=true;valid_until_day_end=true;fresh_snapshot_replan_required=true",
                EstimatedTicks = target is null ? 90 : Math.Max(90, target.Distance * 60 + 90),
                EnergyCost = 0,
                AvailabilityClass = "transparent_native_deterministic_daily_statue_blessing",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] StatueBlessingCandidateParameters(JsonElement projection, StatueBlessingTarget? target)
    {
        if (target is null)
        {
            return Array.Empty<SmallModelActionParameter>();
        }
        var blessing = projection.GetProperty("blessing");
        return new[]
        {
            Parameter("statue_blessing_id", ReadInt(projection, "blessing_id").ToString()),
            Parameter("statue_blessing_buff_id", ReadString(projection, "buff_id")),
            Parameter("statue_blessing_effect_kind", ReadString(blessing, "kind")),
            Parameter("statue_blessing_exact_effect", ReadString(blessing, "exact_effect")),
            Parameter("statue_blessing_days_played", ReadInt(projection, "days_played").ToString()),
            Parameter("statue_blessing_random_upper_bound_exclusive", ReadInt(projection, "random_upper_bound_exclusive").ToString()),
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("target_runtime_type", target.RuntimeType),
            Parameter("qualified_item_id", ReadString(projection, "qualified_item_id")),
            Parameter("interaction_kind", ReadString(projection, "interaction_kind")),
            Parameter("expected_action_type", ReadString(projection, "expected_action_type")),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static StatueBlessingTarget? SelectStatueBlessingTarget(JsonElement projection, SnapshotEnvelope snapshot)
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
                    .Select(stand => new StatueBlessingTarget(
                        ReadInt(statue, "tile_x"), ReadInt(statue, "tile_y"),
                        ReadInt(stand, "tile_x"), ReadInt(stand, "tile_y"),
                        ReadString(statue, "target_runtime_type"),
                        Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
                : Enumerable.Empty<StatueBlessingTarget>())
            .OrderBy(row => row.Distance)
            .ThenBy(row => row.TargetY)
            .ThenBy(row => row.TargetX)
            .ThenBy(row => row.StandY)
            .ThenBy(row => row.StandX)
            .FirstOrDefault();
    }

    private sealed record StatueBlessingTarget(int TargetX, int TargetY, int StandX, int StandY, string RuntimeType, int Distance);
}
