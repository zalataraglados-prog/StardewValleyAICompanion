namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyTargetDatePortfolioSupervision(
        string manifestPath,
        string proofReceiptPath,
        string admissionPath,
        string outputRoot)
    {
        var supervision = AcquisitionRoutePortfolioSupervisionBuilder.Build(
            manifestPath,
            proofReceiptPath,
            admissionPath);
        var supervisionPath = Path.Combine(
            outputRoot,
            "portfolio-supervision-dataset.json");
        Write(supervisionPath, supervision);
        var verified = AcquisitionRoutePortfolioSupervisionBuilder.Verify(
            supervisionPath,
            manifestPath,
            proofReceiptPath,
            admissionPath);
        Require(verified.Status ==
                    "ready_verified_portfolio_supervision_dataset" &&
                verified.TransitionCount == 3 &&
                verified.Rows.Length == 3 &&
                verified.TeacherPreferenceCount == 3 &&
                verified.NativeOutcomeCount == 3 &&
                verified.StudentObservationCount == 0 &&
                verified.TeacherTrainingEvidenceEligible &&
                !verified.FormalProductTrainingAuthorized &&
                verified.Rows.Select(row => row.TransitionIndex)
                    .SequenceEqual(new[] { 1, 2, 3 }) &&
                verified.Rows.All(row =>
                    row.Payload.TeacherPreference.SourceKind ==
                        AcquisitionRoutePortfolioSupervisionSourceKinds
                            .TeacherPreference &&
                    row.Payload.NativeOutcome.SourceKind ==
                        AcquisitionRoutePortfolioSupervisionSourceKinds
                            .NativeOutcome &&
                    row.Payload.StudentObservation.SourceKind ==
                        AcquisitionRoutePortfolioSupervisionSourceKinds
                            .StudentObservation &&
                    !row.Payload.StudentObservation.Observed &&
                    !row.Payload.StudentObservation
                        .PositivePreferenceLabelEmitted &&
                    row.Payload.TeacherPreference
                        .UnavailableCandidateLabelSemantics ==
                        "defer_without_negative_label") &&
                verified.Rows[0].PriorRowSha256.Length == 0 &&
                verified.Rows.Skip(1).Select((row, index) =>
                    row.PriorRowSha256 ==
                        verified.Rows[index].RowSha256).All(value => value) &&
                verified.RowChainTipSha256 == verified.Rows[^1].RowSha256,
            "Typed portfolio supervision dataset drifted.");

        var admission = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutAdmissionReceipt>(
            admissionPath,
            "Portfolio supervision rollout admission receipt");
        var forgedAdmissionPath = Path.Combine(
            outputRoot,
            "forged-rollout-admission-receipt.json");
        admission.TransitionCount++;
        Write(forgedAdmissionPath, admission);
        var forgedAdmissionRejected = false;
        try
        {
            AcquisitionRoutePortfolioSupervisionBuilder.Build(
                manifestPath,
                proofReceiptPath,
                forgedAdmissionPath);
        }
        catch (InvalidDataException)
        {
            forgedAdmissionRejected = true;
        }
        Require(forgedAdmissionRejected,
            "Caller-authored rollout admission emitted supervision rows.");

        var tamperedSupervisionPath = Path.Combine(
            outputRoot,
            "tampered-portfolio-supervision-dataset.json");
        supervision.Rows[0].TransitionIndex++;
        Write(tamperedSupervisionPath, supervision);
        var tamperedSupervisionRejected = false;
        try
        {
            AcquisitionRoutePortfolioSupervisionBuilder.Verify(
                tamperedSupervisionPath,
                manifestPath,
                proofReceiptPath,
                admissionPath);
        }
        catch (InvalidDataException)
        {
            tamperedSupervisionRejected = true;
        }
        Require(tamperedSupervisionRejected,
            "Tampered portfolio supervision dataset unexpectedly verified.");
    }
}
