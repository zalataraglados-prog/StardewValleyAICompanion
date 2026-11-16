using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static class CropTeacherSharedPortfolioMutation
{
    internal static (TRequest Request, TReply Reply) Read<TRequest, TReply>(string requestPath, string replyPath,
        string journalPath, string backendUrl, string operation)
    {
        Require(new FileInfo(requestPath).Length <= ExecutionRequestJournalReader.MaximumRequestBytes &&
                new FileInfo(journalPath).Length <= 16 * 1024,
            "Shared transfer return exceeds the original RPC journal budget.");
        var requestHash = CurrentTeacherFrontierSupport.HashFile(requestPath);
        var stem = Path.Combine(Path.GetDirectoryName(journalPath)!, "portfolio-mutation-" + requestHash);
        Require(CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(requestPath, stem + "-rpc-request.json") &&
                CropTeacherSeedTransferNativeOutcome.PathComparer.Equals(journalPath, stem + "-rpc-journal.json"),
            "Shared transfer return lost its original content-addressed RPC paths.");
        var expected = JsonSerializer.SerializeToNode(new
        {
            schema_version = "stardewai.portfolio_rpc_journal.v1", backend_url = backendUrl,
            operation, request_file = Path.GetFileName(requestPath), request_sha256 = requestHash
        });
        Require(Uri.TryCreate(backendUrl, UriKind.Absolute, out var origin) && origin.Scheme is "http" or "https" &&
                origin.UserInfo.Length == 0 && origin.Query.Length == 0 && origin.Fragment.Length == 0 &&
                JsonNode.DeepEquals(expected, CurrentTeacherFrontierSupport.Read<JsonNode>(journalPath, "original shared transfer RPC journal")),
            "Shared transfer return lost its original RPC identity.");
        return (CurrentTeacherFrontierSupport.Read<TRequest>(requestPath, "original shared transfer " + operation + " request"),
            CurrentTeacherFrontierSupport.Read<TReply>(replyPath, "original shared transfer " + operation + " reply"));
    }
}
