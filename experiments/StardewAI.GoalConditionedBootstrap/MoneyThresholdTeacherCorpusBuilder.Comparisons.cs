using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static MoneyThresholdTeacherComparisonEvidence
        BuildComparisonEvidence(
            GrandpaMoneyThreshold threshold,
            MoneyThresholdVerifiedNativeOutcome outcome,
            MoneyThresholdTeacherComparisonSource source)
    {
        var manifestPath = RequiredFile(
            source.AdmissionManifestPath,
            "Money-threshold comparison admission manifest");
        var receiptPath = RequiredFile(
            source.AdmissionReceiptPath,
            "Money-threshold comparison admission receipt");
        var expected = MoneyRevenueComparisonAdmissionBuilder.Build(
            manifestPath);
        var receipt = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueComparisonAdmissionReceipt>(
            receiptPath,
            "Money-threshold comparison admission receipt");
        if (!EqualJson(receipt, expected))
        {
            throw new InvalidDataException(
                "Money-threshold comparison receipt drifted from its exact rebuild.");
        }

        var join = ValidateComparisonJoin(threshold, outcome, receipt);
        return new MoneyThresholdTeacherComparisonEvidence(
            threshold.CriterionId,
            manifestPath,
            CurrentTeacherFrontierSupport.HashFile(manifestPath),
            receiptPath,
            CurrentTeacherFrontierSupport.HashFile(receiptPath),
            receipt.SourceStateHash,
            receipt.RequestDenominatorCount,
            join.PreferredAlternative,
            join.RejectedAlternatives,
            outcome.ExecutionEpisodeSha256,
            receipt.Comparison.SelectionPolicy,
            outcome.PrimarySha256);
    }

    internal static MoneyThresholdComparisonJoin
        VerifyComparisonJoinForTest(
            string criterionId,
            string executionEpisodePath,
            MoneyRevenueComparisonAdmissionReceipt receipt)
    {
        var threshold = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .SingleOrDefault(value => value.CriterionId == criterionId) ??
            throw new InvalidDataException(
                "Money-threshold test criterion is not authoritative.");
        var sale = NativeShopSaleEpisodeVerifier.Verify(
            executionEpisodePath);
        return ValidateComparisonJoin(
            threshold,
            VerifyNativeSale(sale.ExecutionEpisodePath),
            receipt);
    }

    internal static MoneyThresholdComparisonJoin
        VerifyTrajectoryComparisonJoinForTest(
            string criterionId,
            string trajectoryReceiptPath,
            MoneyThresholdNativeTrajectoryReceipt trajectory,
            MoneyRevenueComparisonAdmissionReceipt receipt)
    {
        var threshold = GrandpaEvaluationGoalEvaluator.MoneyThresholds
            .SingleOrDefault(value => value.CriterionId == criterionId) ??
            throw new InvalidDataException(
                "Money-threshold test criterion is not authoritative.");
        return ValidateComparisonJoin(
            threshold,
            ProjectNativeTrajectory(
                trajectoryReceiptPath,
                trajectory),
            receipt);
    }

    private static MoneyThresholdComparisonJoin ValidateComparisonJoin(
        GrandpaMoneyThreshold threshold,
        MoneyThresholdVerifiedNativeOutcome outcome,
        MoneyRevenueComparisonAdmissionReceipt receipt)
    {
        var comparison = receipt.Comparison;
        if (receipt.SchemaVersion !=
                MoneyRevenueComparisonAdmissionVersions.Receipt ||
            !receipt.CandidateDenominatorAuthoritative ||
            !receipt.TerminalExecutionProofsVerified ||
            !receipt.ProductionTeacherPreferenceLabelEligible ||
            receipt.FormalProductTrainingAuthorized ||
            comparison.CriterionId != threshold.CriterionId ||
            comparison.TargetTotalMoneyEarned != threshold.Target ||
            comparison.SourceStateHash != receipt.SourceStateHash ||
            receipt.SourceStateHash != outcome.SourceCheckpoint.StateHash ||
            comparison.ExpectedLedgerRevision !=
                receipt.SourceLedgerRevision ||
            !comparison.CandidateDenominatorComplete ||
            comparison.CandidateDenominatorCount !=
                receipt.RequestDenominatorCount ||
            comparison.SelectionDisposition !=
                MoneyRevenueAlternativeSelectionDisposition
                    .UniqueAuthoritativePreference ||
            !comparison.TeacherPreferenceLabelEligible ||
            comparison.FormalTrainingAuthorized ||
            comparison.UsesLearnerScore ||
            string.IsNullOrWhiteSpace(comparison.SelectedAlternativeId))
        {
            throw new InvalidDataException(
                "Money-threshold comparison is not an authoritative criterion-local preference.");
        }

        var alternatives = receipt.Alternatives;
        var concreteIds = receipt.ConcreteAlternativeIds;
        if (alternatives.Length != receipt.RequestDenominatorCount ||
            concreteIds.Length != receipt.RequestDenominatorCount ||
            concreteIds.Distinct(StringComparer.Ordinal).Count() !=
                concreteIds.Length ||
            alternatives.Select(value => value.ConcreteAlternativeId)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    concreteIds.Order(StringComparer.Ordinal),
                    StringComparer.Ordinal) == false)
        {
            throw new InvalidDataException(
                "Money-threshold comparison denominator identity is incomplete.");
        }

        var selected = alternatives.SingleOrDefault(value =>
            value.ConcreteAlternativeId ==
                comparison.SelectedAlternativeId) ??
            throw new InvalidDataException(
                "Money-threshold selected alternative is missing.");
        ValidateSelectedNativeOutcome(selected, outcome);

        var rejected = concreteIds
            .Where(value => value != comparison.SelectedAlternativeId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var preferences = comparison.PairwisePreferences;
        if (rejected.Length == 0 ||
            preferences.Length != rejected.Length ||
            preferences.Any(value =>
                value.PreferredAlternativeId !=
                    comparison.SelectedAlternativeId ||
                value.Criterion !=
                    "authoritative_threshold_then_reserve_aware_strict_pareto") ||
            !preferences.Select(value => value.AlternativeId)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(rejected, StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Money-threshold comparison pairwise denominator is incomplete.");
        }
        var selectedEvaluation = comparison.Evaluations.SingleOrDefault(
            value => value.AlternativeId ==
                comparison.SelectedAlternativeId);
        if (selectedEvaluation is null ||
            !selectedEvaluation.TargetSatisfied ||
            !selectedEvaluation.OnParetoFrontier)
        {
            throw new InvalidDataException(
                "Money-threshold selected alternative does not satisfy its target.");
        }
        return new MoneyThresholdComparisonJoin(
            comparison.SelectedAlternativeId,
            rejected);
    }

    private static void ValidateSelectedNativeOutcome(
        MoneyRevenueComparisonAdmissionAlternative selected,
        MoneyThresholdVerifiedNativeOutcome outcome)
    {
        if (selected.AcquisitionRolloutRequired !=
                outcome.AcquisitionRolloutRequired ||
            (outcome.Kind ==
                MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode &&
             selected.AcquisitionRolloutRequired) ||
            (outcome.Kind ==
                MoneyThresholdNativeOutcomeKinds.RevenueTrajectory &&
             (selected.ConcreteAlternativeId !=
                outcome.ConcreteAlternativeId ||
              selected.TerminalExecutionEvidenceSha256 !=
                outcome.TerminalExecutionEvidenceSha256)))
        {
            throw new InvalidDataException(
                "Money-threshold selected alternative does not match the native outcome trajectory identity.");
        }
        var artifacts = selected.NativeTerminalArtifacts;
        var rolePrefix = outcome.Kind ==
            MoneyThresholdNativeOutcomeKinds.RevenueTrajectory
                ? "terminal_"
                : string.Empty;
        var expectedArtifactCount = outcome.Kind ==
            MoneyThresholdNativeOutcomeKinds.RevenueTrajectory
                ? outcome.Artifacts.Count(value => value.Role.StartsWith(
                    rolePrefix,
                    StringComparison.Ordinal))
                : outcome.Artifacts.Length;
        if (artifacts.Length == 0 ||
            artifacts.Length != expectedArtifactCount ||
            artifacts.Select(value => value.Role)
                .Distinct(StringComparer.Ordinal).Count() != artifacts.Length ||
            artifacts.Any(artifact => !MatchesArtifact(
                outcome.Artifacts,
                rolePrefix + artifact.Role,
                artifact.Path,
                artifact.Sha256)))
        {
            throw new InvalidDataException(
                "Money-threshold selected alternative does not bind the native terminal artifacts.");
        }

        var prefix = selected.RevenuePrefix;
        if (!EqualJson(prefix, outcome.RevenuePrefix) ||
            prefix.SaveId != outcome.SourceCheckpoint.SaveId ||
            prefix.PlayerId != outcome.SourceCheckpoint.PlayerId ||
            prefix.InitialTotalMoneyEarned !=
                outcome.SourceCheckpoint.TotalMoneyEarned)
        {
            throw new InvalidDataException(
                "Money-threshold selected alternative does not bind the native Revenue prefix.");
        }
    }

    private static bool MatchesArtifact(
        MoneyRevenueArtifactBinding[] artifacts,
        string role,
        string path,
        string sha256)
    {
        var artifact = artifacts.SingleOrDefault(value => value.Role == role);
        return artifact is not null &&
            Path.GetFullPath(artifact.Path) == Path.GetFullPath(path) &&
            artifact.Sha256 == sha256;
    }
}

internal sealed record MoneyThresholdComparisonJoin(
    string PreferredAlternative,
    string[] RejectedAlternatives);
