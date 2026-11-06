using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumPolicyRowAdmissionBuilder
{
    private static PolicyEventCandidatePrediction[] BuildCandidates(VerifiedMuseumTerminalSource source, MuseumPolicyInputProjector projector)
    {
        var request = ReadTyped<MuseumTerminalTeacherComparisonRequest>(source.Comparison.RequestPath, "museum_terminal_teacher_comparison_request.v1");
        Require(CurrentTeacherFrontierSupport.HashFile(request.StrategyLedgerPath) == source.Comparison.SourceSha256["strategy_ledger"],
            "Museum admission ledger differs from its verified comparison.");
        var snapshot = source.Native.Before;
        var ledger = AcquisitionStrategyLedgerReader.Read(request.StrategyLedgerPath, snapshot).Ledger;
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, source.Intake.ConsideredOptionIds, commitmentLedger: ledger);
        var originals = availability.Options.SelectMany(option => option.EventCandidates.Select(candidate => (option.OptionId, Candidate: candidate)))
            .OrderBy(row => row.Candidate.CandidateId, StringComparer.Ordinal).ToArray();
        Require(originals.Length == source.Receipt.OriginalCandidateCount && originals.Select(row => row.Candidate.CandidateId).SequenceEqual(
            source.Comparison.CandidateEvaluations.Where(row => row.Kind != MuseumPolicyInputProjector.DeferKind)
                .Select(row => row.CandidateId).Order(StringComparer.Ordinal)), "Museum admission lost an original candidate.");
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability, source.Comparison.GoalId)
            .ToDictionary(row => row.CandidateId, StringComparer.Ordinal);
        var candidates = originals.Select(row =>
        {
            if (ranked.TryGetValue(row.Candidate.CandidateId, out var current))
                return CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(current, MuseumTerminalTeacherComparisonBuilder.PolicyId);
            var copy = JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(JsonSerializer.Serialize(row.Candidate, JsonDefaults.Options), JsonDefaults.Options)!;
            copy.OptionId = row.OptionId;
            copy.TimelineReasons = new[] { "original_timeline_did_not_admit_candidate" };
            return CurrentStageOneCollectionTeacherPreferenceBuilder.NeutralizeLearnerSignals(copy, MuseumTerminalTeacherComparisonBuilder.PolicyId);
        }).ToList();
        var reference = source.Comparison.CandidateEvaluations.Single(row => row.Kind == MuseumPolicyInputProjector.DeferKind);
        Require(reference.EstimatedTicks == MuseumTerminalTeacherComparisonBuilder.HorizonTicks &&
            reference.CompiledQueue is { Items.Length: 1 } && reference.CompiledQueue.Items[0].OptionId == "executor.wait_ticks",
            "Museum no-donation reference lacks its original bounded wait compilation.");
        // A comparison reference keeps its primitive identity, not an invented policy option.
        candidates.Add(new PolicyEventCandidatePrediction
        {
            CandidateId = reference.CandidateId, OptionId = "executor.wait_ticks", Kind = reference.Kind,
            Available = reference.Admitted, AllowedNow = reference.Admitted, EstimatedTicks = reference.EstimatedTicks!.Value,
            BlockReasons = reference.BlockingReasons, PolicyModelSource = MuseumTerminalTeacherComparisonBuilder.PolicyId,
            Parameters = new[] { new SmallModelActionParameter
                { Name = "comparison_horizon_ticks", Value = MuseumTerminalTeacherComparisonBuilder.HorizonTicks.ToString(CultureInfo.InvariantCulture) } }
        });
        for (var index = 0; index < candidates.Count; index++) candidates[index].Rank = index + 1;
        return projector.ProjectCandidates(candidates);
    }
}
