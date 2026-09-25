using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCropPlantingSupportingRequest(
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowering,
        AcquisitionRouteDispatchCandidateMatch[] support)
    {
        const string routeDecision =
            "target-date-acquisition-route:full_shipment:crop:parsnip";
        var materialClaim = new MaterialReservationUpsertRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = ledger.Revision,
            ReservationId = "reservation:full_shipment:crop:parsnip:seed:0",
            SourceDecisionId = routeDecision,
            GoalId = "grandpa.stage1.21_points",
            NodeId = "player:123",
            SlotIndex = 0,
            QualifiedItemId = "(O)472",
            Quantity = 1,
            Purpose = "reserve target-date acquisition material input"
        };
        var claimSet = new AcquisitionRouteReservationClaimSet(
            routeDecision,
            snapshot.StateHash,
            ledger.Revision,
            true,
            false,
            Array.Empty<string>(),
            new[] { materialClaim },
            Array.Empty<CurrencyReservationUpsertRequest>());
        var reservation = new AcquisitionRouteTargetDateReservation(
            requirement.RouteOccurrenceId,
            null!,
            "resolved_inventory_reservation_match",
            true,
            true,
            "claim_proposed",
            claimSet,
            Array.Empty<string>(),
            Array.Empty<string>());
        var processing = new AcquisitionRouteTargetDateProcessing(
            requirement.RouteOccurrenceId,
            reservation,
            "resolved_processing_lead_time_miss",
            true,
            false,
            "crop_growth_or_ready_crop",
            new[]
            {
                new AcquisitionProcessingLeadTimeEvaluation(
                    "Farm",
                    "new_crop_from_seed",
                    "resolved_new_crop_requires_future_daily_growth",
                    "strict_lower_bound_from_native_daily_growth",
                    4,
                    1,
                    1,
                    null,
                    null,
                    false,
                    new[] { "transparent_planting_context" },
                    Array.Empty<string>())
            },
            new[] { "crop_output_not_ready_on_target_date" },
            Array.Empty<string>());
        var request = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                reservation,
                processing,
                snapshot,
                ledger,
                support,
                supportDeadlineTotalDay: 10);
        Require(request.SupportRequestReady &&
                request.DeadlineProofVerified &&
                request.ReservationClaimBoundToCandidate &&
                request.AtomicCommitPreflightPassed &&
                request.ExpectedReadyTotalDay == 4 &&
                request.AtomicCommitRequest is not null &&
                request.AtomicCommitRequest.MaterialClaims.Length == 1 &&
                request.AtomicCommitRequest.MaterialClaims[0].ReservationId ==
                    materialClaim.ReservationId &&
                !request.FormalTrainingAuthorized,
            "Deadline-safe crop planting did not produce an atomic support reservation request: " +
            request.Status + ":" + string.Join(",", request.BlockingReasons));
        var commitResult = new ReservationPortfolioLedgerService().Commit(
            ledger,
            snapshot,
            request.AtomicCommitRequest!,
            "2026-09-26T01:00:00Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "The shared reservation service rejected a valid crop support request.");
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                commitResult.Ledger!,
                snapshot,
                commitResult);
        Require(commitReceipt.SupportReservationCommitVerified &&
                commitReceipt.ExactActiveClaimSetVerified &&
                commitReceipt.SingleRevisionCommitVerified &&
                commitReceipt.CommittedLedgerRevision == ledger.Revision + 1 &&
                !commitReceipt.FormalTrainingAuthorized,
            "A valid crop support reservation commit was not verified: " +
            string.Join(",", commitReceipt.BlockingReasons));
        var staleLedgerReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                ledger,
                snapshot,
                commitResult);
        Require(!staleLedgerReceipt.SupportReservationCommitVerified &&
                staleLedgerReceipt.BlockingReasons.Contains(
                    "atomic_commit_did_not_advance_exactly_one_revision",
                    StringComparer.Ordinal),
            "A support commit receipt accepted an unmodified base ledger.");
        var extraClaimLedger = JsonSerializer.Deserialize<
            StrategyCommitmentLedger>(
            JsonSerializer.Serialize(
                commitResult.Ledger,
                JsonDefaults.Options),
            JsonDefaults.Options) ?? throw new InvalidDataException(
                "Support commit self-test ledger clone failed.");
        extraClaimLedger.MaterialReservations =
            extraClaimLedger.MaterialReservations.Append(new MaterialReservation
            {
                ReservationId = "unexpected-support-claim",
                Revision = 1,
                Status = StrategyCommitmentStatuses.Active,
                SourceDecisionId = routeDecision,
                SourceStateHash = snapshot.StateHash,
                GoalId = "grandpa.stage1.21_points",
                OwnerPlayerId = 123,
                NodeId = "player:123",
                SlotIndex = 0,
                QualifiedItemId = "(O)472",
                Quantity = 1,
                Purpose = "unexpected"
            }).ToArray();
        var extraClaimReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                extraClaimLedger,
                snapshot,
                commitResult);
        Require(!extraClaimReceipt.SupportReservationCommitVerified &&
                extraClaimReceipt.BlockingReasons.Contains(
                    "support_route_active_claim_set_mismatch",
                    StringComparer.Ordinal),
            "A support commit receipt accepted an extra route-owned claim.");

        var tooEarly = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                reservation,
                processing,
                snapshot,
                ledger,
                support,
                supportDeadlineTotalDay: 3);
        Require(!tooEarly.SupportRequestReady &&
                tooEarly.BlockingReasons.Contains(
                    "crop_planting_does_not_fit_support_deadline",
                    StringComparer.Ordinal) &&
                tooEarly.AtomicCommitRequest is null,
            "A crop planting transition that misses its deadline was admitted.");

        var wrongSlotClaim = materialClaim.WithSlot(1);
        var wrongReservation = reservation.WithClaim(new[] { wrongSlotClaim });
        var wrongSlot = AcquisitionRouteSupportingTransitionRequestBuilder
            .BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                wrongReservation,
                processing.WithReservation(wrongReservation),
                snapshot,
                ledger,
                support,
                supportDeadlineTotalDay: 10);
        Require(!wrongSlot.SupportRequestReady &&
                wrongSlot.BlockingReasons.Contains(
                    "crop_planting_candidate_seed_claim_mismatch",
                    StringComparer.Ordinal),
            "A crop support candidate escaped its exact reserved seed slot.");
    }

    private static MaterialReservationUpsertRequest WithSlot(
        this MaterialReservationUpsertRequest source,
        int slotIndex) => new()
        {
            StateHash = source.StateHash,
            ExpectedLedgerRevision = source.ExpectedLedgerRevision,
            ReservationId = source.ReservationId,
            SourceDecisionId = source.SourceDecisionId,
            GoalId = source.GoalId,
            NodeId = source.NodeId,
            SlotIndex = slotIndex,
            QualifiedItemId = source.QualifiedItemId,
            Quantity = source.Quantity,
            Purpose = source.Purpose
        };

    private static AcquisitionRouteTargetDateReservation WithClaim(
        this AcquisitionRouteTargetDateReservation source,
        MaterialReservationUpsertRequest[] materialClaims) => source with
        {
            ClaimSet = source.ClaimSet! with
            {
                MaterialClaims = materialClaims
            }
        };

    private static AcquisitionRouteTargetDateProcessing WithReservation(
        this AcquisitionRouteTargetDateProcessing source,
        AcquisitionRouteTargetDateReservation reservation) => source with
        {
            UpstreamRoute = reservation
        };
}
