using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class MoneyRevenueCropTerminalSettlementModeVerifier
{
    internal static MoneyRevenueCropTerminalSettlementVerification VerifyShop(
        MoneyRevenueCropTerminalSettlementProofRequest request,
        SnapshotEnvelope terminalStart,
        MoneyRevenueCropOutputRetentionProof retention)
    {
        Require(request.ShippingDepositTransitions.Length == 0,
            "Money Revenue crop ShopMenu settlement contains shipping deposits.");
        Require(request.SettlementRouteTransitions.Length == 0,
            "Money Revenue crop ShopMenu settlement contains a shipping home route.");
        var salePath = RequiredFile(
            request.ShopSaleEpisodePath,
            "Money Revenue crop ShopMenu sale episode");
        var sale = NativeShopSaleEpisodeVerifier.Verify(salePath);
        var route = MoneyRevenueResourceNeutralTerminalRouteVerifier.Verify(
            terminalStart,
            request.RouteTransitions,
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
        var inventoryBefore = ExactInventoryReceiptVerifier.InventoryCount(
            saleBefore,
            sale.QualifiedItemId,
            minimumQuality: 0);
        var inventoryAfter = ExactInventoryReceiptVerifier.InventoryCount(
            saleAfter,
            sale.QualifiedItemId,
            minimumQuality: 0);
        Require(route.TerminalActionBefore.StateHash ==
                    sale.Progression.Before.StateHash &&
                sale.QualifiedItemId == retention.HarvestItemQualifiedId &&
                sale.Quantity == retention.MinimumUnits &&
                retention.InventoryQuantityIncrease >= sale.Quantity &&
                inventoryBefore.HasValue &&
                inventoryAfter.HasValue &&
                checked(inventoryBefore.Value - inventoryAfter.Value) ==
                    sale.Quantity &&
                checked(sale.Quantity * sale.UnitPrice) ==
                    sale.Progression.MoneyDelta &&
                sale.Progression.MoneyDelta >= retention.MinimumGrossRevenue,
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
        var rebuilt = MoneyRevenueRecurrenceProofBuilder.Build(
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
                BootstrapArtifactGuard.EqualJson(recurrence, rebuilt) &&
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

        return new MoneyRevenueCropTerminalSettlementVerification(
            MoneyRevenueRecurrenceProofKinds.NativeShopSale,
            route.TerminalActionBefore.StateHash,
            sale.Progression.After.StateHash,
            route.Evidence,
            sale.ExecutionEpisodePath,
            sale.ExecutionEpisodeSha256,
            sale.ShopId,
            Array.Empty<MoneyRevenueCropShippingDepositEvidence>(),
            Array.Empty<MoneyRevenueTerminalQueueTransitionEvidence>(),
            sale.SlotIndex,
            sale.Quantity,
            sale.UnitPrice,
            sale.Progression.MoneyDelta,
            recurrenceManifestPath,
            Hash(recurrenceManifestPath),
            recurrenceReceiptPath,
            Hash(recurrenceReceiptPath));
    }
}
