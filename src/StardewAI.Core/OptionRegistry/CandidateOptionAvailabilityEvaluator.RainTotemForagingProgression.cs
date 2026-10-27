using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry;

public sealed partial class CandidateOptionAvailabilityEvaluator
{
    private EventCandidate[] RainTotemForagingSkillProgressionCandidates(
        SnapshotEnvelope snapshot,
        MasterAnglerRainTotemSupplyIntentValidation intent)
    {
        if (!TryReadRainTotemForagingSkill(
                snapshot,
                out var level,
                out var experience))
        {
            return
            [
                BlockedRainTotemCraftingCandidate(
                    "master_angler_rain_totem_foraging_skill_projection_unavailable",
                    CanonicalRainTotemSupplyParameters(intent))
            ];
        }
        if (level >= 9)
        {
            return
            [
                BlockedRainTotemCraftingCandidate(
                    "master_angler_rain_totem_recipe_missing_after_foraging_level_9",
                    CanonicalRainTotemSupplyParameters(intent))
            ];
        }

        var candidates = WildTreeChopAcquisitionCandidates(snapshot)
            .Where(candidate => candidate.Available &&
                string.Equals(
                    ReadParameter(
                        candidate.Parameters,
                        "skill_experience_skill_id"),
                    "foraging",
                    StringComparison.Ordinal) &&
                int.TryParse(
                    ReadParameter(
                        candidate.Parameters,
                        "skill_experience_on_success_min"),
                    NumberStyles.Integer,
                    CultureInfo.InvariantCulture,
                    out var minimumExperience) &&
                minimumExperience > 0)
            .Select(candidate => BindRainTotemForagingSkillCandidate(
                candidate,
                intent,
                level,
                experience))
            .OrderBy(candidate => candidate.EstimatedTicks)
            .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length > 0
            ? candidates
            :
            [
                BlockedRainTotemCraftingCandidate(
                    "master_angler_rain_totem_no_current_exact_foraging_xp_candidate",
                    CanonicalRainTotemSupplyParameters(intent))
            ];
    }

    private static EventCandidate BindRainTotemForagingSkillCandidate(
        EventCandidate candidate,
        MasterAnglerRainTotemSupplyIntentValidation intent,
        int level,
        int experience)
    {
        var parameters = candidate.Parameters
            .Concat(CanonicalRainTotemSupplyParameters(intent))
            .Concat(new[]
            {
                Parameter(
                    "rain_totem_foraging_progression_status",
                    "exact_positive_native_xp_candidate_bound"),
                Parameter(
                    "rain_totem_foraging_current_unmodified_level",
                    level.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "rain_totem_foraging_current_experience",
                    experience.ToString(CultureInfo.InvariantCulture)),
                Parameter("rain_totem_foraging_required_level", "9"),
                Parameter(
                    "rain_totem_foraging_fresh_replan_required",
                    "true")
            })
            .ToArray();
        return CloneCandidate(
            candidate,
            candidateId: "rain-totem-foraging-xp:" + candidate.CandidateId,
            expectedEffect: candidate.ExpectedEffect +
                ";rain_totem_foraging_progression_status=exact_positive_native_xp_candidate_bound" +
                ";rain_totem_foraging_required_level=9" +
                ";fresh_snapshot_and_weather_supply_replan_required=true",
            parameters: parameters,
            availabilityClass:
                "master_angler_rain_totem_foraging_skill_progression");
    }

    private static bool TryReadRainTotemForagingSkill(
        SnapshotEnvelope snapshot,
        out int level,
        out int experience)
    {
        level = -1;
        experience = -1;
        var detail = ReadStateFieldValue(
            snapshot,
            "player",
            "skills_detail");
        if (!detail.HasValue ||
            detail.Value.ValueKind != JsonValueKind.Object ||
            !detail.Value.TryGetProperty("skills", out var skills) ||
            skills.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var rows = skills.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                string.Equals(
                    ReadString(row, "skill_id"),
                    "foraging",
                    StringComparison.Ordinal))
            .ToArray();
        if (rows.Length != 1)
            return false;

        level = ReadInt(rows[0], "unmodified_level");
        experience = ReadInt(rows[0], "experience");
        return level is >= 0 and <= 10 && experience >= 0;
    }
}
