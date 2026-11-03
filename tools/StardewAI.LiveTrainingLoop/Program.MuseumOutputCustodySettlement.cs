using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;

static partial class Program
{
    private static async Task SettleMuseumOutputCustodyAsync(HttpClient http, LiveTrainingOptions options,
        JsonObject item, JsonObject execution, JsonObject beforeSnapshot, JsonObject afterSnapshot)
    {
        if (ReadString(item, "option_id") != MaterialOutputCustodyContract.MuseumConsumer ||
            string.IsNullOrWhiteSpace(ReadQueueParameterString(item, "museum_output_custody_reservation_id")) ||
            ReadString(execution, "status") != "applied" ||
            ReadString(execution, "primitive_verification_status") != "verified" ||
            execution["after_snapshot_fresh"]?.GetValue<bool>() != true) return;
        try
        {
            var ledger = await http.GetFromJsonAsync<StrategyCommitmentLedger>(options.BackendUrl +
                "/api/v1/strategy/commitments/latest?stateHash=" + Uri.EscapeDataString(ReadString(afterSnapshot, "state_hash"))) ??
                throw new InvalidDataException("museum_custody_settlement_ledger_unavailable");
            var build = MuseumDonationCustodySettlementRequestBuilder.Build(
                item.Deserialize<ActionQueueItem>(JsonOptions)!, execution.Deserialize<QueueExecutionStepReceipt>(JsonOptions)!,
                MuseumCustodySnapshotProjection(beforeSnapshot), MuseumCustodySnapshotProjection(afterSnapshot), ledger);
            if (build.Request is null)
            {
                MarkMaterialReservationSettlementFailed(execution, build.Errors);
                return;
            }
            var response = await PostJsonStringAsync(http, options.BackendUrl +
                "/api/v1/strategy/commitments/reservation-portfolios/settle-supporting-transition",
                JsonSerializer.Serialize(build.Request, JsonOptions));
            var ids = ReadArrayStrings(response, "completed_material_reservation_ids").Order(StringComparer.Ordinal).ToArray();
            if (response["accepted"]?.GetValue<bool>() != true ||
                !ids.SequenceEqual(build.Request.MaterialConsumptions.Select(row => row.MaterialReservationId).Order(StringComparer.Ordinal)))
            {
                MarkMaterialReservationSettlementFailed(execution, new[] { "museum_output_custody_settlement_rejected" });
                execution["material_reservation_settlement_response"] = response.DeepClone();
                return;
            }
            execution["material_reservation_settlement_status"] = "completed";
            execution["material_reservation_settlement_receipt_sha256"] = build.Request.SupportingTransitionReceiptSha256;
            execution["material_reservation_settlement_completed_ids"] = new JsonArray(ids.Select(value => (JsonNode?)JsonValue.Create(value)).ToArray());
            execution["material_reservation_settlement_ledger_revision"] = response["committed_ledger_revision"]?.DeepClone();
        }
        catch (Exception exception)
        {
            MarkMaterialReservationSettlementFailed(execution, new[] { "museum_output_custody_settlement_exception:" + exception.GetType().Name });
        }
    }

    private static SnapshotEnvelope MuseumCustodySnapshotProjection(JsonObject snapshot)
    {
        // Reuse ingested snapshot identities; serialize only the three consumed field groups, never the full snapshot.
        var fields = new[]
        {
            (Section: "identity", Names: new[] { "save_id", "player_id" }),
            (Section: "player", Names: new[] { "inventory" }),
            (Section: "world_progress", Names: new[] { "museum" })
        };
        return new SnapshotEnvelope
        {
            StateHash = ReadString(snapshot, "state_hash"), GameTick = ReadLong(snapshot, "game_tick"),
            State = fields.ToDictionary(group => group.Section, group => JsonSerializer.SerializeToElement(
                group.Names.Where(name => snapshot["state"]?[group.Section]?[name] is not null)
                    .ToDictionary(name => name, name => snapshot["state"]![group.Section]![name]!.Deserialize<JsonElement>(JsonOptions))))
        };
    }
}
