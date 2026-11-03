using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public static class ProductReceiptRecoveryCommand
{
    public static async Task<int> RunAsync(string[] arguments)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < arguments.Length; index += 2)
        {
            if (index + 1 >= arguments.Length || arguments[index] is not ("--request-journal" or "--product-url" or "--output") ||
                !values.TryAdd(arguments[index], arguments[index + 1]))
                throw new ArgumentException("Receipt lookup requires one request-journal, product-url and output.");
        }
        if (values.Count != 3) throw new ArgumentException("Receipt lookup requires request-journal, product-url and output.");
        var output = Path.GetFullPath(values["--output"]);
        if (File.Exists(output)) throw new IOException("Receipt lookup output already exists; refusing overwrite.");
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        var result = await LookupAsync(http, values["--request-journal"], values["--product-url"]);
        Directory.CreateDirectory(Path.GetDirectoryName(output)!);
        await using var file = new FileStream(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        await JsonSerializer.SerializeAsync(file, result, new JsonSerializerOptions { WriteIndented = true });
        Console.WriteLine("Product receipt lookup: " + result["lookup_response"]?["status"]?.GetValue<string>());
        return result["lookup_response"]?["status"]?.GetValue<string>() == "final" ? 0 : 2;
    }

    public static async Task<JsonObject> LookupAsync(HttpClient http, string journalPath, string productUrl,
        CancellationToken cancellationToken = default)
    {
        var (record, request) = await RuntimeExecutionRequestJournal.ReadAsync(journalPath);
        if (!Uri.TryCreate(productUrl.TrimEnd('/') + "/api/v1/product/execute", UriKind.Absolute, out var expected) ||
            expected.Scheme is not ("http" or "https") || expected.UserInfo.Length != 0 || expected.Query.Length != 0 || expected.Fragment.Length != 0 ||
            !Uri.TryCreate(record.ExecutorUrl, UriKind.Absolute, out var original) || original != expected)
            throw new InvalidDataException("Receipt lookup target does not match the original Product execution endpoint.");
        var lookupUri = new Uri(productUrl.TrimEnd('/') + "/api/v1/product/receipts/lookup");
        using var content = new StringContent(request, System.Text.Encoding.UTF8, "application/json");
        using var response = await http.PostAsync(lookupUri, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = JsonNode.Parse(await response.Content.ReadAsStringAsync(cancellationToken)) as JsonObject
            ?? throw new InvalidDataException("Receipt lookup response must be an object.");
        var status = result["status"]?.GetValue<string>();
        if (result["schema_version"]?.GetValue<string>() != "stardewai.product_receipt_lookup.v1" ||
            status is not ("final" or "pending" or "missing" or "conflict" or "corrupt" or "rejected") ||
            result["native_dispatch_performed"]?.GetValue<bool>() != false || result["execution_authorized"]?.GetValue<bool>() != false ||
            result["fresh_verification_required"]?.GetValue<bool>() != true ||
            (status != "rejected" && result["request_sha256"]?.GetValue<string>() != record.ProductRequestSha256))
            throw new InvalidDataException("Receipt lookup response does not bind the request and read-only policy.");
        return new JsonObject
        {
            ["schema_version"] = "stardewai.product_receipt_recovery_observation.v1",
            ["request_journal_path"] = record.JournalPath, ["request_sha256"] = record.RawRequestSha256,
            ["run_id"] = record.RunId, ["queue_id"] = record.QueueId, ["queue_item_id"] = record.QueueItemId,
            ["lookup_response"] = result, ["query_observation_only"] = true, ["native_outcome_verified"] = false,
            ["execution_authorized"] = false, ["formal_training_authorized"] = false
        };
    }
}
