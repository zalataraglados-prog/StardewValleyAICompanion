using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioRolloutCheckpointBuilder
{
    public static AcquisitionRoutePortfolioRolloutCheckpoint BuildInitial(
        AcquisitionRouteExecutionBindingInputs inputs,
        string executionBindingPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string freshTerminalReceiptPath,
        string runId,
        string executorVersion,
        string settlementRequestPath,
        string settlementResultPath,
        string settledLedgerPath,
        string settlementReceiptPath)
    {
        var expectedSettlement =
            AcquisitionRoutePortfolioSettlementBuilder.BuildReceipt(
                inputs,
                executionBindingPath,
                executionReceiptPath,
                afterSnapshotPath,
                freshTerminalReceiptPath,
                runId,
                executorVersion,
                settlementRequestPath,
                settlementResultPath,
                settledLedgerPath);
        var settlement = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioSettlementReceipt>(
            Path.GetFullPath(settlementReceiptPath),
            "Acquisition route portfolio settlement receipt");
        Require(EqualJson(settlement, expectedSettlement) &&
                settlement.ReservationLifecycleVerified &&
                settlement.FreshReplanRequired &&
                !settlement.FormalTrainingAuthorized,
            "Initial rollout settlement receipt is not verified.");
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreferenceRequest>(
            Path.GetFullPath(inputs.PortfolioPreferenceRequestPath),
            "Acquisition route portfolio Teacher preference request");
        var preference = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioTeacherPreference>(
            Path.GetFullPath(inputs.PortfolioTeacherPreferencePath),
            "Acquisition route portfolio Teacher preference");
        var expectedPreference =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.Build(
                AcquisitionRouteExecutionBindingBuilder.PortfolioInputs(inputs),
                inputs.PortfolioPreferenceRequestPath);
        Require(EqualJson(preference, expectedPreference) &&
                preference.TeacherPreferenceLabelEligible &&
                preference.SelectedProposal is not null &&
                preference.SelectedAdmission is not null &&
                !preference.FormalTrainingAuthorized,
            "Initial rollout Teacher preference is not verified.");
        var opportunity = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteTargetDateOpportunityCostReport>(
            Path.GetFullPath(inputs.TargetDateOpportunityCostPath),
            "Acquisition route target-date opportunity cost");
        var matchingRoutes = opportunity.Routes.Where(candidate =>
                candidate.RouteOccurrenceId == settlement.RouteOccurrenceId)
            .ToArray();
        Require(matchingRoutes.Length == 1,
            "Settled rollout route is not present exactly once.");
        var route = matchingRoutes[0];
        var requirement = AcquisitionRoutePortfolioBuilder.RequirementRoute(
            route);
        var proposal = preference.SelectedProposal ??
            throw new InvalidDataException(
                "Initial rollout Teacher proposal is missing.");
        Require(proposal.SelectedRouteOccurrenceIds.Count(value =>
                    value == route.RouteOccurrenceId) == 1 &&
                settlement.GoalId == preference.GoalId &&
                settlement.PortfolioId ==
                    "target-date-acquisition-portfolio:" +
                    proposal.ProposalId &&
                settlement.AfterStateHash.Length > 0,
            "Settled route is not owned by the selected portfolio.");
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            Path.GetFullPath(inputs.RequirementInventoryPath),
            "Authoritative requirement inventory");
        var progress = BuildProgress(inventory, request, requirement);
        var selected = proposal.SelectedRouteOccurrenceIds
            .Order(StringComparer.Ordinal)
            .ToArray();
        var completed = new[] { route.RouteOccurrenceId };
        var pending = selected.Except(completed, StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        using var afterDocument = JsonDocument.Parse(
            File.ReadAllText(Path.GetFullPath(afterSnapshotPath)));
        var ledger = AcquisitionStrategyLedgerReader.Read(
            Path.GetFullPath(settledLedgerPath),
            afterDocument.RootElement).Ledger;
        Require(ledger.Revision == settlement.SettledLedgerRevision &&
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(settledLedgerPath)) ==
                settlement.SettledLedgerSha256,
            "Initial rollout settled ledger drifted.");
        var selectedDecisionIds = selected.Select(value =>
                AcquisitionRoutePortfolioBuilder.RouteDecisionPrefix + value)
            .ToHashSet(StringComparer.Ordinal);
        var selectedActiveClaimCount = ledger.MaterialReservations.Count(row =>
                row.Status == StrategyCommitmentStatuses.Active &&
                selectedDecisionIds.Contains(row.SourceDecisionId)) +
            ledger.CurrencyReservations.Count(row =>
                row.Status == StrategyCommitmentStatuses.Active &&
                selectedDecisionIds.Contains(row.SourceDecisionId));
        var complete = progress.All(scope => scope.ScopeComplete) &&
            pending.Length == 0 && selectedActiveClaimCount == 0;
        var requestSha = CurrentTeacherFrontierSupport.HashFile(
            Path.GetFullPath(inputs.PortfolioPreferenceRequestPath));
        return new AcquisitionRoutePortfolioRolloutCheckpoint
        {
            Status = complete
                ? "verified_initial_portfolio_completion"
                : "verified_initial_transition_fresh_replan_required",
            RolloutId = RolloutId(requestSha),
            GoalId = preference.GoalId,
            RootPreferenceRequestSha256 = requestSha,
            CurrentTeacherPreferenceSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(inputs.PortfolioTeacherPreferencePath)),
            CurrentProposalId = proposal.ProposalId,
            CurrentPortfolioId = settlement.PortfolioId,
            LatestSettlementReceiptSha256 =
                CurrentTeacherFrontierSupport.HashFile(
                    Path.GetFullPath(settlementReceiptPath)),
            LatestStateHash = settlement.AfterStateHash,
            LatestLedgerRevision = settlement.SettledLedgerRevision,
            LatestLedgerSha256 = settlement.SettledLedgerSha256,
            TransitionCount = 1,
            ScopedProgress = progress,
            SelectedRouteOccurrenceIds = selected,
            CompletedRouteOccurrenceIds = completed,
            PendingSelectedRouteOccurrenceIds = pending,
            CheckpointVerified = true,
            PortfolioCompletionVerified = complete,
            FreshReplanRequired = !complete,
            FormalTrainingAuthorized = false,
            BlockingReasons = Array.Empty<string>()
        };
    }

    internal static AcquisitionRoutePortfolioScopeProgress[] BuildProgress(
        AuthoritativeRequirementInventoryReport inventory,
        AcquisitionRoutePortfolioTeacherPreferenceRequest request,
        AcquisitionRouteTargetDateUnlock completed)
    {
        var groups = inventory.RequirementSets.SelectMany(set =>
                set.Groups.Select(group => new
                {
                    set.RequirementSetId,
                    Group = group
                }))
            .ToDictionary(row => ScopeKey(
                    row.RequirementSetId,
                    row.Group.RequirementId),
                row => row,
                StringComparer.Ordinal);
        return request.ScopedRequirements
            .OrderBy(scope => ScopeKey(
                scope.RequirementSetId,
                scope.RequirementId), StringComparer.Ordinal)
            .Select(scope =>
            {
                var row = groups[ScopeKey(
                    scope.RequirementSetId,
                    scope.RequirementId)];
                var indices = row.RequirementSetId ==
                        completed.RequirementSetId &&
                    row.Group.RequirementId == completed.RequirementId
                        ? new[] { completed.AlternativeIndex }
                        : Array.Empty<int>();
                var required = row.Group.SelectionRule == "all_required"
                    ? row.Group.Alternatives.Length
                    : row.Group.RequiredAlternativeCount;
                var remaining = Math.Max(0, required - indices.Length);
                return new AcquisitionRoutePortfolioScopeProgress(
                    row.RequirementSetId,
                    row.Group.RequirementId,
                    row.Group.SelectionRule,
                    required,
                    row.Group.Alternatives.Length,
                    indices,
                    remaining,
                    remaining == 0);
            }).ToArray();
    }

    private static string ScopeKey(string setId, string requirementId) =>
        Uri.EscapeDataString(setId) + "/" +
        Uri.EscapeDataString(requirementId);

    private static string RolloutId(string requestSha256)
    {
        var digest = Convert.ToHexString(SHA256.HashData(
                Encoding.UTF8.GetBytes(requestSha256)))
            .ToLowerInvariant();
        return "target-date-acquisition-rollout:" + digest;
    }

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
