using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentFriendshipTeacherPreferenceBuilder
{
    private const string TeacherId = "deterministic_teacher.current_friendship.v1";

    public static CurrentFriendshipTeacherPreference Build(string snapshotPath, string calibrationPath) =>
        BuildCore(snapshotPath, calibrationPath);

    private static CurrentFriendshipTeacherPreference BuildCore(string snapshotPath, string calibrationPath)
    {
        snapshotPath = Path.GetFullPath(snapshotPath);
        calibrationPath = Path.GetFullPath(calibrationPath);
        using var document = JsonDocument.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(snapshotPath));
        var snapshot = document.RootElement.Deserialize<SnapshotEnvelope>(JsonDefaults.Options) ??
            throw new InvalidDataException("Friendship snapshot is null.");
        var report = new CurrentFriendshipTeacherPreference
        {
            SnapshotPath = snapshotPath, SnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(snapshotPath),
            CalibrationPath = calibrationPath,
            Deficit = new CurrentFriendshipDeficitBuilder().Build(snapshot)
        };
        if (report.Deficit.Status != "ready_current_state_only")
            return Blocked(report, report.Deficit.BlockingReasons);
        if (report.Deficit.VillagersMissingForTen == 0)
        {
            report.Status = "goal_already_satisfied_current_state";
            return report;
        }
        int currentTime, availableEnergy;
        try
        {
            var time = Value(snapshot, "time", "time");
            var energy = Value(snapshot, "player", "energy");
            if (time.ValueKind != JsonValueKind.Number || !time.TryGetInt32(out currentTime) ||
                currentTime < 600 || currentTime >= GameClockBudgetPolicy.AutonomousRecoveryStartTime || currentTime % 100 >= 60 ||
                energy.ValueKind != JsonValueKind.Number || !energy.TryGetDouble(out var stamina) ||
                !double.IsFinite(stamina) || stamina < 0 || stamina > int.MaxValue)
                return Blocked(report, "current_social_day_budget_unavailable_or_recovery_due");
            availableEnergy = (int)Math.Floor(stamina);
        }
        catch (InvalidDataException error)
        {
            return Blocked(report, error.Message);
        }
        var minutes = GameClockBudgetPolicy.ClockMinutesBetween(currentTime, GameClockBudgetPolicy.AutonomousRecoveryStartTime);
        var calibration = CurrentTeacherFrontierSupport.ReadArtifactText(calibrationPath);
        report.CalibrationSha256 = CurrentTeacherFrontierSupport.HashFile(calibrationPath);
        var direction = GrandpaDirectionCatalog.Entries.Single(entry => entry.DirectionId == "raise_friendships");
        report.ConsideredOptionIds = direction.PermittedOptionIds.Order(StringComparer.Ordinal).ToArray();
        var evaluator = CandidateOptionAvailabilityEvaluator.ForCompleteOfflineSocialComparison();
        var availability = evaluator.EvaluateForAutonomousRuntimePlanning(
            snapshot, report.ConsideredOptionIds);
        var neutral = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability,
                direction.EffectiveGoalId)
            .Select(candidate => CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(candidate, TeacherId))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray();
        var rankedIds = neutral.Select(candidate => (candidate.OptionId, candidate.CandidateId)).ToHashSet();
        report.OptionAudit = availability.Options.Select(option =>
        {
            var duplicates = option.SocialCandidates.GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Where(group => group.Count() > 1).OrderBy(group => group.Key, StringComparer.Ordinal).ToArray();
            var blockedDuplicates = duplicates.Where(group => group.Any(candidate => candidate.Available ||
                candidate.AllowedNow == true || candidate.AllowedToday == true || candidate.BlockReasons.Length == 0)).ToArray();
            var blockingIds = blockedDuplicates.Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            return new CurrentFriendshipTeacherOptionAudit
            {
                OptionId = option.OptionId, Status = option.Status, Available = option.Available,
                MissingStateFactors = option.MissingStateFactors, BlockingReasons = option.BlockingReasons,
                CandidateCount = neutral.Count(candidate => candidate.OptionId == option.OptionId),
                SourceCandidateCount = option.EventCandidates.Concat(option.SocialCandidates)
                    .Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Count(),
                SourceSocialCandidateCount = option.SocialCandidates.Length,
                AppliedSocialCandidateLimit = evaluator.SocialCandidateLimit,
                AtLegacySocialCandidateLimit = evaluator.SocialCandidateLimit == 64 && option.SocialCandidates.Length >= 64,
                OmittedAvailableCandidateIds = option.EventCandidates.Concat(option.SocialCandidates)
                    .Where(candidate => candidate.Available && !rankedIds.Contains((option.OptionId, candidate.CandidateId)))
                    .Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                // Keep every known-unavailable duplicate occurrence; it is neither an action nor a negative label.
                DuplicateSourceCandidateIds = duplicates.Select(group => group.Key).ToArray(),
                BlockingDuplicateSourceCandidateIds = blockedDuplicates.Select(group => group.Key).ToArray(),
                UnavailableDuplicateSourceOccurrences = duplicates.Where(group => !blockingIds.Contains(group.Key))
                    .SelectMany(group => group).ToArray()
            };
        }).OrderBy(option => option.OptionId, StringComparer.Ordinal).ToArray();
        report.CandidateDenominatorComplete = evaluator.SocialCandidateLimit is null &&
            report.OptionAudit.Select(option => option.OptionId)
                .SequenceEqual(report.ConsideredOptionIds, StringComparer.Ordinal) &&
            report.OptionAudit.All(option => option.MissingStateFactors.Length == 0 &&
                !option.AtLegacySocialCandidateLimit && option.OmittedAvailableCandidateIds.Length == 0 &&
                option.BlockingDuplicateSourceCandidateIds.Length == 0);
        if (neutral.Select(candidate => candidate.CandidateId).Distinct(StringComparer.Ordinal).Count() != neutral.Length)
            return Blocked(report, "friendship_teacher_duplicate_candidate_identity");
        if (!report.CandidateDenominatorComplete)
        {
            report.DecisionCandidates = neutral;
            return Blocked(report, "friendship_teacher_incomplete_denominator_or_legacy_candidate_limit");
        }
        var originalTeacher = new CurrentSocialDayTeacherLabelBuilder().Build(document.RootElement, calibration);
        var routeBudgets = new CurrentSocialRouteContactBudgetBuilder(document.RootElement, calibration);
        var frontier = routeBudgets.Frontier;
        var admissionReasons = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var candidate in neutral)
        {
            CertifyCurrentContact(candidate, snapshot, frontier, currentTime, minutes, availableEnergy, out var reasons);
            admissionReasons[candidate.CandidateId] = reasons;
        }
        var binding = new GrandpaDirectionDailyCandidateBinding().Bind(new GrandpaDirectionBindingRequest
        {
            DirectionId = direction.DirectionId, StateHash = snapshot.StateHash, RankedCandidates = neutral
        }, snapshot);
        var bound = binding.BoundCandidates.ToDictionary(candidate => candidate.CandidateId, StringComparer.Ordinal);
        report.DecisionCandidates = neutral.Select(candidate => bound.TryGetValue(candidate.CandidateId, out var value)
            ? value : candidate).ToArray();
        // The original public Teacher keeps its own control/portfolio policy. This consumer never selects for it.
        // Dispatch queue IDs are deliberately fresh. Project the old result rather than changing that contract.
        report.OriginalTeacher = new CurrentFriendshipTeacherControl
        {
            Status = originalTeacher.Status, SourceStateHash = originalTeacher.SourceStateHash,
            TrainingLabelEligible = originalTeacher.TrainingLabelEligible,
            SelectedCandidateId = originalTeacher.SelectedCandidate?.CandidateId ?? string.Empty,
            Itinerary = originalTeacher.Itinerary, BlockingReasons = originalTeacher.BlockingReasons
        };
        report.Candidates = neutral.Select(candidate => Evaluate(candidate,
            bound.TryGetValue(candidate.CandidateId, out var value) ? value : null,
            snapshot, report.Deficit, frontier, routeBudgets, currentTime, minutes, availableEnergy, admissionReasons[candidate.CandidateId]))
            .ToArray();
        if (!originalTeacher.Itinerary.TrainingLabelEligible || originalTeacher.SelectedCandidate is null)
            return Blocked(report, report.OriginalTeacher.BlockingReasons.DefaultIfEmpty(
                "original_social_teacher_not_ready").ToArray());
        var selectedId = originalTeacher.SelectedCandidate.CandidateId;
        var selected = report.Candidates.SingleOrDefault(row => row.CandidateId == selectedId);
        if (selected is null || selected.Status != "admitted_current_social_comparison" || !bound.ContainsKey(selectedId))
            return Blocked(report, "original_social_control_not_in_admitted_comparison_denominator");
        report.SelectedCandidate = bound[selectedId];
        var step = report.OriginalTeacher.Itinerary.Steps[0];
        if (selected.NpcName != step.NpcName || selected.FriendshipPointsBefore != step.FriendshipPointsBefore ||
            selected.ExpectedFriendshipPointsAfter != step.ExpectedFriendshipPointsAfter ||
            selected.OpportunityId != step.OpportunityId ||
            selected.ContactBudgetArrivalTime != step.InteractionStartTime)
            return Blocked(report, "original_social_control_and_contact_budget_disagree");

        var alternatives = report.Candidates.Where(row => row.CandidateId != selectedId &&
            row.Status == "admitted_current_social_comparison").ToArray();
        report.PairwisePreferences = alternatives.Select(row => CurrentFriendshipTeacherComparisonPolicy.Compare(selected, row))
            .Where(pair => pair is not null).Select(pair => pair!).ToArray();
        var compared = report.PairwisePreferences.Select(pair => pair.AlternativeCandidateId).ToHashSet(StringComparer.Ordinal);
        report.UncomparedCandidateIds = report.Candidates.Where(row => row.CandidateId != selectedId &&
                row.ComparisonRequired && !compared.Contains(row.CandidateId))
            .Select(row => row.CandidateId).ToArray();
        report.TeacherPreferenceLabelEligible = report.PairwisePreferences.Length > 0 &&
            report.UncomparedCandidateIds.Length == 0 &&
            report.CandidateDenominatorComplete;
        report.Status = report.TeacherPreferenceLabelEligible ? "ready_current_independent_comparison" :
            alternatives.Length == 0 && report.UncomparedCandidateIds.Length == 0
                ? "control_only_single_candidate" : "blocked_incomplete_or_non_strict_comparison";
        if (!report.TeacherPreferenceLabelEligible)
            report.BlockingReasons = new[] { report.Status };
        return report;
    }

    public static CurrentFriendshipTeacherPreference Verify(string preferencePath)
    {
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(preferencePath, "Friendship Teacher preference");
        var stored = raw.Deserialize<CurrentFriendshipTeacherPreference>(JsonDefaults.Options) ??
            throw new InvalidDataException("Friendship Teacher preference is null.");
        if (stored.SchemaVersion != "current_friendship_teacher_preference.v3")
            throw new InvalidDataException("Rebuild the friendship preference from its sources using the complete contact-budget intake.");
        var rebuilt = BuildCore(stored.SnapshotPath, stored.CalibrationPath);
        if (!JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)))
            throw new InvalidDataException("Friendship Teacher preference differs from original sources and comparison.");
        return rebuilt;
    }

    private static CurrentFriendshipTeacherPreference Blocked(CurrentFriendshipTeacherPreference report, params string[] reasons)
    {
        report.Status = "blocked";
        report.BlockingReasons = reasons;
        return report;
    }
}
