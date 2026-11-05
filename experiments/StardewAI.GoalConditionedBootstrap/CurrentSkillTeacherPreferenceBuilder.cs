using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentSkillTeacherPreferenceBuilder
{
    private const string DirectionId = "raise_skill_levels";
    private const string TeacherId = "deterministic_teacher.current_skill_xp.v1";

    public static CurrentSkillTeacherPreference Build(string snapshotPath, string? strategyLedgerPath = null) =>
        BuildCore(snapshotPath, strategyLedgerPath, out _);

    private static CurrentSkillTeacherPreference BuildCore(string snapshotPath, string? strategyLedgerPath,
        out PolicyEventCandidatePrediction[] decisionCandidates)
    {
        decisionCandidates = Array.Empty<PolicyEventCandidatePrediction>();
        snapshotPath = Path.GetFullPath(snapshotPath);
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(snapshotPath, "Skill Teacher snapshot");
        var report = new CurrentSkillTeacherPreference
        {
            SnapshotPath = snapshotPath,
            SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotPath),
            Deficit = new CurrentSkillExperienceDeficitBuilder().Build(snapshot)
        };
        if (report.Deficit.Status != "ready_current_state_only")
            return Blocked(report, report.Deficit.BlockingReasons);
        if (report.Deficit.BaseLevelsMissingForSecondCriterion == 0)
        {
            report.Status = "goal_already_satisfied_current_state";
            return report;
        }
        JsonElement timeValue, energyValue;
        try
        {
            timeValue = Value(snapshot, "time", "time");
            energyValue = Value(snapshot, "player", "energy");
        }
        catch (InvalidDataException error)
        {
            return Blocked(report, error.Message);
        }
        if (timeValue.ValueKind != JsonValueKind.Number || !timeValue.TryGetInt32(out var time) ||
            time < 600 || time >= GameClockBudgetPolicy.AutonomousRecoveryStartTime || time % 100 >= 60 ||
            energyValue.ValueKind != JsonValueKind.Number || !energyValue.TryGetDouble(out var energy) ||
            !double.IsFinite(energy) || energy < 0 || energy > int.MaxValue)
            return Blocked(report, "skill_teacher_current_time_or_energy_unavailable_or_recovery_due");

        var ledger = string.IsNullOrWhiteSpace(strategyLedgerPath) ? null :
            AcquisitionStrategyLedgerReader.Read(Path.GetFullPath(strategyLedgerPath), snapshot).Ledger;
        if (ledger is not null)
        {
            report.StrategyLedgerPath = Path.GetFullPath(strategyLedgerPath!);
            report.StrategyLedgerSha256 = CurrentTeacherFrontierSupport.HashFile(report.StrategyLedgerPath);
        }
        var direction = GrandpaDirectionCatalog.Entries.Single(row => row.DirectionId == DirectionId);
        var registry = new OptionRegistry();
        var options = direction.PermittedOptionIds.Select(registry.GetRequired).ToArray();
        report.ConsideredOptionIds = options.Where(option =>
                (option.TrainingRole is TrainingRoles.StrategyValue or TrainingRoles.Mixed) &&
                option.InvocationPolicy != OptionInvocationPolicy.PlayerCommandOnly)
            .Select(option => option.OptionId).Order(StringComparer.Ordinal).ToArray();
        report.CompilerOwnedOptionIds = options.Where(option =>
                option.TrainingRole == TrainingRoles.ExecutorCalibration)
            .Select(option => option.OptionId).Order(StringComparer.Ordinal).ToArray();
        var availability = new CandidateOptionAvailabilityEvaluator().EvaluateForAutonomousRuntimePlanning(
            snapshot, report.ConsideredOptionIds, ledger);
        report.OptionAudit = availability.Options.Select(option => new CurrentSkillTeacherOptionAudit
        {
            OptionId = option.OptionId, Status = option.Status, Available = option.Available,
            MissingStateFactors = option.MissingStateFactors, BlockingReasons = option.BlockingReasons,
            NativeCandidateCount = option.EventCandidates.Length
        }).OrderBy(option => option.OptionId, StringComparer.Ordinal).ToArray();
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability,
            direction.EffectiveGoalId);
        var minutes = GameClockBudgetPolicy.ClockMinutesBetween(time,
            GameClockBudgetPolicy.AutonomousRecoveryStartTime);
        var neutral = ranked.Select(candidate => AdaptProjection(
            CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate, TeacherId)))
            .Select(candidate => CertifyCurrentLocalTime(candidate, snapshot, ledger, minutes, (int)Math.Floor(energy)))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray();
        if (neutral.Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Count() != neutral.Length)
            return Blocked(report, "skill_teacher_duplicate_candidate_identity");
        var binding = new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            DirectionId = DirectionId, StateHash = snapshot.StateHash, RankedCandidates = neutral
        }, snapshot);
        if (binding.BindingStatus != "ready")
            return Blocked(report, binding.BlockReasons);
        var bound = binding.BoundCandidates.ToDictionary(candidate => candidate.CandidateId, StringComparer.Ordinal);
        decisionCandidates = neutral.Select(candidate => bound.TryGetValue(candidate.CandidateId, out var value)
            ? value : candidate).ToArray();
        report.Candidates = neutral.Select(candidate => Evaluate(candidate,
            bound.TryGetValue(candidate.CandidateId, out var value) ? value : null,
            report.Deficit, snapshot, ledger, minutes, (int)Math.Floor(energy)))
            .ToArray();
        foreach (var aliases in report.Candidates.Where(candidate => candidate.Status == "admitted_current_xp")
                     .GroupBy(candidate => candidate.NativeActionIdentity, StringComparer.Ordinal).Where(group => group.Count() > 1))
            foreach (var alias in aliases)
            {
                alias.Status = "excluded";
                alias.Reasons = new[] { "duplicate_native_action_alias_requires_unique_source" };
            }
        var admitted = report.Candidates.Where(candidate => candidate.Status == "admitted_current_xp").ToArray();
        // One dominance witness per dominated choice is enough to find the frontier.
        // Never build the quadratic cross-product of unrelated native effects.
        report.PairwisePreferences = admitted.GroupBy(candidate => candidate.NativeEffectSignature, StringComparer.Ordinal)
            .SelectMany(group => group.Select(alternative => group
                .Select(preferred => CurrentSkillTeacherComparisonPolicy.Compare(preferred, alternative))
                .FirstOrDefault(pair => pair is not null)))
            .Where(pair => pair is not null).Select(pair => pair!).ToArray();
        var dominated = report.PairwisePreferences.Select(pair => pair.AlternativeCandidateId).ToHashSet(StringComparer.Ordinal);
        report.NonDominatedCandidateIds = admitted.Where(candidate => !dominated.Contains(candidate.CandidateId))
            .Select(candidate => candidate.CandidateId).ToArray();
        if (report.NonDominatedCandidateIds.Length == 1)
        {
            report.SelectedCandidate = bound[report.NonDominatedCandidateIds[0]];
            var selected = admitted.Single(candidate => candidate.CandidateId == report.SelectedCandidate.CandidateId);
            report.PairwisePreferences = admitted.Where(candidate => candidate.CandidateId != selected.CandidateId)
                .Select(candidate => CurrentSkillTeacherComparisonPolicy.Compare(selected, candidate))
                .Where(pair => pair is not null).Select(pair => pair!).ToArray();
        }
        report.TeacherPreferenceLabelEligible = report.SelectedCandidate is not null &&
            report.PairwisePreferences.Length > 0 && report.PairwisePreferences.Length == admitted.Length - 1;
        report.Status = report.TeacherPreferenceLabelEligible ? "ready_current_independent_comparison" :
            admitted.Length == 1 ? "control_only_single_candidate" : "blocked_no_unique_strictly_preferred_candidate";
        if (!report.TeacherPreferenceLabelEligible)
            report.BlockingReasons = new[] { report.Status };
        return report;
    }

    public static CurrentSkillTeacherPreference Verify(string preferencePath) => Verify(preferencePath, out _);

    internal static CurrentSkillTeacherPreference Verify(string preferencePath,
        out PolicyEventCandidatePrediction[] decisionCandidates)
    {
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(preferencePath, "Skill Teacher preference");
        var stored = raw.Deserialize<CurrentSkillTeacherPreference>(JsonDefaults.Options) ??
            throw new InvalidDataException("Skill Teacher preference is null.");
        var rebuilt = BuildCore(stored.SnapshotPath, stored.StrategyLedgerPath, out decisionCandidates);
        if (!JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)))
            throw new InvalidDataException("Skill Teacher preference differs from its original sources and independent comparison.");
        return rebuilt;
    }

    private static CurrentSkillTeacherPreference Blocked(CurrentSkillTeacherPreference report, params string[] reasons)
    {
        report.BlockingReasons = reasons;
        return report;
    }
}
