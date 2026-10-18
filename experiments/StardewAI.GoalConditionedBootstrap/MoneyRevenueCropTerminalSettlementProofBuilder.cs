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
                    "money_revenue_crop_terminal_settlement_proof_request.v2" &&
                request.RouteTransitions is not null &&
                request.ShippingDepositTransitions is not null &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue crop terminal-settlement proof request is invalid.");
        var routeTransitions = request.RouteTransitions ??
            throw new InvalidDataException(
                "Money Revenue crop terminal route transitions are missing.");
        var shippingDepositTransitions = request.ShippingDepositTransitions ??
            throw new InvalidDataException(
                "Money Revenue crop shipping transitions are missing.");

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

        var hasShopSale = !string.IsNullOrWhiteSpace(
            request.ShopSaleEpisodePath);
        var hasShippingDeposits = shippingDepositTransitions.Length > 0;
        Require(hasShopSale != hasShippingDeposits,
            "Money Revenue crop settlement must select exactly one native terminal mode.");
        var settlement = hasShopSale
            ? MoneyRevenueCropTerminalSettlementModeVerifier.VerifyShop(
                request,
                terminalStart,
                retention)
            : MoneyRevenueCropTerminalSettlementModeVerifier.VerifyShipping(
                request,
                terminalStart,
                retention);

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
            TerminalKind = settlement.TerminalKind,
            TerminalStartStateHash = terminalStart.StateHash,
            TerminalActionBeforeStateHash =
                settlement.TerminalActionBeforeStateHash,
            TerminalActionAfterStateHash =
                settlement.TerminalActionAfterStateHash,
            RouteTransitions = settlement.RouteTransitions,
            ShopSaleEpisodePath = settlement.ShopSaleEpisodePath,
            ShopSaleEpisodeSha256 = settlement.ShopSaleEpisodeSha256,
            ShopId = settlement.ShopId,
            ShippingDeposits = settlement.ShippingDeposits,
            SlotIndex = settlement.SlotIndex,
            SettledQuantity = settlement.SettledQuantity,
            UnitPrice = settlement.UnitPrice,
            SettledRevenue = settlement.SettledRevenue,
            RevenueRecurrenceManifestPath =
                settlement.RevenueRecurrenceManifestPath,
            RevenueRecurrenceManifestSha256 =
                settlement.RevenueRecurrenceManifestSha256,
            RevenueRecurrenceReceiptPath =
                settlement.RevenueRecurrenceReceiptPath,
            RevenueRecurrenceReceiptSha256 =
                settlement.RevenueRecurrenceReceiptSha256,
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
