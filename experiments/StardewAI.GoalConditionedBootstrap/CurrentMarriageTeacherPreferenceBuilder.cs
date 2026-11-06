using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class CurrentMarriageTeacherPreferenceBuilder
{
    internal const string GoalId = "goal.social.marriage_and_house_upgrade";

    public static CurrentMarriageTeacherPreference Build(string intakePath, string calibrationPath, string ledgerPath)
    {
        var report = new CurrentMarriageTeacherPreference
        {
            IntakePath = RequiredFile(intakePath), CalibrationPath = RequiredFile(calibrationPath), LedgerPath = RequiredFile(ledgerPath)
        };
        report.IntakeSha256 = CurrentTeacherFrontierSupport.HashFile(report.IntakePath);
        report.CalibrationSha256 = CurrentTeacherFrontierSupport.HashFile(report.CalibrationPath);
        report.LedgerSha256 = CurrentTeacherFrontierSupport.HashFile(report.LedgerPath);
        report.Intake = CurrentMarriageHouseIntakeBuilder.Verify(report.IntakePath);
        report.BindingRole = report.Intake.OriginalDirection.GetProperty("nodes").EnumerateArray()
            .Single(row => row.TryGetProperty("option_id", out var option) && option.GetString() == "social.gift_npc").GetProperty("id").GetString() ??
            throw new InvalidDataException("Marriage gift dependency role is missing.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(report.Intake.SnapshotPath, "Marriage comparison snapshot");
        using var raw = JsonDocument.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(report.Intake.SnapshotPath));
        report.Settlement = new CurrentMarriageHouseSettlementProjector().Build(snapshot);
        if (!report.Intake.CurrentTransitionSourcesReadable || report.Settlement.Status != "ready_conditional_settlement_projection")
            return Blocked(report, "marriage_current_sources_or_native_calendar_unavailable");
        var neutral = new EventCandidateRanker().Rank(new(), report.Intake.Availability, GoalId)
            .Select(row => CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(row, "deterministic_teacher.current_marriage.v1"))
            .OrderBy(row => row.CandidateId, StringComparer.Ordinal).ToArray();
        report.DecisionCandidates = neutral;
        var available = report.Intake.Availability.Options.SelectMany(option => option.EventCandidates.Concat(option.SocialCandidates))
            .Where(row => row.Available).ToArray();
        report.CandidateDenominatorComplete = available.Select(row => row.CandidateId).Distinct(StringComparer.Ordinal).Count() == available.Length &&
            neutral.Select(row => row.CandidateId).Distinct(StringComparer.Ordinal).Count() == neutral.Length &&
            available.Select(row => row.CandidateId).Order(StringComparer.Ordinal).SequenceEqual(neutral.Select(row => row.CandidateId), StringComparer.Ordinal);
        if (!report.CandidateDenominatorComplete) return Blocked(report, "marriage_original_available_candidate_denominator_incomplete");
        if (report.Intake.CurrentState.CurrentCriterionSatisfied == true || report.Intake.CurrentState.Engaged == true || report.Intake.CurrentState.MarriedOrRoommate == true)
            return Blocked(report, "marriage_already_satisfied_or_pending_no_new_preparation_label");
        var resources = new CurrentMarriageTeacherResources(report.LedgerPath, snapshot, raw.RootElement);
        var (time, minutes, energy) = Budget(snapshot);
        var routes = new CurrentSocialRouteContactBudgetBuilder(raw.RootElement, CurrentTeacherFrontierSupport.ReadArtifactText(report.CalibrationPath));
        report.Candidates = neutral.Select(candidate => Evaluate(candidate, report, snapshot, routes, resources, time, minutes, energy)).ToArray();
        if (CurrentTeacherFrontierSupport.HashFile(report.LedgerPath) != report.LedgerSha256)
            throw new InvalidDataException("Marriage resource ledger changed during comparison; re-read the current revision.");
        var groups = report.Candidates.Where(row => row.Status == "admitted_current_partner_preparation")
            .GroupBy(row => (row.NpcName, row.NativeEffectSignature, row.TimingEvidenceId));
        var pairs = groups.SelectMany(group => group.SelectMany(preferred => group.Select(alternative =>
                CurrentMarriageTeacherComparisonPolicy.Compare(preferred, alternative))))
            .Where(pair => pair is not null).Select(pair => pair!).ToArray();
        // Stable IDs select among equivalent proven leaders; ties do not become preference labels.
        var leaders = pairs.Select(pair => pair.PreferredCandidateId).Except(pairs.Select(pair => pair.AlternativeCandidateId), StringComparer.Ordinal)
            .Order(StringComparer.Ordinal).ToArray();
        if (leaders.Length == 0) return Blocked(report, "marriage_no_strict_current_preparation_pair");
        var selected = leaders[0];
        report.SelectedCandidate = neutral.Single(row => row.CandidateId == selected);
        report.PairwisePreferences = pairs.Where(pair => pair.PreferredCandidateId == selected).ToArray();
        var compared = report.PairwisePreferences.Select(pair => pair.AlternativeCandidateId).ToHashSet(StringComparer.Ordinal);
        report.UncomparedCandidateIds = neutral.Where(row => row.CandidateId != selected && !compared.Contains(row.CandidateId)).Select(row => row.CandidateId).ToArray();
        report.IndependentPairwiseLabelEligible = report.PairwisePreferences.Length > 0;
        report.CompletePreferenceLabelEligible = report.IndependentPairwiseLabelEligible && report.UncomparedCandidateIds.Length == 0;
        report.Status = report.CompletePreferenceLabelEligible ? "ready_complete_current_preparation_order" : "ready_independent_preparation_pairs_only";
        return report;
    }

    public static CurrentMarriageTeacherPreference Verify(string path)
    {
        var raw = CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(path));
        var stored = JsonSerializer.Deserialize<CurrentMarriageTeacherPreference>(raw, JsonDefaults.Options) ?? throw new InvalidDataException("Marriage preference is null.");
        var rebuilt = Build(stored.IntakePath, stored.CalibrationPath, stored.LedgerPath);
        if (!JsonNode.DeepEquals(JsonNode.Parse(raw), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)))
            throw new InvalidDataException("Marriage preference differs from its original sources or declared comparison scope.");
        return rebuilt;
    }

    private static (int Time, int Minutes, int Energy) Budget(SnapshotEnvelope snapshot)
    {
        var time = SnapshotGameClockBudgetReader.CurrentTime(snapshot);
        var value = Value(snapshot, "player", "energy");
        if (!time.HasValue || time.Value >= GameClockBudgetPolicy.AutonomousRecoveryStartTime ||
            value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var energy) || !double.IsFinite(energy) || energy < 0 || energy > int.MaxValue)
            throw new InvalidDataException("Marriage current day/energy budget is unavailable or recovery is due.");
        return (time.Value, GameClockBudgetPolicy.ClockMinutesBetween(time.Value, GameClockBudgetPolicy.AutonomousRecoveryStartTime), (int)Math.Floor(energy));
    }

    private static CurrentMarriageTeacherPreference Blocked(CurrentMarriageTeacherPreference report, params string[] reasons)
    {
        report.BlockingReasons = reasons;
        report.UncomparedCandidateIds = report.DecisionCandidates.Select(row => row.CandidateId).ToArray();
        return report;
    }
}
