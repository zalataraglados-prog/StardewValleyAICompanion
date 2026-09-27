namespace StardewAI.GoalConditionedBootstrap;

public static class GoalMethodFormalTrainingAdmissionBuilder
{
    private static readonly string[] IntentionalSafetyLocks =
    {
        "teacher_coverage_cannot_self_authorize",
        "coverage_reconciliation_cannot_self_authorize",
        "corpus_manifest_cannot_self_authorize",
        "checkpoint_cannot_self_authorize",
        "support_terminal_matrix_cannot_self_authorize",
        "admission_reconciliation_is_read_only"
    };

    public static GoalMethodFormalTrainingAdmissionReport Build(
        GoalMethodFrontierBuildInputs inputs,
        string coverageRequestPath,
        string corpusManifestPath,
        string checkpointPath,
        string supportTerminalCoverageRequestPath)
    {
        var fullCoverageRequestPath = RequiredFullPath(
            coverageRequestPath,
            "Teacher coverage request");
        var fullCorpusManifestPath = RequiredFullPath(
            corpusManifestPath,
            "Goal-method corpus manifest");
        var fullCheckpointPath = RequiredFullPath(
            checkpointPath,
            "Goal-method checkpoint");
        var fullSupportRequestPath = RequiredFullPath(
            supportTerminalCoverageRequestPath,
            "Support terminal coverage request");

        var reconciliation = GoalMethodCoverageReconciliationBuilder.Build(
            inputs,
            fullCoverageRequestPath);
        var corpus = GoalMethodPairwiseTrainer.VerifyCorpus(
            fullCorpusManifestPath);
        var checkpoint = new GoalMethodPairwiseCheckpointStore().Load(
            fullCheckpointPath);
        GoalMethodPairwiseRanker.ValidateDatasetBinding(
            checkpoint,
            corpus,
            fullCorpusManifestPath);
        var evaluation = GoalMethodPairwiseTrainer
            .ValidateCheckpointEvaluation(checkpoint, corpus);
        var support =
            AcquisitionRouteSupportingTransitionTerminalCoverageBuilder
                .Build(fullSupportRequestPath);

        ValidateCoverageCorpusBinding(
            fullCoverageRequestPath,
            fullCorpusManifestPath);
        ValidateLeafAuthorityIsolation(
            reconciliation,
            corpus.Manifest,
            checkpoint,
            support);

        var evidence = new VerifiedEvidence(
            Bind("teacher_coverage_request", fullCoverageRequestPath),
            Bind("acquisition_corpus_manifest", fullCorpusManifestPath),
            Bind("goal_method_checkpoint", fullCheckpointPath),
            Bind("support_terminal_coverage_request", fullSupportRequestPath),
            corpus.Manifest.Sources.Length,
            corpus.Manifest.Counts.AcceptedRows,
            evaluation,
            checkpoint.CheckpointId,
            CurrentTeacherFrontierSupport.HashFile(fullCheckpointPath),
            support,
            true);
        return BuildReport(reconciliation, evidence);
    }

    internal static GoalMethodFormalTrainingAdmissionReport BuildReport(
        GoalMethodCoverageReconciliationReport reconciliation,
        VerifiedEvidence evidence)
    {
        ValidateReconciliation(reconciliation);
        var currentBlockers = CurrentTeacherBlockers(reconciliation);
        var downstreamBlockers = DownstreamPromotionBlockers(
            reconciliation);
        var teacherCoverageReady =
            reconciliation.CoverageGateReadyCriterionCount ==
                reconciliation.CriterionDenominatorCount &&
            reconciliation.Criteria.All(criterion =>
                criterion.CoverageGateReady);
        var supportReady = evidence.Support
            .TerminalLineageCoverageComplete;
        var teacherEvidenceReady = teacherCoverageReady && supportReady;
        var runtimePromotionReady = downstreamBlockers.Length == 0;

        var gates = new[]
        {
            Gate(
                GoalMethodFormalTrainingGateIds
                    .AuthoritativeGoalDenominator,
                GoalMethodFormalTrainingGateCategories.EvidenceIntegrity,
                reconciliation.CriterionDenominatorCount == 19 &&
                    reconciliation.TargetScore == 21,
                "criterion_denominator=" +
                    reconciliation.CriterionDenominatorCount,
                "target_score=" + reconciliation.TargetScore),
            Gate(
                GoalMethodFormalTrainingGateIds.AcquisitionCorpusReplay,
                GoalMethodFormalTrainingGateCategories.EvidenceIntegrity,
                evidence.CorpusSourceCount > 0 &&
                    evidence.CorpusRowCount > 0,
                "source_count=" + evidence.CorpusSourceCount,
                "row_count=" + evidence.CorpusRowCount),
            Gate(
                GoalMethodFormalTrainingGateIds.DatasetSplitIntegrity,
                GoalMethodFormalTrainingGateCategories.EvidenceIntegrity,
                EvaluationHasAllPartitions(evidence.Evaluation),
                "train_rows=" + evidence.Evaluation.TrainRows,
                "validation_rows=" + evidence.Evaluation.ValidationRows,
                "test_rows=" + evidence.Evaluation.TestRows),
            Gate(
                GoalMethodFormalTrainingGateIds.CheckpointCorpusBinding,
                GoalMethodFormalTrainingGateCategories.EvidenceIntegrity,
                !string.IsNullOrWhiteSpace(evidence.CheckpointId) &&
                    IsSha256(evidence.CheckpointSha256),
                "checkpoint_id=" + evidence.CheckpointId,
                "checkpoint_sha256=" + evidence.CheckpointSha256),
            Gate(
                GoalMethodFormalTrainingGateIds
                    .HoldoutEvaluationRecomputed,
                GoalMethodFormalTrainingGateCategories.EvidenceIntegrity,
                evidence.EvaluationRecomputed &&
                    evidence.Evaluation.TestPairs > 0,
                "test_pairs=" + evidence.Evaluation.TestPairs,
                "test_pair_accuracy=" +
                    evidence.Evaluation.TestPairAccuracy),
            Gate(
                GoalMethodFormalTrainingGateIds.SupportTerminalLineage,
                GoalMethodFormalTrainingGateCategories.TeacherCoverage,
                supportReady,
                "covered=" + evidence.Support
                    .CoveredSupportTransitionKinds.Length + "/" +
                    evidence.Support.RequiredSupportTransitionKinds.Length),
            Gate(
                GoalMethodFormalTrainingGateIds.CompleteTeacherCoverage,
                GoalMethodFormalTrainingGateCategories.TeacherCoverage,
                teacherCoverageReady,
                "coverage_ready=" +
                    reconciliation.CoverageGateReadyCriterionCount + "/" +
                    reconciliation.CriterionDenominatorCount),
            Gate(
                GoalMethodFormalTrainingGateIds
                    .ReferencedOptionExecutionInventory,
                GoalMethodFormalTrainingGateCategories.RuntimePromotion,
                runtimePromotionReady,
                downstreamBlockers),
            Gate(
                GoalMethodFormalTrainingGateIds.LeafAuthorityIsolation,
                GoalMethodFormalTrainingGateCategories.SafetyIsolation,
                true,
                IntentionalSafetyLocks)
        };
        var readyForReview = gates
            .Where(gate => gate.RequiredForPromotionReview)
            .All(gate => gate.Satisfied);

        return new GoalMethodFormalTrainingAdmissionReport
        {
            Status = readyForReview
                ? "ready_for_separate_formal_training_promotion_review"
                : "blocked_open_formal_training_evidence",
            GoalId = reconciliation.GoalId,
            TargetScore = reconciliation.TargetScore,
            CriterionDenominatorCount =
                reconciliation.CriterionDenominatorCount,
            CoverageReadyCriterionCount =
                reconciliation.CoverageGateReadyCriterionCount,
            RootMethodCount = reconciliation.RootMethodCount,
            CoverageReadyMethodCount = reconciliation.Methods.Count(method =>
                method.PrimaryDisposition ==
                    GoalMethodCoverageDispositions.CoverageReady),
            CorpusSourceCount = evidence.CorpusSourceCount,
            CorpusRowCount = evidence.CorpusRowCount,
            TrainRows = evidence.Evaluation.TrainRows,
            TrainPairs = evidence.Evaluation.TrainPairs,
            ValidationRows = evidence.Evaluation.ValidationRows,
            ValidationPairs = evidence.Evaluation.ValidationPairs,
            ValidationPairAccuracy =
                evidence.Evaluation.ValidationPairAccuracy,
            TestRows = evidence.Evaluation.TestRows,
            TestPairs = evidence.Evaluation.TestPairs,
            TestPairAccuracy = evidence.Evaluation.TestPairAccuracy,
            CheckpointId = evidence.CheckpointId,
            CheckpointSha256 = evidence.CheckpointSha256,
            RequiredSupportTransitionCount = evidence.Support
                .RequiredSupportTransitionKinds.Length,
            CoveredSupportTransitionCount = evidence.Support
                .CoveredSupportTransitionKinds.Length,
            TeacherTrainingEvidenceReady = teacherEvidenceReady,
            RuntimeProductPromotionReady = runtimePromotionReady,
            ReadyForSeparatePromotionReview = readyForReview,
            FormalProductTrainingAuthorized = false,
            ArtifactBindings = evidence.ArtifactBindings,
            Gates = gates,
            IntentionalSafetyLocks = IntentionalSafetyLocks.ToArray(),
            CurrentTeacherBlockingReasons = currentBlockers,
            DownstreamPromotionBlockingReasons = downstreamBlockers,
            NextActions = reconciliation.Methods
                .SelectMany(method => method.NextActions)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static void ValidateCoverageCorpusBinding(
        string coverageRequestPath,
        string corpusManifestPath)
    {
        var request = CurrentTeacherFrontierSupport.Read<
            GoalMethodTeacherCoverageRequest>(
            coverageRequestPath,
            "Formal admission Teacher coverage request");
        var matches = request.Sources.Where(source =>
                source.SourceKind == GoalMethodTeacherCoverageSourceKinds
                    .AcquisitionRoutePortfolioCorpus &&
                string.Equals(
                    Path.GetFullPath(source.ArtifactPath),
                    corpusManifestPath,
                    StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                "Formal admission corpus is not the unique acquisition source in the Teacher coverage request.");
        }
    }

    private static void ValidateLeafAuthorityIsolation(
        GoalMethodCoverageReconciliationReport reconciliation,
        AcquisitionRoutePortfolioSupervisionCorpusManifest corpus,
        GoalMethodPairwiseCheckpoint checkpoint,
        AcquisitionRouteSupportingTransitionTerminalCoverageReport support)
    {
        if (reconciliation.FormalProductTrainingAuthorized ||
            corpus.FormalProductTrainingAuthorized ||
            checkpoint.FormalProductTrainingAuthorized ||
            support.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "A leaf artifact attempted to self-authorize formal product training.");
        }
    }

    private static void ValidateReconciliation(
        GoalMethodCoverageReconciliationReport report)
    {
        if (report.SchemaVersion != "goal_method_coverage_reconciliation.v2" ||
            report.GoalId != GoalMethodTeacherCoverageGoalIds.Authoritative ||
            report.TargetScore != 21 ||
            report.CriterionDenominatorCount != 19 ||
            report.Criteria.Length != report.CriterionDenominatorCount ||
            report.Methods.Length != report.RootMethodCount ||
            report.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Goal-method coverage reconciliation identity is invalid.");
        }
    }

    private static string[] CurrentTeacherBlockers(
        GoalMethodCoverageReconciliationReport report) => report
        .CriterionDispositionCounts
        .Where(row => row.Disposition !=
            GoalMethodCoverageDispositions.CoverageReady && row.Count > 0)
        .Select(row => row.Disposition + ":" + row.Count)
        .Order(StringComparer.Ordinal)
        .ToArray();

    private static string[] DownstreamPromotionBlockers(
        GoalMethodCoverageReconciliationReport report)
    {
        var options = report.Methods
            .SelectMany(method => method.ReferencedOptions)
            .GroupBy(option => option.OptionId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return Gap("transparent_read_gap", options.Where(option =>
                !option.TransparentReadGateReady))
            .Concat(Gap("native_runtime_gap", options.Where(option =>
                option.RuntimeStatus is not
                    ("RuntimeVerified" or "LongDurationVerified"))))
            .Concat(Gap("five_gate_gap", options.Where(option =>
                !option.FiveGateTrainingReady)))
            .Concat(Gap("internal_execution_pipeline_gap", options.Where(
                option => !option.InternalExecutionPipelineSupported)))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static IEnumerable<string> Gap(
        string prefix,
        IEnumerable<GoalMethodCoverageOptionReconciliation> options)
    {
        var ids = options.Select(option => option.OptionId)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        return ids.Length == 0
            ? Array.Empty<string>()
            : new[] { prefix + ":" + string.Join(",", ids) };
    }

    private static GoalMethodFormalTrainingGateResult Gate(
        string gateId,
        string category,
        bool satisfied,
        params string[] details) => new(
        gateId,
        category,
        satisfied,
            true,
            details);

    private static GoalMethodFormalTrainingArtifactBinding Bind(
        string kind,
        string path) => new(
            kind,
            path,
            CurrentTeacherFrontierSupport.HashFile(path));

    private static bool EvaluationHasAllPartitions(
        GoalMethodPairwiseTrainingSummary value) =>
        value.TrainRows > 0 && value.TrainPairs > 0 &&
        value.ValidationRows > 0 && value.ValidationPairs > 0 &&
        value.TestRows > 0 && value.TestPairs > 0;

    private static string RequiredFullPath(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(label + " is required.")
            : Path.GetFullPath(value);

    private static bool IsSha256(string value) =>
        value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or
            >= 'a' and <= 'f' or >= 'A' and <= 'F');

    internal sealed record VerifiedEvidence(
        GoalMethodFormalTrainingArtifactBinding CoverageRequest,
        GoalMethodFormalTrainingArtifactBinding CorpusManifest,
        GoalMethodFormalTrainingArtifactBinding Checkpoint,
        GoalMethodFormalTrainingArtifactBinding SupportCoverageRequest,
        int CorpusSourceCount,
        int CorpusRowCount,
        GoalMethodPairwiseTrainingSummary Evaluation,
        string CheckpointId,
        string CheckpointSha256,
        AcquisitionRouteSupportingTransitionTerminalCoverageReport Support,
        bool EvaluationRecomputed)
    {
        public GoalMethodFormalTrainingArtifactBinding[] ArtifactBindings =>
            new[]
            {
                CoverageRequest,
                CorpusManifest,
                Checkpoint,
                SupportCoverageRequest
            };
    }
}
