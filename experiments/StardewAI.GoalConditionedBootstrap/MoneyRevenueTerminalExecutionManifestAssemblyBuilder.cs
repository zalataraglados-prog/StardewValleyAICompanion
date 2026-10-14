namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueTerminalExecutionManifestAssemblyBuilder
{
    public static MoneyRevenueTerminalExecutionProofManifest Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue terminal execution manifest assembly request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalExecutionManifestAssemblyRequest>(
                fullRequestPath,
                "Money Revenue terminal execution manifest assembly request");
        var alternatives = request.Alternatives ??
            Array.Empty<MoneyRevenueTerminalAlternativeProof>();
        Require(request.SchemaVersion ==
                    "money_revenue_terminal_execution_manifest_assembly_request.v1" &&
                alternatives.Length > 0 &&
                alternatives.All(value => value is not null &&
                    !string.IsNullOrWhiteSpace(value.RequestId)) &&
                !request.FormalProductTrainingAuthorized,
            "Money Revenue terminal execution manifest assembly request is invalid.");

        var requestManifestPath = RequiredFile(
            request.CounterfactualRequestManifestPath,
            "Money Revenue counterfactual request manifest");
        var requestCatalogPath = RequiredFile(
            request.CounterfactualRequestCatalogPath,
            "Money Revenue counterfactual request catalog");
        var expectedCatalog = MoneyRevenueCounterfactualProofRequestBuilder
            .Build(requestManifestPath);
        var catalog = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCounterfactualProofRequestCatalog>(
                requestCatalogPath,
                "Money Revenue counterfactual request catalog");
        Require(EqualJson(catalog, expectedCatalog) &&
                catalog.RequestDenominatorAuthoritative &&
                catalog.CounterfactualProofCollectionReady &&
                !catalog.FormalProductTrainingAuthorized,
            "Money Revenue counterfactual request catalog is not exact.");

        var proofById = alternatives
            .GroupBy(value => value.RequestId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        Require(proofById.Count == catalog.RequestCount &&
                proofById.All(value => value.Value.Length == 1) &&
                catalog.RequestIds.Order(StringComparer.Ordinal).SequenceEqual(
                    proofById.Keys.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "Money Revenue terminal execution proof denominator is not exact.");

        return new MoneyRevenueTerminalExecutionProofManifest
        {
            CounterfactualRequestManifestPath = requestManifestPath,
            CounterfactualRequestCatalogPath = requestCatalogPath,
            Alternatives = catalog.RequestIds.Select(requestId =>
                    Normalize(proofById[requestId][0]))
                .ToArray(),
            FormalProductTrainingAuthorized = false
        };
    }

    private static MoneyRevenueTerminalAlternativeProof Normalize(
        MoneyRevenueTerminalAlternativeProof source) => new()
    {
        RequestId = source.RequestId,
        BeforeStrategyLedgerCapturePath = RequiredFile(
            source.BeforeStrategyLedgerCapturePath,
            "Before-terminal strategy-ledger capture"),
        AfterStrategyLedgerCapturePath = RequiredFile(
            source.AfterStrategyLedgerCapturePath,
            "After-terminal strategy-ledger capture"),
        RouteTransitions = (source.RouteTransitions ??
                Array.Empty<MoneyRevenueTerminalQueueTransitionProof>())
            .Select(Normalize)
            .ToArray(),
        ShopSaleEpisodePath = OptionalFile(
            source.ShopSaleEpisodePath,
            "Native shop-sale episode"),
        ShippingDepositTransition = source.ShippingDepositTransition is null
            ? null
            : Normalize(source.ShippingDepositTransition),
        RevenueRecurrenceManifestPath = RequiredFile(
            source.RevenueRecurrenceManifestPath,
            "Money Revenue recurrence manifest"),
        RevenueRecurrenceReceiptPath = RequiredFile(
            source.RevenueRecurrenceReceiptPath,
            "Money Revenue recurrence receipt")
    };

    private static MoneyRevenueTerminalQueueTransitionProof Normalize(
        MoneyRevenueTerminalQueueTransitionProof source) => new()
    {
        TransitionId = source.TransitionId,
        QueuePath = RequiredFile(source.QueuePath, "Terminal route queue"),
        BeforeSnapshotPath = RequiredFile(
            source.BeforeSnapshotPath,
            "Terminal route before snapshot"),
        ExecutionReceiptPath = RequiredFile(
            source.ExecutionReceiptPath,
            "Terminal route execution receipt"),
        AfterSnapshotPath = RequiredFile(
            source.AfterSnapshotPath,
            "Terminal route after snapshot"),
        RunId = source.RunId,
        ExecutorVersion = source.ExecutorVersion,
        SelectedCandidateId = source.SelectedCandidateId
    };

    private static string OptionalFile(string path, string label) =>
        string.IsNullOrWhiteSpace(path) ? string.Empty : RequiredFile(path, label);

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
