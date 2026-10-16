namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueTerminalProofCollectionCheckpointBuilder
{
    public static MoneyRevenueTerminalProofCollectionCheckpoint Build(
        string requestPath)
    {
        var context = ReadContext(requestPath);
        return BuildCheckpoint(
            context.RequestPath,
            context.RequestSha256,
            context.PlanPath,
            context.PlanSha256,
            context.Plan,
            context.FragmentIndexPath,
            context.FragmentIndexSha256,
            context.Catalog,
            context.FragmentIndex);
    }

    public static MoneyRevenueTerminalProofIndex BuildFinalIndex(
        string requestPath,
        string checkpointPath)
    {
        var fullCheckpointPath = RequiredFile(
            checkpointPath,
            "Money Revenue terminal-proof collection checkpoint");
        var context = ReadContext(requestPath);
        var expected = BuildCheckpoint(
            context.RequestPath,
            context.RequestSha256,
            context.PlanPath,
            context.PlanSha256,
            context.Plan,
            context.FragmentIndexPath,
            context.FragmentIndexSha256,
            context.Catalog,
            context.FragmentIndex);
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalProofCollectionCheckpoint>(
                fullCheckpointPath,
                "Money Revenue terminal-proof collection checkpoint");
        Require(BootstrapArtifactGuard.EqualJson(persisted, expected),
            "Money Revenue terminal-proof collection is incomplete or drifted.");
        return BuildFinalIndex(
            fullCheckpointPath,
            CurrentTeacherFrontierSupport.HashFile(fullCheckpointPath),
            expected,
            context.Plan,
            context.FragmentIndex);
    }

    internal static MoneyRevenueTerminalProofIndex BuildFinalIndex(
        string checkpointPath,
        string checkpointSha256,
        MoneyRevenueTerminalProofCollectionCheckpoint checkpoint,
        MoneyRevenueTerminalProofCollectionPlan plan,
        MoneyRevenueTerminalProofFragmentIndex fragmentIndex)
    {
        Require(checkpoint.TerminalProofIndexReady &&
                checkpoint.PendingRequestCount == 0 &&
                checkpoint.VerifiedFragmentCount == checkpoint.RequestCount &&
                checkpoint.RequestCount == plan.RequestCount &&
                !checkpoint.FormalProductTrainingAuthorized,
            "Money Revenue terminal-proof collection is incomplete.");
        var proofByRequest = fragmentIndex.Alternatives
            .ToDictionary(value => value.RequestId, StringComparer.Ordinal);
        return new MoneyRevenueTerminalProofIndex
        {
            CollectionCheckpointPath = checkpointPath,
            CollectionCheckpointSha256 = checkpointSha256,
            Alternatives = plan.RequestIds
                .Select(requestId => proofByRequest[requestId])
                .ToArray(),
            FormalProductTrainingAuthorized = false
        };
    }

    internal static MoneyRevenueTerminalProofCollectionCheckpoint
        BuildCheckpoint(
            string requestPath,
            string requestSha256,
            string planPath,
            string planSha256,
            MoneyRevenueTerminalProofCollectionPlan plan,
            string fragmentIndexPath,
            string fragmentIndexSha256,
            MoneyRevenueCounterfactualProofRequestCatalog catalog,
            MoneyRevenueTerminalProofFragmentIndex fragmentIndex)
    {
        var requests = catalog.Requests ??
            Array.Empty<MoneyRevenueCounterfactualProofRequest>();
        var planRequestIds = plan.RequestIds ?? Array.Empty<string>();
        var fragments = fragmentIndex.Alternatives ??
            Array.Empty<MoneyRevenueTerminalAlternativeProof>();
        Require(plan.RequestCount > 0 &&
                plan.RequestCount == requests.Length &&
                planRequestIds.Length == plan.RequestCount &&
                requests.All(value => value is not null &&
                    !string.IsNullOrWhiteSpace(value.RequestId)) &&
                requests.Select(value => value.RequestId)
                    .Distinct(StringComparer.Ordinal).Count() == requests.Length &&
                planRequestIds.SequenceEqual(
                    requests.Select(value => value.RequestId),
                    StringComparer.Ordinal) &&
                !plan.TerminalProofIndexReady &&
                !plan.FormalProductTrainingAuthorized &&
                !fragmentIndex.FormalProductTrainingAuthorized,
            "Money Revenue terminal-proof checkpoint denominator is invalid.");
        var requestById = requests.ToDictionary(
            value => value.RequestId,
            StringComparer.Ordinal);
        Require(fragments.All(value => value is not null &&
                    !string.IsNullOrWhiteSpace(value.RequestId)),
            "Money Revenue terminal-proof fragment identity is invalid.");
        var groupedFragments = fragments
            .GroupBy(value => value.RequestId, StringComparer.Ordinal)
            .ToArray();
        Require(groupedFragments.All(group => group.Count() == 1) &&
                groupedFragments.All(group =>
                    requestById.ContainsKey(group.Key)),
            "Money Revenue terminal-proof fragments are duplicated or out of denominator.");

        var proofById = groupedFragments.ToDictionary(
            group => group.Key,
            group => group.Single(),
            StringComparer.Ordinal);
        var verifiedIds = planRequestIds
            .Where(proofById.ContainsKey)
            .ToArray();
        var evidence = verifiedIds.Select(requestId =>
                MoneyRevenueTerminalExecutionProofBuilder.VerifyAlternative(
                    requestById[requestId],
                    proofById[requestId]))
            .ToArray();
        var pendingIds = planRequestIds
            .Where(requestId => !proofById.ContainsKey(requestId))
            .ToArray();
        var complete = pendingIds.Length == 0;
        return new MoneyRevenueTerminalProofCollectionCheckpoint
        {
            Status = complete
                ? "terminal_proof_denominator_complete"
                : "terminal_proof_collection_in_progress",
            RequestPath = requestPath,
            RequestSha256 = requestSha256,
            CollectionPlanPath = planPath,
            CollectionPlanSha256 = planSha256,
            FragmentIndexPath = fragmentIndexPath,
            FragmentIndexSha256 = fragmentIndexSha256,
            RequestCount = plan.RequestCount,
            VerifiedFragmentCount = verifiedIds.Length,
            PendingRequestCount = pendingIds.Length,
            VerifiedRequestIds = verifiedIds,
            PendingRequestIds = pendingIds,
            VerifiedEvidence = evidence,
            TerminalProofIndexReady = complete,
            FormalProductTrainingAuthorized = false
        };
    }

    private static CollectionContext ReadContext(string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue terminal-proof checkpoint request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalProofCollectionCheckpointRequest>(
                fullRequestPath,
                "Money Revenue terminal-proof checkpoint request");
        Require(request.SchemaVersion ==
                    "money_revenue_terminal_proof_collection_checkpoint_request.v1" &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue terminal-proof checkpoint request is invalid.");

        var planPath = RequiredFile(
            request.CollectionPlanPath,
            "Money Revenue terminal-proof collection plan");
        var plan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalProofCollectionPlan>(
                planPath,
                "Money Revenue terminal-proof collection plan");
        var rebuiltPlan = MoneyRevenueTerminalProofCollectionPlanBuilder.Build(
            plan.RequestPath);
        Require(BootstrapArtifactGuard.EqualJson(plan, rebuiltPlan) &&
                !plan.FormalProductTrainingAuthorized,
            "Money Revenue terminal-proof collection plan is not exact.");

        var catalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCounterfactualProofRequestCatalog>(
                plan.CounterfactualRequestCatalogPath,
                "Money Revenue counterfactual request catalog");
        var rebuiltCatalog = MoneyRevenueCounterfactualProofRequestBuilder.Build(
            plan.CounterfactualRequestManifestPath);
        Require(BootstrapArtifactGuard.EqualJson(catalog, rebuiltCatalog) &&
                catalog.CounterfactualProofCollectionReady,
            "Money Revenue counterfactual request catalog is not exact.");

        var fragmentIndexPath = RequiredFile(
            request.FragmentIndexPath,
            "Money Revenue terminal-proof fragment index");
        var fragmentIndex = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalProofFragmentIndex>(
                fragmentIndexPath,
                "Money Revenue terminal-proof fragment index");
        Require(fragmentIndex.SchemaVersion ==
                    "money_revenue_terminal_proof_fragment_index.v1" &&
                string.Equals(
                    Path.GetFullPath(fragmentIndex.CollectionPlanPath),
                    planPath,
                    StringComparison.OrdinalIgnoreCase) &&
                !fragmentIndex.FormalProductTrainingAuthorized,
            "Money Revenue terminal-proof fragment index is invalid.");

        return new CollectionContext(
            fullRequestPath,
            CurrentTeacherFrontierSupport.HashFile(fullRequestPath),
            planPath,
            CurrentTeacherFrontierSupport.HashFile(planPath),
            plan,
            fragmentIndexPath,
            CurrentTeacherFrontierSupport.HashFile(fragmentIndexPath),
            catalog,
            fragmentIndex);
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record CollectionContext(
        string RequestPath,
        string RequestSha256,
        string PlanPath,
        string PlanSha256,
        MoneyRevenueTerminalProofCollectionPlan Plan,
        string FragmentIndexPath,
        string FragmentIndexSha256,
        MoneyRevenueCounterfactualProofRequestCatalog Catalog,
        MoneyRevenueTerminalProofFragmentIndex FragmentIndex);
}
