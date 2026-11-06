using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class MuseumTeacherCorpusBuilder
{
    public static MuseumTeacherCorpus Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<MuseumTeacherCorpusRequest>(requestPath, "museum_teacher_corpus_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.CorpusId) && !request.FormalProductTrainingAuthorized &&
            request.Sources is { Length: > 0 } && request.Sources.All(row => row is not null && !string.IsNullOrWhiteSpace(row.SourceId)) &&
            request.Sources.Select(row => row.SourceId).Distinct(StringComparer.Ordinal).Count() == request.Sources.Length,
            "Museum corpus request is invalid or repeats a source identity.");
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hashes = new HashSet<string>(StringComparer.Ordinal);
        var rows = request.Sources.Select(source =>
        {
            var path = RequiredFile(source.JoinPath);
            var hash = CurrentTeacherFrontierSupport.HashFile(path);
            Require(paths.Add(path) && hashes.Add(hash), "Museum corpus repeats the same join artifact.");
            // Each original full join is proved once; no row/provider replay or second native read follows.
            var evidence = MuseumTerminalNativeJoinBuilder.Verify(path);
            Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Museum join changed during corpus verification.");
            return new MuseumTeacherCorpusRow(source.SourceId, path, hash, evidence);
        }).OrderBy(row => row.Evidence.SplitKey, StringComparer.Ordinal).ThenBy(row => row.SourceId, StringComparer.Ordinal).ToArray();
        ValidateRows(rows);
        return new(requestPath, CurrentTeacherFrontierSupport.HashFile(requestPath), request.CorpusId, rows);
    }

    public static MuseumTeacherCorpus Verify(string corpusPath)
    {
        var raw = ReadTyped<JsonElement>(RequiredFile(corpusPath), "museum_teacher_corpus.v1");
        Require(raw.TryGetProperty("request_path", out var request) && request.ValueKind == JsonValueKind.String,
            "Museum corpus request path is missing.");
        var rebuilt = Build(request.GetString()!);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Museum corpus differs from its complete original terminal sources.");
        return rebuilt;
    }

    private static void ValidateRows(MuseumTeacherCorpusRow[] rows)
    {
        Require(rows.Length > 0, "Museum corpus has no verified terminal sources.");
        var groups = new HashSet<string>(StringComparer.Ordinal);
        var runs = new HashSet<string>(StringComparer.Ordinal);
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var evidence = row.Evidence;
            Require(evidence.TeacherComparisonVerified && evidence.NativeTerminalOutcomeVerified && !evidence.FullRootVerified &&
                !evidence.PriorAcquisitionVerified && !evidence.FormalTrainingAuthorized && evidence.DecisionTotalDay is >= 0 and < 224 &&
                evidence.SplitKey == evidence.SaveId + ":" + evidence.PlayerId + ":" + evidence.DecisionTotalDay &&
                evidence.DatasetPartition == PolicyTrajectoryDatasetBuilder.PartitionFor(evidence.SplitKey) &&
                groups.Add(evidence.SplitKey) && runs.Add(evidence.RunId) && nonces.Add(evidence.Action.RequestNonce) &&
                actions.Add(evidence.RunId + ":" + evidence.Action.QueueId + ":" + evidence.Action.QueueItemId),
                "Museum corpus reuses or misgroups an original decision/native action.");
            Require(evidence.Items.Length == 95 && evidence.PairwisePreferences.Any(pair => pair.PreferredCandidateId == evidence.PreferredCandidateId),
                "Museum corpus lacks the full95 source or the selected independent pair.");
            foreach (var key in new[] { "comparison.knowledge", "comparison.requirement_inventory", "comparison.dependencies" })
                Require(evidence.SourceSha256.TryGetValue(key, out var hash) && !string.IsNullOrWhiteSpace(hash) &&
                    rows[0].Evidence.SourceSha256.TryGetValue(key, out var first) && hash == first,
                    "Museum corpus mixes original knowledge/requirement/dependency authorities.");
        }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
