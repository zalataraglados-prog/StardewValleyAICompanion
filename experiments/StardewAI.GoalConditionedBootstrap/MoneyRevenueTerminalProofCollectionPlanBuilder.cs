namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueTerminalProofCollectionPlanBuilder
{
    public static MoneyRevenueTerminalProofCollectionPlan Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue terminal-proof collection-plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalProofCollectionPlanRequest>(
                fullRequestPath,
                "Money Revenue terminal-proof collection-plan request");
        Require(request.SchemaVersion ==
                    "money_revenue_terminal_proof_collection_plan_request.v2" &&
                !string.IsNullOrWhiteSpace(request.RunId) &&
                MoneyThresholdEvidenceCellProjector.IsRequiredPartition(
                    request.DatasetPartition) &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue terminal-proof collection-plan request is invalid.");

        var comparisonPlanPath = RequiredFile(
            request.ComparisonCollectionPlanPath,
            "Money Revenue comparison collection plan");
        var comparisonPlan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueComparisonCollectionPlan>(
                comparisonPlanPath,
                "Money Revenue comparison collection plan");
        var rebuiltComparisonPlan = MoneyRevenueComparisonCollectionPlanBuilder
            .Build(comparisonPlan.RequestPath);
        Require(BootstrapArtifactGuard.EqualJson(
                    comparisonPlan,
                    rebuiltComparisonPlan) &&
                !comparisonPlan.FormalProductTrainingAuthorized,
            "Money Revenue comparison collection plan is not exact.");

        var counterfactualManifestPath = RequiredFile(
            request.CounterfactualRequestManifestPath,
            "Money Revenue counterfactual request manifest");
        var counterfactualCatalogPath = RequiredFile(
            request.CounterfactualRequestCatalogPath,
            "Money Revenue counterfactual request catalog");
        var counterfactualCatalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCounterfactualProofRequestCatalog>(
                counterfactualCatalogPath,
                "Money Revenue counterfactual request catalog");
        var rebuiltCatalog = MoneyRevenueCounterfactualProofRequestBuilder.Build(
            counterfactualManifestPath);
        Require(BootstrapArtifactGuard.EqualJson(
                    counterfactualCatalog,
                    rebuiltCatalog) &&
                counterfactualCatalog.RequestDenominatorAuthoritative &&
                counterfactualCatalog.CounterfactualProofCollectionReady &&
                !counterfactualCatalog.FormalProductTrainingAuthorized,
            "Money Revenue counterfactual request catalog is not exact.");
        var concreteCatalogPath = RequiredFile(
            counterfactualCatalog.ConcreteAlternativeCatalogPath,
            "Money Revenue concrete alternative catalog");
        var concreteCatalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueConcreteAlternativeDenominatorCatalog>(
                concreteCatalogPath,
                "Money Revenue concrete alternative catalog");
        var rebuiltConcreteCatalog =
            MoneyRevenueConcreteAlternativeDenominatorBuilder.Build(
                concreteCatalog.ManifestPath);
        Require(BootstrapArtifactGuard.EqualJson(
                    concreteCatalog,
                    rebuiltConcreteCatalog),
            "Money Revenue concrete alternative catalog is not exact.");

        var verifiedSale = NativeShopSaleEpisodeVerifier.Verify(
            comparisonPlan.NativeTerminalEpisodePath);
        Require(verifiedSale.ExecutionEpisodeSha256 ==
                    comparisonPlan.NativeTerminalEpisodeSha256,
            "Money Revenue reusable native episode hash drifted.");
        var reusableSale = new MoneyRevenueReusableShopSaleEpisode(
            verifiedSale.ExecutionEpisodePath,
            verifiedSale.ExecutionEpisodeSha256,
            verifiedSale.Progression.Before.StateHash,
            verifiedSale.ShopId,
            verifiedSale.QualifiedItemId,
            verifiedSale.SlotIndex,
            verifiedSale.Quantity,
            verifiedSale.UnitPrice);

        return BuildPlan(
            fullRequestPath,
            CurrentTeacherFrontierSupport.HashFile(fullRequestPath),
            request.RunId,
            request.DatasetPartition,
            comparisonPlanPath,
            CurrentTeacherFrontierSupport.HashFile(comparisonPlanPath),
            comparisonPlan,
            counterfactualManifestPath,
            CurrentTeacherFrontierSupport.HashFile(counterfactualManifestPath),
            counterfactualCatalogPath,
            CurrentTeacherFrontierSupport.HashFile(counterfactualCatalogPath),
            counterfactualCatalog,
            concreteCatalogPath,
            CurrentTeacherFrontierSupport.HashFile(concreteCatalogPath),
            concreteCatalog,
            reusableSale);
    }

    internal static MoneyRevenueTerminalProofCollectionPlan BuildPlan(
        string requestPath,
        string requestSha256,
        string requestedRunId,
        string requestedDatasetPartition,
        string comparisonPlanPath,
        string comparisonPlanSha256,
        MoneyRevenueComparisonCollectionPlan comparisonPlan,
        string counterfactualManifestPath,
        string counterfactualManifestSha256,
        string counterfactualCatalogPath,
        string counterfactualCatalogSha256,
        MoneyRevenueCounterfactualProofRequestCatalog catalog,
        string concreteCatalogPath,
        string concreteCatalogFileSha256,
        MoneyRevenueConcreteAlternativeDenominatorCatalog concreteCatalog,
        MoneyRevenueReusableShopSaleEpisode reusableSale)
    {
        var requests = catalog.Requests ??
            Array.Empty<MoneyRevenueCounterfactualProofRequest>();
        var requestIds = catalog.RequestIds ?? Array.Empty<string>();
        var concreteIds = concreteCatalog.ConcreteAlternativeIds ??
            Array.Empty<string>();
        var concreteAlternatives = concreteCatalog.Alternatives ??
            Array.Empty<MoneyRevenueConcreteAlternative>();
        Require(catalog.RequestCount > 0 &&
                requests.Length == catalog.RequestCount &&
                concreteCatalog.CurrentConcreteDenominatorAuthoritative &&
                !concreteCatalog.FormalProductTrainingAuthorized &&
                comparisonPlan.AlternativeCount == concreteCatalog.SkeletonCount &&
                concreteCatalog.ConcreteAlternativeCount == catalog.RequestCount &&
                concreteAlternatives.Length == catalog.RequestCount &&
                concreteIds.Length == catalog.RequestCount &&
                comparisonPlan.RunId == requestedRunId &&
                comparisonPlan.DatasetPartition == requestedDatasetPartition &&
                catalog.SourceStateHash == comparisonPlan.SourceStateHash &&
                concreteCatalog.SourceStateHash == comparisonPlan.SourceStateHash &&
                MoneyThresholdEvidenceCellProjector.IsRequiredPartition(
                    comparisonPlan.DatasetPartition) &&
                !string.IsNullOrWhiteSpace(comparisonPlan.RunId) &&
                SamePath(
                    concreteCatalog.AlternativeDenominatorCatalogPath,
                    comparisonPlan.AlternativeDenominatorCatalogPath) &&
                concreteCatalog.AlternativeDenominatorFileSha256 ==
                    comparisonPlan.AlternativeDenominatorFileSha256 &&
                concreteCatalog.AlternativeDenominatorCatalogSha256 ==
                    comparisonPlan.AlternativeDenominatorCatalogSha256 &&
                SamePath(
                    catalog.ConcreteAlternativeCatalogPath,
                    concreteCatalogPath) &&
                catalog.ConcreteAlternativeCatalogFileSha256 ==
                    concreteCatalogFileSha256 &&
                catalog.ConcreteAlternativeCatalogSha256 ==
                    concreteCatalog.CatalogSha256 &&
                requests.All(value => value is not null &&
                    !string.IsNullOrWhiteSpace(value.RequestId)) &&
                requests.Select(value => value.RequestId)
                    .Distinct(StringComparer.Ordinal).Count() == requests.Length &&
                requestIds.SequenceEqual(
                    requests.Select(value => value.RequestId),
                    StringComparer.Ordinal) &&
                concreteIds.SequenceEqual(
                    requests.Select(value => value.ConcreteAlternativeId),
                    StringComparer.Ordinal),
            "Money Revenue terminal-proof request denominator is not exact.");

        var workItems = requests.Select((request, index) => BuildWorkItem(
                index,
                request,
                comparisonPlan,
                reusableSale))
            .ToArray();
        var reuseCount = workItems.Count(value => value.CollectionMode ==
            MoneyRevenueTerminalProofCollectionModes
                .ReuseVerifiedShopSaleEpisode);
        Require(reuseCount == comparisonPlan.ExistingNativeEpisodeReuseCount &&
                reuseCount == 1,
            "Money Revenue terminal-proof plan did not retain exactly one verified native episode.");

        return new MoneyRevenueTerminalProofCollectionPlan
        {
            Status = "terminal_proof_collection_required",
            RequestPath = requestPath,
            RequestSha256 = requestSha256,
            ComparisonCollectionPlanPath = comparisonPlanPath,
            ComparisonCollectionPlanSha256 = comparisonPlanSha256,
            AlternativeDenominatorCatalogPath =
                comparisonPlan.AlternativeDenominatorCatalogPath,
            AlternativeDenominatorFileSha256 =
                comparisonPlan.AlternativeDenominatorFileSha256,
            AlternativeDenominatorCatalogSha256 =
                comparisonPlan.AlternativeDenominatorCatalogSha256,
            ConcreteAlternativeCatalogPath = concreteCatalogPath,
            ConcreteAlternativeCatalogFileSha256 = concreteCatalogFileSha256,
            ConcreteAlternativeCatalogSha256 = concreteCatalog.CatalogSha256,
            CounterfactualRequestManifestPath = counterfactualManifestPath,
            CounterfactualRequestManifestSha256 =
                counterfactualManifestSha256,
            CounterfactualRequestCatalogPath = counterfactualCatalogPath,
            CounterfactualRequestCatalogSha256 = counterfactualCatalogSha256,
            RunId = comparisonPlan.RunId,
            DatasetPartition = comparisonPlan.DatasetPartition,
            SourceStateHash = comparisonPlan.SourceStateHash,
            RequestCount = requests.Length,
            RequestIds = requests.Select(value => value.RequestId).ToArray(),
            OfflineReuseReadyCount = reuseCount,
            NativeCollectionRequiredCount = requests.Length - reuseCount,
            WorkItems = workItems,
            TerminalProofIndexReady = false,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueTerminalProofCollectionWorkItem BuildWorkItem(
        int index,
        MoneyRevenueCounterfactualProofRequest request,
        MoneyRevenueComparisonCollectionPlan comparisonPlan,
        MoneyRevenueReusableShopSaleEpisode reusableSale)
    {
        var shop = request.NativeShopSaleProofRequired &&
            request.RequiredRevenueTransitionProofKind ==
                MoneyRevenueRecurrenceProofKinds.NativeShopSale &&
            request.TerminalOptionId == "economy.sell_items";
        var shipping = request.NativeShippingDepositProofRequired &&
            request.NativeShippingSettlementProofRequired &&
            request.RequiredRevenueTransitionProofKind ==
                "native_shipping_deposit_and_day_settlement" &&
            request.TerminalOptionId == "economy.ship_items";
        Require(shop != shipping &&
                request.TerminalOpportunityCostProofRequired &&
                request.TerminalReservationClaimProofRequired &&
                request.RevenueRecurrenceProofRequired,
            "Money Revenue terminal-proof request shape is invalid: " +
            request.RequestId + ".");

        var candidate = request.TerminalCandidate ??
            throw new InvalidDataException(
                "Money Revenue terminal candidate is missing: " +
                request.RequestId + ".");
        var reuse = shop &&
            request.TerminalStateHash == comparisonPlan.SourceStateHash &&
            candidate.CatalogCandidateId ==
                comparisonPlan.SelectedCatalogCandidateId &&
            reusableSale.BeforeStateHash == request.TerminalStateHash &&
            reusableSale.ShopId == candidate.ShopId &&
            reusableSale.QualifiedItemId == candidate.QualifiedItemId &&
            reusableSale.SlotIndex == candidate.SlotIndex &&
            reusableSale.Quantity == candidate.Quantity &&
            reusableSale.UnitPrice == candidate.UnitPrice;
        var mode = reuse
            ? MoneyRevenueTerminalProofCollectionModes
                .ReuseVerifiedShopSaleEpisode
            : shop
                ? MoneyRevenueTerminalProofCollectionModes
                    .CollectNativeShopSale
                : MoneyRevenueTerminalProofCollectionModes
                    .CollectNativeShippingSettlement;
        var phases = new List<string>();
        if (request.AcquisitionRolloutProofRequired)
            phases.Add("restore_verified_acquisition_terminal_state");
        phases.Add("capture_before_strategy_ledger");
        phases.Add("collect_resource_neutral_terminal_route");
        phases.Add(reuse
            ? "reuse_exact_native_shop_sale_episode"
            : shop
                ? "collect_native_shop_sale_episode"
                : "collect_native_shipping_deposit_and_day_settlement");
        phases.Add("capture_after_strategy_ledger");
        phases.Add("build_revenue_recurrence_proof");
        phases.Add("verify_terminal_execution_proof");

        return new MoneyRevenueTerminalProofCollectionWorkItem(
            index,
            request.RequestId,
            request.RequestSha256,
            request.ConcreteAlternativeId,
            request.ConcreteAlternativeSha256,
            request.TerminalStateHash,
            request.TerminalSnapshotPath,
            request.TerminalSnapshotSha256,
            request.TerminalOptionId,
            candidate.CatalogCandidateId,
            candidate.CandidateSha256,
            request.TerminalStrategyLedgerPath,
            request.TerminalStrategyLedgerSha256,
            request.TerminalStrategyLedgerId,
            request.TerminalStrategyLedgerRevision,
            request.AcquisitionRolloutProofRequired,
            request.RequiredRevenueTransitionProofKind,
            mode,
            reuse ? reusableSale.Path : string.Empty,
            reuse ? reusableSale.Sha256 : string.Empty,
            phases.ToArray(),
            reuse ? "offline_materialization_ready" :
                "native_collection_required");
    }

    private static string RequiredFile(string path, string label)
    {
        var fullPath = string.IsNullOrWhiteSpace(path)
            ? throw new InvalidDataException(label + " path is required.")
            : Path.GetFullPath(path);
        return File.Exists(fullPath)
            ? fullPath
            : throw new FileNotFoundException(label + " is missing.", fullPath);
    }

    private static bool SamePath(string left, string right) =>
        !string.IsNullOrWhiteSpace(left) &&
        !string.IsNullOrWhiteSpace(right) &&
        string.Equals(
            Path.GetFullPath(left),
            Path.GetFullPath(right),
            StringComparison.OrdinalIgnoreCase);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
