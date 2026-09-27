namespace StardewAI.GoalConditionedBootstrap;

public static class
    AcquisitionRouteSupportingTransitionTerminalCoverageBuilder
{
    public static AcquisitionRouteSupportingTransitionTerminalCoverageReport
        Build(string requestPath)
    {
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionTerminalCoverageRequest>(
            Path.GetFullPath(requestPath),
            "Support terminal coverage request");
        Require(request.SchemaVersion ==
                "acquisition_route_supporting_transition_terminal_coverage_request.v1",
            "Support terminal coverage request schema is invalid.");
        return Build(request.Sources);
    }

    public static AcquisitionRouteSupportingTransitionTerminalCoverageReport
        Build(IEnumerable<
            AcquisitionRouteSupportingTransitionTerminalCoverageSource>
            sources)
    {
        var rows = sources.Select(BuildRow).ToArray();
        return BuildReport(rows);
    }

    internal static
        AcquisitionRouteSupportingTransitionTerminalCoverageReport
        BuildReport(IEnumerable<
            AcquisitionRouteSupportingTransitionTerminalCoverageRow> sourceRows)
    {
        var rows = sourceRows
            .OrderBy(row => Array.IndexOf(
                AcquisitionRouteSupportingTransitionKinds.All,
                row.SupportTransitionKind))
            .ThenBy(row => row.SupportRequestId, StringComparer.Ordinal)
            .ToArray();
        var duplicateKinds = rows
            .GroupBy(row => row.SupportTransitionKind, StringComparer.Ordinal)
            .Where(group => group.Count() != 1)
            .Select(group => group.Key)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var covered = rows
            .Where(row => row.CoverageVerified)
            .Select(row => row.SupportTransitionKind)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => Array.IndexOf(
                AcquisitionRouteSupportingTransitionKinds.All,
                value))
            .ToArray();
        var missing = AcquisitionRouteSupportingTransitionKinds.All
            .Except(covered, StringComparer.Ordinal)
            .ToArray();
        var reasons = missing
            .Select(kind => "support_terminal_lineage_missing:" + kind)
            .Concat(duplicateKinds.Select(kind =>
                "support_terminal_lineage_duplicate:" + kind))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var complete = rows.Length ==
                AcquisitionRouteSupportingTransitionKinds.All.Length &&
            missing.Length == 0 &&
            duplicateKinds.Length == 0 &&
            rows.All(row => row.CoverageVerified);

        return new AcquisitionRouteSupportingTransitionTerminalCoverageReport
        {
            Status = complete
                ? "verified_complete_support_terminal_lineage_coverage"
                : "blocked_incomplete_support_terminal_lineage_coverage",
            RequiredSupportTransitionKinds =
                AcquisitionRouteSupportingTransitionKinds.All.ToArray(),
            CoveredSupportTransitionKinds = covered,
            MissingSupportTransitionKinds = missing,
            Rows = rows,
            TerminalLineageCoverageComplete = complete,
            FormalProductTrainingAuthorized = false,
            BlockingReasons = reasons
        };
    }

    private static AcquisitionRouteSupportingTransitionTerminalCoverageRow
        BuildRow(
            AcquisitionRouteSupportingTransitionTerminalCoverageSource source)
    {
        var manifestPath = Path.GetFullPath(
            source.RolloutProofManifestPath);
        var proofReceiptPath = Path.GetFullPath(
            source.RolloutProofReceiptPath);
        var admissionPath = Path.GetFullPath(
            source.RolloutAdmissionReceiptPath);
        var datasetPath = Path.GetFullPath(source.SupervisionDatasetPath);
        var manifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            manifestPath,
            "Support terminal coverage rollout manifest");
        var support = manifest.InitialCheckpointProof.SupportingTransition ??
            throw new InvalidDataException(
                "Support terminal coverage row has no supporting transition.");
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionRequest>(
            Path.GetFullPath(
                support.SettlementProof.SupportRequestPath),
            "Support terminal coverage request");
        Require(AcquisitionRouteSupportingTransitionKinds.IsKnown(
                request.SupportTransitionKind),
            "Support terminal coverage row has an unknown transition kind.");

        var expectedProof = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(manifestPath);
        var actualProof = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            proofReceiptPath,
            "Support terminal coverage proof receipt");
        Require(EqualJson(actualProof, expectedProof) &&
                expectedProof.ProofChainVerified &&
                expectedProof.PortfolioCompletionVerified,
            "Support terminal coverage proof receipt did not recompute.");
        var expectedAdmission =
            AcquisitionRoutePortfolioRolloutAdmissionBuilder.Build(
                manifestPath,
                proofReceiptPath);
        var actualAdmission = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutAdmissionReceipt>(
            admissionPath,
            "Support terminal coverage admission receipt");
        Require(EqualJson(actualAdmission, expectedAdmission) &&
                expectedAdmission.ControllerAdmissionGranted &&
                expectedAdmission.TeacherTrainingEvidenceEligible &&
                !expectedAdmission.FormalProductTrainingAuthorized,
            "Support terminal coverage admission did not recompute.");
        var dataset = AcquisitionRoutePortfolioSupervisionBuilder.Verify(
            datasetPath,
            manifestPath,
            proofReceiptPath,
            admissionPath);
        var terminalRoutes = dataset.Rows
            .Select(row => row.Payload.NativeOutcome.RouteOccurrenceId)
            .ToArray();
        var terminalRunIds = dataset.Rows
            .Select(row => row.Payload.NativeOutcome.RunId)
            .ToArray();
        var supportExcluded = terminalRunIds.Length > 0 &&
            !string.IsNullOrWhiteSpace(support.SettlementProof.RunId) &&
            !terminalRunIds.Contains(
                support.SettlementProof.RunId,
                StringComparer.Ordinal);
        Require(dataset.TeacherTrainingEvidenceEligible &&
                !dataset.FormalProductTrainingAuthorized &&
                dataset.TransitionCount == expectedProof.TransitionCount &&
                dataset.NativeOutcomeCount == expectedProof.TransitionCount &&
                supportExcluded,
            "Support transition leaked into terminal supervision outcomes.");

        return new AcquisitionRouteSupportingTransitionTerminalCoverageRow
        {
            SupportTransitionKind = request.SupportTransitionKind,
            SupportRequestId = request.SupportRequestId,
            SupportRouteOccurrenceId = request.RouteOccurrenceId,
            SupportRunId = support.SettlementProof.RunId,
            TerminalRouteOccurrenceIds = terminalRoutes,
            TerminalRunIds = terminalRunIds,
            RolloutId = expectedProof.RolloutId,
            SupportChainRecomputed = true,
            TerminalRolloutRecomputed = true,
            SupervisionRecomputed = true,
            SupportExcludedFromTerminalOutcomes = true,
            CoverageVerified = true
        };
    }
}
