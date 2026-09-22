using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSupervisionCorpusBuilder
{
    private const int MaxSources = 4096;

    public static AcquisitionRoutePortfolioSupervisionCorpusBuildResult Build(
        string requestPath,
        string outputRoot)
    {
        var requestFullPath = Path.GetFullPath(requestPath);
        var outputFullPath = Path.GetFullPath(outputRoot);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioSupervisionCorpusRequest>(
            requestFullPath,
            "Acquisition route portfolio supervision corpus request");
        ValidateRequest(request);
        Directory.CreateDirectory(outputFullPath);

        var requestDirectory = Path.GetDirectoryName(requestFullPath)!;
        var verifiedSourceCache = new Dictionary<string, VerifiedSource>(
            StringComparer.OrdinalIgnoreCase);
        var sources = request.Sources.Select(source =>
            {
                var cacheKey = SourceCacheKey(requestDirectory, source);
                if (!verifiedSourceCache.TryGetValue(
                        cacheKey,
                        out var verified))
                {
                    verified = VerifySource(requestDirectory, source);
                    verifiedSourceCache.Add(cacheKey, verified);
                }
                return verified;
            })
            .OrderBy(source => source.Digest.DatasetSha256,
                StringComparer.Ordinal)
            .ThenBy(source => source.Digest.DatasetPath,
                StringComparer.Ordinal)
            .ToArray();
        var inputRows = sources.Sum(source => source.Dataset.Rows.Length);
        var duplicates = 0;
        var acceptedById = new Dictionary<string, CorpusRowSource>(
            StringComparer.Ordinal);
        foreach (var source in sources)
        {
            foreach (var row in source.Dataset.Rows)
            {
                var rowJson = JsonSerializer.Serialize(
                    row,
                    JsonDefaults.Compact);
                if (!acceptedById.TryGetValue(row.RowId, out var existing))
                {
                    acceptedById.Add(
                        row.RowId,
                        new CorpusRowSource(source, row, rowJson));
                    continue;
                }
                if (!string.Equals(
                        existing.CanonicalRowJson,
                        rowJson,
                        StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        "Conflicting portfolio supervision row ID: " +
                        row.RowId);
                }
                duplicates++;
            }
        }

        var rows = acceptedById.Values
            .Select(value => CorpusRow(value.Source, value.Row))
            .OrderBy(row =>
                row.SupervisionRow.Payload.DecisionContext.SplitKey,
                StringComparer.Ordinal)
            .ThenBy(row => row.SupervisionRow.RowId, StringComparer.Ordinal)
            .ToArray();
        var cleanedPath = Path.Combine(
            outputFullPath,
            "portfolio-supervision-cleaned.jsonl");
        var trainPath = Path.Combine(
            outputFullPath,
            "portfolio-supervision-train.jsonl");
        var validationPath = Path.Combine(
            outputFullPath,
            "portfolio-supervision-validation.jsonl");
        var testPath = Path.Combine(
            outputFullPath,
            "portfolio-supervision-test.jsonl");
        WriteJsonl(cleanedPath, rows);
        var train = RowsFor(rows, PolicyDatasetPartitions.Train);
        var validation = RowsFor(rows, PolicyDatasetPartitions.Validation);
        var test = RowsFor(rows, PolicyDatasetPartitions.Test);
        WriteJsonl(trainPath, train);
        WriteJsonl(validationPath, validation);
        WriteJsonl(testPath, test);

        var pairwiseCount = rows.Sum(row => row.SupervisionRow.Payload
            .TeacherPreference.PairwisePreferences.Length);
        var unavailableCount = rows.Sum(row => row.SupervisionRow.Payload
            .TeacherPreference.CandidateEvaluations.Count(candidate =>
                !candidate.AdmissionReady));
        var studentObservationCount = rows.Count(row =>
            row.SupervisionRow.Payload.StudentObservation.Observed);
        var gameVersions = rows.Select(row => row.SupervisionRow.Payload
                .DecisionContext.GameVersion)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var bridgeVersions = rows.Select(row => row.SupervisionRow.Payload
                .DecisionContext.BridgeVersion)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var blockingReasons = TrainerBlockingReasons(
            rows,
            train,
            validation,
            test,
            gameVersions,
            bridgeVersions);
        var manifest = new
            AcquisitionRoutePortfolioSupervisionCorpusManifest
            {
                Status = blockingReasons.Length == 0
                    ? "ready_goal_method_trainer_input"
                    : "verified_teacher_corpus_trainer_blocked",
                CorpusId = request.CorpusId,
                RequestSha256 = CurrentTeacherFrontierSupport.HashFile(
                    requestFullPath),
                Sources = sources.Select(source => source.Digest).ToArray(),
                Cleaned = Digest(cleanedPath, rows.Length),
                Partitions = new[]
                {
                    PartitionDigest(
                        PolicyDatasetPartitions.Train,
                        trainPath,
                        train),
                    PartitionDigest(
                        PolicyDatasetPartitions.Validation,
                        validationPath,
                        validation),
                    PartitionDigest(
                        PolicyDatasetPartitions.Test,
                        testPath,
                        test)
                },
                Counts = new
                    AcquisitionRoutePortfolioSupervisionCorpusCounts
                    {
                        InputSourceEntries = request.Sources.Length,
                        UniqueSourceDatasets = sources.Select(source =>
                                source.Digest.DatasetSha256)
                            .Distinct(StringComparer.Ordinal)
                            .Count(),
                        InputRows = inputRows,
                        AcceptedRows = rows.Length,
                        ExactDuplicateRows = duplicates,
                        TeacherPairwisePreferences = pairwiseCount,
                        UnavailableCandidateCount = unavailableCount,
                        StudentObservationCount = studentObservationCount
                    },
                GameVersions = gameVersions,
                BridgeVersions = bridgeVersions,
                TeacherTrainingEvidenceEligible = rows.Length > 0,
                GoalMethodTrainerInputReady = blockingReasons.Length == 0,
                FormalProductTrainingAuthorized = false,
                BlockingReasons = blockingReasons
            };
        var manifestPath = Path.Combine(
            outputFullPath,
            "portfolio-supervision-corpus-manifest.json");
        WriteJson(manifestPath, manifest);
        return new AcquisitionRoutePortfolioSupervisionCorpusBuildResult
        {
            ManifestPath = manifestPath,
            Manifest = manifest
        };
    }

    private static VerifiedSource VerifySource(
        string requestDirectory,
        AcquisitionRoutePortfolioSupervisionCorpusSource source)
    {
        var datasetPath = Resolve(requestDirectory, source.DatasetPath);
        var proofManifestPath = Resolve(
            requestDirectory,
            source.ProofManifestPath);
        var proofReceiptPath = Resolve(
            requestDirectory,
            source.ProofReceiptPath);
        var admissionPath = Resolve(
            requestDirectory,
            source.RolloutAdmissionReceiptPath);
        var dataset = AcquisitionRoutePortfolioSupervisionBuilder.Verify(
            datasetPath,
            proofManifestPath,
            proofReceiptPath,
            admissionPath);
        if (!dataset.TeacherTrainingEvidenceEligible ||
            dataset.FormalProductTrainingAuthorized ||
            dataset.Rows.Length == 0 ||
            dataset.Rows.Length != dataset.TransitionCount)
        {
            throw new InvalidDataException(
                "Portfolio supervision source is not eligible Teacher evidence: " +
                datasetPath);
        }
        return new VerifiedSource(
            dataset,
            new AcquisitionRoutePortfolioSupervisionCorpusSourceDigest
            {
                DatasetPath = datasetPath,
                DatasetSha256 = CurrentTeacherFrontierSupport.HashFile(
                    datasetPath),
                ProofManifestPath = proofManifestPath,
                ProofManifestSha256 =
                    CurrentTeacherFrontierSupport.HashFile(
                        proofManifestPath),
                ProofReceiptPath = proofReceiptPath,
                ProofReceiptSha256 = CurrentTeacherFrontierSupport.HashFile(
                    proofReceiptPath),
                RolloutAdmissionReceiptPath = admissionPath,
                RolloutAdmissionReceiptSha256 =
                    CurrentTeacherFrontierSupport.HashFile(admissionPath),
                RolloutId = dataset.RolloutId,
                GoalId = dataset.GoalId,
                RowCount = dataset.Rows.Length
            });
    }

    private static AcquisitionRoutePortfolioSupervisionCorpusRow CorpusRow(
        VerifiedSource source,
        AcquisitionRoutePortfolioSupervisionRow row)
    {
        var partition = PolicyTrajectoryDatasetBuilder.PartitionFor(
            row.Payload.DecisionContext.SplitKey);
        return new AcquisitionRoutePortfolioSupervisionCorpusRow
        {
            SourceDatasetSha256 = source.Digest.DatasetSha256,
            RolloutId = source.Dataset.RolloutId,
            GoalId = source.Dataset.GoalId,
            DatasetPartition = partition,
            SupervisionRow = row
        };
    }

}
