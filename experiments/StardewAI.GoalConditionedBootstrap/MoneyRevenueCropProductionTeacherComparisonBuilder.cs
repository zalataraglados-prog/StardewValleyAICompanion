namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyRevenueCropProductionTeacherComparisonBuilder
{
    public static MoneyRevenueCropProductionTeacherComparisonReceipt Build(
        string segmentReceiptPath)
    {
        var fullSegmentReceiptPath = RequiredFile(
            segmentReceiptPath,
            "Money Revenue crop native-production segment receipt");
        var segment = MoneyRevenueCropNativeProductionSegmentBuilder.Verify(
            fullSegmentReceiptPath);
        Require(segment.TeacherDecisionVerified &&
                segment.NativeProductionSegmentVerified &&
                !segment.TeacherComparisonVerified &&
                !segment.FormalProductTrainingAuthorized,
            "Money Revenue crop segment is not eligible for Teacher comparison.");

        var input = CurrentTeacherFrontierSupport.Read<
            MoneyCropNativeProductionInputManifest>(
                segment.ProductionInputManifestPath,
                "Money Revenue crop native-production input manifest");
        Require(CurrentTeacherFrontierSupport.HashFile(
                    segment.ProductionInputManifestPath) ==
                    segment.ProductionInputManifestSha256,
            "Money Revenue crop production input manifest drifted after segment verification.");
        var decisionPath = RequiredFile(
            input.DecisionRequestPath,
            "Money Revenue crop production Teacher decision");
        var decision = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionDecisionRequest>(
                decisionPath,
                "Money Revenue crop production Teacher decision");
        var rebuiltDecision =
            MoneyRevenueCropProductionTeacherDecisionBuilder.Build(
                input.TeacherRequestPath);
        Require(BootstrapArtifactGuard.EqualJson(decision, rebuiltDecision) &&
                CurrentTeacherFrontierSupport.HashFile(decisionPath) ==
                    input.DecisionRequestSha256 &&
                decision.DecisionId == segment.DecisionId &&
                decision.TeacherSelectionPolicy ==
                    MoneyRevenueCropProductionTeacherDecisionBuilder
                        .SelectionPolicy &&
                !decision.FormalProductTrainingAuthorized,
            "Money Revenue crop production Teacher denominator did not rebuild exactly.");

        var evaluations = decision.TeacherCandidateEvaluations ??
            Array.Empty<MoneyRevenueCropTeacherCandidateEvaluation>();
        var admitted = evaluations.Where(value => value.Admitted).ToArray();
        var selected = evaluations.Single(value => value.Selected);
        var expectedSelected = admitted
            .OrderByDescending(value => value.MinimumNetRevenue)
            .ThenBy(value => value.LastHarvestTotalDay)
            .ThenBy(value => value.RequiredWateringActionCount)
            .ThenBy(value => value.SeedId, StringComparer.Ordinal)
            .First();
        Require(evaluations.Length == segment.TeacherCandidateCount &&
                evaluations.Length > 1 &&
                admitted.Length > 1 &&
                evaluations.Count(value => value.Selected) == 1 &&
                selected.Admitted &&
                selected == expectedSelected &&
                selected.SeedId == segment.SelectedSeedId &&
                selected.SeedQualifiedItemId ==
                    segment.SelectedSeedQualifiedItemId &&
                selected.TileCount == segment.SelectedTileCount &&
                selected.MinimumGrossRevenue ==
                    segment.TeacherMinimumGrossRevenue &&
                selected.SeedAcquisitionCost ==
                    segment.TeacherSeedAcquisitionCost &&
                selected.MinimumNetRevenue ==
                    segment.TeacherMinimumNetRevenue &&
                segment.ActualSeedAcquisitionCost ==
                    selected.SeedAcquisitionCost &&
                segment.ActualGrossRevenue >=
                    selected.MinimumGrossRevenue &&
                segment.ActualCashNetRevenue >=
                    selected.MinimumNetRevenue,
            "Money Revenue crop selected native outcome drifted from the authoritative Teacher preference.");

        var candidateIds = evaluations.ToDictionary(
            value => value,
            CandidateId);
        Require(candidateIds.Values.Distinct(StringComparer.Ordinal).Count() ==
                    evaluations.Length &&
                evaluations.All(value => value.BlockReasons is not null) &&
                evaluations.Where(value => value.Admitted).All(value =>
                    value.BlockReasons.Length == 0) &&
                evaluations.Where(value => !value.Admitted).All(value =>
                    value.BlockReasons.Length > 0),
            "Money Revenue crop Teacher candidate denominator is incomplete or duplicated.");
        var candidates = evaluations
            .OrderBy(value => candidateIds[value], StringComparer.Ordinal)
            .Select(value => new
                MoneyRevenueCropProductionTeacherComparisonCandidate(
                    candidateIds[value],
                    value.SeedId,
                    value.SeedQualifiedItemId,
                    value.ShopId,
                    value.TileCount,
                    value.HarvestWaveCount,
                    value.MinimumGrossRevenue,
                    value.SeedAcquisitionCost,
                    value.MinimumNetRevenue,
                    value.LastHarvestTotalDay,
                    value.RequiredWateringActionCount,
                    value.Admitted,
                    value.BlockReasons,
                    value.Selected,
                    value.Selected,
                    value.Selected ? segment.ActualGrossRevenue : null,
                    value.Selected
                        ? segment.ActualSeedAcquisitionCost
                        : null,
                    value.Selected ? segment.ActualCashNetRevenue : null))
            .ToArray();
        var selectedCandidateId = candidateIds[selected];
        var pairwise = admitted
            .Where(value => !value.Selected)
            .OrderBy(value => candidateIds[value], StringComparer.Ordinal)
            .Select(value => new
                MoneyRevenueCropProductionTeacherPairwisePreference(
                    selectedCandidateId,
                    candidateIds[value],
                    decision.TeacherSelectionPolicy))
            .ToArray();
        Require(pairwise.Length == admitted.Length - 1 &&
                pairwise.All(value =>
                    value.PreferredCandidateId == selectedCandidateId),
            "Money Revenue crop Teacher pairwise denominator is incomplete.");

        var receipt = new
            MoneyRevenueCropProductionTeacherComparisonReceipt
        {
            Status = "verified_outcome_backed_crop_teacher_comparison",
            SegmentReceiptPath = fullSegmentReceiptPath,
            SegmentReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullSegmentReceiptPath),
            TeacherDecisionPath = decisionPath,
            TeacherDecisionSha256 = input.DecisionRequestSha256,
            DecisionId = decision.DecisionId,
            SourceStateHash = segment.SourceCheckpoint.StateHash,
            TeacherSelectionPolicy = decision.TeacherSelectionPolicy,
            CandidateDenominatorCount = candidates.Length,
            AdmittedCandidateCount = admitted.Length,
            BlockedCandidateCount = candidates.Length - admitted.Length,
            SelectedCandidateId = selectedCandidateId,
            SelectedSeedQualifiedItemId = selected.SeedQualifiedItemId,
            ActualSeedAcquisitionCost = segment.ActualSeedAcquisitionCost,
            ActualGrossRevenue = segment.ActualGrossRevenue,
            ActualCashNetRevenue = segment.ActualCashNetRevenue,
            CrossedCriterionIds = segment.CrossedCriterionIds,
            Candidates = candidates,
            PairwisePreferences = pairwise,
            CandidateDenominatorAuthoritative = true,
            SelectedNativeOutcomeVerified = true,
            OutcomeBackedTeacherComparisonVerified = true,
            CriterionLocalJoinReady = false,
            FormalProductTrainingAuthorized = false
        };
        receipt.ReceiptSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                receipt);
        return receipt;
    }

    public static MoneyRevenueCropProductionTeacherComparisonReceipt Verify(
        string receiptPath)
    {
        var fullPath = RequiredFile(
            receiptPath,
            "Money Revenue crop production Teacher comparison receipt");
        var persisted = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropProductionTeacherComparisonReceipt>(
                fullPath,
                "Money Revenue crop production Teacher comparison receipt");
        var rebuilt = Build(persisted.SegmentReceiptPath);
        Require(persisted.SchemaVersion ==
                    MoneyRevenueCropProductionTeacherComparisonVersions
                        .Receipt &&
                persisted.Status ==
                    "verified_outcome_backed_crop_teacher_comparison" &&
                persisted.CandidateDenominatorAuthoritative &&
                persisted.SelectedNativeOutcomeVerified &&
                persisted.OutcomeBackedTeacherComparisonVerified &&
                !persisted.CriterionLocalJoinReady &&
                !persisted.FormalProductTrainingAuthorized &&
                BootstrapArtifactGuard.EqualJson(persisted, rebuilt),
            "Money Revenue crop production Teacher comparison receipt did not rebuild exactly.");
        return persisted;
    }

    private static string CandidateId(
        MoneyRevenueCropTeacherCandidateEvaluation value) =>
        string.Join(
            ":",
            "crop",
            value.SeedId,
            value.ShopId,
            value.TileCount);

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
}
