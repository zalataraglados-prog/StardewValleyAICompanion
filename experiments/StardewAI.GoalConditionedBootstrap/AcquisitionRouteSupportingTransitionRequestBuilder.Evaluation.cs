using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    internal static AcquisitionRouteSupportingTransitionRequest BuildCore(
        string goalId,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowered,
        AcquisitionRouteTargetDateReservation reservation,
        AcquisitionRouteTargetDateProcessing processing,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        AcquisitionRouteDispatchCandidateMatch[] matches,
        int supportDeadlineTotalDay,
        IEnumerable<string>? inheritedReasons = null,
        string processingSha256 = "",
        string loweringSha256 = "",
        string ledgerSha256 = "",
        string snapshotSha256 = "",
        string rankingSha256 = "")
    {
        var reasons = (inheritedReasons ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        var currentDay = TryStateInt(
            snapshot,
            "time",
            "total_days",
            out var parsedDay)
                ? parsedDay
                : (int?)null;
        var supportTransitionKind = SupportingTransitionKind(requirement);
        ValidateRoute(
            supportTransitionKind,
            requirement,
            lowered,
            reservation,
            processing,
            reasons);
        if (matches.Length == 0)
        {
            reasons.Add(supportTransitionKind switch
            {
                "crop_planting" =>
                    "no_current_exact_crop_planting_support_candidate",
                "machine_input_load" =>
                    "no_current_exact_machine_input_support_candidate",
                _ => "no_current_exact_support_candidate"
            });
        }
        var selected = matches.FirstOrDefault(match =>
                CandidateCoveredByClaim(
                    match.Candidate,
                    reservation.ClaimSet,
                    supportTransitionKind)) ??
            matches.FirstOrDefault();
        var candidate = selected?.Candidate;
        var candidateEvaluation = EvaluateCandidate(
            supportTransitionKind,
            requirement,
            reservation,
            processing,
            snapshot,
            candidate,
            currentDay,
            supportDeadlineTotalDay);
        reasons.AddRange(candidateEvaluation.BlockingReasons);

        var supportRequestId = SupportRequestId(
            requirement.RouteOccurrenceId,
            snapshot.StateHash);
        ValidateClaimIdentity(
            reservation.ClaimSet,
            snapshot.StateHash,
            ledger.Revision,
            reasons);
        var commitRequest = reasons.Count == 0
            ? BuildCommitRequest(
                goalId,
                supportRequestId,
                snapshot.StateHash,
                ledger,
                reservation)
            : null;
        var preflight = false;
        if (commitRequest is not null)
        {
            var result = new ReservationPortfolioLedgerService().Commit(
                ledger,
                snapshot,
                commitRequest,
                "support-request-preflight");
            preflight = result.Accepted;
            if (!result.Accepted)
            {
                reasons.AddRange(result.Errors.Select(error =>
                    "support_atomic_commit_preflight:" + error));
            }
        }
        else if (reasons.Count == 0)
        {
            reasons.Add("support_atomic_commit_request_missing");
        }

        var blocking = reasons.Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var ready = blocking.Length == 0 && preflight;
        var claims = reservation.ClaimSet;
        return new AcquisitionRouteSupportingTransitionRequest
        {
            Status = ready
                ? "ready_for_atomic_support_reservation_commit"
                : "blocked_supporting_transition_request",
            SupportRequestId = supportRequestId,
            GoalId = goalId,
            RouteOccurrenceId = requirement.RouteOccurrenceId,
            RouteKind = requirement.RouteKind,
            SupportTransitionKind = supportTransitionKind,
            SourceId = requirement.SourceId,
            QualifiedItemId = requirement.QualifiedItemId,
            SourceStateHash = snapshot.StateHash,
            CurrentTotalDay = currentDay,
            SupportDeadlineTotalDay = supportDeadlineTotalDay,
            ExpectedReadyTotalDay = candidateEvaluation.ExpectedReadyTotalDay,
            AdjustedGrowDays = candidateEvaluation.AdjustedGrowDays,
            DaysRemainingInSeason = candidateEvaluation.DaysRemainingInSeason,
            SelectedCandidateId = candidate?.CandidateId ?? string.Empty,
            TargetLocationId = candidate?.LocationId ?? string.Empty,
            TargetTileX = candidate?.TileX,
            TargetTileY = candidate?.TileY,
            SeedId = supportTransitionKind == "crop_planting"
                ? candidate?.ItemId ?? string.Empty
                : string.Empty,
            SeedSlotIndex = supportTransitionKind == "crop_planting"
                ? candidate?.SlotIndex
                : null,
            InputQualifiedItemId =
                candidateEvaluation.InputQualifiedItemId,
            InputSlotIndex = candidateEvaluation.InputSlotIndex,
            InputRequiredQuantity =
                candidateEvaluation.InputRequiredQuantity,
            MachineQualifiedItemId =
                candidateEvaluation.MachineQualifiedItemId,
            PredictedProcessingMinutes =
                candidateEvaluation.PredictedProcessingMinutes,
            BaseLedgerRevision = ledger.Revision,
            TargetDateProcessingSha256 = processingSha256,
            AcquisitionLoweringSha256 = loweringSha256,
            BaseLedgerSha256 = ledgerSha256,
            SnapshotSha256 = snapshotSha256,
            RankingSha256 = rankingSha256,
            ReservationClaimIds = (claims?.MaterialClaims ??
                    Array.Empty<MaterialReservationUpsertRequest>())
                .Select(value => value.ReservationId)
                .Concat((claims?.CurrencyClaims ??
                    Array.Empty<CurrencyReservationUpsertRequest>())
                    .Select(value => value.ReservationId))
                .Order(StringComparer.Ordinal)
                .ToArray(),
            ReservationMaterialClaims = (claims?.MaterialClaims ??
                    Array.Empty<MaterialReservationUpsertRequest>())
                .OrderBy(value => value.ReservationId, StringComparer.Ordinal)
                .ToArray(),
            ReservationCurrencyClaims = (claims?.CurrencyClaims ??
                    Array.Empty<CurrencyReservationUpsertRequest>())
                .OrderBy(value => value.ReservationId, StringComparer.Ordinal)
                .ToArray(),
            DeadlineProofVerified =
                candidateEvaluation.DeadlineProofVerified,
            ReservationClaimBoundToCandidate =
                candidateEvaluation.ReservationClaimBoundToCandidate,
            AtomicCommitPreflightPassed = preflight,
            AtomicCommitRequest = commitRequest,
            SupportRequestReady = ready,
            FormalTrainingAuthorized = false,
            BlockingReasons = blocking
        };
    }

}
