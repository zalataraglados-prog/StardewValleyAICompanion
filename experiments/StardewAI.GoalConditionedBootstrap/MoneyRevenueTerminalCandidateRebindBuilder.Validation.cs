namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueTerminalCandidateRebindBuilder
{
    private static void ValidateManifest(
        MoneyRevenueTerminalCandidateRebindManifest manifest)
    {
        Require(manifest.SchemaVersion ==
                    "money_revenue_terminal_candidate_rebind_manifest.v1" &&
                !manifest.FormalProductTrainingAuthorized &&
                !string.IsNullOrWhiteSpace(
                    manifest.AlternativeDenominatorCatalogPath) &&
                manifest.Rebinds is not null &&
                manifest.Rebinds.All(row => row is not null),
            "Money Revenue terminal candidate rebind manifest is invalid.");
    }

    private static void ValidateRebindDenominator(
        IReadOnlyList<MoneyRevenueAlternativeSkeleton> required,
        IReadOnlyList<MoneyRevenueTerminalCandidateRebindRequest> requests)
    {
        Require(required.Count > 0,
            "Money Revenue alternative denominator has no terminal rebind skeletons.");
        Require(requests.Count == required.Count &&
                requests.All(row =>
                    !string.IsNullOrWhiteSpace(row.AlternativeId) &&
                    !string.IsNullOrWhiteSpace(
                        row.AcquisitionRolloutProofManifestPath) &&
                    !string.IsNullOrWhiteSpace(
                        row.AcquisitionRolloutProofReceiptPath)) &&
                requests.Select(row => row.AlternativeId)
                    .Distinct(StringComparer.Ordinal).Count() == requests.Count,
            "Money Revenue terminal rebind requests are incomplete or duplicated.");
        var requiredIds = required.Select(row => row.AlternativeId)
            .Order(StringComparer.Ordinal);
        var requestedIds = requests.Select(row => row.AlternativeId)
            .Order(StringComparer.Ordinal);
        Require(requiredIds.SequenceEqual(requestedIds, StringComparer.Ordinal),
            "Money Revenue terminal rebind requests do not equal the required skeleton denominator.");
    }

    private static IEnumerable<RolloutStep> Steps(
        AcquisitionRoutePortfolioRolloutProofManifest manifest)
    {
        yield return new RolloutStep(
            manifest.InitialCheckpointProof.ExecutionInputs,
            manifest.InitialCheckpointProof.ExecutionBindingPath,
            manifest.InitialCheckpointProof.AfterSnapshotPath,
            manifest.InitialCheckpointProof.FreshTerminalReceiptPath);
        foreach (var transition in manifest.ContinuationTransitions)
        {
            yield return new RolloutStep(
                transition.ExecutionInputs,
                transition.ExecutionBindingPath,
                transition.AfterSnapshotPath,
                transition.FreshTerminalReceiptPath);
        }
    }

    private static VerifiedRolloutStep ReadStep(
        RolloutStep step,
        int transitionIndex)
    {
        var bindingPath = RequiredFile(
            step.ExecutionBindingPath,
            "Acquisition route execution binding");
        var freshReceiptPath = RequiredFile(
            step.FreshTerminalReceiptPath,
            "Acquisition route fresh terminal receipt");
        var beforePath = RequiredFile(
            step.Inputs.BeforeSnapshotPath,
            "Acquisition route before snapshot");
        var afterPath = RequiredFile(
            step.AfterSnapshotPath,
            "Acquisition route after snapshot");
        return new VerifiedRolloutStep(
            transitionIndex,
            beforePath,
            afterPath,
            freshReceiptPath,
            CurrentTeacherFrontierSupport.Read<
                AcquisitionRouteExecutionBinding>(
                bindingPath,
                "Acquisition route execution binding"),
            CurrentTeacherFrontierSupport.Read<
                AcquisitionRouteFreshTerminalReceiptAdmission>(
                freshReceiptPath,
                "Acquisition route fresh terminal receipt"));
    }

    internal static MoneyRevenueReboundTerminalCandidate[] ReboundCandidates(
        MoneyRevenueAlternativeSkeleton skeleton,
        string qualifiedItemId,
        IReadOnlyList<MoneyRevenueIncreasedInventorySlot> increasedSlots,
        MoneyRevenueCandidateDenominatorCatalog freshCatalog)
    {
        Require(skeleton.RequiresFreshTerminalCandidateRebind &&
                skeleton.RequiresAcquisitionOrProcessingRollout &&
                !skeleton.ConcreteAlternativeIdentityComplete &&
                skeleton.TerminalOptionId is
                    "economy.sell_items" or "economy.ship_items",
            "Money Revenue skeleton is not eligible for terminal rebind.");
        Require(freshCatalog.SourceStateHash.Length > 0 &&
                freshCatalog.NodeCoverageComplete &&
                !freshCatalog.FormalProductTrainingAuthorized,
            "Fresh Money Revenue candidate catalog is incomplete.");
        var nodes = freshCatalog.Nodes.Where(row =>
                row.OptionId == skeleton.TerminalOptionId)
            .ToArray();
        Require(nodes.Length == 1 && nodes[0].EnumerationComplete,
            "Fresh Money Revenue terminal option did not enumerate exactly once.");
        var slotIds = increasedSlots.Select(row => row.SlotIndex)
            .ToHashSet();
        var matches = nodes[0].Candidates
            .Where(row => row.ComparisonEligible &&
                row.SlotIndex.HasValue &&
                slotIds.Contains(row.SlotIndex.Value) &&
                string.Equals(
                    row.QualifiedItemId,
                    qualifiedItemId,
                    StringComparison.Ordinal))
            .OrderBy(row => row.CatalogCandidateId, StringComparer.Ordinal)
            .ToArray();
        Require(matches.Length > 0,
            "Fresh Money Revenue terminal option has no candidate bound to a newly produced inventory slot.");
        Require(matches.Select(row => row.CatalogCandidateId)
                .Distinct(StringComparer.Ordinal).Count() == matches.Length &&
                matches.Select(row => row.CandidateSha256)
                    .Distinct(StringComparer.Ordinal).Count() == matches.Length,
            "Fresh Money Revenue terminal candidates are duplicated.");

        return matches.Select(candidate =>
        {
            var sha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .ArtifactSha256(new
                {
                    skeleton.AlternativeId,
                    skeleton.PathSha256,
                    freshCatalog.SourceStateHash,
                    candidate.CatalogCandidateId,
                    candidate.CandidateSha256
                });
            return new MoneyRevenueReboundTerminalCandidate
            {
                ConcreteAlternativeId =
                    "revenue-concrete:" + sha256[..24],
                TerminalNodeId = nodes[0].NodeId,
                TerminalCandidate = candidate,
                ConcreteAlternativeSha256 = sha256
            };
        }).ToArray();
    }

    private sealed record RolloutStep(
        AcquisitionRouteExecutionBindingInputs Inputs,
        string ExecutionBindingPath,
        string AfterSnapshotPath,
        string FreshTerminalReceiptPath);

    private sealed record VerifiedRolloutStep(
        int TransitionIndex,
        string BeforeSnapshotPath,
        string AfterSnapshotPath,
        string FreshReceiptPath,
        AcquisitionRouteExecutionBinding Binding,
        AcquisitionRouteFreshTerminalReceiptAdmission FreshReceipt);
}
