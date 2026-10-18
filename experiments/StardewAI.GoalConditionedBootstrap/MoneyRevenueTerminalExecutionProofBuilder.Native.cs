using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalExecutionProofBuilder
{
    private static TerminalNativeVerification VerifyNativeTerminal(
        MoneyRevenueCounterfactualProofRequest request,
        MoneyRevenueTerminalAlternativeProof proof,
        TerminalRouteVerification route)
    {
        var recurrenceManifestPath = RequiredFile(
            proof.RevenueRecurrenceManifestPath,
            "Money Revenue terminal recurrence manifest");
        var recurrenceReceiptPath = RequiredFile(
            proof.RevenueRecurrenceReceiptPath,
            "Money Revenue terminal recurrence receipt");
        var recurrenceManifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofManifest>(
            recurrenceManifestPath,
            "Money Revenue terminal recurrence manifest");
        var expectedRecurrence = MoneyRevenueRecurrenceProofBuilder.Build(
            recurrenceManifestPath);
        var recurrence = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofReceipt>(
            recurrenceReceiptPath,
            "Money Revenue terminal recurrence receipt");
        Require(EqualJson(recurrence, expectedRecurrence) &&
                recurrence.ProofVerified &&
                recurrence.Prefix is not null &&
                !recurrence.FormalProductTrainingAuthorized &&
                recurrenceManifest.Transitions.Length > 0,
            "Money Revenue terminal recurrence is not complete and exact.");

        return request.NativeShopSaleProofRequired
            ? VerifyNativeShopSale(
                request,
                proof,
                route,
                recurrenceManifest,
                recurrence,
                recurrenceManifestPath,
                recurrenceReceiptPath)
            : VerifyNativeShippingDeposit(
                request,
                proof,
                route,
                recurrenceManifest,
                recurrence,
                recurrenceManifestPath,
                recurrenceReceiptPath);
    }

    private static TerminalNativeVerification VerifyNativeShopSale(
        MoneyRevenueCounterfactualProofRequest request,
        MoneyRevenueTerminalAlternativeProof proof,
        TerminalRouteVerification route,
        MoneyRevenueRecurrenceProofManifest recurrenceManifest,
        MoneyRevenueRecurrenceProofReceipt recurrence,
        string recurrenceManifestPath,
        string recurrenceReceiptPath)
    {
        Require(request.RequiredRevenueTransitionProofKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShopSale &&
                request.NativeShopSaleProofRequired &&
                !request.NativeShippingDepositProofRequired &&
                !request.NativeShippingSettlementProofRequired &&
                !string.IsNullOrWhiteSpace(proof.ShopSaleEpisodePath) &&
                proof.ShippingDepositTransition is null,
            "Money Revenue shop terminal proof shape is invalid.");
        var sale = NativeShopSaleEpisodeVerifier.Verify(
            proof.ShopSaleEpisodePath);
        var candidate = request.TerminalCandidate;
        Require(sale.Progression.Before.StateHash ==
                    route.TerminalActionBefore.StateHash &&
                sale.Progression.Before.SaveId ==
                    route.TerminalActionBefore.SaveId.Value &&
                sale.Progression.Before.PlayerId ==
                    route.TerminalActionBefore.PlayerId.Value &&
                sale.ShopId == candidate.ShopId &&
                sale.QualifiedItemId == candidate.QualifiedItemId &&
                sale.SlotIndex == candidate.SlotIndex &&
                sale.Quantity == candidate.Quantity &&
                sale.UnitPrice == candidate.UnitPrice,
            "Money Revenue native shop sale does not match its terminal request.");
        var first = recurrenceManifest.Transitions[0];
        Require(first.ProofKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShopSale &&
                Path.GetFullPath(first.ShopSaleEpisodePath) ==
                    sale.ExecutionEpisodePath &&
                recurrence.Transitions.Length > 0 &&
                EqualJson(
                    recurrence.Transitions[0].Progression,
                    sale.Progression) &&
                recurrence.Prefix!.InitialStateHash ==
                    sale.Progression.Before.StateHash,
            "Money Revenue shop sale is not the first verified recurrence transition.");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            sale.AfterSnapshotPath,
            "Money Revenue native shop sale after snapshot");
        return new TerminalNativeVerification(
            sale.RunId,
            sale.Progression.Before.StateHash,
            after,
            sale.AfterSnapshotPath,
            new[]
            {
                Artifact("shop_sale_episode", sale.ExecutionEpisodePath,
                    sale.ExecutionEpisodeSha256),
                Artifact("shop_sale_before_snapshot", sale.BeforeSnapshotPath,
                    sale.BeforeSnapshotSha256),
                Artifact("shop_sale_execution_result", sale.ExecutionResultPath,
                    sale.ExecutionResultSha256),
                Artifact("shop_sale_after_snapshot", sale.AfterSnapshotPath,
                    sale.AfterSnapshotSha256)
            },
            recurrenceManifestPath,
            Hash(recurrenceManifestPath),
            recurrenceReceiptPath,
            Hash(recurrenceReceiptPath));
    }

    private static TerminalNativeVerification VerifyNativeShippingDeposit(
        MoneyRevenueCounterfactualProofRequest request,
        MoneyRevenueTerminalAlternativeProof proof,
        TerminalRouteVerification route,
        MoneyRevenueRecurrenceProofManifest recurrenceManifest,
        MoneyRevenueRecurrenceProofReceipt recurrence,
        string recurrenceManifestPath,
        string recurrenceReceiptPath)
    {
        Require(request.RequiredRevenueTransitionProofKind ==
                    "native_shipping_deposit_and_day_settlement" &&
                !request.NativeShopSaleProofRequired &&
                request.NativeShippingDepositProofRequired &&
                request.NativeShippingSettlementProofRequired &&
                string.IsNullOrWhiteSpace(proof.ShopSaleEpisodePath) &&
                proof.ShippingDepositTransition is not null,
            "Money Revenue shipping terminal proof shape is invalid.");
        var depositProof = proof.ShippingDepositTransition!;
        var queuePath = RequiredFile(
            depositProof.QueuePath,
            "Money Revenue shipping deposit queue");
        var beforePath = RequiredFile(
            depositProof.BeforeSnapshotPath,
            "Money Revenue shipping deposit before snapshot");
        var receiptPath = RequiredFile(
            depositProof.ExecutionReceiptPath,
            "Money Revenue shipping deposit execution receipt");
        var afterPath = RequiredFile(
            depositProof.AfterSnapshotPath,
            "Money Revenue shipping deposit after snapshot");
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(
            queuePath,
            "Money Revenue shipping deposit queue");
        var before = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            beforePath,
            "Money Revenue shipping deposit before snapshot");
        var receipt = CurrentTeacherFrontierSupport.Read<
            QueueExecutionReceiptEnvelope>(
            receiptPath,
            "Money Revenue shipping deposit execution receipt");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Money Revenue shipping deposit after snapshot");
        Require(EqualJson(before, route.TerminalActionBefore) &&
                before.StateHash == route.TerminalActionBefore.StateHash,
            "Money Revenue shipping deposit is not route-contiguous.");
        var options = (queue.Items ?? Array.Empty<ActionQueueItem>())
            .Select(row => row.OptionId)
            .ToArray();
        Require(options.Length > 0 &&
                options[^1] == "executor.ship_inventory_item_to_bin" &&
                options.Count(row => row ==
                    "executor.ship_inventory_item_to_bin") == 1 &&
                options.Take(options.Length - 1)
                    .All(MoneyRevenueResourceNeutralTerminalRouteVerifier
                        .ResourceNeutralOptions.Contains),
            "Money Revenue shipping deposit queue contains a resource-consuming support action.");
        var receiptReasons = QueueExecutionReceiptValidator.Validate(
            queue,
            before,
            receipt,
            after,
            depositProof.RunId,
            depositProof.ExecutorVersion,
            depositProof.SelectedCandidateId,
            before.StateHash,
            requireTeacherPreferenceStateRebound: false);
        Require(receiptReasons.Length == 0,
            "Money Revenue shipping deposit receipt was rejected: " +
            string.Join(",", receiptReasons));
        var candidate = request.TerminalCandidate;
        var evidence = NativeShippingDepositEvidenceVerifier.Verify(
            queue.Items ?? Array.Empty<ActionQueueItem>(),
            receipt,
            before,
            after,
            candidate.QualifiedItemId,
            candidate.Quantity);
        Require(evidence.Verified &&
                evidence.SlotIndex == candidate.SlotIndex &&
                evidence.UnitPrice == candidate.UnitPrice,
            "Money Revenue native shipping deposit was rejected: " +
            string.Join(",", evidence.BlockingReasons));
        var first = recurrenceManifest.Transitions[0];
        var checkpoint = MoneyRevenueProgressionVerifier.Project(after);
        Require(first.ProofKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement &&
                recurrence.Transitions.Length > 0 &&
                EqualJson(
                    recurrence.Transitions[0].Progression.Before,
                    checkpoint) &&
                recurrence.Prefix!.InitialStateHash == checkpoint.StateHash &&
                recurrence.Prefix.SaveId == checkpoint.SaveId &&
                recurrence.Prefix.PlayerId == checkpoint.PlayerId,
            "Money Revenue shipping deposit does not join its native day-settlement recurrence.");
        return new TerminalNativeVerification(
            depositProof.RunId,
            before.StateHash,
            after,
            afterPath,
            new[]
            {
                Artifact("shipping_deposit_queue", queuePath),
                Artifact("shipping_deposit_before_snapshot", beforePath),
                Artifact("shipping_deposit_execution_receipt", receiptPath),
                Artifact("shipping_deposit_after_snapshot", afterPath)
            },
            recurrenceManifestPath,
            Hash(recurrenceManifestPath),
            recurrenceReceiptPath,
            Hash(recurrenceReceiptPath));
    }

    private static MoneyRevenueArtifactBinding Artifact(
        string role,
        string path,
        string? sha256 = null) => new(
            role,
            Path.GetFullPath(path),
            sha256 ?? Hash(path));

    private sealed record TerminalNativeVerification(
        string RunId,
        string TerminalActionBeforeStateHash,
        SnapshotEnvelope TerminalActionAfter,
        string TerminalActionAfterPath,
        MoneyRevenueArtifactBinding[] Artifacts,
        string RecurrenceManifestPath,
        string RecurrenceManifestSha256,
        string RecurrenceReceiptPath,
        string RecurrenceReceiptSha256);
}
