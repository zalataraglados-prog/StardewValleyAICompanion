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
    private EventCandidate[] CalicoStatueCandidates(SnapshotEnvelope snapshot)
    {
        var projection = ReadStateFieldValue(snapshot, "mining", "calico_statue");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(projection.Value, "gate_status"), "ready", StringComparison.Ordinal))
        {
            return Array.Empty<EventCandidate>();
        }

        var target = SelectCalicoStatueTarget(projection.Value, snapshot);
        if (target is null)
            return Array.Empty<EventCandidate>();
        var parameters = CalicoStatueCandidateParameters(projection.Value, target);
        var reasons = new List<string>();
        if (!string.Equals(ActiveMenuTypeForCandidate(snapshot), "none", StringComparison.OrdinalIgnoreCase))
            reasons.Add("calico_statue_menu_must_be_clear");
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "mining.activate_calico_statue",
            Parameters = parameters
        }));
        var effect = projection.Value.GetProperty("projected_effect");
        var effectId = ReadInt(projection.Value, "projected_effect_id");
        return new[]
        {
            new EventCandidate
            {
                CandidateId = "calico-statue:" + ReadString(projection.Value, "location_id") + ":" +
                    ReadInt(projection.Value, "mine_level") + ":" + target.TargetX + "," + target.TargetY +
                    ":activation:" + ReadInt(projection.Value, "next_activation_number") + ":effect:" + effectId,
                Kind = "activate_calico_statue",
                Available = reasons.Count == 0,
                LocationId = ReadString(projection.Value, "location_id"),
                TileX = target.TargetX,
                TileY = target.TargetY,
                DisplayName = "Calico Statue: " + ReadString(effect, "effect_key"),
                ExpectedEffect = "team.calico_egg_skull_cavern_rating+=1;projected_effect_id=" + effectId +
                    ";projected_effect_key=" + ReadString(effect, "effect_key") +
                    ";strategy_polarity=" + ReadString(effect, "strategy_polarity") +
                    ";exact_effect=" + ReadString(effect, "exact_effect") +
                    ";fresh_snapshot_replan_required=true",
                EstimatedTicks = Math.Max(90, target.Distance * 60 + 90),
                EnergyCost = 0,
                AvailabilityClass = "transparent_exact_seeded_calico_statue_strategy_choice",
                BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray(),
                Parameters = parameters
            }
        };
    }

    private static SmallModelActionParameter[] CalicoStatueCandidateParameters(
        JsonElement projection,
        CalicoStatueTarget target)
    {
        var effect = projection.GetProperty("projected_effect");
        return new[]
        {
            Parameter("calico_statue_accepted_effect_id", ReadInt(projection, "projected_effect_id").ToString()),
            Parameter("calico_statue_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("calico_statue_effect_key", ReadString(effect, "effect_key")),
            Parameter("calico_statue_strategy_polarity", ReadString(effect, "strategy_polarity")),
            Parameter("calico_statue_exact_effect", ReadString(effect, "exact_effect")),
            Parameter("calico_statue_calico_egg_reward", ReadInt(effect, "calico_egg_reward").ToString()),
            Parameter("calico_statue_current_effects_csv", ReadString(projection, "current_effects_csv")),
            Parameter("calico_statue_expected_effects_after_csv", ReadString(projection, "expected_effects_after_csv")),
            Parameter("calico_statue_total_activated_before", ReadInt(projection, "total_activated_today_before").ToString()),
            Parameter("calico_statue_next_activation_number", ReadInt(projection, "next_activation_number").ToString()),
            Parameter("calico_statue_rating_before", ReadInt(projection, "rating_before").ToString()),
            Parameter("calico_statue_expected_rating_after", ReadInt(projection, "expected_rating_after").ToString()),
            Parameter("calico_statue_average_daily_luck", ReadDouble(projection, "average_daily_luck").ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            Parameter("calico_statue_days_played", ReadInt(projection, "days_played").ToString()),
            Parameter("calico_statue_unique_game_id_half", ReadString(projection, "unique_game_id_half")),
            Parameter("calico_statue_use_legacy_random", (ReadBool(projection, "use_legacy_random") == true).ToString().ToLowerInvariant()),
            Parameter("calico_statue_mine_level", ReadInt(projection, "mine_level").ToString()),
            Parameter("calico_statue_festival_day", ReadInt(projection, "desert_festival_day").ToString()),
            Parameter("calico_statue_tile_index_before", ReadInt(projection, "target_tile_index_before").ToString()),
            Parameter("calico_statue_tile_index_after", ReadInt(projection, "target_tile_index_after").ToString()),
            Parameter("calico_statue_eggs_before", ReadInt(projection, "calico_eggs_before").ToString()),
            Parameter("calico_statue_health_before", ReadInt(projection, "health_before").ToString()),
            Parameter("calico_statue_max_health", ReadInt(projection, "max_health").ToString()),
            Parameter("calico_statue_stamina_before", ReadDouble(projection, "stamina_before").ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            Parameter("calico_statue_max_stamina", ReadDouble(projection, "max_stamina").ToString("R", System.Globalization.CultureInfo.InvariantCulture)),
            Parameter("target_location", ReadString(projection, "location_id")),
            Parameter("target_tile_x", target.TargetX.ToString()),
            Parameter("target_tile_y", target.TargetY.ToString()),
            Parameter("stand_tile_x", target.StandX.ToString()),
            Parameter("stand_tile_y", target.StandY.ToString()),
            Parameter("interaction_kind", ReadString(projection, "interaction_kind")),
            Parameter("expected_action_type", ReadString(projection, "expected_action_type")),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
    }

    private static CalicoStatueTarget? SelectCalicoStatueTarget(JsonElement projection, SnapshotEnvelope snapshot)
    {
        if (!projection.TryGetProperty("stand_tiles", out var stands) || stands.ValueKind != JsonValueKind.Array)
            return null;
        var targetX = ReadInt(projection, "target_tile_x");
        var targetY = ReadInt(projection, "target_tile_y");
        var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
        var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
        return stands.EnumerateArray()
            .Where(stand => stand.ValueKind == JsonValueKind.Object && ReadBool(stand, "available") == true)
            .Select(stand => new CalicoStatueTarget(
                targetX, targetY, ReadInt(stand, "tile_x"), ReadInt(stand, "tile_y"),
                Math.Abs(playerX - ReadInt(stand, "tile_x")) + Math.Abs(playerY - ReadInt(stand, "tile_y"))))
            .OrderBy(row => row.Distance)
            .ThenBy(row => row.StandY)
            .ThenBy(row => row.StandX)
            .FirstOrDefault();
    }

    private sealed record CalicoStatueTarget(
        int TargetX,
        int TargetY,
        int StandX,
        int StandY,
        int Distance);
}
