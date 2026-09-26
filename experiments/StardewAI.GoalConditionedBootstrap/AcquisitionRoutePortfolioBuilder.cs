using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioBuilder
{
    internal const string RouteDecisionPrefix =
        "target-date-acquisition-route:";

    public static AcquisitionRoutePortfolioAdmission Build(
        AcquisitionRoutePortfolioInputs inputs)
    {
        var context = Prepare(inputs);
        var proposalPath = Path.GetFullPath(inputs.ProposalPath);
        var proposal = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioProposal>(
            proposalPath,
            "Acquisition route portfolio proposal");
        return Build(
            context,
            proposal,
            CurrentTeacherFrontierSupport.HashFile(proposalPath));
    }

    public static AcquisitionRoutePortfolioAdmission
        BuildAfterSupportingTransition(
            AcquisitionRoutePortfolioInputs inputs,
            string priorSupportingTransitionReplanSha256)
    {
        var context = Prepare(inputs);
        var proposalPath = Path.GetFullPath(inputs.ProposalPath);
        var proposal = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioProposal>(
            proposalPath,
            "Acquisition support-replan portfolio proposal");
        return Build(
            context,
            proposal,
            CurrentTeacherFrontierSupport.HashFile(proposalPath),
            null,
            priorSupportingTransitionReplanSha256);
    }

    internal static AcquisitionRoutePortfolioBuildContext Prepare(
        AcquisitionRoutePortfolioInputs inputs)
    {
        var inventoryPath = Path.GetFullPath(inputs.RequirementInventoryPath);
        var opportunityPath = Path.GetFullPath(
            inputs.TargetDateOpportunityCostPath);
        var ledgerPath = Path.GetFullPath(inputs.StrategyLedgerPath);
        var snapshotPath = Path.GetFullPath(inputs.SnapshotPath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            inventoryPath,
            "Authoritative requirement inventory");
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            opportunityPath,
            "Acquisition route target-date opportunity cost");
        var recomputed = RecomputeOpportunityCost(inputs);
        Require(EqualJson(opportunity, recomputed),
            "Target-date opportunity-cost report drifted from deterministic source compilation.");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Acquisition route portfolio snapshot");
        var targetDateCalendar = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateCalendarReport>(
            Path.GetFullPath(inputs.TargetDateCalendarPath),
            "Acquisition route target-date calendar");
        var snapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
            snapshotPath);
        var communityCenterProvenance =
            AcquisitionRouteCommunityCenterProvenanceSupport.From(
                targetDateCalendar,
                snapshot,
                snapshotSha256);
        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotPath));
        var ledgerState = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            snapshotDocument.RootElement);
        return new AcquisitionRoutePortfolioBuildContext(
            inventory,
            opportunity,
            snapshot,
            ledgerState,
            communityCenterProvenance,
            CurrentTeacherFrontierSupport.HashFile(inventoryPath),
            CurrentTeacherFrontierSupport.HashFile(opportunityPath),
            CurrentTeacherFrontierSupport.HashFile(ledgerPath),
            snapshotSha256);
    }

    internal static AcquisitionRoutePortfolioAdmission Build(
        AcquisitionRoutePortfolioBuildContext context,
        AcquisitionRoutePortfolioProposal proposal,
        string proposalSha256) => Build(
        context,
        proposal,
        proposalSha256,
        null,
        string.Empty);

    internal static AcquisitionRoutePortfolioAdmission Build(
        AcquisitionRoutePortfolioBuildContext context,
        AcquisitionRoutePortfolioProposal proposal,
        string proposalSha256,
        AcquisitionRoutePortfolioContinuationEvidence? continuation,
        string priorSupportingTransitionReplanSha256 = "")
    {
        var inventory = context.Inventory;
        var opportunity = context.Opportunity;
        var snapshot = context.Snapshot;
        var ledgerState = context.LedgerState;

        var reasons = ValidateMetadata(
            inventory,
            opportunity,
            proposal,
            snapshot,
            ledgerState.Ledger,
            continuation,
            priorSupportingTransitionReplanSha256);
        var selected = SelectRoutes(opportunity, proposal, reasons);
        var selectionRulesSatisfied = ValidateSelectionRules(
            inventory,
            proposal,
            selected,
            continuation,
            reasons);
        var allFrontier = ValidateParetoFrontier(selected, reasons);
        var aggregate = reasons.Count == 0
            ? AggregateCosts(selected, reasons)
            : null;
        var request = reasons.Count == 0
            ? BuildCommitRequest(
                opportunity,
                proposal,
                selected,
                ledgerState.Ledger,
                reasons)
            : null;
        var preflight = false;
        if (reasons.Count == 0)
        {
            if (request is null)
            {
                preflight = true;
            }
            else
            {
                var result = new ReservationPortfolioLedgerService().Commit(
                    ledgerState.Ledger,
                    snapshot,
                    request,
                    "portfolio-preflight");
                preflight = result.Accepted;
                if (!result.Accepted)
                {
                    reasons.AddRange(result.Errors.Select(error =>
                        "atomic_commit_preflight:" + error));
                }
            }
        }

        var blocking = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var ready = blocking.Length == 0 && preflight;
        return new AcquisitionRoutePortfolioAdmission
        {
            Status = ready
                ? "admitted_pending_atomic_reservation_commit"
                : "blocked_route_portfolio_admission",
            ProposalId = proposal.ProposalId,
            GoalId = proposal.GoalId,
            SnapshotStateHash = snapshot.StateHash,
            CommunityCenterProvenance =
                AcquisitionRouteCommunityCenterProvenanceSupport.Clone(
                    context.CommunityCenterProvenance),
            StrategyLedgerRevision = ledgerState.Ledger.Revision,
            PriorRolloutCheckpointSha256 =
                proposal.PriorRolloutCheckpointSha256,
            PriorSupportingTransitionReplanSha256 =
                proposal.PriorSupportingTransitionReplanSha256,
            CompletedAlternatives = (proposal.CompletedAlternatives ??
                    Array.Empty<
                        AcquisitionRoutePortfolioCompletedAlternatives>())
                .Where(value => value is not null)
                .Select(CloneCompletedAlternatives)
                .ToArray(),
            TargetTotalDay = opportunity.TargetTotalDay,
            RequirementInventorySha256 = context.RequirementInventorySha256,
            OpportunityCostSha256 = context.OpportunityCostSha256,
            ProposalSha256 = proposalSha256,
            StrategyLedgerSha256 = context.StrategyLedgerSha256,
            SnapshotSha256 = context.SnapshotSha256,
            SelectedRouteOccurrenceIds = selected
                .Select(route => route.RouteOccurrenceId)
                .Order(StringComparer.Ordinal)
                .ToArray(),
            ReplacedRouteOccurrenceIds = (proposal.ReplacedRouteOccurrenceIds ??
                    Array.Empty<string>())
                .Order(StringComparer.Ordinal)
                .ToArray(),
            SelectionRulesSatisfied = selectionRulesSatisfied,
            AllRoutesOnCompleteParetoFrontier = allFrontier,
            AggregateCostVector = aggregate,
            AtomicCommitRequired = request is not null,
            AtomicCommitPreflightPassed = preflight,
            AtomicCommitRequest = request,
            PortfolioAdmissionReady = ready,
            FormalTrainingAuthorized = false,
            BlockingReasons = blocking
        };
    }

    internal sealed record AcquisitionRoutePortfolioBuildContext(
        AuthoritativeRequirementInventoryReport Inventory,
        AcquisitionRouteTargetDateOpportunityCostReport Opportunity,
        SnapshotEnvelope Snapshot,
        AcquisitionStrategyLedgerState LedgerState,
        AcquisitionRouteCommunityCenterProvenance CommunityCenterProvenance,
        string RequirementInventorySha256,
        string OpportunityCostSha256,
        string StrategyLedgerSha256,
        string SnapshotSha256);

    internal sealed record AcquisitionRoutePortfolioContinuationEvidence(
        string PriorCheckpointSha256,
        AcquisitionRoutePortfolioCompletedAlternatives[]
            CompletedAlternatives);

    internal static AcquisitionRoutePortfolioCompletedAlternatives
        CloneCompletedAlternatives(
            AcquisitionRoutePortfolioCompletedAlternatives value) => new(
        value.RequirementSetId,
        value.RequirementId,
        value.AlternativeIndices.ToArray());

    private static AcquisitionRouteTargetDateOpportunityCostReport
        RecomputeOpportunityCost(AcquisitionRoutePortfolioInputs inputs) =>
        AcquisitionRouteTargetDateOpportunityCostBuilder.Build(
            inputs.RequirementInventoryPath,
            inputs.AcquisitionLoweringPath,
            inputs.MasterAnglerWindowsPath,
            inputs.CalendarResolutionPath,
            inputs.TargetDateCalendarPath,
            inputs.TargetDateUnlockPath,
            inputs.TargetDateFestivalPath,
            inputs.TargetDateLocationPath,
            inputs.TargetDateFacilityPath,
            inputs.TargetDateResourcePath,
            inputs.TargetDateCurrencyPath,
            inputs.TargetDateReservationPath,
            inputs.TargetDateProcessingPath,
            inputs.TargetDateFishingProbabilityPath,
            inputs.TargetDateStochasticRetryPath,
            inputs.TargetDateDailyTimeEnergyPath,
            inputs.FishingForecastManifestPath,
            inputs.StrategyLedgerPath,
            inputs.SnapshotPath,
            inputs.RouteTimingCalibrationPath);

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Options),
        JsonSerializer.Serialize(right, JsonDefaults.Options),
        StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
