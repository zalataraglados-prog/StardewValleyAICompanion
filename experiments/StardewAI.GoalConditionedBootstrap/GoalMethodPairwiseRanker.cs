using System.Text.Json;

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

    public GoalMethodPairwiseLiveShadowSelection RankLiveShadow(
        string checkpointPath,
        string corpusManifestPath,
        AcquisitionRoutePortfolioInputs currentInputs,
        string preferenceRequestPath,
        string? priorRolloutProofManifestPath = null)
    {
        var fullCheckpointPath = RequiredFullPath(
            checkpointPath,
            "Checkpoint path");
        var fullManifestPath = RequiredFullPath(
            corpusManifestPath,
            "Corpus manifest path");
        var checkpoint = checkpointStore.Load(fullCheckpointPath);
        var corpus = GoalMethodPairwiseTrainer.VerifyCorpus(fullManifestPath);
        ValidateDatasetBinding(checkpoint, corpus, fullManifestPath);

        AcquisitionRoutePortfolioTeacherPreferenceBuilder
            .AcquisitionRoutePortfolioTeacherScoringSet set;
        int transitionIndex;
        if (string.IsNullOrWhiteSpace(priorRolloutProofManifestPath))
        {
            set = AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .BuildScoringSet(currentInputs, preferenceRequestPath);
            transitionIndex = 1;
        }
        else
        {
            var verified = AcquisitionRoutePortfolioRolloutProofBuilder
                .Verify(priorRolloutProofManifestPath);
            set = AcquisitionRoutePortfolioTeacherPreferenceBuilder
                .BuildContinuationScoringSet(
                    verified,
                    currentInputs,
                    preferenceRequestPath);
            transitionIndex = verified.Checkpoint.TransitionCount + 1;
        }

        var preference = set.Preference;
        var decision = AcquisitionRoutePortfolioSupervisionBuilder
            .DecisionContext(set.Snapshot);
        if (decision.GameVersion != checkpoint.Versions.GameVersion ||
            decision.BridgeVersion != checkpoint.Versions.BridgeVersion)
        {
            throw new InvalidDataException(
                "Goal-method live state version differs from the checkpoint.");
        }
        var result = new GoalMethodPairwiseLiveShadowSelection
        {
            CheckpointId = checkpoint.CheckpointId,
            CheckpointSha256 = CurrentTeacherFrontierSupport.HashFile(
                fullCheckpointPath),
            CorpusManifestSha256 = checkpoint.Dataset.CorpusManifestSha256,
            GoalId = preference.GoalId,
            SnapshotStateHash = preference.SnapshotStateHash,
            StrategyLedgerRevision = preference.ExpectedLedgerRevision,
            TransitionIndex = transitionIndex,
            TeacherPreferenceStatus = preference.Status,
            TeacherPreferenceSha256 =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .ArtifactSha256(preference),
            TeacherSelectedProposalId =
                preference.SelectedProposal?.ProposalId ?? string.Empty,
            PortfolioCommitAuthorized = false,
            FormalProductTrainingAuthorized = false
        };
        if (!preference.CandidateDenominatorComplete ||
            set.Candidates.Length == 0)
        {
            result.Status = "blocked_live_goal_method_shadow_selection";
            result.BlockingReasons = preference.BlockingReasons
                .Append("goal_method_live_candidate_denominator_unavailable")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return result;
        }

        var evaluations = preference.CandidateEvaluations.ToDictionary(
            candidate => candidate.ProposalId,
            StringComparer.Ordinal);
        if (evaluations.Count != set.Candidates.Length)
        {
            throw new InvalidDataException(
                "Goal-method live candidate denominator drifted.");
        }
        foreach (var candidate in set.Candidates)
        {
            if (!evaluations.TryGetValue(
                    candidate.Proposal.ProposalId,
                    out var evaluation) ||
                candidate.ProposalSha256 != evaluation.ProposalSha256 ||
                candidate.ProposalSha256 !=
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .ArtifactSha256(candidate.Proposal) ||
                !candidate.Proposal.SelectedRouteOccurrenceIds.SequenceEqual(
                    evaluation.SelectedRouteOccurrenceIds,
                    StringComparer.Ordinal) ||
                candidate.Admission.PortfolioAdmissionReady !=
                    evaluation.AdmissionReady ||
                !EqualJson(
                    candidate.Admission.AggregateCostVector,
                    evaluation.AggregateCostVector))
            {
                throw new InvalidDataException(
                    "Goal-method live candidate artifact drifted from its deterministic evaluation.");
            }
        }

        var admitted = preference.CandidateEvaluations.Where(candidate =>
                candidate.AdmissionReady &&
                candidate.AggregateCostVector is not null)
            .ToArray();
        if (admitted.Length == 0 ||
            admitted.Length != preference.AdmittedCandidateCount)
        {
            throw new InvalidDataException(
                "Goal-method live admitted candidate count drifted.");
        }
        var featureContext = new GoalMethodPairwiseFeatureContext(
            preference.GoalId,
            decision.Year,
            decision.Season,
            decision.Day,
            decision.Time,
            decision.TotalDay,
            transitionIndex,
            preference.ExpectedLedgerRevision,
            preference.CandidateDenominatorCount,
            preference.AdmittedCandidateCount,
            preference.ParetoFrontierCount,
            preference.SelectionPolicyId);
        var scored = admitted.Select(candidate =>
            {
                var encoded = GoalMethodPairwiseFeatureEncoder.Encode(
                    featureContext,
                    candidate,
                    checkpoint.Model);
                var score = Dot(checkpoint.Model.Weights, encoded);
                if (!Finite(score))
                    throw new InvalidDataException(
                        "Goal-method checkpoint produced a non-finite live score.");
                return new GoalMethodPairwiseCandidateScore
                {
                    ProposalId = candidate.ProposalId,
                    ProposalSha256 = candidate.ProposalSha256,
                    SelectedRouteOccurrenceIds = candidate
                        .SelectedRouteOccurrenceIds.ToArray(),
                    ModelScore = Math.Round(score, 8),
                    TeacherSelected = candidate.ProposalId ==
                        preference.SelectedProposal?.ProposalId,
                    OnDeterministicParetoFrontier =
                        candidate.DominatedByProposalIds.Length == 0
                };
            })
            .OrderByDescending(candidate => candidate.ModelScore)
            .ThenBy(candidate => candidate.ProposalId, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < scored.Length; index++)
            scored[index].Rank = index + 1;
        result.CandidateDenominatorVerified = true;
        result.CandidateScores = scored;
        result.ModelTopProposalId = scored[0].ProposalId;
        result.ModelAgreesWithTeacher = preference.SelectedProposal is null
            ? null
            : scored[0].ProposalId == preference.SelectedProposal.ProposalId;

        string selectedId;
        if (preference.TeacherPreferenceLabelEligible &&
            preference.SelectedProposal is not null &&
            preference.SelectedAdmission is not null)
        {
            selectedId = preference.SelectedProposal.ProposalId;
            result.Status =
                "ready_teacher_authoritative_live_shadow_scoring";
            result.SelectionAuthority =
                "deterministic_unique_strict_pareto_teacher";
        }
        else if (preference.BlockingReasons.Length == 1 &&
            preference.BlockingReasons[0].StartsWith(
                "portfolio_teacher_frontier_incomparable_or_equal:",
                StringComparison.Ordinal) &&
            preference.ParetoFrontierCount > 1)
        {
            var frontier = scored.Where(candidate =>
                    candidate.OnDeterministicParetoFrontier)
                .ToArray();
            if (frontier.Length != preference.ParetoFrontierCount)
            {
                throw new InvalidDataException(
                    "Goal-method live Pareto frontier count drifted.");
            }
            selectedId = frontier[0].ProposalId;
            result.Status =
                "ready_checkpoint_ranked_incomparable_frontier_shadow";
            result.SelectionAuthority =
                "goal_method_checkpoint_shadow_only";
        }
        else
        {
            result.Status = "blocked_live_goal_method_shadow_selection";
            result.BlockingReasons = preference.BlockingReasons
                .Append("goal_method_live_selection_not_permitted")
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray();
            return result;
        }

        var selected = set.Candidates.Single(candidate =>
            candidate.Proposal.ProposalId == selectedId);
        if (!selected.Admission.PortfolioAdmissionReady)
        {
            throw new InvalidDataException(
                "Goal-method live shadow selected a blocked candidate.");
        }
        result.ShadowSelectedProposal = selected.Proposal;
        result.ShadowSelectedAdmission = selected.Admission;
        result.BlockingReasons = Array.Empty<string>();
        return result;
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

    private static bool EqualJson<T>(T left, T right) => string.Equals(
        JsonSerializer.Serialize(left, JsonDefaults.Compact),
        JsonSerializer.Serialize(right, JsonDefaults.Compact),
        StringComparison.Ordinal);

    private static string RequiredFullPath(string value, string label) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException(label + " is required.")
            : Path.GetFullPath(value);
}
