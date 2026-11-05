using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static class RustyKeyTeacherCorpusBuilder
{
    public static RustyKeyTeacherCorpus Build(string requestPath)
    {
        requestPath = RequiredFile(requestPath);
        var request = ReadTyped<RustyKeyTeacherCorpusRequest>(requestPath, "rusty_key_teacher_corpus_request.v1");
        Require(!string.IsNullOrWhiteSpace(request.CorpusId) && !request.FormalProductTrainingAuthorized &&
            request.Sources is { Length: > 0 } && request.Sources.All(source => source is not null &&
                !string.IsNullOrWhiteSpace(source.SourceId)) &&
            request.Sources.Select(source => source.SourceId).Distinct(StringComparer.Ordinal).Count() == request.Sources.Length,
            "Rusty Key corpus request is invalid or has duplicate sources.");
        var rows = request.Sources.Select(source =>
        {
            var path = RequiredFile(source.JoinReceiptPath);
            return new RustyKeyTeacherCorpusRow(source.SourceId, path, CurrentTeacherFrontierSupport.HashFile(path),
                RustyKeyTeacherNativeJoinBuilder.Verify(path));
        }).OrderBy(row => row.Evidence.SplitKey, StringComparer.Ordinal).ThenBy(row => row.SourceId, StringComparer.Ordinal).ToArray();
        ValidateRows(rows);
        return new(requestPath, CurrentTeacherFrontierSupport.HashFile(requestPath), request.CorpusId, rows);
    }

    public static RustyKeyTeacherCorpus Verify(string corpusPath)
    {
        var raw = CurrentTeacherFrontierSupport.Read<JsonElement>(RequiredFile(corpusPath), "Rusty Key Teacher corpus");
        Require(raw.ValueKind == JsonValueKind.Object && raw.TryGetProperty("request_path", out var request) &&
            request.ValueKind == JsonValueKind.String, "Rusty Key corpus request path is missing.");
        var rebuilt = Build(raw.GetProperty("request_path").GetString()!);
        Require(JsonNode.DeepEquals(JsonNode.Parse(raw.GetRawText()), JsonSerializer.SerializeToNode(rebuilt, JsonDefaults.Options)),
            "Rusty Key corpus drifted from independently rebuilt original joins.");
        return rebuilt;
    }

    private static void ValidateRows(RustyKeyTeacherCorpusRow[] rows)
    {
        var groups = new HashSet<string>(StringComparer.Ordinal);
        var runs = new HashSet<string>(StringComparer.Ordinal);
        var nonces = new HashSet<string>(StringComparer.Ordinal);
        var actions = new HashSet<string>(StringComparer.Ordinal);
        foreach (var row in rows)
        {
            var evidence = row.Evidence;
            Require(evidence.TeacherComparisonVerified && evidence.NativeTerminalOutcomeVerified && evidence.CausalContinuationVerified &&
                !evidence.FormalTrainingAuthorized && evidence.SplitKey == evidence.SaveId + ":" + evidence.PlayerId + ":" + evidence.DecisionTotalDay &&
                evidence.DatasetPartition == PolicyTrajectoryDatasetBuilder.PartitionFor(evidence.SplitKey) &&
                groups.Add(evidence.SplitKey) && runs.Add(evidence.RunId),
                "Rusty Key corpus contains an unverified or reused decision/trajectory group.");
            foreach (var action in evidence.Actions)
                Require(nonces.Add(action.RequestNonce) && actions.Add(evidence.RunId + ":" + action.QueueId + ":" + action.QueueItemId),
                    "Rusty Key corpus reuses a native request or action across rows.");
        }
    }
}
