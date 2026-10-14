namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueAlternativeComparisonProofBuilder
{
    private static MoneyRevenueAlternativeRouteArtifactEvidence
        VerifyRouteTransition(
            RolloutTransitionSource source,
            int transitionIndex,
            string acquisitionGoalId)
    {
        var inputs = source.Inputs;
        var opportunityPath = RequiredFile(
            inputs.TargetDateOpportunityCostPath,
            "Money revenue transition opportunity-cost report");
        var opportunity = Read<AcquisitionRouteTargetDateOpportunityCostReport>(
            opportunityPath,
            "Money revenue transition opportunity-cost report");
        var bindingPath = RequiredFile(
            source.ExecutionBindingPath,
            "Money revenue transition execution binding");
        var binding = Read<AcquisitionRouteExecutionBinding>(
            bindingPath,
            "Money revenue transition execution binding");
        Require(binding.DispatchBindingReady &&
                binding.RouteOccurrenceId == inputs.RouteOccurrenceId &&
                binding.BeforeStateHash == opportunity.SnapshotStateHash &&
                binding.OpportunityCostSha256 == Hash(opportunityPath) &&
                binding.BlockingReasons.Length == 0 &&
                !binding.FormalTrainingAuthorized,
            "Money revenue transition does not match its verified execution binding.");
        var routes = opportunity.Routes.Where(route =>
                route.RouteOccurrenceId == inputs.RouteOccurrenceId)
            .ToArray();
        Require(routes.Length == 1 &&
                routes[0].OpportunityCostAxisResolved &&
                routes[0].OpportunityCostMatchesTargetDate == true &&
                routes[0].CostVector is not null &&
                routes[0].BlockingReasons.Length == 0,
            "Money revenue transition route lacks one resolved opportunity cost.");
        var route = routes[0];
        var reservation = ReservationRoute(route);
        var reservationNotRequired =
            reservation.ClaimDisposition == "not_required";
        Require(reservation.ReservationAxisResolved &&
                reservation.InventoryReservationMatchesTargetDate == true &&
                reservation.ClaimDisposition is
                    "not_required" or
                    "claim_proposed" or
                    "claim_already_committed" &&
                reservation.BlockingReasons.Length == 0,
            "Money revenue transition reservation is not reusable.");
        var commitPath = RequiredFile(
            inputs.PortfolioCommitReceiptPath,
            "Money revenue transition portfolio commit receipt");
        var commit = Read<AcquisitionRoutePortfolioCommitReceipt>(
            commitPath,
            "Money revenue transition portfolio commit receipt");
        Require(commit.PortfolioCommitVerified &&
                commit.GoalId == acquisitionGoalId &&
                commit.SnapshotStateHash == opportunity.SnapshotStateHash &&
                commit.SelectedRouteOccurrenceIds.Count(id =>
                    id == route.RouteOccurrenceId) == 1 &&
                commit.CommittedLedgerRevision ==
                    commit.BaseLedgerRevision + 1 &&
                !commit.FormalTrainingAuthorized,
            "Money revenue transition commit does not own its route.");
        var reservationIds = MoneyRevenueAlternativeComparer
            .ValidateReservation(
                route.CostVector!,
                reservationNotRequired,
                reservation.ClaimSet,
                opportunity.SnapshotStateHash,
                commit.BaseLedgerRevision,
                acquisitionGoalId);
        Require(reservationIds.All(id =>
                commit.ActiveReservationIds.Contains(
                    id,
                    StringComparer.Ordinal)),
            "Money revenue transition commit omitted a route reservation.");
        return new MoneyRevenueAlternativeRouteArtifactEvidence(
            transitionIndex,
            route.RouteOccurrenceId,
            opportunity.SnapshotStateHash,
            commit.BaseLedgerRevision,
            commit.CommittedLedgerRevision,
            opportunityPath,
            Hash(opportunityPath),
            commitPath,
            Hash(commitPath),
            reservationNotRequired,
            reservationIds,
            route.CostVector!);
    }

    private static AcquisitionRouteTargetDateReservation ReservationRoute(
        AcquisitionRouteTargetDateOpportunityCost route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute;

    private static IEnumerable<RolloutTransitionSource> Sources(
        AcquisitionRoutePortfolioRolloutProofManifest manifest)
    {
        var initial = manifest.InitialCheckpointProof ??
            throw new InvalidDataException(
                "Money revenue acquisition rollout has no initial proof.");
        yield return new RolloutTransitionSource(
            initial.ExecutionInputs,
            initial.ExecutionBindingPath,
            initial.AfterSnapshotPath);
        foreach (var transitionValue in manifest.ContinuationTransitions ??
                     throw new InvalidDataException(
                         "Money revenue acquisition rollout has no transition list."))
        {
            var transition = transitionValue ??
                throw new InvalidDataException(
                    "Money revenue acquisition rollout contains a null transition.");
            yield return new RolloutTransitionSource(
                transition.ExecutionInputs,
                transition.ExecutionBindingPath,
                transition.AfterSnapshotPath);
        }
    }

    private sealed record RolloutTransitionSource(
        AcquisitionRouteExecutionBindingInputs Inputs,
        string ExecutionBindingPath,
        string AfterSnapshotPath);
}
