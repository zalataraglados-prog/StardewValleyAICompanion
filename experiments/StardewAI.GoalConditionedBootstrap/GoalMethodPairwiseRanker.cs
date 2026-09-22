namespace StardewAI.GoalConditionedBootstrap;

public sealed class GoalMethodPairwiseRanker
{
    private readonly GoalMethodPairwiseCheckpointStore checkpointStore;

    public GoalMethodPairwiseRanker()
        : this(new GoalMethodPairwiseCheckpointStore())
    {
    }

    public GoalMethodPairwiseRanker(
        GoalMethodPairwiseCheckpointStore checkpointStore)
    {
        this.checkpointStore = checkpointStore;
    }

    public GoalMethodPairwiseScoringResult RankVerifiedCorpusRow(
        string checkpointPath,
        string corpusManifestPath,
        string rowId)
    {
        var fullCheckpointPath = RequiredFullPath(
            checkpointPath,
            "Checkpoint path");
        var fullManifestPath = RequiredFullPath(
            corpusManifestPath,
            "Corpus manifest path");
        if (string.IsNullOrWhiteSpace(rowId))
            throw new ArgumentException("Supervision row ID is required.");
        var checkpoint = checkpointStore.Load(fullCheckpointPath);
        var corpus = GoalMethodPairwiseTrainer.VerifyCorpus(fullManifestPath);
        ValidateDatasetBinding(checkpoint, corpus, fullManifestPath);
        var matches = corpus.TrainRows
            .Concat(corpus.ValidationRows)
            .Concat(corpus.TestRows)
            .Where(row => row.SupervisionRow.RowId == rowId)
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                "Goal-method scoring requires exactly one verified corpus row.");
        }

        var row = matches[0];
        var payload = row.SupervisionRow.Payload;
        var teacher = payload.TeacherPreference;
        var candidates = teacher.CandidateEvaluations.Where(candidate =>
                candidate.AdmissionReady &&
                candidate.AggregateCostVector is not null)
            .ToArray();
        if (!teacher.CandidateDenominatorComplete ||
            candidates.Length != teacher.AdmittedCandidateCount ||
            candidates.Length == 0)
        {
            throw new InvalidDataException(
                "Goal-method scoring candidate denominator is incomplete.");
        }

        var scored = candidates.Select(candidate =>
            {
                var encoded = GoalMethodPairwiseFeatureEncoder.Encode(
                    row,
                    candidate,
                    checkpoint.Model);
                var score = Dot(checkpoint.Model.Weights, encoded);
                if (!Finite(score))
                {
                    throw new InvalidDataException(
                        "Goal-method checkpoint produced a non-finite score.");
                }
                return new GoalMethodPairwiseCandidateScore
                {
                    ProposalId = candidate.ProposalId,
                    ProposalSha256 = candidate.ProposalSha256,
                    SelectedRouteOccurrenceIds = candidate
                        .SelectedRouteOccurrenceIds.ToArray(),
                    ModelScore = Math.Round(score, 8),
                    TeacherSelected = candidate.ProposalId ==
                        teacher.SelectedProposalId,
                    OnDeterministicParetoFrontier =
                        candidate.DominatedByProposalIds.Length == 0
                };
            })
            .OrderByDescending(candidate => candidate.ModelScore)
            .ThenBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < scored.Length; index++)
            scored[index].Rank = index + 1;
        var teacherSelected = scored.Single(candidate =>
            candidate.TeacherSelected);

        return new GoalMethodPairwiseScoringResult
        {
            CheckpointId = checkpoint.CheckpointId,
            CheckpointSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullCheckpointPath),
            CorpusManifestSha256 = checkpoint.Dataset.CorpusManifestSha256,
            SupervisionRowId = row.SupervisionRow.RowId,
            SupervisionRowSha256 = row.SupervisionRow.RowSha256,
            DatasetPartition = row.DatasetPartition,
            GoalId = row.GoalId,
            SnapshotStateHash = payload.DecisionStateHash,
            StrategyLedgerRevision = payload.DecisionLedgerRevision,
            TransitionIndex = row.SupervisionRow.TransitionIndex,
            CandidateDenominatorVerified = true,
            CandidateScores = scored,
            ModelTopProposalId = scored[0].ProposalId,
            TeacherSelectedProposalId = teacherSelected.ProposalId,
            ModelAgreesWithTeacher = scored[0].ProposalId ==
                teacherSelected.ProposalId,
            PortfolioCommitAuthorized = false,
            FormalProductTrainingAuthorized = false
        };
    }

    private static void ValidateDatasetBinding(
        GoalMethodPairwiseCheckpoint checkpoint,
        GoalMethodPairwiseTrainer.VerifiedCorpus corpus,
        string manifestPath)
    {
        var dataset = checkpoint.Dataset;
        if (CurrentTeacherFrontierSupport.HashFile(manifestPath) !=
                dataset.CorpusManifestSha256 ||
            corpus.Manifest.Cleaned.Sha256 != dataset.CleanedSha256 ||
            corpus.TrainDigest.Sha256 != dataset.TrainSha256 ||
            corpus.ValidationDigest.Sha256 != dataset.ValidationSha256 ||
            corpus.TestDigest.Sha256 != dataset.TestSha256 ||
            corpus.Manifest.Sources.Length != dataset.SourceCount ||
            corpus.Versions.FeatureSchema !=
                checkpoint.Versions.FeatureSchema ||
            corpus.Versions.CorpusSchema != checkpoint.Versions.CorpusSchema ||
            corpus.Versions.SupervisionRowSchema !=
                checkpoint.Versions.SupervisionRowSchema ||
            corpus.Versions.GameVersion != checkpoint.Versions.GameVersion ||
            corpus.Versions.BridgeVersion !=
                checkpoint.Versions.BridgeVersion)
        {
            throw new InvalidDataException(
                "Goal-method checkpoint does not bind the verified corpus.");
        }
    }

    private static double Dot(double[] left, double[] right)
    {
        var result = 0d;
        for (var index = 0; index < left.Length; index++)
            result += left[index] * right[index];
        return result;
    }

    private static bool Finite(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value);

    private static string RequiredFullPath(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(label + " is required.")
            : Path.GetFullPath(value);
}
