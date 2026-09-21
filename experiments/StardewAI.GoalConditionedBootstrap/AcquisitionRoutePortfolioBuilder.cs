using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioBuilder
{
    private const string RouteDecisionPrefix =
        "target-date-acquisition-route:";

    public static AcquisitionRoutePortfolioAdmission Build(
        AcquisitionRoutePortfolioInputs inputs)
    {
        var inventoryPath = Path.GetFullPath(inputs.RequirementInventoryPath);
        var opportunityPath = Path.GetFullPath(
            inputs.TargetDateOpportunityCostPath);
        var proposalPath = Path.GetFullPath(inputs.ProposalPath);
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
        var proposal = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioProposal>(
            proposalPath,
            "Acquisition route portfolio proposal");
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            snapshotPath,
            "Acquisition route portfolio snapshot");
        using var snapshotDocument = JsonDocument.Parse(
            File.ReadAllText(snapshotPath));
        var ledgerState = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            snapshotDocument.RootElement);

        var reasons = ValidateMetadata(
            inventory,
            opportunity,
            proposal,
            snapshot,
            ledgerState.Ledger);
        var selected = SelectRoutes(opportunity, proposal, reasons);
        var selectionRulesSatisfied = ValidateSelectionRules(
            inventory,
            proposal,
            selected,
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
                ? request is null
                    ? "admitted_reservations_already_committed"
                    : "admitted_pending_atomic_reservation_commit"
                : "blocked_route_portfolio_admission",
            ProposalId = proposal.ProposalId,
            GoalId = proposal.GoalId,
            SnapshotStateHash = snapshot.StateHash,
            StrategyLedgerRevision = ledgerState.Ledger.Revision,
            TargetTotalDay = opportunity.TargetTotalDay,
            RequirementInventorySha256 =
                CurrentTeacherFrontierSupport.HashFile(inventoryPath),
            OpportunityCostSha256 =
                CurrentTeacherFrontierSupport.HashFile(opportunityPath),
            ProposalSha256 =
                CurrentTeacherFrontierSupport.HashFile(proposalPath),
            StrategyLedgerSha256 =
                CurrentTeacherFrontierSupport.HashFile(ledgerPath),
            SnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(snapshotPath),
            SelectedRouteOccurrenceIds = selected
                .Select(route => route.RouteOccurrenceId)
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
