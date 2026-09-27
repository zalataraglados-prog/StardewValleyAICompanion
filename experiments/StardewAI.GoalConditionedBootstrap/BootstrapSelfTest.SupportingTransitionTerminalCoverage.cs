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
        var partial =
            AcquisitionRouteSupportingTransitionTerminalCoverageBuilder.Build(
                requestPath);
        Write(
            Path.Combine(
                Path.GetDirectoryName(requestPath)!,
                "support-terminal-coverage-report.json"),
            partial);
        Require(!partial.TerminalLineageCoverageComplete &&
                !partial.FormalProductTrainingAuthorized &&
                partial.CoveredSupportTransitionKinds.SequenceEqual(
                    new[]
                    {
                        AcquisitionRouteSupportingTransitionKinds
                            .MachineInputLoad,
                        AcquisitionRouteSupportingTransitionKinds
                            .MachineInputMaterialTransfer,
                        AcquisitionRouteSupportingTransitionKinds
                            .MachineInputPurchase,
                        AcquisitionRouteSupportingTransitionKinds
                            .MachineCapacityEstablishment
                    },
                    StringComparer.Ordinal) &&
                partial.MissingSupportTransitionKinds.SequenceEqual(
                    new[]
                    {
                        AcquisitionRouteSupportingTransitionKinds.CropPlanting
                    },
                    StringComparer.Ordinal) &&
                partial.Rows.Length == 4 &&
                partial.Rows.All(row =>
                    row.CoverageVerified &&
                    row.SupportExcludedFromTerminalOutcomes),
            "Partial support-family terminal coverage was misreported.");

        var duplicate =
            AcquisitionRouteSupportingTransitionTerminalCoverageBuilder
                .BuildReport(partial.Rows.Append(partial.Rows[0]));
        Require(!duplicate.TerminalLineageCoverageComplete &&
                duplicate.BlockingReasons.Contains(
                    "support_terminal_lineage_duplicate:" +
                    partial.Rows[0].SupportTransitionKind,
                    StringComparer.Ordinal),
            "Duplicate support-family evidence was counted as coverage.");
    }
}
