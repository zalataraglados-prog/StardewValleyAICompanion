using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
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
        string rankingSha256 = "",
        AcquisitionMachineCapacitySupportBinding? capacityBinding = null)
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
        var playerInventoryNodeId = CurrentPlayerInventoryNodeId(snapshot);
        var selected = matches.FirstOrDefault(match =>
                CandidateCoveredByClaim(
                    match.Candidate,
                    reservation.ClaimSet,
                    SupportingTransitionKind(
                        requirement,
                        match.Candidate),
                    playerInventoryNodeId)) ??
            matches.FirstOrDefault();
        var candidate = selected?.Candidate;
        var supportTransitionKind = SupportingTransitionKind(
            requirement,
            candidate);
        ValidateRoute(
            supportTransitionKind,
            requirement,
            lowered,
            reservation,
            processing,
            capacityBinding,
            reasons);
        if (matches.Length == 0)
        {
            reasons.Add(supportTransitionKind switch
            {
                "crop_planting" =>
                    "no_current_exact_crop_planting_support_candidate",
                "machine_input_load" =>
                    "no_current_exact_machine_input_support_candidate",
                "machine_input_purchase" =>
                    "no_current_exact_machine_input_purchase_candidate",
                "machine_capacity_establishment" =>
                    "no_current_exact_machine_capacity_support_candidate",
                _ => "no_current_exact_support_candidate"
            });
        }
        var candidateEvaluation = EvaluateCandidate(
            supportTransitionKind,
            requirement,
            reservation,
            processing,
            snapshot,
            candidate,
            currentDay,
            supportDeadlineTotalDay,
            capacityBinding);
        reasons.AddRange(candidateEvaluation.BlockingReasons);

        var supportRequestId = SupportRequestId(
            requirement.RouteOccurrenceId,
            snapshot.StateHash);
        if (supportTransitionKind != "machine_capacity_establishment")
        {
            ValidateClaimIdentity(
                reservation.ClaimSet,
                snapshot.StateHash,
                ledger.Revision,
                reasons);
        }
        var machineSupportIntent =
            supportTransitionKind == "machine_capacity_establishment" &&
            candidate is not null &&
            capacityBinding is not null
                ? BuildMachineSupportIntentRequest(
                    goalId,
                    snapshot,
                    ledger,
                    candidate,
                    capacityBinding)
                : null;
        if (supportTransitionKind == "machine_capacity_establishment" &&
            machineSupportIntent is null)
        {
            reasons.Add("machine_capacity_support_intent_binding_missing");
        }
        var commitRequest = reasons.Count == 0
            ? BuildCommitRequest(
                goalId,
                supportRequestId,
                snapshot.StateHash,
                ledger,
                reservation,
                machineSupportIntent)
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
            MachineSupportIntentId =
                machineSupportIntent?.IntentId ?? string.Empty,
            MachineSupportIntentStage =
                machineSupportIntent?.Stage ?? string.Empty,
            MachineCapacitySupportSourcesJson =
                machineSupportIntent?.SupportSourcesJson ?? "[]",
            PredictedProcessingMinutes =
                candidateEvaluation.PredictedProcessingMinutes,
            PurchaseStage = candidateEvaluation.PurchaseStage,
            PurchasePrerequisite = candidateEvaluation.PurchasePrerequisite,
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
            SupportMaterialConsumptions =
                candidateEvaluation.MaterialConsumptions,
            SupportMaterialRelocations =
                candidateEvaluation.MaterialRelocations,
            SupportCurrencyConsumptions =
                candidateEvaluation.CurrencyConsumptions,
            MaterialTransferIntent =
                candidateEvaluation.MaterialTransferIntent,
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

    private static MachineSupportIntentUpsertRequest
        BuildMachineSupportIntentRequest(
            string goalId,
            SnapshotEnvelope snapshot,
            StrategyCommitmentLedger ledger,
            PolicyEventCandidatePrediction candidate,
            AcquisitionMachineCapacitySupportBinding binding) => new()
        {
            StateHash = snapshot.StateHash,
            ExpectedLedgerRevision = ledger.Revision,
            IntentId = binding.IntentId,
            Stage = candidate.Kind == "craft_machine_item"
                ? MachineSupportIntentStages.CraftSelected
                : MachineSupportIntentStages.PlacementBound,
            SourceDecisionId = candidate.CandidateId,
            GoalId = goalId,
            QualifiedItemId = binding.MachineQualifiedItemId,
            ItemId = candidate.ItemId,
            DemandClass = "acquisition_route_requirement",
            SupportKind = "machine_capacity_acquisition_route",
            EvidenceStatus = binding.SupportSourcesJson,
            TaskSourcesJson = "[]",
            SupportSourcesJson = binding.SupportSourcesJson,
            GrossBenefit = 0,
            OpportunityCost = 0,
            NetBenefit = 0,
            SupportScore = 0.12,
            RequiredAdditionalMachineCount = 1,
            TargetLocationId = candidate.Kind == "place_machine_item"
                ? candidate.LocationId
                : string.Empty,
            TargetTileX = candidate.Kind == "place_machine_item"
                ? candidate.TileX
                : null,
            TargetTileY = candidate.Kind == "place_machine_item"
                ? candidate.TileY
                : null
        };

}
