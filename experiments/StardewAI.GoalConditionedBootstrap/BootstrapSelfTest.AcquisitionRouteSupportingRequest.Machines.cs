using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMachineInputSupportingRequest(
        StardewAI.Contracts.State.SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowering,
        AcquisitionRouteDispatchCandidateMatch[] support)
    {
        const string routeDecision =
            "target-date-acquisition-route:full_shipment:machine:keg-wheat";
        var materialClaim = new MaterialReservationUpsertRequest
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = ledger.Revision,
            ReservationId =
                "reservation:full_shipment:machine:keg-wheat:material:0:0",
            SourceDecisionId = routeDecision,
            GoalId = "grandpa.stage1.21_points",
            NodeId = "player:123",
            SlotIndex = 0,
            QualifiedItemId = "(O)262",
            Quantity = 2,
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
        var schedule = new AcquisitionMachineProcessingScheduleBinding(
            "manual_input_processing",
            "(BC)12",
            64,
            15,
            "idle",
            -1,
            2,
            0,
            1750,
            null,
            null,
            1020,
            null);
        var processing = new AcquisitionRouteTargetDateProcessing(
            requirement.RouteOccurrenceId,
            reservation,
            "resolved_processing_lead_time_miss",
            true,
            false,
            "native_machine_processing_schedule",
            new[]
            {
                new AcquisitionProcessingLeadTimeEvaluation(
                    "Farm",
                    "manual_input_processing",
                    "resolved_machine_no_new_output_ready_on_target_date",
                    "exact_native_machine_timer_lower_bound",
                    null,
                    1,
                    1,
                    0,
                    0,
                    false,
                    new[] { "state.farm.machines.value[]" },
                    Array.Empty<string>(),
                    schedule)
            },
            new[]
            {
                "machine_processing_capacity_before_day_end_shortfall:0:2"
            },
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
                supportDeadlineTotalDay: 2,
                rankingSha256: new string('b', 64));
        Require(request.SupportRequestReady &&
                request.SupportTransitionKind == "machine_input_load" &&
                request.DeadlineProofVerified &&
                request.ReservationClaimBoundToCandidate &&
                request.AtomicCommitPreflightPassed &&
                request.ExpectedReadyTotalDay == 1 &&
                request.InputQualifiedItemId == "(O)262" &&
                request.InputSlotIndex == 0 &&
                request.InputRequiredQuantity == 1 &&
                request.MachineQualifiedItemId == "(BC)12" &&
                request.PredictedProcessingMinutes == 1750 &&
                request.AtomicCommitRequest?.MaterialClaims.Single().Quantity ==
                    2,
            "Deadline-safe machine input did not produce an atomic support request: " +
            request.Status + ":" + string.Join(",", request.BlockingReasons));

        var commitResult = new StardewAI.Core.Strategy
            .ReservationPortfolioLedgerService().Commit(
                ledger,
                snapshot,
                request.AtomicCommitRequest!,
                "2026-09-27T01:00:00Z");
        Require(commitResult.Accepted && commitResult.Ledger is not null,
            "The shared reservation service rejected a valid machine support request.");
        var commitReceipt =
            AcquisitionRouteSupportingTransitionCommitReceiptBuilder.BuildCore(
                request,
                ledger,
                commitResult.Ledger!,
                snapshot,
                commitResult,
                requestSha256: new string('c', 64));
        Require(commitReceipt.SupportReservationCommitVerified,
            "A valid machine support reservation commit was not verified: " +
            string.Join(",", commitReceipt.BlockingReasons));
        var compilation =
            AcquisitionRouteSupportingTransitionCompilationBuilder.BuildCore(
                request,
                commitReceipt,
                requirement,
                lowering,
                snapshot,
                commitResult.Ledger!,
                support,
                new string('b', 64),
                new string('c', 64),
                new string('d', 64));
        Require(compilation.DispatchReady &&
                compilation.ActionQueue?.Items.Single().OptionId ==
                    "executor.load_machine_input" &&
                compilation.SupportReservationCommitVerified &&
                compilation.FreshReplanRequiredAfterSuccess &&
                !compilation.TerminalReceiptEligible,
            "Committed machine support request did not compile through the shared queue: " +
            string.Join(",", compilation.BlockingReasons));

        var driftedDuration = CloneCandidate(support[0].Candidate);
        driftedDuration.Parameters = driftedDuration.Parameters.Select(parameter =>
            parameter.Name == "predicted_processing_minutes"
                ? Parameter(parameter.Name, "1740")
                : parameter).ToArray();
        var durationRejected =
            AcquisitionRouteSupportingTransitionRequestBuilder.BuildCore(
                "grandpa.stage1.21_points",
                requirement,
                lowering,
                reservation,
                processing,
                snapshot,
                ledger,
                new[]
                {
                    support[0] with { Candidate = driftedDuration }
                },
                supportDeadlineTotalDay: 2);
        Require(!durationRejected.SupportRequestReady &&
                durationRejected.BlockingReasons.Contains(
                    "machine_input_processing_duration_drifted",
                    StringComparer.Ordinal),
            "A machine input candidate with drifted native duration was admitted.");

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
                supportDeadlineTotalDay: 2);
        Require(!wrongSlot.SupportRequestReady &&
                wrongSlot.BlockingReasons.Contains(
                    "machine_input_candidate_material_claim_mismatch",
                    StringComparer.Ordinal),
            "A machine support candidate escaped its exact reserved input slot.");
    }
}
