using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropTerminalSettlementProofBuilder
{
    public static MoneyRevenueCropTerminalSettlementProof Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue crop terminal-settlement proof request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropTerminalSettlementProofRequest>(
                fullRequestPath,
                "Money Revenue crop terminal-settlement proof request");
        Require(request.SchemaVersion ==
                    "money_revenue_crop_terminal_settlement_proof_request.v1" &&
                request.RouteTransitions is not null &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop terminal-settlement proof request is invalid.");
        var routeTransitions = request.RouteTransitions ??
            throw new InvalidDataException(
                "Money Revenue crop terminal route transitions are missing.");

        var planPath = RequiredFile(
            request.CropProductionPlanPath,
            "Money Revenue crop-production obligation plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionObligationPlan>(
                planPath,
                "Money Revenue crop-production obligation plan");
        var rebuiltPlan = MoneyRevenueCropProductionObligationPlanBuilder.Build(
            plan.RequestPath);
        Require(plan.SchemaVersion ==
                    "money_revenue_crop_production_obligation_plan.v2" &&
                BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan) &&
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue crop-production obligation plan is not exact.");

        var retentionPath = RequiredFile(
            request.OutputRetentionProofPath,
            "Money Revenue crop output-retention proof");
        var retention = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropOutputRetentionProof>(
                retentionPath,
                "Money Revenue crop output-retention proof");
        var rebuiltRetention =
            MoneyRevenueCropOutputRetentionProofBuilder.Build(
                retention.RequestPath);
        Require(retention.SchemaVersion ==
                    "money_revenue_crop_output_retention_proof.v1" &&
                BootstrapArtifactGuard.EqualJson(retention, rebuiltRetention) &&
                SamePath(retention.CropProductionPlanPath, planPath) &&
                retention.CropProductionPlanSha256 == Hash(planPath) &&
                retention.NativeOutputRetentionVerified &&
                !retention.FormalProductTrainingAuthorized &&
                plan.PendingEvidenceRequestIds.Contains(
                    retention.EvidenceRequestId,
                    StringComparer.Ordinal),
            "Money Revenue crop output-retention support is not exact.");

        var terminalStartPath = RequiredBoundFile(
            retention.AfterSnapshotPath,
            retention.AfterSnapshotSha256,
            "Money Revenue crop terminal start snapshot");
        var terminalStart = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            terminalStartPath,
            "Money Revenue crop terminal start snapshot");
        Require(terminalStart.StateHash == retention.AfterStateHash,
            "Money Revenue crop terminal start state drifted.");

        var salePath = RequiredFile(
            request.ShopSaleEpisodePath,
            "Money Revenue crop ShopMenu sale episode");
        var sale = NativeShopSaleEpisodeVerifier.Verify(salePath);
        var route = MoneyRevenueResourceNeutralTerminalRouteVerifier.Verify(
            terminalStart,
            routeTransitions,
            new MoneyRevenueTerminalContinuationIdentity(
                "economy.sell_items",
                sale.QualifiedItemId,
                sale.SlotIndex,
                sale.Quantity,
                sale.UnitPrice,
                sale.ShopId));
        var saleBefore = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            sale.BeforeSnapshotPath,
            "Money Revenue crop ShopMenu sale before snapshot");
        var saleAfter = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            sale.AfterSnapshotPath,
            "Money Revenue crop ShopMenu sale after snapshot");
        var saleInventoryBefore = ExactInventoryReceiptVerifier.InventoryCount(
            saleBefore,
            sale.QualifiedItemId,
            minimumQuality: 0);
        var saleInventoryAfter = ExactInventoryReceiptVerifier.InventoryCount(
            saleAfter,
            sale.QualifiedItemId,
            minimumQuality: 0);
        Require(route.TerminalActionBefore.StateHash ==
                    sale.Progression.Before.StateHash &&
                sale.QualifiedItemId == retention.HarvestItemQualifiedId &&
                sale.Quantity == retention.MinimumUnits &&
                retention.InventoryQuantityIncrease >= sale.Quantity &&
                saleInventoryBefore.HasValue &&
                saleInventoryAfter.HasValue &&
                checked(saleInventoryBefore.Value -
                    saleInventoryAfter.Value) == sale.Quantity &&
                checked(sale.Quantity * sale.UnitPrice) ==
                    sale.Progression.MoneyDelta &&
                sale.Progression.MoneyDelta >=
                    retention.MinimumGrossRevenue,
            "Money Revenue crop ShopMenu sale does not settle the retained wave exactly.");

        var recurrenceManifestPath = RequiredFile(
            request.RevenueRecurrenceManifestPath,
            "Money Revenue crop recurrence manifest");
        var recurrenceReceiptPath = RequiredFile(
            request.RevenueRecurrenceReceiptPath,
            "Money Revenue crop recurrence receipt");
        var manifest = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofManifest>(
                recurrenceManifestPath,
                "Money Revenue crop recurrence manifest");
        var rebuiltRecurrence = MoneyRevenueRecurrenceProofBuilder.Build(
            recurrenceManifestPath);
        var recurrence = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueRecurrenceProofReceipt>(
                recurrenceReceiptPath,
                "Money Revenue crop recurrence receipt");
        Require(manifest.Transitions is not null &&
                manifest.Transitions.Length == 1 &&
                manifest.Transitions[0].ProofKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShopSale &&
                SamePath(
                    manifest.Transitions[0].ShopSaleEpisodePath,
                    sale.ExecutionEpisodePath) &&
                BootstrapArtifactGuard.EqualJson(
                    recurrence,
                    rebuiltRecurrence) &&
                recurrence.ProofVerified &&
                recurrence.Prefix is not null &&
                recurrence.Transitions.Length == 1 &&
                BootstrapArtifactGuard.EqualJson(
                    recurrence.Transitions[0].Progression,
                    sale.Progression) &&
                recurrence.Prefix.InitialStateHash ==
                    sale.Progression.Before.StateHash &&
                recurrence.Prefix.FinalStateHash ==
                    sale.Progression.After.StateHash &&
                recurrence.Prefix.TotalRevenue ==
                    sale.Progression.MoneyDelta &&
                !recurrence.FormalProductTrainingAuthorized,
            "Money Revenue crop ShopMenu sale recurrence is not exact.");

        return new MoneyRevenueCropTerminalSettlementProof
        {
            Status = "verified_native_crop_terminal_settlement",
            RequestPath = fullRequestPath,
            RequestSha256 = Hash(fullRequestPath),
            CropProductionPlanPath = planPath,
            CropProductionPlanSha256 = Hash(planPath),
            OutputRetentionProofPath = retentionPath,
            OutputRetentionProofSha256 = Hash(retentionPath),
            EvidenceRequestId = retention.EvidenceRequestId,
            CommitmentId = retention.CommitmentId,
            WaveIndex = retention.WaveIndex,
            HarvestItemQualifiedId = retention.HarvestItemQualifiedId,
            MinimumUnits = retention.MinimumUnits,
            MinimumGrossRevenue = retention.MinimumGrossRevenue,
            TerminalKind = MoneyRevenueRecurrenceProofKinds.NativeShopSale,
            TerminalStartStateHash = terminalStart.StateHash,
            TerminalActionBeforeStateHash =
                route.TerminalActionBefore.StateHash,
            TerminalActionAfterStateHash = sale.Progression.After.StateHash,
            RouteTransitions = route.Evidence,
            ShopSaleEpisodePath = sale.ExecutionEpisodePath,
            ShopSaleEpisodeSha256 = sale.ExecutionEpisodeSha256,
            ShopId = sale.ShopId,
            SlotIndex = sale.SlotIndex,
            SettledQuantity = sale.Quantity,
            UnitPrice = sale.UnitPrice,
            SettledRevenue = sale.Progression.MoneyDelta,
            RevenueRecurrenceManifestPath = recurrenceManifestPath,
            RevenueRecurrenceManifestSha256 = Hash(recurrenceManifestPath),
            RevenueRecurrenceReceiptPath = recurrenceReceiptPath,
            RevenueRecurrenceReceiptSha256 = Hash(recurrenceReceiptPath),
            NativeTerminalSettlementVerified = true,
            FormalProductTrainingAuthorized = false
        };
    }

    private static string RequiredBoundFile(
        string path,
        string expectedSha256,
        string label)
    {
        var fullPath = RequiredFile(path, label);
        Require(!string.IsNullOrWhiteSpace(expectedSha256) &&
                Hash(fullPath) == expectedSha256,
            label + " hash drifted.");
        return fullPath;
    }

    private static string RequiredFile(string path, string label)
    {
        Require(!string.IsNullOrWhiteSpace(path), label + " path is required.");
        var fullPath = Path.GetFullPath(path);
        Require(File.Exists(fullPath), label + " is missing: " + fullPath);
        return fullPath;
    }

    private static string Hash(string path) =>
        CurrentTeacherFrontierSupport.HashFile(path);

    private static bool SamePath(string left, string right) =>
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
