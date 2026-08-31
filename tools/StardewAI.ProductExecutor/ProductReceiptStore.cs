using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

namespace StardewAI.ProductExecutor;

public sealed class ProductReceiptStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string root;

    public ProductReceiptStore(ProductExecutorOptions options)
    {
        root = Path.GetFullPath(options.JournalRoot);
        Directory.CreateDirectory(root);
    }

    public string ReceiptId(TrainingExecutionRequest request) =>
        Sha256(request.RunId + "\n" + request.RequestNonce);

    public async Task<ProductReceiptLookup> ReadAsync(
        TrainingExecutionRequest request,
        string requestHash,
        CancellationToken cancellationToken)
    {
        var receiptId = ReceiptId(request);
        var finalPath = Path.Combine(root, receiptId + ".final.json");
        if (File.Exists(finalPath))
        {
            var document = JsonNode.Parse(await File.ReadAllTextAsync(finalPath, cancellationToken))?.AsObject();
            var storedHash = document?["request_sha256"]?.GetValue<string>() ?? string.Empty;
            var response = document?["response"] as JsonObject;
            return storedHash == requestHash && response is not null
                ? new ProductReceiptLookup(receiptId, ProductReceiptState.Final, Clone(response), true)
                : new ProductReceiptLookup(receiptId, ProductReceiptState.Conflict, null, false);
        }

        var pendingPath = Path.Combine(root, receiptId + ".pending.json");
        if (!File.Exists(pendingPath))
            return new ProductReceiptLookup(receiptId, ProductReceiptState.Missing, null, false);
        var pending = JsonNode.Parse(await File.ReadAllTextAsync(pendingPath, cancellationToken))?.AsObject();
        return pending?["request_sha256"]?.GetValue<string>() == requestHash
            ? new ProductReceiptLookup(receiptId, ProductReceiptState.Pending, null, false)
            : new ProductReceiptLookup(receiptId, ProductReceiptState.Conflict, null, false);
    }

    public Task WritePendingAsync(
        TrainingExecutionRequest request,
        JsonObject rawRequest,
        string requestHash,
        string beforeStateHash,
        long beforeGameTick,
        CancellationToken cancellationToken) =>
        WriteAtomicAsync(
            Path.Combine(root, ReceiptId(request) + ".pending.json"),
            new JsonObject
            {
                ["schema_version"] = "stardewai.product_execution_pending.v1",
                ["receipt_id"] = ReceiptId(request),
                ["request_sha256"] = requestHash,
                ["run_id"] = request.RunId,
                ["queue_id"] = request.QueueId,
                ["queue_item_id"] = request.QueueItemId,
                ["request_nonce"] = request.RequestNonce,
                ["option_id"] = request.OptionId,
                ["request_before_state_hash"] = request.BeforeStateHash,
                ["before_state_hash"] = beforeStateHash,
                ["before_game_tick"] = beforeGameTick,
                ["written_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["request"] = Clone(rawRequest)
            },
            cancellationToken);

    public async Task WriteFinalAsync(
        TrainingExecutionRequest request,
        string requestHash,
        JsonObject response,
        CancellationToken cancellationToken)
    {
        var receiptId = ReceiptId(request);
        await WriteAtomicAsync(
            Path.Combine(root, receiptId + ".final.json"),
            new JsonObject
            {
                ["schema_version"] = "stardewai.product_execution_receipt.v1",
                ["receipt_id"] = receiptId,
                ["request_sha256"] = requestHash,
                ["run_id"] = request.RunId,
                ["queue_id"] = request.QueueId,
                ["queue_item_id"] = request.QueueItemId,
                ["request_nonce"] = request.RequestNonce,
                ["option_id"] = request.OptionId,
                ["completed_at"] = DateTimeOffset.UtcNow.ToString("O"),
                ["response"] = Clone(response)
            },
            cancellationToken);
        var pendingPath = Path.Combine(root, receiptId + ".pending.json");
        if (File.Exists(pendingPath))
            File.Move(pendingPath, pendingPath + ".resolved", overwrite: true);
    }

    public static string Sha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static async Task WriteAtomicAsync(
        string path,
        JsonObject value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        await File.WriteAllTextAsync(
            temporary,
            value.ToJsonString(JsonOptions),
            Encoding.UTF8,
            cancellationToken);
        File.Move(temporary, path, overwrite: true);
    }

    private static JsonObject Clone(JsonObject value) =>
        JsonNode.Parse(value.ToJsonString())!.AsObject();
}

public enum ProductReceiptState
{
    Missing,
    Pending,
    Final,
    Conflict
}

public sealed record ProductReceiptLookup(
    string ReceiptId,
    ProductReceiptState State,
    JsonObject? Response,
    bool IdempotentReplay);
