namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyThresholdTeacherCorpusBuilder
{
    private static MoneyThresholdVerifiedNativeOutcome VerifyNativeOutcome(
        MoneyThresholdTeacherCorpusSource source)
    {
        var hasSale = !string.IsNullOrWhiteSpace(
            source.ExecutionEpisodePath);
        var hasTrajectory = !string.IsNullOrWhiteSpace(
            source.NativeTrajectoryReceiptPath);
        if (hasSale == hasTrajectory)
        {
            throw new InvalidDataException(
                "Money-threshold corpus source must select exactly one native outcome kind.");
        }
        return hasSale
            ? VerifyNativeSale(source.ExecutionEpisodePath)
            : VerifyNativeTrajectory(source.NativeTrajectoryReceiptPath);
    }

    private static MoneyThresholdVerifiedNativeOutcome VerifyNativeSale(
        string executionEpisodePath)
    {
        var sale = NativeShopSaleEpisodeVerifier.Verify(
            executionEpisodePath);
        var progression = sale.Progression;
        var prefix = MoneyRevenueRecurrenceVerifier.VerifyPrefix(
            new[] { progression });
        return new MoneyThresholdVerifiedNativeOutcome(
            MoneyThresholdNativeOutcomeKinds.ShopSaleEpisode,
            sale.ExecutionEpisodePath,
            sale.ExecutionEpisodeSha256,
            sale.ExecutionEpisodePath,
            sale.ExecutionEpisodeSha256,
            sale.BeforeSnapshotPath,
            sale.BeforeSnapshotSha256,
            sale.AfterSnapshotPath,
            sale.AfterSnapshotSha256,
            sale.ExecutionResultPath,
            sale.ExecutionResultSha256,
            string.Empty,
            progression.Before,
            progression.After,
            prefix,
            string.Empty,
            string.Empty,
            string.Empty,
            false,
            new[]
            {
                Artifact(
                    "shop_sale_episode",
                    sale.ExecutionEpisodePath,
                    sale.ExecutionEpisodeSha256),
                Artifact(
                    "shop_sale_before_snapshot",
                    sale.BeforeSnapshotPath,
                    sale.BeforeSnapshotSha256),
                Artifact(
                    "shop_sale_execution_result",
                    sale.ExecutionResultPath,
                    sale.ExecutionResultSha256),
                Artifact(
                    "shop_sale_after_snapshot",
                    sale.AfterSnapshotPath,
                    sale.AfterSnapshotSha256)
            });
    }

    private static MoneyThresholdVerifiedNativeOutcome VerifyNativeTrajectory(
        string receiptPath)
    {
        var fullReceiptPath = RequiredFile(
            receiptPath,
            "Money-threshold native trajectory receipt");
        var trajectory = MoneyThresholdNativeTrajectoryBuilder.Verify(
            fullReceiptPath);
        return ProjectNativeTrajectory(fullReceiptPath, trajectory);
    }

    internal static MoneyThresholdVerifiedNativeOutcome
        ProjectNativeTrajectory(
            string receiptPath,
            MoneyThresholdNativeTrajectoryReceipt trajectory)
    {
        var fullReceiptPath = RequiredFile(
            receiptPath,
            "Money-threshold native trajectory receipt");
        var receiptFileSha256 = CurrentTeacherFrontierSupport.HashFile(
            fullReceiptPath);
        var terminalReceipt = trajectory.Artifacts.SingleOrDefault(value =>
            value.Role == "terminal_execution_proof_receipt") ??
            throw new InvalidDataException(
                "Money-threshold native trajectory terminal receipt is missing.");
        var artifacts = trajectory.Artifacts
            .Append(new MoneyRevenueArtifactBinding(
                "native_trajectory_receipt",
                fullReceiptPath,
                receiptFileSha256))
            .OrderBy(value => value.Role, StringComparer.Ordinal)
            .ToArray();
        if (artifacts.Select(value => value.Role)
                .Distinct(StringComparer.Ordinal).Count() != artifacts.Length)
        {
            throw new InvalidDataException(
                "Money-threshold native trajectory artifact roles are not unique.");
        }
        return new MoneyThresholdVerifiedNativeOutcome(
            MoneyThresholdNativeOutcomeKinds.RevenueTrajectory,
            fullReceiptPath,
            receiptFileSha256,
            string.Empty,
            string.Empty,
            trajectory.SourceSnapshotPath,
            trajectory.SourceSnapshotSha256,
            trajectory.FinalSnapshotPath,
            trajectory.FinalSnapshotSha256,
            terminalReceipt.Path,
            terminalReceipt.Sha256,
            trajectory.TerminalExecutionEvidenceSha256,
            trajectory.SourceCheckpoint,
            trajectory.FinalCheckpoint,
            trajectory.RevenuePrefix,
            fullReceiptPath,
            receiptFileSha256,
            trajectory.ConcreteAlternativeId,
            trajectory.AcquisitionRolloutRequired,
            artifacts);
    }

    private static MoneyRevenueArtifactBinding Artifact(
        string role,
        string path,
        string sha256) => new(
            role,
            Path.GetFullPath(path),
            sha256);

}

internal sealed record MoneyThresholdVerifiedNativeOutcome(
    string Kind,
    string PrimaryPath,
    string PrimarySha256,
    string ExecutionEpisodePath,
    string ExecutionEpisodeSha256,
    string BeforeSnapshotPath,
    string BeforeSnapshotSha256,
    string AfterSnapshotPath,
    string AfterSnapshotSha256,
    string TerminalEvidencePath,
    string TerminalEvidenceSha256,
    string TerminalExecutionEvidenceSha256,
    MoneyRevenueCheckpoint SourceCheckpoint,
    MoneyRevenueCheckpoint FinalCheckpoint,
    MoneyRevenueRecurrencePrefixEvidence RevenuePrefix,
    string NativeTrajectoryReceiptPath,
    string NativeTrajectoryReceiptSha256,
    string ConcreteAlternativeId,
    bool AcquisitionRolloutRequired,
    MoneyRevenueArtifactBinding[] Artifacts);
