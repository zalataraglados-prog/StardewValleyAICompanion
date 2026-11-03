using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

namespace StardewAI.ProductExecutor;

public sealed partial class ProductExecutionService
{
    public async Task<JsonObject> LookupReceiptAsync(JsonObject rawRequest, CancellationToken cancellationToken)
    {
        TrainingExecutionRequest? request;
        try { request = rawRequest.Deserialize<TrainingExecutionRequest>(JsonOptions); }
        catch (JsonException) { return LookupResult("rejected", new[] { "request_json_invalid" }); }
        if (request is null) return LookupResult("rejected", new[] { "request_json_invalid" });
        var reasons = policy.Authorize(request, DateTimeOffset.UtcNow, allowExpiredReplay: true);
        if (reasons.Length > 0) return LookupResult("rejected", reasons);
        var requestHash = ProductReceiptStore.Sha256(rawRequest.ToJsonString());
        ProductReceiptLookup lookup;
        try { lookup = await receipts.ReadAsync(request, requestHash, cancellationToken); }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException)
        {
            return LookupResult("corrupt", new[] { "stored_receipt_unreadable" }, requestHash);
        }
        var status = lookup.State.ToString().ToLowerInvariant();
        var errors = lookup.State switch
        {
            ProductReceiptState.Missing => new[] { "receipt_not_found_no_dispatch_permission" },
            ProductReceiptState.Pending => new[] { "native_dispatch_indeterminate_no_replay" },
            ProductReceiptState.Conflict => new[] { "request_nonce_reused_with_different_payload" },
            _ => Array.Empty<string>()
        };
        var result = LookupResult(status, errors, requestHash);
        result["receipt_id"] = lookup.ReceiptId;
        if (lookup.State == ProductReceiptState.Final && lookup.Response is not null)
            result["response"] = lookup.Response.DeepClone();
        return result;
    }

    private static JsonObject LookupResult(string status, string[] reasons, string requestHash = "") => new()
    {
        ["schema_version"] = "stardewai.product_receipt_lookup.v1",
        ["status"] = status,
        ["request_sha256"] = requestHash,
        ["block_reasons"] = new JsonArray(reasons.Select(value => JsonValue.Create(value)).ToArray()),
        ["native_dispatch_performed"] = false,
        ["execution_authorized"] = false,
        ["fresh_verification_required"] = true
    };
}
