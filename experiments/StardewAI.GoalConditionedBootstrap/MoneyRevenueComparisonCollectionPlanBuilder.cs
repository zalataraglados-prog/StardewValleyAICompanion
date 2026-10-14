namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueComparisonCollectionPlanBuilder
{
    public static MoneyRevenueComparisonCollectionPlan Build(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue comparison collection plan request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueComparisonCollectionPlanRequest>(
                fullRequestPath,
                "Money Revenue comparison collection plan request");
        Require(request.SchemaVersion ==
                    "money_revenue_comparison_collection_plan_request.v1" &&
                !string.IsNullOrWhiteSpace(request.PlanId) &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue comparison collection plan request is invalid.");

        var run = MoneyThresholdNativeOutcomeRunVerifier.Verify(
            request.NativeRunSummaryPath);
        var alternativePath = RequiredFile(
            request.AlternativeDenominatorCatalogPath,
            "Money Revenue alternative denominator catalog");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueAlternativeDenominatorCatalog>(
                alternativePath,
                "Money Revenue alternative denominator catalog");
        var rebuilt = MoneyRevenueAlternativeDenominatorBuilder.Build(
            run.Summary.CandidateDenominatorPath);
        Require(EqualJson(persisted, rebuilt) &&
                persisted.AlternativeSkeletonDenominatorComplete &&
                persisted.SourceCandidateDenominatorAuthoritative &&
                persisted.SourceStateHash == run.Summary.SourceStateHash &&
                persisted.SourceCandidateCatalogFileSha256 ==
                    run.Summary.CandidateDenominatorSha256 &&
                !persisted.ProductionTeacherPreferenceLabelEligible &&
                !persisted.FormalProductTrainingAuthorized,
            "Money Revenue alternative denominator does not bind the native run.");

        var selectedCandidateId =
            run.Selection.SelectedCandidate.CatalogCandidateId;
        var workItems = persisted.Alternatives
            .OrderBy(value => value.AlternativeId, StringComparer.Ordinal)
            .Select(value => BuildWorkItem(value, selectedCandidateId))
            .ToArray();
        Require(workItems.Length == persisted.AlternativeCount &&
                workItems.Select(value => value.AlternativeId)
                    .SequenceEqual(
                        persisted.AlternativeIds,
                        StringComparer.Ordinal) &&
                workItems.Count(value =>
                    value.ExistingNativeEpisodeReusable) > 0,
            "Money Revenue comparison plan did not retain the selected native sale alternative.");

        var rebindCount = workItems.Count(value =>
            value.RequiresFreshTerminalRebind);
        var reuseCount = workItems.Count(value =>
            value.ExistingNativeEpisodeReusable);
        return new MoneyRevenueComparisonCollectionPlan
        {
            Status = rebindCount > 0
                ? "acquisition_rollout_and_fresh_rebind_required"
                : "terminal_counterfactual_proof_collection_required",
            PlanId = request.PlanId,
            RequestPath = fullRequestPath,
            RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullRequestPath),
            NativeRunSummaryPath = run.VerifiedRun.SummaryPath,
            NativeRunSummarySha256 = run.VerifiedRun.SummarySha256,
            RunId = run.Summary.RunId,
            DatasetPartition = run.Summary.DatasetPartition,
            CrossedCriterionIds = run.Summary.CrossedCriterionIds.ToArray(),
            SourceStateHash = run.Summary.SourceStateHash,
            CandidateDenominatorSha256 =
                run.Summary.CandidateDenominatorSha256,
            NativeTerminalEpisodePath =
                run.VerifiedRun.ExecutionEpisodePath,
            NativeTerminalEpisodeSha256 =
                run.CorpusRow.ExecutionEpisodeSha256,
            SelectedCatalogCandidateId = selectedCandidateId,
            AlternativeDenominatorCatalogPath = alternativePath,
            AlternativeDenominatorFileSha256 =
                CurrentTeacherFrontierSupport.HashFile(alternativePath),
            AlternativeDenominatorCatalogSha256 = persisted.CatalogSha256,
            AlternativeCount = workItems.Length,
            DirectAlternativeCount = workItems.Count(value =>
                !value.RequiresFreshTerminalRebind),
            FreshRebindAlternativeCount = rebindCount,
            ExistingNativeEpisodeReuseCount = reuseCount,
            WorkItems = workItems,
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueComparisonCollectionWorkItem BuildWorkItem(
        MoneyRevenueAlternativeSkeleton alternative,
        string selectedCandidateId)
    {
        var reuseNativeEpisode =
            !alternative.RequiresFreshTerminalCandidateRebind &&
            alternative.RequiresNativeShopSaleProof &&
            string.Equals(
                alternative.SourceCatalogCandidateId,
                selectedCandidateId,
                StringComparison.Ordinal);
        var nextAction = alternative.RequiresFreshTerminalCandidateRebind
            ? "collect_acquisition_rollout_then_build_fresh_terminal_rebind"
            : reuseNativeEpisode
                ? "reuse_native_sale_episode_then_collect_remaining_terminal_proofs"
                : alternative.RequiresNativeShopSaleProof
                    ? "collect_native_shop_sale_and_terminal_proofs"
                    : "collect_native_shipping_deposit_settlement_and_terminal_proofs";
        return new MoneyRevenueComparisonCollectionWorkItem(
            alternative.AlternativeId,
            alternative.SourceCatalogCandidateId,
            alternative.SourceCandidateId,
            alternative.SourceOptionId,
            alternative.TerminalOptionId,
            alternative.ProofShape,
            alternative.RequiresAcquisitionOrProcessingRollout,
            alternative.RequiresFreshTerminalCandidateRebind,
            alternative.RequiresNativeShopSaleProof,
            alternative.RequiresNativeShippingDepositProof,
            alternative.RequiresNativeShippingSettlementProof,
            reuseNativeEpisode,
            nextAction);
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
}
