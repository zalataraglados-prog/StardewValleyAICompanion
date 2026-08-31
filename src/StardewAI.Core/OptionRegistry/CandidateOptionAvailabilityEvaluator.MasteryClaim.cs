using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] MasteryClaimCandidates(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] intent)
    {
        var projection = ReadStateFieldValue(snapshot, "player", "mastery_claim");
        if (!projection.HasValue || projection.Value.ValueKind != JsonValueKind.Object ||
            ReadString(projection.Value, "projection_status") != "complete_locked_base_1.6.15" ||
            ReadString(projection.Value, "invocation_policy") != "autonomous_strategic_choice")
            return Array.Empty<EventCandidate>();

        var requestedSkill = MasteryClaimIntent(intent, "mastery_skill_id");
        var requestedFingerprint = MasteryClaimIntent(intent, "mastery_option_fingerprint");
        if (!projection.Value.TryGetProperty("claimable_options", out var options) || options.ValueKind != JsonValueKind.Array)
            return Array.Empty<EventCandidate>();
        var rows = options.EnumerateArray()
            .Where(option => option.ValueKind == JsonValueKind.Object)
            .Where(option => string.IsNullOrWhiteSpace(requestedSkill) ||
                ReadInt(option, "skill_id").ToString(CultureInfo.InvariantCulture) == requestedSkill)
            .Where(option => string.IsNullOrWhiteSpace(requestedFingerprint) ||
                ReadString(option, "option_fingerprint") == requestedFingerprint)
            .ToArray();
        if (rows.Length == 0) return Array.Empty<EventCandidate>();

        var currentLocation = ReadStateFieldString(snapshot, "player", "location_id");
        if (!string.Equals(currentLocation, "MasteryCave", StringComparison.OrdinalIgnoreCase))
            return rows.SelectMany(option => MasteryClaimRouteCandidates(snapshot, projection.Value, option, currentLocation)).ToArray();
        return rows.Select(option => MasteryClaimCandidate(snapshot, projection.Value, option)).ToArray();
    }

    private EventCandidate MasteryClaimCandidate(SnapshotEnvelope snapshot, JsonElement projection, JsonElement option)
    {
        var reasons = MasteryClaimStringArray(projection, "blocked_diagnostics").ToList();
        if (ReadString(projection, "service_status") != "ready")
            reasons.Add("mastery_claim_service_not_ready:" + ReadString(projection, "service_status"));
        if (!option.TryGetProperty("action_tile", out var tile) || tile.ValueKind != JsonValueKind.Object)
        {
            reasons.Add("mastery_claim_native_action_endpoint_unavailable");
            return MasteryClaimUnavailableCandidate(option, reasons);
        }
        var targetX = ReadInt(tile, "tile_x");
        var targetY = ReadInt(tile, "tile_y");
        var stand = FindBestStandTile(snapshot, targetX, targetY);
        if (stand is null) reasons.Add("mastery_claim_no_reachable_native_endpoint");
        var parameters = stand is null
            ? Array.Empty<SmallModelActionParameter>()
            : MasteryClaimCandidateParameters(projection, option, tile, stand);
        reasons.AddRange(CompilerProbeBlockingReasons(snapshot, new OptionAvailabilityCandidate
        {
            OptionId = "skills.claim_mastery",
            Parameters = parameters
        }));
        var blocking = reasons.Distinct(StringComparer.Ordinal).ToArray();
        var skillId = ReadInt(option, "skill_id");
        return new EventCandidate
        {
            CandidateId = "mastery-claim:" + ReadString(option, "skill_key") + ":" +
                ReadString(option, "option_fingerprint")[..12],
            Kind = "claim_mastery",
            Available = blocking.Length == 0,
            AllowedNow = blocking.Length == 0,
            AllowedToday = blocking.Length == 0,
            LocationId = "MasteryCave",
            TileX = targetX,
            TileY = targetY,
            DisplayName = "Claim " + ReadString(option, "skill_key") + " mastery",
            EstimatedTicks = stand is null ? 180 : Math.Max(180,
                (Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_x") - stand.X) +
                 Math.Abs(ReadStateFieldInt(snapshot, "player", "tile_y") - stand.Y)) * 60 + 180),
            EnergyCost = 0,
            AvailabilityClass = "transparent_native_unspent_mastery_strategic_choice",
            ExpectedEffect = "mastery_" + skillId + "+=1;masteryLevelsSpent+=1;exact_native_rewards_settled;fresh_snapshot_replan_required=true",
            BlockReasons = blocking,
            Parameters = parameters
        };
    }

    private EventCandidate[] MasteryClaimRouteCandidates(
        SnapshotEnvelope snapshot,
        JsonElement projection,
        JsonElement option,
        string currentLocation)
    {
        if (ReadString(projection, "service_status") != "route_required") return Array.Empty<EventCandidate>();
        var route = FindResolvedRoutePlan(snapshot, currentLocation, "MasteryCave",
            RouteConnectorCandidates(snapshot, int.MaxValue).Where(candidate => candidate.Kind == "route_connector_tile").ToArray());
        if (route?.FirstConnectorCandidate is null) return Array.Empty<EventCandidate>();
        return new[]
        {
            CloneCandidate(route.FirstConnectorCandidate,
                candidateId: "mastery-claim-route:" + ReadString(option, "skill_key") + ":" + currentLocation,
                expectedEffect: route.FirstConnectorCandidate.ExpectedEffect + ";mastery_claim_continuation=true",
                parameters: route.FirstConnectorCandidate.Parameters.Concat(MasteryClaimContinuationParameters(projection, option)).ToArray(),
                availabilityClass: "mastery_claim_rolling_route")
        };
    }

    private static EventCandidate MasteryClaimUnavailableCandidate(JsonElement option, IEnumerable<string> reasons) => new()
    {
        CandidateId = "mastery-claim:" + ReadString(option, "skill_key") + ":unavailable",
        Kind = "claim_mastery",
        Available = false,
        AllowedNow = false,
        AllowedToday = false,
        LocationId = "MasteryCave",
        DisplayName = "Claim " + ReadString(option, "skill_key") + " mastery",
        EstimatedTicks = 180,
        EnergyCost = 0,
        AvailabilityClass = "transparent_native_unspent_mastery_strategic_choice",
        ExpectedEffect = "none",
        BlockReasons = reasons.Distinct(StringComparer.Ordinal).ToArray()
    };

    private static SmallModelActionParameter[] MasteryClaimCandidateParameters(
        JsonElement projection,
        JsonElement option,
        JsonElement tile,
        CandidateTile stand)
    {
        var statsCsv = string.Join(",", projection.GetProperty("skills").EnumerateArray()
            .OrderBy(row => ReadInt(row, "skill_id"))
            .Select(row => ReadInt(row, "mastery_stat_value").ToString(CultureInfo.InvariantCulture)));
        var parameters = new List<SmallModelActionParameter>
        {
            Parameter("mastery_skill_id", ReadInt(option, "skill_id").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_skill_key", ReadString(option, "skill_key")),
            Parameter("mastery_projection_fingerprint", ReadString(projection, "projection_fingerprint")),
            Parameter("mastery_option_fingerprint", ReadString(option, "option_fingerprint")),
            Parameter("mastery_experience_before", ReadInt(projection, "mastery_experience").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_level_before", ReadInt(projection, "current_mastery_level").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_levels_spent_before", ReadInt(projection, "mastery_levels_spent").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_skill_stat_before", ReadInt(option, "mastery_stat_value").ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_all_skill_stats_before_csv", statsCsv),
            Parameter("mastery_recipe_rewards_json", option.GetProperty("recipe_rewards").GetRawText()),
            Parameter("mastery_direct_rewards_json", option.GetProperty("direct_rewards").GetRawText()),
            Parameter("mastery_grants_trinket_slot", (ReadBool(option, "grants_trinket_slot") == true).ToString().ToLowerInvariant()),
            Parameter("mastery_trinket_slots_before", ReadInt(projection, "trinket_slots").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_location", ReadString(tile, "location_id")),
            Parameter("target_tile_x", ReadInt(tile, "tile_x").ToString(CultureInfo.InvariantCulture)),
            Parameter("target_tile_y", ReadInt(tile, "tile_y").ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_x", stand.X.ToString(CultureInfo.InvariantCulture)),
            Parameter("stand_tile_y", stand.Y.ToString(CultureInfo.InvariantCulture)),
            Parameter("mastery_action_raw", ReadString(tile, "action_raw")),
            Parameter("native_contract", ReadString(projection, "native_contract")),
            Parameter("max_movement_tiles", "512")
        };
        parameters.AddRange(MasteryClaimContinuationParameters(projection, option));
        return parameters.ToArray();
    }

    private static SmallModelActionParameter[] MasteryClaimContinuationParameters(JsonElement projection, JsonElement option) => new[]
    {
        Parameter("continuation.option_id", "skills.claim_mastery"),
        Parameter("continuation.mastery_skill_id", ReadInt(option, "skill_id").ToString(CultureInfo.InvariantCulture)),
        Parameter("continuation.mastery_option_fingerprint", ReadString(option, "option_fingerprint")),
        Parameter("continuation.mastery_projection_fingerprint", ReadString(projection, "projection_fingerprint"))
    };

    private static string MasteryClaimIntent(SmallModelActionParameter[] intent, string name) =>
        intent.FirstOrDefault(parameter => parameter.Name == "continuation." + name || parameter.Name == name)?.Value ?? string.Empty;

    private static string[] MasteryClaimStringArray(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString() ?? string.Empty).Where(item => item.Length > 0).ToArray()
            : Array.Empty<string>();
}
