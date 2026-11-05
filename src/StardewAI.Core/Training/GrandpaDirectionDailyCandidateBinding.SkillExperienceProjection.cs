using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed class SkillExperienceProjectionRange
{
    public SkillExperienceProjectionRange(string skillId, int minimum, int maximum)
    {
        SkillId = skillId;
        Minimum = minimum;
        Maximum = maximum;
    }

    public string SkillId { get; }
    public int Minimum { get; }
    public int Maximum { get; }
}

public sealed partial class GrandpaDirectionDailyCandidateBinding
{
    public static bool TryReadSkillExperienceProjection(
        PolicyEventCandidatePrediction candidate,
        out SkillExperienceProjectionRange[] ranges,
        out string rejectionReason)
    {
        ranges = Array.Empty<SkillExperienceProjectionRange>();
        if (candidate is null)
            throw new ArgumentNullException(nameof(candidate));
        if (!HasSkillExperienceEvidence(candidate, out rejectionReason))
            return false;

        // Materialize only after the original binding guard accepts its format.
        if (TryReadUniqueParameter(candidate, "expected_skill_experience_deltas_json", out var json))
        {
            using var document = JsonDocument.Parse(json);
            ranges = document.RootElement.EnumerateArray().Select(row =>
            {
                TryReadStructuredString(row, "skillId", "SkillId", out var id);
                TryReadStructuredInt(row, "delta", "Delta", out var delta);
                return new SkillExperienceProjectionRange(id, delta, delta);
            }).ToArray();
        }
        else if (TryReadUniqueParameter(candidate, "skill_experience_skill_id", out var id))
        {
            TryReadExperienceBounds(candidate, "skill_experience_on_success_min",
                "skill_experience_on_success_max", out var minimum, out var maximum);
            ranges = new[] { new SkillExperienceProjectionRange(id, minimum, maximum) };
        }
        else
        {
            TryReadExperienceBounds(candidate, "foraging_experience_on_success_min",
                "foraging_experience_on_success_max", out var foragingMin, out var foragingMax);
            TryReadExperienceBounds(candidate, "farming_experience_on_success_min",
                "farming_experience_on_success_max", out var farmingMin, out var farmingMax);
            ranges = new[]
            {
                new SkillExperienceProjectionRange("foraging", foragingMin, foragingMax),
                new SkillExperienceProjectionRange("farming", farmingMin, farmingMax)
            };
        }
        return true;
    }
}
