using System.Text.Json.Nodes;

static partial class Program
{
    private static void MarkMaterialReservationSettlementFailed(JsonObject execution, IEnumerable<string> reasons)
    {
        var combined = ReadArrayStrings(execution, "primitive_verification_reasons").Concat(reasons)
            .Distinct(StringComparer.Ordinal).Select(value => (JsonNode?)JsonValue.Create(value)).ToArray();
        execution["primitive_verification_status"] = "reservation_settlement_failed";
        execution["primitive_verification_reasons"] = new JsonArray(combined);
        execution["material_reservation_settlement_status"] = "failed";
        execution["reservation_settlement_retry_required"] = true;
    }
}
