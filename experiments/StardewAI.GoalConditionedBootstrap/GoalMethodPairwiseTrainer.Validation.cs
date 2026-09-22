using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class GoalMethodPairwiseTrainer
{
    private static VerifiedCorpus VerifyCorpus(string manifestPath)
    {
        var manifest = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioSupervisionCorpusManifest>(
            manifestPath,
            "Goal-method corpus manifest");
        if (manifest.SchemaVersion !=
                GoalMethodPairwiseVersionPins.CorpusSchema ||
            manifest.Status != "ready_goal_method_trainer_input" ||
            !manifest.TeacherTrainingEvidenceEligible ||
            !manifest.GoalMethodTrainerInputReady ||
            manifest.FormalProductTrainingAuthorized ||
            manifest.BlockingReasons.Length != 0 ||
            manifest.Sources.Length == 0 ||
            manifest.GameVersions.Length != 1 ||
            manifest.BridgeVersions.Length != 1)
        {
            throw new InvalidDataException(
                "Corpus is not admitted goal-method trainer input.");
        }

        var verifiedSources = manifest.Sources.Select(VerifySource).ToArray();
        var expectedRows = RebuildCorpusRows(verifiedSources);
        var cleanedRows = ReadRows(manifest.Cleaned, expectedPartition: null);
        RequireRowsEqual(
            expectedRows,
            cleanedRows,
            "Cleaned goal-method corpus does not equal verified sources.");

        var trainDigest = Partition(
            manifest,
            PolicyDatasetPartitions.Train);
        var validationDigest = Partition(
            manifest,
            PolicyDatasetPartitions.Validation);
        var testDigest = Partition(
            manifest,
            PolicyDatasetPartitions.Test);
        var trainRows = ReadRows(
            trainDigest,
            PolicyDatasetPartitions.Train);
        var validationRows = ReadRows(
            validationDigest,
            PolicyDatasetPartitions.Validation);
        var testRows = ReadRows(
            testDigest,
            PolicyDatasetPartitions.Test);
        RequireRowsEqual(
            cleanedRows.Where(row => row.DatasetPartition ==
                    PolicyDatasetPartitions.Train)
                .ToArray(),
            trainRows,
            "Train partition differs from cleaned corpus.");
        RequireRowsEqual(
            cleanedRows.Where(row => row.DatasetPartition ==
                    PolicyDatasetPartitions.Validation)
                .ToArray(),
            validationRows,
            "Validation partition differs from cleaned corpus.");
        RequireRowsEqual(
            cleanedRows.Where(row => row.DatasetPartition ==
                    PolicyDatasetPartitions.Test)
                .ToArray(),
            testRows,
            "Test partition differs from cleaned corpus.");

        ValidateManifestCounts(manifest, verifiedSources, cleanedRows);
        foreach (var row in cleanedRows)
            ValidateTrainingRow(row, manifest);
        if (PairCount(trainRows) == 0 ||
            PairCount(validationRows) == 0 ||
            PairCount(testRows) == 0)
        {
            throw new InvalidDataException(
                "Every goal-method partition requires explicit Teacher pairs.");
        }

        return new VerifiedCorpus(
            manifest,
            trainDigest,
            validationDigest,
            testDigest,
            trainRows,
            validationRows,
            testRows,
            new GoalMethodPairwiseVersionBinding
            {
                GameVersion = manifest.GameVersions[0],
                BridgeVersion = manifest.BridgeVersions[0]
            });
    }

    private static VerifiedSource VerifySource(
        AcquisitionRoutePortfolioSupervisionCorpusSourceDigest digest)
    {
        foreach (var path in new[]
                 {
                     digest.DatasetPath,
                     digest.ProofManifestPath,
                     digest.ProofReceiptPath,
                     digest.RolloutAdmissionReceiptPath
                 })
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !File.Exists(Path.GetFullPath(path)))
            {
                throw new InvalidDataException(
                    "Goal-method corpus source artifact is missing.");
            }
        }
        var dataset = AcquisitionRoutePortfolioSupervisionBuilder.Verify(
            digest.DatasetPath,
            digest.ProofManifestPath,
            digest.ProofReceiptPath,
            digest.RolloutAdmissionReceiptPath);
        if (CurrentTeacherFrontierSupport.HashFile(digest.DatasetPath) !=
                digest.DatasetSha256 ||
            CurrentTeacherFrontierSupport.HashFile(digest.ProofManifestPath) !=
                digest.ProofManifestSha256 ||
            CurrentTeacherFrontierSupport.HashFile(digest.ProofReceiptPath) !=
                digest.ProofReceiptSha256 ||
            CurrentTeacherFrontierSupport.HashFile(
                digest.RolloutAdmissionReceiptPath) !=
                digest.RolloutAdmissionReceiptSha256 ||
            digest.RolloutId != dataset.RolloutId ||
            digest.GoalId != dataset.GoalId ||
            digest.RowCount != dataset.Rows.Length ||
            !dataset.TeacherTrainingEvidenceEligible ||
            dataset.FormalProductTrainingAuthorized)
        {
            throw new InvalidDataException(
                "Goal-method corpus source digest drifted.");
        }
        return new VerifiedSource(digest, dataset);
    }

    private static AcquisitionRoutePortfolioSupervisionCorpusRow[]
        RebuildCorpusRows(IReadOnlyList<VerifiedSource> sources)
    {
        var accepted = new Dictionary<
            string,
            AcquisitionRoutePortfolioSupervisionCorpusRow>(
            StringComparer.Ordinal);
        var canonical = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in sources
                     .OrderBy(value => value.Digest.DatasetSha256,
                         StringComparer.Ordinal)
                     .ThenBy(value => value.Digest.DatasetPath,
                         StringComparer.Ordinal))
        {
            foreach (var row in source.Dataset.Rows)
            {
                var corpusRow = new
                    AcquisitionRoutePortfolioSupervisionCorpusRow
                    {
                        SourceDatasetSha256 = source.Digest.DatasetSha256,
                        RolloutId = source.Dataset.RolloutId,
                        GoalId = source.Dataset.GoalId,
                        DatasetPartition =
                            PolicyTrajectoryDatasetBuilder.PartitionFor(
                                row.Payload.DecisionContext.SplitKey),
                        SupervisionRow = row
                    };
                var rowJson = JsonSerializer.Serialize(
                    row,
                    JsonDefaults.Compact);
                if (!accepted.TryAdd(row.RowId, corpusRow) &&
                    canonical[row.RowId] != rowJson)
                {
                    throw new InvalidDataException(
                        "Conflicting verified supervision row ID: " +
                        row.RowId);
                }
                canonical.TryAdd(row.RowId, rowJson);
            }
        }
        return accepted.Values
            .OrderBy(row => row.SupervisionRow.Payload.DecisionContext
                    .SplitKey,
                StringComparer.Ordinal)
            .ThenBy(row => row.SupervisionRow.RowId, StringComparer.Ordinal)
            .ToArray();
    }

    private static AcquisitionRoutePortfolioSupervisionCorpusRow[] ReadRows(
        PolicyDatasetFileDigest digest,
        string? expectedPartition)
    {
        VerifyDigest(digest);
        var rows = new List<AcquisitionRoutePortfolioSupervisionCorpusRow>();
        var lineNumber = 0;
        foreach (var line in File.ReadLines(Path.GetFullPath(digest.Path)))
        {
            lineNumber++;
            if (string.IsNullOrWhiteSpace(line))
                throw new InvalidDataException(
                    "Goal-method corpus contains a blank row at line " +
                    lineNumber + ".");
            try
            {
                var row = JsonSerializer.Deserialize<
                    AcquisitionRoutePortfolioSupervisionCorpusRow>(
                    line,
                    JsonDefaults.Compact) ??
                    throw new InvalidDataException(
                        "Goal-method corpus row is null at line " +
                        lineNumber + ".");
                if (expectedPartition is not null &&
                    row.DatasetPartition != expectedPartition)
                {
                    throw new InvalidDataException(
                        "Goal-method row crossed its declared partition.");
                }
                rows.Add(row);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    "Goal-method corpus JSON is invalid at line " +
                    lineNumber + ".",
                    exception);
            }
        }
        if (rows.Count != digest.Rows)
            throw new InvalidDataException(
                "Goal-method corpus row count differs from its digest.");
        return rows.ToArray();
    }

    private static void ValidateTrainingRow(
        AcquisitionRoutePortfolioSupervisionCorpusRow row,
        AcquisitionRoutePortfolioSupervisionCorpusManifest manifest)
    {
        var supervision = row.SupervisionRow;
        var payload = supervision.Payload;
        var context = payload.DecisionContext;
        var teacher = payload.TeacherPreference;
        if (supervision.SchemaVersion !=
                GoalMethodPairwiseVersionPins.SupervisionRowSchema ||
            !IsSha256(row.SourceDatasetSha256) ||
            string.IsNullOrWhiteSpace(row.RolloutId) ||
            string.IsNullOrWhiteSpace(row.GoalId) ||
            string.IsNullOrWhiteSpace(supervision.RowId) ||
            !IsSha256(supervision.RowSha256) ||
            context.GameVersion != manifest.GameVersions[0] ||
            context.BridgeVersion != manifest.BridgeVersions[0] ||
            row.DatasetPartition !=
                PolicyTrajectoryDatasetBuilder.PartitionFor(
                    context.SplitKey) ||
            teacher.SourceKind !=
                AcquisitionRoutePortfolioSupervisionSourceKinds
                    .TeacherPreference ||
            teacher.Status != "verified_independent_teacher_preference" ||
            !teacher.CandidateDenominatorComplete ||
            teacher.CandidateDenominatorCount !=
                teacher.CandidateEvaluations.Length ||
            teacher.AdmittedCandidateCount !=
                teacher.CandidateEvaluations.Count(candidate =>
                    candidate.AdmissionReady) ||
            teacher.SelectionPolicyId is not
                "complete_portfolio_denominator_unique_strict_pareto.v1" and not
                "verified_checkpoint_continuation_unique_strict_pareto.v1" ||
            teacher.UsesLearnerRankOrScore ||
            teacher.EmitsNegativeLabelsForUnavailablePortfolios ||
            teacher.UnavailableCandidateLabelSemantics !=
                "defer_without_negative_label" ||
            payload.NativeOutcome.SourceKind !=
                AcquisitionRoutePortfolioSupervisionSourceKinds.NativeOutcome ||
            !payload.NativeOutcome.QueueExecutionVerified ||
            !payload.NativeOutcome.FreshTerminalReceiptVerified ||
            !payload.NativeOutcome.ReservationLifecycleVerified ||
            payload.StudentObservation.SourceKind !=
                AcquisitionRoutePortfolioSupervisionSourceKinds
                    .StudentObservation ||
            payload.StudentObservation.Observed ||
            payload.StudentObservation.PositivePreferenceLabelEmitted)
        {
            throw new InvalidDataException(
                "Goal-method supervision row is not eligible explicit Teacher evidence.");
        }

        var candidates = teacher.CandidateEvaluations.ToDictionary(
            candidate => candidate.ProposalId,
            StringComparer.Ordinal);
        if (!candidates.TryGetValue(
                teacher.SelectedProposalId,
                out var preferred) ||
            !preferred.AdmissionReady ||
            preferred.AggregateCostVector is null ||
            preferred.ProposalSha256 != teacher.SelectedProposalSha256 ||
            !preferred.SelectedRouteOccurrenceIds.SequenceEqual(
                teacher.SelectedRouteOccurrenceIds,
                StringComparer.Ordinal))
        {
            throw new InvalidDataException(
                "Goal-method selected proposal is not an admitted candidate.");
        }
        foreach (var pair in teacher.PairwisePreferences)
        {
            if (pair.PreferredProposalId != teacher.SelectedProposalId ||
                pair.AlternativeProposalId == pair.PreferredProposalId ||
                pair.Criterion !=
                    "strict_aggregate_opportunity_cost_pareto_dominance" ||
                pair.LabelSemantics !=
                    "preferred_over_admitted_counterfactual" ||
                !candidates.TryGetValue(
                    pair.AlternativeProposalId,
                    out var alternative) ||
                !alternative.AdmissionReady ||
                alternative.AggregateCostVector is null ||
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(
                        preferred.AggregateCostVector,
                        alternative.AggregateCostVector))
            {
                throw new InvalidDataException(
                    "Goal-method pair is not an explicit verified Teacher preference.");
            }
        }
        if (teacher.PairwisePreferences.Select(pair =>
                    pair.AlternativeProposalId)
                .Distinct(StringComparer.Ordinal)
                .Count() != teacher.PairwisePreferences.Length)
        {
            throw new InvalidDataException(
                "Goal-method row contains duplicate pairwise alternatives.");
        }
    }

    private static void ValidateManifestCounts(
        AcquisitionRoutePortfolioSupervisionCorpusManifest manifest,
        IReadOnlyList<VerifiedSource> sources,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] rows)
    {
        var inputRows = sources.Sum(source => source.Dataset.Rows.Length);
        var uniqueSources = sources.Select(source =>
                source.Digest.DatasetSha256)
            .Distinct(StringComparer.Ordinal)
            .Count();
        var duplicates = inputRows - rows.Length;
        var pairwise = PairCount(rows);
        var unavailable = rows.Sum(row => row.SupervisionRow.Payload
            .TeacherPreference.CandidateEvaluations.Count(candidate =>
                !candidate.AdmissionReady));
        var student = rows.Count(row =>
            row.SupervisionRow.Payload.StudentObservation.Observed);
        if (manifest.Counts.InputSourceEntries != sources.Count ||
            manifest.Counts.UniqueSourceDatasets != uniqueSources ||
            manifest.Counts.InputRows != inputRows ||
            manifest.Counts.AcceptedRows != rows.Length ||
            manifest.Counts.ExactDuplicateRows != duplicates ||
            manifest.Counts.TeacherPairwisePreferences != pairwise ||
            manifest.Counts.UnavailableCandidateCount != unavailable ||
            manifest.Counts.StudentObservationCount != student ||
            manifest.Cleaned.Rows != rows.Length ||
            manifest.Partitions.Sum(partition => partition.Rows) != rows.Length)
        {
            throw new InvalidDataException(
                "Goal-method corpus counts do not match verified sources.");
        }
    }

    private static void VerifyDigest(PolicyDatasetFileDigest digest)
    {
        var fullPath = string.IsNullOrWhiteSpace(digest?.Path)
            ? string.Empty
            : Path.GetFullPath(digest.Path);
        if (string.IsNullOrWhiteSpace(fullPath) ||
            !File.Exists(fullPath) ||
            new FileInfo(fullPath).Length != digest!.Bytes ||
            CurrentTeacherFrontierSupport.HashFile(fullPath) != digest.Sha256)
        {
            throw new InvalidDataException(
                "Goal-method corpus artifact digest is invalid.");
        }
    }

    private static PolicyDatasetPartitionDigest Partition(
        AcquisitionRoutePortfolioSupervisionCorpusManifest manifest,
        string partition)
    {
        var matches = manifest.Partitions.Where(value =>
                value.Partition == partition)
            .ToArray();
        return matches.Length == 1
            ? matches[0]
            : throw new InvalidDataException(
                "Goal-method corpus must contain exactly one " + partition +
                " partition.");
    }

    private static void RequireRowsEqual(
        IReadOnlyList<AcquisitionRoutePortfolioSupervisionCorpusRow> expected,
        IReadOnlyList<AcquisitionRoutePortfolioSupervisionCorpusRow> actual,
        string message)
    {
        if (expected.Count != actual.Count)
            throw new InvalidDataException(message);
        for (var index = 0; index < expected.Count; index++)
        {
            if (JsonSerializer.Serialize(expected[index], JsonDefaults.Compact) !=
                JsonSerializer.Serialize(actual[index], JsonDefaults.Compact))
            {
                throw new InvalidDataException(message);
            }
        }
    }

    private static int PairCount(
        IEnumerable<AcquisitionRoutePortfolioSupervisionCorpusRow> rows) =>
        rows.Sum(row => row.SupervisionRow.Payload.TeacherPreference
            .PairwisePreferences.Length);

    private sealed record VerifiedSource(
        AcquisitionRoutePortfolioSupervisionCorpusSourceDigest Digest,
        AcquisitionRoutePortfolioSupervisionDataset Dataset);

    private sealed record VerifiedCorpus(
        AcquisitionRoutePortfolioSupervisionCorpusManifest Manifest,
        PolicyDatasetPartitionDigest TrainDigest,
        PolicyDatasetPartitionDigest ValidationDigest,
        PolicyDatasetPartitionDigest TestDigest,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] TrainRows,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] ValidationRows,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] TestRows,
        GoalMethodPairwiseVersionBinding Versions);
}
