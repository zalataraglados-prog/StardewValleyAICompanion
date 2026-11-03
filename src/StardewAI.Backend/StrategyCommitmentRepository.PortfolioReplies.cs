using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

public sealed partial class FileStrategyCommitmentRepository
{
    private const string PortfolioReplyProperty = "_last_portfolio_reply";
    private const string PortfolioReplySchema = "stardewai.portfolio_mutation_reply.v1";
    private readonly Dictionary<string, JsonNode?> lastPortfolioReplies = new(StringComparer.Ordinal);
    // Match the mutation endpoints' Web defaults, including case and quoted numbers.
    private static readonly JsonSerializerOptions PortfolioRequestJson = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private void SavePortfolioReply<TRequest, TResult>(string key, StrategyCommitmentLedger ledger,
        string operation, TRequest request, TResult result)
    {
        var reply = JsonSerializer.SerializeToNode(result, JsonOptions)!.AsObject();
        var ledgerDocument = reply["ledger"]!.AsObject();
        reply.Remove("ledger");
        var requestNode = JsonSerializer.SerializeToNode(request, JsonOptions)!;
        Save(key, ledger, new JsonObject
        {
            ["schema_version"] = PortfolioReplySchema,
            ["operation"] = operation,
            ["state_hash"] = requestNode["state_hash"]!.GetValue<string>(),
            ["request_sha256"] = PortfolioHash(requestNode),
            ["ledger_revision"] = ledger.Revision,
            ["ledger_sha256"] = PortfolioHash(ledgerDocument),
            ["reply_sha256"] = PortfolioHash(reply),
            ["reply"] = reply
        }, ledgerDocument);
    }

    public JsonObject LookupPortfolioMutationReply(SnapshotEnvelope snapshot, string operation, JsonElement request)
    {
        if (!TryNormalizePortfolioRequest(operation, request, out var normalized, out var stateHash) ||
            !string.Equals(snapshot.StateHash, stateHash, StringComparison.Ordinal))
            return PortfolioMutationReplyLookup.Observation("rejected", "original request/snapshot mismatch");
        lock (sync)
        {
            // Do not call Get/Load: Get reconciles, while recovery must not mutate even the cache.
            var path = LedgerPath(IdentityKey(snapshot));
            if (!File.Exists(path))
                return PortfolioMutationReplyLookup.Observation("not_found");
            try
            {
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                    return PortfolioMutationReplyLookup.Observation("corrupt", "ledger is a reparse point");
                var document = JsonNode.Parse(File.ReadAllText(path)) as JsonObject
                    ?? throw new JsonException("ledger root is not an object");
                var ledger = document.Deserialize<StrategyCommitmentLedger>(JsonOptions)
                    ?? throw new JsonException("ledger is null");
                if (document["schema_version"]?.GetValue<string>() != "strategy_commitment_ledger.v1" ||
                    ledger.SaveId != SaveId(snapshot) || ledger.PlayerId != PlayerId(snapshot))
                    return PortfolioMutationReplyLookup.Observation("corrupt", "ledger identity/schema mismatch");
                if (document[PortfolioReplyProperty] is null)
                    return PortfolioMutationReplyLookup.Observation("not_found");
                var record = document[PortfolioReplyProperty]!.AsObject();
                if (record["schema_version"]?.GetValue<string>() != PortfolioReplySchema)
                    return PortfolioMutationReplyLookup.Observation("corrupt", "reply schema missing/unsupported");
                if (record["operation"]?.GetValue<string>() != operation ||
                    record["state_hash"]?.GetValue<string>() != stateHash ||
                    record["request_sha256"]?.GetValue<string>() != PortfolioHash(normalized!))
                    return PortfolioMutationReplyLookup.Observation("conflict", "different last request");
                if (record["ledger_revision"]?.GetValue<int>() != ledger.Revision)
                    return PortfolioMutationReplyLookup.Observation("superseded", "ledger revision has advanced");
                if (record["ledger_sha256"]?.GetValue<string>() !=
                    PortfolioHash(JsonSerializer.SerializeToNode(ledger, JsonOptions)!))
                    return PortfolioMutationReplyLookup.Observation("corrupt", "ledger hash mismatch");
                var reply = record["reply"]?.AsObject() ?? throw new JsonException("reply missing");
                if (record["reply_sha256"]?.GetValue<string>() != PortfolioHash(reply) ||
                    reply.ContainsKey("ledger") || reply["accepted"]?.GetValue<bool>() != true ||
                    reply["committed_ledger_revision"]?.GetValue<int>() != ledger.Revision ||
                    reply["errors"] is not JsonArray { Count: 0 })
                    return PortfolioMutationReplyLookup.Observation("corrupt", "stored reply invalid");
                var restored = reply.DeepClone().AsObject();
                restored["ledger"] = JsonSerializer.SerializeToNode(ledger, JsonOptions);
                return PortfolioMutationReplyLookup.Observation("found", reply: restored);
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or
                JsonException or InvalidOperationException or FormatException)
            {
                return PortfolioMutationReplyLookup.Observation("corrupt", error.GetType().Name);
            }
        }
    }

    internal static bool TryNormalizePortfolioRequest(string operation, JsonElement request,
        out JsonNode? normalized, out string stateHash)
    {
        normalized = null;
        stateHash = string.Empty;
        if (request.ValueKind != JsonValueKind.Object)
            return false;
        try
        {
            object? typed = operation switch
            {
                "commit" => request.Deserialize<ReservationPortfolioCommitRequest>(PortfolioRequestJson),
                "settle-completed-route" => request.Deserialize<ReservationPortfolioRouteSettlementRequest>(PortfolioRequestJson),
                "settle-supporting-transition" => request.Deserialize<ReservationPortfolioSupportingTransitionSettlementRequest>(PortfolioRequestJson),
                _ => null
            };
            if (typed is null)
                return false;
            normalized = JsonSerializer.SerializeToNode(typed, typed.GetType(), JsonOptions);
            stateHash = normalized?["state_hash"]?.GetValue<string>() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(stateHash);
        }
        catch (Exception error) when (error is JsonException or InvalidOperationException or NotSupportedException)
        {
            return false;
        }
    }

    private static string PortfolioHash(JsonNode value) => Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(value.ToJsonString(JsonOptions)))).ToLowerInvariant();
}
