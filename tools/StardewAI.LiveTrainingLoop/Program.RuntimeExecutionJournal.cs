using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<(JsonObject Execution, RuntimeExecutionRequestRecord Journal)> PostJournaledExecutionRequestAsync(
        HttpClient http, LiveTrainingOptions options, int iteration, int ordinal,
        TrainingExecutionRequest executionRequest, string beforeSnapshotPath)
    {
        var request = JsonSerializer.Serialize(executionRequest, JsonOptions);
        var endpoint = options.ExecutorUrl + options.ExecutorEndpointPath;
        var journal = await RuntimeExecutionRequestJournal.WriteAsync(
            options.SnapshotDir, iteration, ordinal, request, endpoint, beforeSnapshotPath);
        JsonObject execution;
        try { execution = await PostJsonStringAsync(http, endpoint, request); }
        catch (Exception transport) when (options.UseProductExecutor &&
            transport is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            var recovered = await TryRecoverStoredProductExecutionAsync(http, options, iteration, ordinal, executionRequest, journal, transport);
            if (recovered is null) throw;
            execution = recovered;
        }
        execution["execution_request_journal_path"] = journal.JournalPath;
        execution["execution_request_sha256"] = journal.RawRequestSha256;
        execution["execution_request_nonce"] = journal.RequestNonce;
        return (execution, journal);
    }

    private static async Task<JsonObject?> TryRecoverStoredProductExecutionAsync(
        HttpClient http, LiveTrainingOptions options, int iteration, int ordinal,
        TrainingExecutionRequest request, RuntimeExecutionRequestRecord journal, Exception transport)
    {
        var path = Path.Combine(options.SnapshotDir, $"execution-lookup-{iteration:D4}-item-{ordinal:D4}.json");
        transport.Data["execution_request_journal_path"] = journal.JournalPath;
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            var observed = await ProductReceiptRecoveryCommand.LookupAsync(http, journal.JournalPath, options.ExecutorUrl, timeout.Token);
            await using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                await JsonSerializer.SerializeAsync(file, observed, JsonOptions);
            var lookup = observed["lookup_response"] as JsonObject;
            var result = lookup?["response"] as JsonObject;
            if (ReadString(lookup, "status") != "final" || result is null ||
                ReadString(result, "schema_version") != "training_execution_result.v1" ||
                ReadString(result, "run_id") != request.RunId || ReadString(result, "queue_id") != request.QueueId ||
                ReadString(result, "queue_item_id") != request.QueueItemId || ReadString(result, "option_id") != request.OptionId ||
                ReadString(result, "before_state_hash") != request.BeforeStateHash ||
                ReadString(result, "source") != "product_executor" ||
                ReadString(result, "product_request_sha256") != journal.ProductRequestSha256 ||
                ReadString(result, "product_request_before_state_hash") != request.BeforeStateHash)
                throw new InvalidDataException("No final Product result bound to the original request is available.");
            var recovered = (JsonObject)result.DeepClone();
            recovered["product_receipt_recovered_by_lookup"] = true;
            recovered["product_receipt_lookup_path"] = path;
            return recovered;
        }
        catch (Exception lookupFailure) when (lookupFailure is IOException or InvalidDataException or HttpRequestException or TaskCanceledException or
            JsonException or InvalidOperationException)
        {
            transport.Data["product_receipt_lookup_failure"] = lookupFailure.Message;
            if (!File.Exists(path))
            {
                try
                {
                    await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    await JsonSerializer.SerializeAsync(file, new JsonObject
                    {
                        ["schema_version"] = "stardewai.product_receipt_lookup_failure.v1",
                        ["request_journal_path"] = journal.JournalPath, ["error"] = lookupFailure.Message,
                        ["native_dispatch_retried"] = false, ["native_outcome_verified"] = false,
                        ["execution_authorized"] = false, ["formal_training_authorized"] = false
                    }, JsonOptions);
                }
                catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
                {
                    transport.Data["product_receipt_lookup_record_failure"] = failure.Message;
                }
            }
            return null;
        }
    }
}
