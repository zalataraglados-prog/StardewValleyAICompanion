namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueTerminalRebindManifestAssemblyBuilder
{
    public static MoneyRevenueTerminalCandidateRebindManifest Build(
        string requestPath)
    {
        var fullRequestPath = RequiredFile(
            requestPath,
            "Money Revenue terminal rebind manifest assembly request");
        var request = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueTerminalRebindManifestAssemblyRequest>(
                fullRequestPath,
                "Money Revenue terminal rebind manifest assembly request");
        var rolloutProofs = request.RolloutProofs ??
            Array.Empty<MoneyRevenueAcquisitionRolloutProofSource>();
        Require(request.SchemaVersion ==
                    "money_revenue_terminal_rebind_manifest_assembly_request.v1" &&
                !request.FormalProductTrainingAuthorized &&
                rolloutProofs.All(value => value is not null &&
                    !string.IsNullOrWhiteSpace(value.AlternativeId) &&
                    !string.IsNullOrWhiteSpace(
                        value.RolloutProofManifestPath) &&
                    !string.IsNullOrWhiteSpace(
                        value.RolloutProofReceiptPath)),
            "Money Revenue terminal rebind manifest assembly request is invalid.");

        var planPath = RequiredFile(
            request.ComparisonCollectionPlanPath,
            "Money Revenue comparison collection plan");
        var persistedPlan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueComparisonCollectionPlan>(
                planPath,
                "Money Revenue comparison collection plan");
        var rebuiltPlan = MoneyRevenueComparisonCollectionPlanBuilder.Build(
            persistedPlan.RequestPath);
        Require(EqualJson(persistedPlan, rebuiltPlan) &&
                !persistedPlan.FormalProductTrainingAuthorized,
            "Money Revenue comparison collection plan drifted from its source rebuild.");

        var requiredIds = persistedPlan.WorkItems
            .Where(value => value.RequiresFreshTerminalRebind)
            .Select(value => value.AlternativeId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var proofById = rolloutProofs
            .GroupBy(value => value.AlternativeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.ToArray(),
                StringComparer.Ordinal);
        Require(proofById.Count == requiredIds.Length &&
                proofById.All(value => value.Value.Length == 1) &&
                requiredIds.SequenceEqual(
                    proofById.Keys.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "Money Revenue acquisition rollout proof denominator is not exact.");

        var rebinds = requiredIds.Select(alternativeId =>
        {
            var proof = proofById[alternativeId][0];
            return new MoneyRevenueTerminalCandidateRebindRequest
            {
                AlternativeId = alternativeId,
                AcquisitionRolloutProofManifestPath = RequiredFile(
                    proof.RolloutProofManifestPath,
                    "Acquisition rollout proof manifest"),
                AcquisitionRolloutProofReceiptPath = RequiredFile(
                    proof.RolloutProofReceiptPath,
                    "Acquisition rollout proof receipt")
            };
        }).ToArray();
        return new MoneyRevenueTerminalCandidateRebindManifest
        {
            AlternativeDenominatorCatalogPath =
                persistedPlan.AlternativeDenominatorCatalogPath,
            Rebinds = rebinds,
            FormalProductTrainingAuthorized = false
        };
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
