using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteDispatchCompilationBuilder
{
    public static AvailabilityAwarePolicyPredictionEnvelope BuildCurrentControlRanking(
        AcquisitionRoutePortfolioInputs inputs, string controlPath, string supportProofPath = "",
        string currentLedgerPath = "")
    {
        var control = string.IsNullOrWhiteSpace(supportProofPath)
            ? RustyKeyRollingSupplyProposalBuilder.Build(inputs)
            : RustyKeyRollingSupplyProposalBuilder.BuildAfterSupportingTransition(inputs, supportProofPath);
        AcquisitionRouteControlSelectionVerifier.VerifyArtifactIdentity(controlPath, control);
        if (!control.RollingStepReady || control.SelectedAcquisitionProposal is null)
            throw new InvalidDataException("Current acquisition ranking requires a rebuilt selected supply route.");
        var context = AcquisitionRoutePortfolioBuilder.Prepare(inputs);
        var route = context.Opportunity.Routes.Single(row => row.RouteOccurrenceId == control.SelectedCandidateId);
        var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(route);
        var lowering = CurrentTeacherFrontierSupport.Read<AcquisitionRouteOptionLoweringReport>(inputs.AcquisitionLoweringPath, "Current route lowering");
        var lowered = LoweredRoute(lowering, requirement);
        var ledger = string.IsNullOrWhiteSpace(currentLedgerPath) ? context.LedgerState.Ledger :
            AcquisitionStrategyLedgerReader.Read(currentLedgerPath, context.Snapshot).Ledger;
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var ranker = new EventCandidateRanker();
        var initial = evaluator.Evaluate(context.Snapshot, lowered.EndpointOptionIds,
            includeExecutorCalibrationOptions: true, commitmentLedger: ledger);
        var supplied = ranker.Rank(new BaselineTrainingReport(), initial, control.GoalId);
        var requests = RebuildEvaluationCandidates(context.Snapshot, requirement, lowered.EndpointOptionIds, supplied, out var reasons);
        if (reasons.Length > 0) throw new InvalidDataException("Current source intent unavailable: " + string.Join(",", reasons));
        var supporting = lowered.SupportingOptionIds.Except(lowered.EndpointOptionIds, StringComparer.Ordinal)
            .Where(id => id is not ("inventory.transfer_item" or "farm.establish_supported_machine_capacity"))
            .Select(id => new OptionAvailabilityCandidate { OptionId = id });
        var availability = evaluator.Evaluate(context.Snapshot, requests.Concat(supporting).ToArray(),
            includeExecutorCalibrationOptions: true, commitmentLedger: ledger);
        return new AvailabilityAwarePolicyPredictionEnvelope
        {
            Availability = availability,
            RankedEventCandidates = ranker.Rank(new BaselineTrainingReport(), availability, control.GoalId)
        };
    }
}
