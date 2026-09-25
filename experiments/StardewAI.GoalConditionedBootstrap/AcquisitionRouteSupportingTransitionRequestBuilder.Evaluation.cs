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
        ValidateRoute(
            requirement,
            lowered,
            reservation,
            processing,
            reasons);
        if (matches.Length == 0)
            reasons.Add("no_current_exact_crop_planting_support_candidate");
        var selected = matches.FirstOrDefault(match =>
                CandidateCoveredByClaim(
                    match.Candidate,
                    reservation.ClaimSet)) ??
            matches.FirstOrDefault();
        var candidate = selected?.Candidate;
        int? adjustedGrowDays = null;
        int? daysRemaining = null;
        int? expectedReadyDay = null;
        var deadlineVerified = false;
        var claimBound = false;
        if (candidate is not null)
        {
            adjustedGrowDays = ReadPositiveIntParameter(
                candidate,
                "adjusted_grow_days");
            daysRemaining = ReadNonNegativeIntParameter(
                candidate,
                "days_remaining_in_season");
            if (!candidate.Available)
            {
                reasons.Add("crop_planting_support_candidate_not_ready_now");
            }
            if (!currentDay.HasValue ||
                !adjustedGrowDays.HasValue ||
                !daysRemaining.HasValue)
            {
                reasons.Add("crop_planting_deadline_evidence_incomplete");
            }
            else
            {
                try
                {
                    expectedReadyDay = checked(
                        currentDay.Value + adjustedGrowDays.Value);
                }
                catch (OverflowException)
                {
                    reasons.Add("crop_planting_expected_ready_day_overflow");
                }
                deadlineVerified = expectedReadyDay.HasValue &&
                    supportDeadlineTotalDay > currentDay.Value &&
                    expectedReadyDay.Value <= supportDeadlineTotalDay &&
                    adjustedGrowDays.Value <= daysRemaining.Value &&
                    requirement.MatchingWindows.Any(window =>
                        currentDay.Value >= window.FirstTotalDay &&
                        expectedReadyDay.Value <= window.LastTotalDay);
                if (!deadlineVerified)
                    reasons.Add("crop_planting_does_not_fit_support_deadline");
            }
            claimBound = CandidateCoveredByClaim(
                candidate,
                reservation.ClaimSet);
            if (!claimBound)
                reasons.Add("crop_planting_candidate_seed_claim_mismatch");
        }

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
            SourceId = requirement.SourceId,
            QualifiedItemId = requirement.QualifiedItemId,
            SourceStateHash = snapshot.StateHash,
            CurrentTotalDay = currentDay,
            SupportDeadlineTotalDay = supportDeadlineTotalDay,
            ExpectedReadyTotalDay = expectedReadyDay,
            AdjustedGrowDays = adjustedGrowDays,
            DaysRemainingInSeason = daysRemaining,
            SelectedCandidateId = candidate?.CandidateId ?? string.Empty,
            TargetLocationId = candidate?.LocationId ?? string.Empty,
            TargetTileX = candidate?.TileX,
            TargetTileY = candidate?.TileY,
            SeedId = candidate?.ItemId ?? string.Empty,
            SeedSlotIndex = candidate?.SlotIndex,
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
            DeadlineProofVerified = deadlineVerified,
            ReservationClaimBoundToCandidate = claimBound,
            AtomicCommitPreflightPassed = preflight,
            AtomicCommitRequest = commitRequest,
            SupportRequestReady = ready,
            FormalTrainingAuthorized = false,
            BlockingReasons = blocking
        };
    }

}
