namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static AcquisitionRoutePortfolioSupervisionCorpusSource
        VerifyTargetDatePortfolioSupervision(
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
        var decisionContext = verified.Rows[0].Payload.DecisionContext;
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
                    row.Payload.DecisionContext.SaveId ==
                        decisionContext.SaveId &&
                    row.Payload.DecisionContext.PlayerId ==
                        decisionContext.PlayerId &&
                    row.Payload.DecisionContext.Year ==
                        decisionContext.Year &&
                    row.Payload.DecisionContext.Season ==
                        decisionContext.Season &&
                    row.Payload.DecisionContext.Day ==
                        decisionContext.Day &&
                    row.Payload.DecisionContext.SplitKey ==
                        decisionContext.SaveId + ":" +
                        decisionContext.Year + ":" +
                        decisionContext.Season + ":" +
                        decisionContext.Day &&
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

        return new
            AcquisitionRoutePortfolioSupervisionCorpusSource
            {
                DatasetPath = supervisionPath,
                ProofManifestPath = manifestPath,
                ProofReceiptPath = proofReceiptPath,
                RolloutAdmissionReceiptPath = admissionPath
            };
    }

    private static void VerifyTargetDatePortfolioSupervisionCorpus(
        IReadOnlyList<AcquisitionRoutePortfolioSupervisionCorpusSource>
            sources,
        string outputRoot)
    {
        Require(sources.Count == 3,
            "Portfolio supervision corpus fixture requires three sources.");
        var sourcePartitions = sources.Select(source =>
            {
                var dataset = CurrentTeacherFrontierSupport.Read<
                    AcquisitionRoutePortfolioSupervisionDataset>(
                    source.DatasetPath,
                    "Portfolio supervision corpus source");
                Require(dataset.Rows.Length == 3 &&
                        dataset.Rows.Select(row => row.Payload.DecisionContext
                                .SplitKey)
                            .Distinct(StringComparer.Ordinal)
                            .Count() == 1,
                    "Portfolio supervision source crossed save-day boundaries.");
                return new
                {
                    Source = source,
                    Dataset = dataset,
                    Partition = StardewAI.Core.Training
                        .PolicyTrajectoryDatasetBuilder.PartitionFor(
                            dataset.Rows[0].Payload.DecisionContext.SplitKey)
                };
            })
            .ToArray();
        Require(sourcePartitions.Select(value => value.Partition)
                .Order(StringComparer.Ordinal)
                .SequenceEqual(
                    new[] { "test", "train", "validation" },
                    StringComparer.Ordinal) &&
                sourcePartitions.Select(value => value.Dataset.Rows[0]
                        .Payload.DecisionContext.SplitKey)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 3 &&
                sourcePartitions.Select(value => value.Dataset.RolloutId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 3,
            "Independent rollout fixtures did not cover train, validation, and test exactly once.");

        var validationSource = sourcePartitions.Single(value =>
            value.Partition == "validation").Source;
        var corpusRequestPath = Path.Combine(
            outputRoot,
            "blocked-portfolio-supervision-corpus-request.json");
        Write(corpusRequestPath, new
            AcquisitionRoutePortfolioSupervisionCorpusRequest
            {
                CorpusId = "self-test-blocked-acquisition-portfolio-corpus",
                Sources = new[] { validationSource, validationSource }
            });
        var corpus = AcquisitionRoutePortfolioSupervisionCorpusBuilder.Build(
            corpusRequestPath,
            Path.Combine(outputRoot, "blocked-portfolio-supervision-corpus"));
        var corpusManifest = corpus.Manifest;
        Require(corpusManifest.Status ==
                    "verified_teacher_corpus_trainer_blocked" &&
                corpusManifest.Counts.InputSourceEntries == 2 &&
                corpusManifest.Counts.UniqueSourceDatasets == 1 &&
                corpusManifest.Counts.InputRows == 6 &&
                corpusManifest.Counts.AcceptedRows == 3 &&
                corpusManifest.Counts.ExactDuplicateRows == 3 &&
                corpusManifest.Counts.TeacherPairwisePreferences == 2 &&
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
                    "train_pairwise_comparison_empty",
                    StringComparer.Ordinal) &&
                corpusManifest.BlockingReasons.Contains(
                    "test_pairwise_comparison_empty",
                    StringComparer.Ordinal) &&
                !corpusManifest.BlockingReasons.Contains(
                    "teacher_pairwise_comparison_empty",
                    StringComparer.Ordinal) &&
                File.ReadLines(corpusManifest.Cleaned.Path).Count() == 3,
            "Portfolio supervision corpus governance drifted.");

        var readyCorpusRequestPath = Path.Combine(
            outputRoot,
            "ready-portfolio-supervision-corpus-request.json");
        Write(readyCorpusRequestPath, new
            AcquisitionRoutePortfolioSupervisionCorpusRequest
            {
                CorpusId = "self-test-ready-acquisition-portfolio-corpus",
                Sources = sources.ToArray()
            });
        var readyCorpus =
            AcquisitionRoutePortfolioSupervisionCorpusBuilder.Build(
                readyCorpusRequestPath,
                Path.Combine(outputRoot, "ready-portfolio-supervision-corpus"));
        var readyManifest = readyCorpus.Manifest;
        Require(readyManifest.Status ==
                    "ready_goal_method_trainer_input" &&
                readyManifest.Counts.InputSourceEntries == 3 &&
                readyManifest.Counts.UniqueSourceDatasets == 3 &&
                readyManifest.Counts.InputRows == 9 &&
                readyManifest.Counts.AcceptedRows == 9 &&
                readyManifest.Counts.ExactDuplicateRows == 0 &&
                readyManifest.Counts.TeacherPairwisePreferences == 6 &&
                readyManifest.Counts.StudentObservationCount == 0 &&
                readyManifest.Partitions.Length == 3 &&
                readyManifest.Partitions.All(partition =>
                    partition.Rows == 3 &&
                    partition.SplitKeyCount == 1) &&
                readyManifest.GameVersions.SequenceEqual(
                    new[] { "1.6.15" },
                    StringComparer.Ordinal) &&
                readyManifest.BridgeVersions.Length == 1 &&
                readyManifest.TeacherTrainingEvidenceEligible &&
                readyManifest.GoalMethodTrainerInputReady &&
                !readyManifest.FormalProductTrainingAuthorized &&
                readyManifest.BlockingReasons.Length == 0 &&
                File.ReadLines(readyManifest.Cleaned.Path).Count() == 9,
            "Independent train/validation/test Teacher corpus did not cross the goal-method trainer-input gate.");
        VerifyGoalMethodPairwiseTrainer(
            readyCorpus.ManifestPath,
            corpus.ManifestPath,
            outputRoot);

        var tamperedCorpusRequestPath = Path.Combine(
            outputRoot,
            "tampered-portfolio-supervision-corpus-request.json");
        var tamperedSource = new
            AcquisitionRoutePortfolioSupervisionCorpusSource
            {
                DatasetPath = Path.Combine(
                    Path.GetDirectoryName(sources[0].DatasetPath)!,
                    "tampered-portfolio-supervision-dataset.json"),
                ProofManifestPath = sources[0].ProofManifestPath,
                ProofReceiptPath = sources[0].ProofReceiptPath,
                RolloutAdmissionReceiptPath =
                    sources[0].RolloutAdmissionReceiptPath
            };
        Write(tamperedCorpusRequestPath, new
            AcquisitionRoutePortfolioSupervisionCorpusRequest
            {
                CorpusId = "self-test-tampered-acquisition-corpus",
                Sources = new[] { tamperedSource }
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
