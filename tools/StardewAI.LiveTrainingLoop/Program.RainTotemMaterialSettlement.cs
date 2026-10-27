using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task SettleRainTotemMaterialReservationsAsync(
        HttpClient http,
        LiveTrainingOptions options,
        JsonObject item,
        JsonObject execution,
        JsonObject afterSnapshot)
    {
        if (!string.Equals(
                ReadString(item, "option_id"),
                "executor.craft_rain_totem",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(execution, "status"),
                "applied",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(execution, "primitive_verification_status"),
                "verified",
                StringComparison.Ordinal) ||
            execution["after_snapshot_fresh"]?.GetValue<bool>() != true)
        {
            return;
        }

        try
        {
            var typedItem = JsonSerializer.Deserialize<ActionQueueItem>(
                    item.ToJsonString(JsonOptions),
                    JsonOptions) ??
                throw new InvalidDataException(
                    "rain_totem_settlement_queue_item_invalid");
            var executionReceiptSha256 = Convert.ToHexString(
                    SHA256.HashData(Encoding.UTF8.GetBytes(
                        execution.ToJsonString(JsonOptions))))
                .ToLowerInvariant();
            var build =
                RainTotemMaterialReservationSettlementRequestBuilder.Build(
                    typedItem,
                    ReadString(execution, "effective_before_state_hash"),
                    ReadString(afterSnapshot, "state_hash"),
                    executionReceiptSha256);
            if (!build.Valid || build.Request is null)
            {
                MarkRainTotemMaterialSettlementFailed(
                    execution,
                    build.Errors.Length == 0
                        ? ["rain_totem_material_settlement_request_invalid"]
                        : build.Errors);
                return;
            }

            var response = await PostJsonStringAsync(
                http,
                options.BackendUrl +
                "/api/v1/strategy/commitments/" +
                "reservation-portfolios/settle-supporting-transition",
                JsonSerializer.Serialize(build.Request, JsonOptions));
            var expectedIds = build.Request.MaterialConsumptions
                .Select(row => row.MaterialReservationId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var completedIds = ReadArrayStrings(
                    response,
                    "completed_material_reservation_ids")
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (response["accepted"]?.GetValue<bool>() != true ||
                !completedIds.SequenceEqual(
                    expectedIds,
                    StringComparer.Ordinal))
            {
                MarkRainTotemMaterialSettlementFailed(
                    execution,
                    ["rain_totem_material_settlement_rejected"]);
                execution["material_reservation_settlement_response"] =
                    JsonNode.Parse(response.ToJsonString(JsonOptions));
                return;
            }

            execution["material_reservation_settlement_status"] =
                "completed";
            execution["material_reservation_settlement_receipt_sha256"] =
                build.Request.SupportingTransitionReceiptSha256;
            execution["material_reservation_settlement_completed_ids"] =
                new JsonArray(completedIds.Select(value =>
                    (JsonNode?)JsonValue.Create(value)).ToArray());
            execution["material_reservation_settlement_ledger_revision"] =
                response["committed_ledger_revision"]?.DeepClone();
        }
        catch (Exception exception)
        {
            MarkRainTotemMaterialSettlementFailed(
                execution,
                ["rain_totem_material_settlement_exception:" +
                 exception.GetType().Name]);
        }
    }

    private static void MarkRainTotemMaterialSettlementFailed(
        JsonObject execution,
        IEnumerable<string> reasons)
    {
        var combined = ReadArrayStrings(
                execution,
                "primitive_verification_reasons")
            .Concat(reasons)
            .Distinct(StringComparer.Ordinal)
            .Select(value => (JsonNode?)JsonValue.Create(value))
            .ToArray();
        execution["primitive_verification_status"] =
            "reservation_settlement_failed";
        execution["primitive_verification_reasons"] =
            new JsonArray(combined);
        execution["material_reservation_settlement_status"] = "failed";
        execution["reservation_settlement_retry_required"] = true;
    }
}
