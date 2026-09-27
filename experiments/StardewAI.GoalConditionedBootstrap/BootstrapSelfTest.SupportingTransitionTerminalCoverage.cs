namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifySupportingTransitionTerminalCoverageMatrix(
        params AcquisitionRouteSupportingTransitionTerminalCoverageSource[]
            sources)
    {
        var requestPath = Path.Combine(
            Path.GetDirectoryName(
                sources[0].RolloutProofManifestPath)!,
            "support-terminal-coverage-request.json");
        Write(
            requestPath,
            new AcquisitionRouteSupportingTransitionTerminalCoverageRequest
            {
                Sources = sources
            });
        var complete =
            AcquisitionRouteSupportingTransitionTerminalCoverageBuilder.Build(
                requestPath);
        Write(
            Path.Combine(
                Path.GetDirectoryName(requestPath)!,
                "support-terminal-coverage-report.json"),
            complete);
        Require(complete.TerminalLineageCoverageComplete &&
                !complete.FormalProductTrainingAuthorized &&
                complete.CoveredSupportTransitionKinds.SequenceEqual(
                    AcquisitionRouteSupportingTransitionKinds.All,
                    StringComparer.Ordinal) &&
                complete.MissingSupportTransitionKinds.Length == 0 &&
                complete.Rows.Length ==
                    AcquisitionRouteSupportingTransitionKinds.All.Length &&
                complete.Rows.All(row =>
                    row.CoverageVerified &&
                    row.SupportExcludedFromTerminalOutcomes),
            "Complete support-family terminal coverage was misreported.");

        var duplicate =
            AcquisitionRouteSupportingTransitionTerminalCoverageBuilder
                .BuildReport(complete.Rows.Append(complete.Rows[0]));
        Require(!duplicate.TerminalLineageCoverageComplete &&
                duplicate.BlockingReasons.Contains(
                    "support_terminal_lineage_duplicate:" +
                    complete.Rows[0].SupportTransitionKind,
                    StringComparer.Ordinal),
            "Duplicate support-family evidence was counted as coverage.");
    }
}
