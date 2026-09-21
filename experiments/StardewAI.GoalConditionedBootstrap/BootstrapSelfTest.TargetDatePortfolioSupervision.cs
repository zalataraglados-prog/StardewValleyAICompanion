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
                    row.Payload.DecisionContext.SaveId == "fixture-save" &&
                    row.Payload.DecisionContext.PlayerId == "1" &&
                    row.Payload.DecisionContext.Year == 1 &&
                    row.Payload.DecisionContext.Season == "spring" &&
                    row.Payload.DecisionContext.Day == 1 &&
                    row.Payload.DecisionContext.SplitKey ==
                        "fixture-save:1:spring:1" &&
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

        var source = new
            AcquisitionRoutePortfolioSupervisionCorpusSource
            {
                DatasetPath = supervisionPath,
                ProofManifestPath = manifestPath,
                ProofReceiptPath = proofReceiptPath,
                RolloutAdmissionReceiptPath = admissionPath
            };
        var corpusRequestPath = Path.Combine(
            outputRoot,
            "portfolio-supervision-corpus-request.json");
        Write(corpusRequestPath, new
            AcquisitionRoutePortfolioSupervisionCorpusRequest
            {
                CorpusId = "self-test-acquisition-portfolio-corpus",
                Sources = new[] { source, source }
            });
        var corpus = AcquisitionRoutePortfolioSupervisionCorpusBuilder.Build(
            corpusRequestPath,
            Path.Combine(outputRoot, "portfolio-supervision-corpus"));
        var corpusManifest = corpus.Manifest;
        Require(corpusManifest.Status ==
                    "verified_teacher_corpus_trainer_blocked" &&
                corpusManifest.Counts.InputSourceEntries == 2 &&
                corpusManifest.Counts.UniqueSourceDatasets == 1 &&
                corpusManifest.Counts.InputRows == 6 &&
                corpusManifest.Counts.AcceptedRows == 3 &&
                corpusManifest.Counts.ExactDuplicateRows == 3 &&
                corpusManifest.Counts.TeacherPairwisePreferences == 0 &&
                corpusManifest.Counts.StudentObservationCount == 0 &&
                corpusManifest.Partitions.Sum(row => row.Rows) == 3 &&
                corpusManifest.Partitions.Count(row => row.Rows == 3) == 1 &&
                corpusManifest.Partitions.Single(row => row.Rows == 3)
                    .SplitKeyCount == 1 &&
                corpusManifest.GameVersions.SequenceEqual(
                    new[] { "1.6.15" },
                    StringComparer.Ordinal) &&
                corpusManifest.BridgeVersions.Length == 1 &&
                corpusManifest.TeacherTrainingEvidenceEligible &&
                !corpusManifest.GoalMethodTrainerInputReady &&
                !corpusManifest.FormalProductTrainingAuthorized &&
                corpusManifest.BlockingReasons.Contains(
                    "teacher_pairwise_comparison_empty",
                    StringComparer.Ordinal) &&
                File.ReadLines(corpusManifest.Cleaned.Path).Count() == 3,
            "Portfolio supervision corpus governance drifted.");

        var tamperedCorpusRequestPath = Path.Combine(
            outputRoot,
            "tampered-portfolio-supervision-corpus-request.json");
        source.DatasetPath = tamperedSupervisionPath;
        Write(tamperedCorpusRequestPath, new
            AcquisitionRoutePortfolioSupervisionCorpusRequest
            {
                CorpusId = "self-test-tampered-acquisition-corpus",
                Sources = new[] { source }
            });
        var tamperedCorpusRejected = false;
        try
        {
            AcquisitionRoutePortfolioSupervisionCorpusBuilder.Build(
                tamperedCorpusRequestPath,
                Path.Combine(outputRoot, "tampered-supervision-corpus"));
        }
        catch (InvalidDataException)
        {
            tamperedCorpusRejected = true;
        }
        Require(tamperedCorpusRejected,
            "Tampered supervision source entered the corpus.");
    }
}
