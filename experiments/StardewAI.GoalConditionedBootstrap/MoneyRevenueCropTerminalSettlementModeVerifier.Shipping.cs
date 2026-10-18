using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class MoneyRevenueCropTerminalSettlementModeVerifier
{
    private const string NativeShippingOptionId =
        "executor.ship_inventory_item_to_bin";

    internal static MoneyRevenueCropTerminalSettlementVerification
        VerifyShipping(
            MoneyRevenueCropTerminalSettlementProofRequest request,
            SnapshotEnvelope terminalStart,
            MoneyRevenueCropOutputRetentionProof retention)
    {
        Require(string.IsNullOrWhiteSpace(request.ShopSaleEpisodePath),
            "Money Revenue crop shipping settlement shape is invalid.");
        var batch = VerifyShippingDepositBatch(
            terminalStart,
            request.RouteTransitions,
            request.ShippingDepositTransitions,
            retention.HarvestItemQualifiedId,
            retention.MinimumUnits,
            retention.MinimumGrossRevenue,
            expectedPendingValue: 0,
            transitionIndexOffset: 0);
        var transitions = request.ShippingDepositTransitions;
        var recurrence = VerifyShippingRecurrence(
            request.RevenueRecurrenceManifestPath,
            request.RevenueRecurrenceReceiptPath,
            transitions[^1].AfterSnapshotPath,
            batch.After.StateHash,
            batch.PendingValueAfter);

        return new MoneyRevenueCropTerminalSettlementVerification(
            MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement,
            batch.TerminalActionBefore.StateHash,
            recurrence.FinalStateHash,
            batch.RouteTransitions,
            string.Empty,
            string.Empty,
            string.Empty,
            batch.Deposits,
            batch.SlotIndex,
            batch.SettledQuantity,
            batch.UnitPrice,
            checked((int)batch.PendingValueAfter),
            recurrence.ManifestPath,
            recurrence.ManifestSha256,
            recurrence.ReceiptPath,
            recurrence.ReceiptSha256);
    }

    internal static MoneyRevenueCropShippingDepositBatchVerification
        VerifyShippingDepositBatch(
            SnapshotEnvelope terminalStart,
            MoneyRevenueTerminalQueueTransitionProof[] routeTransitions,
            MoneyRevenueTerminalQueueTransitionProof[] transitions,
            string qualifiedItemId,
            int expectedQuantity,
            long minimumGrossRevenue,
            long expectedPendingValue,
            int transitionIndexOffset)
    {
        Require(expectedQuantity > 0 &&
                minimumGrossRevenue > 0 &&
                expectedPendingValue >= 0 &&
                transitionIndexOffset >= 0 &&
                transitions is { Length: > 0 } &&
                transitions.Length == expectedQuantity,
            "Money Revenue crop shipping deposit batch shape is invalid.");
        Require(transitions.All(row => row is not null &&
                    !string.IsNullOrWhiteSpace(row.TransitionId)) &&
                transitions.Select(row => row.TransitionId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                    transitions.Length,
            "Money Revenue crop shipping transition identity is invalid.");

        var expected = terminalStart;
        MoneyRevenueResourceNeutralTerminalRouteVerification? route = null;
        var deposits = new List<MoneyRevenueCropShippingDepositEvidence>();
        long pendingValue = expectedPendingValue;
        int? unitPrice = null;
        int? slotIndex = null;
        for (var index = 0; index < transitions.Length; index++)
        {
            var transition = transitions[index];
            var queuePath = RequiredFile(
                transition.QueuePath,
                "Money Revenue crop shipping queue");
            var beforePath = RequiredFile(
                transition.BeforeSnapshotPath,
                "Money Revenue crop shipping before snapshot");
            var receiptPath = RequiredFile(
                transition.ExecutionReceiptPath,
                "Money Revenue crop shipping execution receipt");
            var afterPath = RequiredFile(
                transition.AfterSnapshotPath,
                "Money Revenue crop shipping after snapshot");
            var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
                queuePath,
                "Money Revenue crop shipping queue");
            var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                beforePath,
                "Money Revenue crop shipping before snapshot");
            var receipt = CurrentTeacherFrontierSupport.Read<
                QueueExecutionReceiptEnvelope>(
                    receiptPath,
                    "Money Revenue crop shipping execution receipt");
            var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                afterPath,
                "Money Revenue crop shipping after snapshot");
            var receiptReasons = QueueExecutionReceiptValidator.Validate(
                queue,
                before,
                receipt,
                after,
                transition.RunId,
                transition.ExecutorVersion,
                transition.SelectedCandidateId,
                before.StateHash,
                requireTeacherPreferenceStateRebound: false);
            Require(receiptReasons.Length == 0,
                "Money Revenue crop shipping receipt was rejected: " +
                string.Join(",", receiptReasons));
            var queueItems = queue.Items ?? Array.Empty<ActionQueueItem>();
            var optionIds = queueItems.Select(row => row.OptionId).ToArray();
            Require(optionIds.Length == 1 &&
                    optionIds[0] == NativeShippingOptionId,
                "Money Revenue crop shipping deposit must be one exact native action.");
            var deposit = NativeShippingDepositEvidenceVerifier.Verify(
                queueItems,
                receipt,
                before,
                after,
                qualifiedItemId,
                expectedQuantity: 1);
            Require(deposit.Verified &&
                    deposit.SlotIndex.HasValue &&
                    deposit.UnitPrice.HasValue &&
                    deposit.BeforePendingSettlementValue.HasValue &&
                    deposit.AfterPendingSettlementValue.HasValue,
                "Money Revenue crop native shipping deposit was rejected: " +
                string.Join(",", deposit.BlockingReasons));
            var exactUnitPrice = deposit.UnitPrice ??
                throw new InvalidDataException(
                    "Money Revenue crop shipping unit price is missing.");
            var exactSlotIndex = deposit.SlotIndex ??
                throw new InvalidDataException(
                    "Money Revenue crop shipping slot is missing.");
            var exactQuantity = deposit.Quantity ??
                throw new InvalidDataException(
                    "Money Revenue crop shipping quantity is missing.");
            var beforePending = deposit.BeforePendingSettlementValue ??
                throw new InvalidDataException(
                    "Money Revenue crop shipping before value is missing.");
            var afterPending = deposit.AfterPendingSettlementValue ??
                throw new InvalidDataException(
                    "Money Revenue crop shipping after value is missing.");
            if (index == 0)
            {
                route = MoneyRevenueResourceNeutralTerminalRouteVerifier.Verify(
                    terminalStart,
                    routeTransitions,
                    new MoneyRevenueTerminalContinuationIdentity(
                        NativeShippingOptionId,
                        qualifiedItemId,
                        exactSlotIndex,
                        1,
                        exactUnitPrice,
                        string.Empty));
                expected = route.TerminalActionBefore;
            }
            Require(BootstrapArtifactGuard.EqualJson(before, expected) &&
                    before.StateHash == expected.StateHash,
                "Money Revenue crop shipping deposits are not checkpoint-contiguous.");
            unitPrice ??= exactUnitPrice;
            slotIndex ??= exactSlotIndex;
            Require(beforePending == pendingValue &&
                    afterPending == checked(pendingValue + exactUnitPrice),
                "Money Revenue crop shipping deposit value lineage drifted.");
            pendingValue = afterPending;
            deposits.Add(new MoneyRevenueCropShippingDepositEvidence(
                transitionIndexOffset + index,
                transition.TransitionId,
                Hash(queuePath),
                Hash(beforePath),
                Hash(receiptPath),
                Hash(afterPath),
                before.StateHash,
                after.StateHash,
                deposit.QueueItemId,
                exactSlotIndex,
                exactQuantity,
                exactUnitPrice,
                beforePending,
                afterPending,
                optionIds));
            expected = after;
        }
        Require(pendingValue - expectedPendingValue >= minimumGrossRevenue,
            "Money Revenue crop shipping deposits do not cover conservative wave revenue.");
        var exactRoute = route ?? throw new InvalidDataException(
            "Money Revenue crop shipping route was not verified.");

        return new MoneyRevenueCropShippingDepositBatchVerification(
            exactRoute.TerminalActionBefore,
            expected,
            exactRoute.Evidence,
            deposits.ToArray(),
            slotIndex!.Value,
            unitPrice!.Value,
            deposits.Count,
            expectedPendingValue,
            pendingValue);
    }

    internal static MoneyRevenueCropShippingRecurrenceVerification
        VerifyShippingRecurrence(
            string recurrenceManifest,
            string recurrenceReceipt,
            string expectedBeforeSnapshotPath,
            string expectedBeforeStateHash,
            long pendingValue)
    {
        Require(pendingValue > 0,
            "Money Revenue crop shipping recurrence value is invalid.");
        var recurrenceManifestPath = RequiredFile(
            recurrenceManifest,
            "Money Revenue crop shipping recurrence manifest");
        var recurrenceReceiptPath = RequiredFile(
            recurrenceReceipt,
            "Money Revenue crop shipping recurrence receipt");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofManifest>(
                recurrenceManifestPath,
                "Money Revenue crop shipping recurrence manifest");
        var rebuilt = MoneyRevenueRecurrenceProofBuilder.Build(
            recurrenceManifestPath);
        var recurrence = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofReceipt>(
                recurrenceReceiptPath,
                "Money Revenue crop shipping recurrence receipt");
        Require(manifest.Transitions is not null &&
                manifest.Transitions.Length == 1 &&
                manifest.Transitions[0].ProofKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement &&
                SamePath(
                    manifest.Transitions[0].BeforeSnapshotPath,
                    expectedBeforeSnapshotPath) &&
                BootstrapArtifactGuard.EqualJson(recurrence, rebuilt) &&
                recurrence.ProofVerified &&
                recurrence.Prefix is not null &&
                recurrence.Transitions.Length == 1 &&
                recurrence.Transitions[0].Progression.TransitionKind ==
                    MoneyRevenueTransitionKinds.NativeShippingDaySettlement &&
                recurrence.Prefix.InitialStateHash == expectedBeforeStateHash &&
                recurrence.Prefix.TotalRevenue == pendingValue &&
                recurrence.Transitions[0].Progression
                    .ConsumedPendingShippingValue == pendingValue &&
                !recurrence.FormalProductTrainingAuthorized,
            "Money Revenue crop native shipping recurrence is not exact.");
        var prefix = recurrence.Prefix ??
            throw new InvalidDataException(
                "Money Revenue crop shipping recurrence prefix is missing.");

        return new MoneyRevenueCropShippingRecurrenceVerification(
            prefix.FinalStateHash,
            recurrenceManifestPath,
            Hash(recurrenceManifestPath),
            recurrenceReceiptPath,
            Hash(recurrenceReceiptPath));
    }
}
