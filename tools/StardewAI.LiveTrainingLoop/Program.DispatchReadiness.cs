using System.Net.Http.Json;
using System.Text.Json.Nodes;

static partial class Program
{
    private static async Task<JsonObject?> ReadDispatchReadinessAsync(
        HttpClient http,
        LiveTrainingOptions options,
        JsonObject item,
        string stateHash,
        string queueId)
    {
        var optionId = ReadStringOrEmpty(item, "option_id");
        if (!string.Equals(
                optionId,
                "executor.craft_machine_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                optionId,
                "executor.craft_storage_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                optionId,
                "executor.place_machine",
                StringComparison.Ordinal) &&
            !string.Equals(
                optionId,
                "executor.place_storage",
                StringComparison.Ordinal))
        {
            return null;
        }

        var queueItemId = ReadStringOrEmpty(item, "queue_item_id");
        if (string.IsNullOrWhiteSpace(queueItemId))
        {
            throw new InvalidOperationException(
                "material-ledger guarded queue item did not include queue_item_id");
        }
        var url = options.BackendUrl +
            "/api/v1/action-queues/" + Uri.EscapeDataString(queueId) +
            "/items/" + Uri.EscapeDataString(queueItemId) +
            "/dispatch-readiness?stateHash=" + Uri.EscapeDataString(stateHash);
        return await http.GetFromJsonAsync<JsonObject>(url) ??
            throw new InvalidOperationException(
                "dispatch readiness endpoint returned an empty response");
    }

    private static JsonObject DispatchRejectedExecution(
        JsonObject item,
        JsonObject readiness,
        string queueId,
        string stateHash,
        string reason)
    {
        var blockingReasons = readiness["blocking_reasons"] is JsonArray reasons
            ? JsonNode.Parse(reasons.ToJsonString())?.AsArray() ?? new JsonArray()
            : new JsonArray();
        blockingReasons.Add(reason);
        return new JsonObject
        {
            ["schema_version"] = "training_execution_result.v1",
            ["feedback_available"] = true,
            ["status"] = "blocked",
            ["failure_category"] = "controller_dispatch_guard",
            ["primitive_verification_status"] = "dispatch_rejected",
            ["primitive_verification_reasons"] = blockingReasons,
            ["effective_queue_id"] = queueId,
            ["queue_id"] = queueId,
            ["queue_item_id"] = ReadStringOrEmpty(item, "queue_item_id"),
            ["option_id"] = ReadStringOrEmpty(item, "option_id"),
            ["effective_before_state_hash"] = stateHash,
            ["effective_queue_item"] = JsonNode.Parse(item.ToJsonString()),
            ["dispatch_readiness"] = JsonNode.Parse(readiness.ToJsonString()),
            ["source"] = "controller_dispatch_guard"
        };
    }
}
