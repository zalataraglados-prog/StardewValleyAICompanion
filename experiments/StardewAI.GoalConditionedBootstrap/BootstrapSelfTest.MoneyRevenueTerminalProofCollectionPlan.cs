namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueTerminalProofCollectionPlan()
    {
        const string sourceStateHash = "state.money-terminal-collection";
        var comparisonPlan = new MoneyRevenueComparisonCollectionPlan
        {
            RunId = "run.money-terminal-collection",
            DatasetPartition = "train",
            SourceStateHash = sourceStateHash,
            SelectedCatalogCandidateId = "candidate.selected-shop-sale",
            AlternativeCount = 3,
            ExistingNativeEpisodeReuseCount = 1
        };
        var requests = new[]
        {
            TerminalCollectionRequest(
                "request.reuse",
                "alternative.reuse",
                sourceStateHash,
                "candidate.selected-shop-sale",
                "SeedShop",
                "(O)24",
                0,
                1,
                20,
                shop: true,
                acquisition: false),
            TerminalCollectionRequest(
                "request.rebound-shop",
                "alternative.rebound-shop",
                "state.rebound-shop",
                "candidate.rebound-shop-sale",
                "SeedShop",
                "(O)188",
                1,
                2,
                35,
                shop: true,
                acquisition: true),
            TerminalCollectionRequest(
                "request.shipping",
                "alternative.shipping",
                sourceStateHash,
                "candidate.shipping",
                string.Empty,
                "(O)388",
                2,
                3,
                2,
                shop: false,
                acquisition: false)
        };
        var catalog = new MoneyRevenueCounterfactualProofRequestCatalog
        {
            SourceStateHash = sourceStateHash,
            RequestCount = requests.Length,
            RequestIds = requests.Select(value => value.RequestId).ToArray(),
            Requests = requests
        };
        var reusableSale = new MoneyRevenueReusableShopSaleEpisode(
            "episode.json",
            new string('a', 64),
            sourceStateHash,
            "SeedShop",
            "(O)24",
            0,
            1,
            20);

        var plan = MoneyRevenueTerminalProofCollectionPlanBuilder.BuildPlan(
            "request.json",
            new string('1', 64),
            "comparison-plan.json",
            new string('2', 64),
            comparisonPlan,
            "counterfactual-manifest.json",
            new string('3', 64),
            "counterfactual-catalog.json",
            new string('4', 64),
            catalog,
            reusableSale);
        Require(plan.RequestCount == 3 &&
                plan.OfflineReuseReadyCount == 1 &&
                plan.NativeCollectionRequiredCount == 2 &&
                !plan.TerminalProofIndexReady &&
                !plan.FormalProductTrainingAuthorized &&
                plan.WorkItems[0].CollectionMode ==
                    MoneyRevenueTerminalProofCollectionModes
                        .ReuseVerifiedShopSaleEpisode &&
                plan.WorkItems[0].ReusableNativeEpisodePath ==
                    reusableSale.Path &&
                plan.WorkItems[1].CollectionMode ==
                    MoneyRevenueTerminalProofCollectionModes
                        .CollectNativeShopSale &&
                plan.WorkItems[1].RequiredPhases[0] ==
                    "restore_verified_acquisition_terminal_state" &&
                plan.WorkItems[2].CollectionMode ==
                    MoneyRevenueTerminalProofCollectionModes
                        .CollectNativeShippingSettlement,
            "Money Revenue terminal-proof collection plan was not exact.");

        catalog.RequestIds = new[]
        {
            requests[0].RequestId,
            requests[0].RequestId,
            requests[2].RequestId
        };
        catalog.Requests = new[] { requests[0], requests[0], requests[2] };
        var duplicateRejected = false;
        try
        {
            _ = MoneyRevenueTerminalProofCollectionPlanBuilder.BuildPlan(
                "request.json",
                new string('1', 64),
                "comparison-plan.json",
                new string('2', 64),
                comparisonPlan,
                "counterfactual-manifest.json",
                new string('3', 64),
                "counterfactual-catalog.json",
                new string('4', 64),
                catalog,
                reusableSale);
        }
        catch (InvalidDataException)
        {
            duplicateRejected = true;
        }
        Require(duplicateRejected,
            "Duplicate Money Revenue terminal-proof requests were admitted.");
    }

    private static MoneyRevenueCounterfactualProofRequest
        TerminalCollectionRequest(
            string requestId,
            string alternativeId,
            string stateHash,
            string candidateId,
            string shopId,
            string itemId,
            int slotIndex,
            int quantity,
            int unitPrice,
            bool shop,
            bool acquisition) => new()
            {
                RequestId = requestId,
                RequestSha256 = new string('5', 64),
                ConcreteAlternativeId = alternativeId,
                ConcreteAlternativeSha256 = new string('6', 64),
                TerminalStateHash = stateHash,
                TerminalSnapshotPath = alternativeId + ".snapshot.json",
                TerminalSnapshotSha256 = new string('7', 64),
                TerminalOptionId = shop
                    ? "economy.sell_items"
                    : "economy.ship_items",
                TerminalCandidate = new MoneyRevenueCandidateCatalogEntry
                {
                    CatalogCandidateId = candidateId,
                    CandidateSha256 = new string('8', 64),
                    ShopId = shopId,
                    QualifiedItemId = itemId,
                    SlotIndex = slotIndex,
                    Quantity = quantity,
                    UnitPrice = unitPrice
                },
                TerminalStrategyLedgerPath = alternativeId + ".ledger.json",
                TerminalStrategyLedgerSha256 = new string('9', 64),
                TerminalStrategyLedgerId = "ledger.money",
                TerminalStrategyLedgerRevision = acquisition ? 2 : 1,
                AcquisitionRolloutProofRequired = acquisition,
                RequiredRevenueTransitionProofKind = shop
                    ? MoneyRevenueRecurrenceProofKinds.NativeShopSale
                    : "native_shipping_deposit_and_day_settlement",
                TerminalOpportunityCostProofRequired = true,
                TerminalReservationClaimProofRequired = true,
                NativeShopSaleProofRequired = shop,
                NativeShippingDepositProofRequired = !shop,
                NativeShippingSettlementProofRequired = !shop,
                RevenueRecurrenceProofRequired = true
            };
}
