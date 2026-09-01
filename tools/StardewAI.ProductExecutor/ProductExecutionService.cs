using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

namespace StardewAI.ProductExecutor;

public sealed class ProductExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ProductExecutorOptions options;
    private readonly ProductExecutionPolicy policy;
    private readonly ProductReceiptStore receipts;
    private readonly HttpClient http;
    private readonly SemaphoreSlim dispatchLock = new(1, 1);

    public ProductExecutionService(
        ProductExecutorOptions options,
        ProductExecutionPolicy policy,
        ProductReceiptStore receipts,
        HttpClient http)
    {
        this.options = options;
        this.policy = policy;
        this.receipts = receipts;
        this.http = http;
    }

    public async Task<JsonObject> ExecuteAsync(JsonObject rawRequest, CancellationToken cancellationToken)
    {
        TrainingExecutionRequest? request;
        try
        {
            request = rawRequest.Deserialize<TrainingExecutionRequest>(JsonOptions);
        }
        catch (JsonException)
        {
            return Blocked(null, new[] { "request_json_invalid" }, "product_request_rejected");
        }
        if (request is null)
            return Blocked(null, new[] { "request_json_invalid" }, "product_request_rejected");

        var authorizationReasons = policy.Authorize(
            request,
            DateTimeOffset.UtcNow,
            allowExpiredReplay: true);
        if (authorizationReasons.Length > 0)
            return Blocked(request, authorizationReasons, "product_authorization_rejected");

        var requestJson = rawRequest.ToJsonString();
        var requestHash = ProductReceiptStore.Sha256(requestJson);
        await dispatchLock.WaitAsync(cancellationToken);
        try
        {
            var lookup = await receipts.ReadAsync(request, requestHash, cancellationToken);
            if (lookup.State == ProductReceiptState.Final && lookup.Response is not null)
            {
                lookup.Response["product_idempotent_replay"] = true;
                return lookup.Response;
            }
            if (lookup.State == ProductReceiptState.Conflict)
                return Blocked(request, new[] { "request_nonce_reused_with_different_payload" }, "product_nonce_conflict");
            if (lookup.State == ProductReceiptState.Pending)
            {
                var indeterminate = Blocked(
                    request,
                    new[] { "native_dispatch_indeterminate_no_replay" },
                    "product_pending_recovery_blocked");
                StampProduct(
                    indeterminate,
                    lookup.ReceiptId,
                    requestHash,
                    request.BeforeStateHash,
                    string.Empty,
                    0,
                    string.Empty,
                    0,
                    null,
                    true);
                await receipts.WriteFinalAsync(request, requestHash, indeterminate, cancellationToken);
                return indeterminate;
            }

            authorizationReasons = policy.Authorize(request, DateTimeOffset.UtcNow);
            if (authorizationReasons.Length > 0)
                return Blocked(request, authorizationReasons, "product_authorization_rejected");

            JsonObject beforeSnapshot;
            try
            {
                beforeSnapshot = await ReadSnapshotAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                return Blocked(request, new[] { "fresh_before_snapshot_unavailable" }, "product_snapshot_gate_failed");
            }
            var liveBeforeHash = ReadString(beforeSnapshot, "state_hash");
            var beforeTick = ReadLong(beforeSnapshot, "game_tick");
            var requestStateHashDrift = liveBeforeHash != request.BeforeStateHash;

            if (lookup.State == ProductReceiptState.Missing)
            {
                await receipts.WritePendingAsync(
                    request,
                    rawRequest,
                    requestHash,
                    liveBeforeHash,
                    beforeTick,
                    cancellationToken);
            }

            JsonObject nativeResult;
            try
            {
                nativeResult = await PostNativeAsync(requestJson, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                var indeterminate = Blocked(
                    request,
                    new[] { "native_dispatch_indeterminate_no_replay" },
                    "product_native_dispatch_indeterminate");
                StampProduct(
                    indeterminate,
                    lookup.ReceiptId,
                    requestHash,
                    request.BeforeStateHash,
                    liveBeforeHash,
                    beforeTick,
                    string.Empty,
                    0,
                    requestStateHashDrift,
                    true);
                await receipts.WriteFinalAsync(request, requestHash, indeterminate, cancellationToken);
                return indeterminate;
            }

            var nativeContractReasons = ValidateNativeResult(request, nativeResult);
            JsonObject afterSnapshot;
            try
            {
                afterSnapshot = await ReadSnapshotAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
            {
                nativeContractReasons.Add("fresh_after_snapshot_unavailable");
                afterSnapshot = new JsonObject();
            }
            var afterHash = ReadString(afterSnapshot, "state_hash");
            var afterTick = ReadLong(afterSnapshot, "game_tick");
            if (nativeContractReasons.Count > 0)
            {
                nativeResult = Blocked(request, nativeContractReasons, "product_native_receipt_rejected");
            }
            var replanRequired =
                requestStateHashDrift ||
                ReadString(nativeResult, "status") != "applied" ||
                ReadString(nativeResult, "primitive_verification_status") != "verified";
            StampProduct(
                nativeResult,
                lookup.ReceiptId,
                requestHash,
                request.BeforeStateHash,
                liveBeforeHash,
                beforeTick,
                afterHash,
                afterTick,
                requestStateHashDrift,
                replanRequired);
            await receipts.WriteFinalAsync(request, requestHash, nativeResult, cancellationToken);
            return nativeResult;
        }
        finally
        {
            dispatchLock.Release();
        }
    }

    private async Task<JsonObject> ReadSnapshotAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(
            ForceFreshSnapshotUrl(options.BridgeSnapshotUrl),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        var value = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?.AsObject()
            ?? throw new JsonException("snapshot response is not an object");
        if (ReadString(value, "schema_version") != "snapshot.v1" || string.IsNullOrWhiteSpace(ReadString(value, "state_hash")))
            throw new JsonException("snapshot response contract mismatch");
        return value;
    }

    private static string ForceFreshSnapshotUrl(string value)
    {
        var uri = new Uri(value, UriKind.Absolute);
        var query = uri.Query.TrimStart('?')
            .Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Where(pair => !string.Equals(
                Uri.UnescapeDataString(pair.Split('=', 2)[0]),
                "fresh",
                StringComparison.OrdinalIgnoreCase))
            .Append("fresh=1");
        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", query)
        };
        return builder.Uri.AbsoluteUri;
    }

    private async Task<JsonObject> PostNativeAsync(string requestJson, CancellationToken cancellationToken)
    {
        using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(
            options.NativeExecutorUrl.TrimEnd('/') + "/api/v1/training/execute",
            content,
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken))?.AsObject()
            ?? throw new JsonException("native executor response is not an object");
    }

    private static List<string> ValidateNativeResult(
        TrainingExecutionRequest request,
        JsonObject result)
    {
        var reasons = new List<string>();
        if (ReadString(result, "schema_version") != "training_execution_result.v1")
            reasons.Add("native_result_schema_mismatch");
        if (ReadString(result, "run_id") != request.RunId ||
            ReadString(result, "queue_id") != request.QueueId ||
            ReadString(result, "queue_item_id") != request.QueueItemId ||
            ReadString(result, "before_state_hash") != request.BeforeStateHash ||
            ReadString(result, "option_id") != request.OptionId)
        {
            reasons.Add("native_result_identity_mismatch");
        }
        if (result["feedback_available"]?.GetValue<bool>() != true)
            reasons.Add("native_feedback_unavailable");
        if (ReadString(result, "status") is not ("applied" or "blocked"))
            reasons.Add("native_result_status_invalid");
        if (string.IsNullOrWhiteSpace(ReadString(result, "primitive_verification_status")))
            reasons.Add("native_verification_status_missing");
        return reasons;
    }

    private static JsonObject Blocked(
        TrainingExecutionRequest? request,
        IEnumerable<string> reasons,
        string verificationStatus)
    {
        var reasonArray = reasons.Distinct(StringComparer.Ordinal).ToArray();
        return new JsonObject
        {
            ["schema_version"] = "training_execution_result.v1",
            ["run_id"] = request?.RunId ?? string.Empty,
            ["queue_id"] = request?.QueueId ?? string.Empty,
            ["queue_item_id"] = request?.QueueItemId ?? string.Empty,
            ["before_state_hash"] = request?.BeforeStateHash ?? string.Empty,
            ["option_id"] = request?.OptionId ?? string.Empty,
            ["status"] = "blocked",
            ["feedback_available"] = true,
            ["primitive_kind"] = "product_executor_gate",
            ["primitive_verification_status"] = verificationStatus,
            ["primitive_verification_reasons"] = new JsonArray(
                reasonArray.Select(reason => (JsonNode?)JsonValue.Create(reason)).ToArray()),
            ["started_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["completed_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["block_reasons"] = new JsonArray(
                reasonArray.Select(reason => (JsonNode?)JsonValue.Create(reason)).ToArray()),
            ["source"] = "product_executor",
            ["product_replan_required"] = true
        };
    }

    private static void StampProduct(
        JsonObject result,
        string receiptId,
        string requestHash,
        string requestBeforeHash,
        string beforeHash,
        long beforeTick,
        string afterHash,
        long afterTick,
        bool? requestStateHashDrift,
        bool replanRequired)
    {
        result["source"] = "product_executor";
        result["product_executor_schema_version"] = "stardewai.product_executor.v1";
        result["product_authorization_status"] = "authorized";
        result["product_receipt_id"] = receiptId;
        result["product_request_sha256"] = requestHash;
        result["product_request_before_state_hash"] = requestBeforeHash;
        result["product_before_state_hash"] = beforeHash;
        result["product_before_game_tick"] = beforeTick;
        result["product_after_state_hash"] = afterHash;
        result["product_after_game_tick"] = afterTick;
        result["product_request_state_hash_drift"] = requestStateHashDrift.HasValue
            ? JsonValue.Create(requestStateHashDrift.Value)
            : null;
        result["product_dispatch_guard"] = "native_action_preconditions";
        result["product_replan_required"] = replanRequired;
        result["product_idempotent_replay"] = false;
    }

    private static string ReadString(JsonObject value, string name) =>
        value[name] is JsonValue node && node.TryGetValue<string>(out var result) ? result : string.Empty;

    private static long ReadLong(JsonObject value, string name) =>
        value[name] is JsonValue node && node.TryGetValue<long>(out var result) ? result : 0;
}
