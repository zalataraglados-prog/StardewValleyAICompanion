using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRoutePortfolioSupervisionCorpusBuilder
{
    private static string[] TrainerBlockingReasons(
        AcquisitionRoutePortfolioSupervisionCorpusRow[] rows,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] train,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] validation,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] test,
        string[] gameVersions,
        string[] bridgeVersions)
    {
        var reasons = new List<string>();
        if (rows.Length == 0)
            reasons.Add("teacher_supervision_corpus_empty");
        if (PairwiseCount(rows) == 0)
            reasons.Add("teacher_pairwise_comparison_empty");
        if (PairwiseCount(train) == 0)
            reasons.Add("train_pairwise_comparison_empty");
        if (PairwiseCount(validation) == 0)
            reasons.Add("validation_pairwise_comparison_empty");
        if (PairwiseCount(test) == 0)
            reasons.Add("test_pairwise_comparison_empty");
        if (gameVersions.Length != 1)
            reasons.Add("game_version_set_not_immutable");
        if (bridgeVersions.Length != 1)
            reasons.Add("bridge_version_set_not_immutable");
        return reasons
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private static int PairwiseCount(
        IEnumerable<AcquisitionRoutePortfolioSupervisionCorpusRow> rows) =>
        rows.Sum(row => row.SupervisionRow.Payload.TeacherPreference
            .PairwisePreferences.Length);

    private static AcquisitionRoutePortfolioSupervisionCorpusRow[] RowsFor(
        IEnumerable<AcquisitionRoutePortfolioSupervisionCorpusRow> rows,
        string partition) => rows.Where(row => string.Equals(
                row.DatasetPartition,
                partition,
                StringComparison.Ordinal))
            .ToArray();

    private static PolicyDatasetPartitionDigest PartitionDigest(
        string partition,
        string path,
        AcquisitionRoutePortfolioSupervisionCorpusRow[] rows)
    {
        var digest = Digest(path, rows.Length);
        return new PolicyDatasetPartitionDigest
        {
            Partition = partition,
            Path = digest.Path,
            Sha256 = digest.Sha256,
            Bytes = digest.Bytes,
            Rows = digest.Rows,
            SplitKeyCount = rows.Select(row => row.SupervisionRow.Payload
                    .DecisionContext.SplitKey)
                .Distinct(StringComparer.Ordinal)
                .Count()
        };
    }

    private static PolicyDatasetFileDigest Digest(string path, int rows)
    {
        var fullPath = Path.GetFullPath(path);
        var info = new FileInfo(fullPath);
        return new PolicyDatasetFileDigest
        {
            Path = fullPath,
            Sha256 = CurrentTeacherFrontierSupport.HashFile(fullPath),
            Bytes = info.Length,
            Rows = rows
        };
    }

    private static void ValidateRequest(
        AcquisitionRoutePortfolioSupervisionCorpusRequest request)
    {
        if (request.SchemaVersion !=
                "acquisition_route_portfolio_supervision_corpus_request.v1" ||
            string.IsNullOrWhiteSpace(request.CorpusId) ||
            request.FormalProductTrainingAuthorized ||
            request.Sources is null ||
            request.Sources.Length is < 1 or > MaxSources ||
            request.Sources.Any(source => source is null ||
                string.IsNullOrWhiteSpace(source.DatasetPath) ||
                string.IsNullOrWhiteSpace(source.ProofManifestPath) ||
                string.IsNullOrWhiteSpace(source.ProofReceiptPath) ||
                string.IsNullOrWhiteSpace(
                    source.RolloutAdmissionReceiptPath)))
        {
            throw new InvalidDataException(
                "Portfolio supervision corpus request is invalid.");
        }
    }

    private static string Resolve(string baseDirectory, string path) =>
        Path.GetFullPath(Path.IsPathRooted(path)
            ? path
            : Path.Combine(baseDirectory, path));

    private static string SourceCacheKey(
        string requestDirectory,
        AcquisitionRoutePortfolioSupervisionCorpusSource source) =>
        string.Join("\n", new[]
        {
            Resolve(requestDirectory, source.DatasetPath),
            Resolve(requestDirectory, source.ProofManifestPath),
            Resolve(requestDirectory, source.ProofReceiptPath),
            Resolve(requestDirectory, source.RolloutAdmissionReceiptPath)
        });

    private static void WriteJsonl<T>(string path, IEnumerable<T> rows)
    {
        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        var serialized = rows.Select(row => JsonSerializer.Serialize(
                row,
                JsonDefaults.Compact))
            .ToArray();
        var payload = serialized.Length == 0
            ? string.Empty
            : string.Join("\n", serialized) + "\n";
        File.WriteAllText(temporary, payload, new UTF8Encoding(false));
        File.Move(temporary, fullPath, true);
    }

    private static void WriteJson(string path, object value)
    {
        var fullPath = Path.GetFullPath(path);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        File.WriteAllText(
            temporary,
            JsonSerializer.Serialize(value, JsonDefaults.Options) +
            "\n",
            new UTF8Encoding(false));
        File.Move(temporary, fullPath, true);
    }

    private sealed record VerifiedSource(
        AcquisitionRoutePortfolioSupervisionDataset Dataset,
        AcquisitionRoutePortfolioSupervisionCorpusSourceDigest Digest);

    private sealed record CorpusRowSource(
        VerifiedSource Source,
        AcquisitionRoutePortfolioSupervisionRow Row,
        string CanonicalRowJson);
}
