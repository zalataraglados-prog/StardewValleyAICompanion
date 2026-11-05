using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentSkillTeacherPreferenceBuilder
{
    private static PolicyEventCandidatePrediction AdaptProjection(PolicyEventCandidatePrediction candidate)
    {
        if (candidate.Kind != "collect_crab_pot" ||
            candidate.Parameters.Any(parameter => parameter.Name == "skill_experience_projection_status"))
            return candidate;
        var id = UniqueParameter(candidate, "expected_skill_id");
        var xp = UniqueParameter(candidate, "expected_skill_experience_delta");
        if (id != "fishing" || !int.TryParse(xp, NumberStyles.Integer,
                CultureInfo.InvariantCulture, out var delta) || delta <= 0)
            return candidate;
        candidate.Parameters = candidate.Parameters.Concat(new[]
        {
            Parameter("skill_experience_skill_id", id),
            Parameter("skill_experience_on_success_min", xp!),
            Parameter("skill_experience_on_success_max", xp!),
            Parameter("skill_experience_condition", "native_CrabPot_checkForAction_gainExperience"),
            Parameter("skill_experience_projection_status", "exact_native_crab_pot_collection")
        }).ToArray();
        return candidate;
    }

    private static PolicyEventCandidatePrediction CertifyCurrentLocalTime(
        PolicyEventCandidatePrediction candidate, SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? ledger, int minutes, int energy)
    {
        if (candidate.AllowedNow == true && candidate.AllowedToday == true)
            return candidate;
        if (!GrandpaDirectionDailyCandidateBinding.TryReadSkillExperienceProjection(candidate, out _, out _))
            return candidate;
        var plan = new DailyPlanCompiler().Compile(new[] { candidate }, snapshot.StateHash,
            goalId: "goal.skills.raise_skill_levels", maxCandidates: 1,
            availableMinutes: minutes, energyBudget: energy);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        var location = NativeExecutionSourceSupport.Value(snapshot, "player", "location_id");
        if (location.ValueKind == JsonValueKind.String)
            CurrentSkillLocalTimeAdmission.TryCertify(candidate, plan, queue,
                snapshot.StateHash, location.GetString()!, minutes);
        return candidate;
    }

    private static CurrentSkillTeacherCandidate Evaluate(
        PolicyEventCandidatePrediction source,
        PolicyEventCandidatePrediction? bound,
        CurrentSkillExperienceDeficit deficit,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger? ledger,
        int availableMinutes,
        int availableEnergy)
    {
        var row = new CurrentSkillTeacherCandidate
        {
            CandidateId = source.CandidateId, OptionId = source.OptionId, Kind = source.Kind,
            EstimatedTicks = source.EstimatedTicks, EnergyCost = source.EnergyCost
        };
        if (!source.Available || source.AllowedNow == false || source.AllowedToday == false ||
            source.TimelineStatus != "ready_now" || source.BlockReasons.Length > 0)
            return Excluded(row, "unavailable_or_not_current_no_negative_label");
        if (bound is null)
            return Excluded(row, "original_skill_direction_binding_rejected");
        if (!GrandpaDirectionDailyCandidateBinding.TryReadSkillExperienceProjection(bound, out var xp, out var reason))
            return Excluded(row, reason);
        row.NativeXp = xp;
        var byId = xp.ToDictionary(value => value.SkillId, StringComparer.Ordinal);
        row.UsefulXp = deficit.Skills.Select(skill =>
            byId.TryGetValue(skill.SkillId, out var value)
                ? new SkillExperienceProjectionRange(skill.SkillId,
                    Math.Min(skill.ExperienceToLevelTen, value.Minimum),
                    Math.Min(skill.ExperienceToLevelTen, value.Maximum))
                : new SkillExperienceProjectionRange(skill.SkillId, 0, 0)).ToArray();
        if (!row.UsefulXp.Any(value => value.Minimum > 0))
            return Excluded(row, "no_guaranteed_positive_xp_for_missing_base_skill");
        if (source.EstimatedTicks <= 0 || source.EnergyCost < 0 ||
            bound.Parameters.GroupBy(parameter => parameter.Name, StringComparer.Ordinal).Any(group => group.Count() > 1))
            return Excluded(row, "unknown_cost_or_ambiguous_native_parameters");

        var plan = new DailyPlanCompiler().Compile(new[] { bound }, snapshot.StateHash,
            goalId: "goal.skills.raise_skill_levels", maxCandidates: 1,
            availableMinutes: availableMinutes, energyBudget: availableEnergy);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        var diagnostics = CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue);
        if (plan.Steps.Length == 0 || queue.Items.Length == 0 || queue.Status != "pending" ||
            queue.Items.Any(item => item.Status != "pending") || diagnostics.Length > 0)
            return Excluded(row, diagnostics.Length > 0 ? diagnostics : new[] { "original_compiler_not_dispatchable" });
        row.CompiledMinutes = plan.CandidateAudit.Single(audit => audit.Decision == "accepted").CandidateMinutes;
        row.NativeActionIdentity = JsonSerializer.Serialize(new
        {
            bound.OptionId, bound.Kind, bound.LocationId, bound.TileX, bound.TileY, bound.SlotIndex
        });
        row.NativeEffectSignature = NativeEffectSignature(bound);
        row.Status = "admitted_current_xp";
        return row;
    }

    public static string NativeEffectSignature(PolicyEventCandidatePrediction candidate)
    {
        var effect = candidate.ExpectedEffect;
        // The original crab-pot effect text includes one geometry-only stand prefix.
        // Preserve all other text and every unknown parameter, including resource effects.
        if (candidate.Kind == "collect_crab_pot")
        {
            var x = UniqueParameter(candidate, "stand_tile_x");
            var y = UniqueParameter(candidate, "stand_tile_y");
            if (x is not null && y is not null)
                effect = effect.Replace($"crab_pot_stand_tile={x},{y};",
                    "crab_pot_stand_tile=<geometry>;", StringComparison.Ordinal);
        }
        var semantic = new
        {
            candidate.OptionId, candidate.Kind, candidate.LocationId, candidate.ItemId,
            candidate.QualifiedItemId, candidate.Quantity, candidate.SlotIndex,
            candidate.UnitPrice, candidate.TotalValue, ExpectedEffect = effect,
            Parameters = candidate.Parameters.Where(parameter => parameter.Name is not
                    ("target_tile_x" or "target_tile_y" or "stand_tile_x" or "stand_tile_y" or
                     "skill_current_admission_minutes"))
                .OrderBy(parameter => parameter.Name, StringComparer.Ordinal).ToArray()
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(semantic, JsonDefaults.Options)))).ToLowerInvariant();
    }

    private static string? UniqueParameter(PolicyEventCandidatePrediction candidate, string name)
    {
        var values = candidate.Parameters.Where(parameter => parameter.Name == name).ToArray();
        return values.Length == 1 && !string.IsNullOrWhiteSpace(values[0].Value) ? values[0].Value : null;
    }

    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };

    private static CurrentSkillTeacherCandidate Excluded(CurrentSkillTeacherCandidate row, params string[] reasons)
    {
        row.Reasons = reasons;
        return row;
    }
}
