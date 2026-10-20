using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record FullShipmentExpectedResourceMutationInvariant(
    string ContractVersion,
    string SaveId,
    string PlayerId,
    string GameVersion,
    int TotalDay,
    string UnrelatedPersistentStateSha256);

internal static partial class FullShipmentRecoverySettlementBoundaryProjector
{
    internal static bool TryProjectExpectedResourceMutationInvariant(
        SnapshotEnvelope snapshot,
        out FullShipmentExpectedResourceMutationInvariant? projection)
    {
        projection = null;
        if (!ReadableIdentity(snapshot.SaveId, out var saveId) ||
            !ReadableIdentity(snapshot.PlayerId, out var playerId) ||
            string.IsNullOrWhiteSpace(snapshot.GameVersion) ||
            !TryStateValue(snapshot, "time", "total_days", out var totalDay) ||
            !totalDay.TryGetInt32(out var totalDayValue) ||
            totalDayValue < 0)
        {
            return false;
        }

        var rows = new List<KeyValuePair<string, JsonElement>>();
        foreach (var domain in snapshot.State.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            if (VolatileDomains.Contains(domain.Key))
                continue;
            if (domain.Value.ValueKind != JsonValueKind.Object)
            {
                rows.Add(new KeyValuePair<string, JsonElement>(
                    domain.Key,
                    domain.Value.Clone()));
                continue;
            }

            foreach (var field in domain.Value.EnumerateObject()
                         .OrderBy(value => value.Name, StringComparer.Ordinal))
            {
                if (IsVolatile(domain.Key, field.Name) ||
                    ExpectedResourceMutationField(domain.Key, field.Name))
                {
                    continue;
                }
                rows.Add(new KeyValuePair<string, JsonElement>(
                    domain.Key + "." + field.Name,
                    NormalizeExpectedResourceMutationEnvelope(
                        domain.Key,
                        field.Name,
                        field.Value)));
            }
        }

        projection = new FullShipmentExpectedResourceMutationInvariant(
            "full_shipment_expected_resource_mutation_invariant.v1",
            saveId,
            playerId,
            snapshot.GameVersion,
            totalDayValue,
            HashCanonical(rows));
        return true;
    }

    private static bool ExpectedResourceMutationField(
        string domain,
        string field) =>
        domain == "player" && field is
            "inventory" or
            "inventory_capacity" or
            "money" or
            "multiplayer_wallet" or
            "seed_inventory" or
            "shop_currency_balances" ||
        domain == "farm" && field == "material_inventory_graph";

    private static JsonElement NormalizeExpectedResourceMutationEnvelope(
        string domain,
        string field,
        JsonElement envelope)
    {
        var normalizedEnvelope = NormalizePersistentEnvelope(
            domain,
            field,
            envelope);
        if (normalizedEnvelope.ValueKind != JsonValueKind.Object)
            return normalizedEnvelope;
        var normalized = JsonNode.Parse(normalizedEnvelope.GetRawText())
            as JsonObject ?? throw new InvalidDataException(
                $"Expected resource mutation field {domain}.{field} is invalid.");
        if (normalized["value"] is not JsonNode value)
            return JsonSerializer.SerializeToElement(normalized);

        switch (domain, field)
        {
            case ("player", "prize_ticket_reward"):
                RemoveFields(value,
                    "inventory_occupied_slots",
                    "pending_ticket_capacity_sufficient",
                    "projection_fingerprint");
                break;
            case ("quests", "adventure_guild_reward"):
                RemoveFields(value,
                    "inventory_occupied_slots",
                    "inventory_capacity_sufficient");
                break;
            case ("quests", "mailbox_processing"):
                RemoveFields(value,
                    "inventory_empty_slots",
                    "attachment_capacity_sufficient");
                break;
            case ("world_progress", "community_center"):
                NormalizeCommunityCenterResourceProjection(value);
                break;
            case ("world_progress", "joja_development"):
                NormalizeJojaResourceProjection(value);
                break;
            case ("world_progress", "marriage_house"):
                NormalizeMarriageHouseResourceProjection(value);
                break;
        }
        return JsonSerializer.SerializeToElement(normalized);
    }

    private static void NormalizeCommunityCenterResourceProjection(
        JsonNode value)
    {
        if (value["bundle_rows"] is not JsonArray rows)
            return;
        foreach (var payment in rows.OfType<JsonObject>()
                     .Select(row => row["money_payment"])
                     .OfType<JsonObject>())
        {
            RemoveFields(payment,
                "money_before",
                "money_after",
                "affordable",
                "action_status");
        }
    }

    private static void NormalizeJojaResourceProjection(JsonNode value)
    {
        RemoveFields(value, "money", "membership_action_status");
        if (value["projects"] is not JsonArray projects)
            return;
        foreach (var project in projects.OfType<JsonObject>())
            project.Remove("action_status");
    }

    private static void NormalizeMarriageHouseResourceProjection(
        JsonNode value)
    {
        RemoveFields(value, "money");
        if (value["house_upgrade"] is JsonObject upgrade)
            upgrade.Remove("action_status");
        if (value["home_renovations"] is not JsonObject renovations ||
            renovations["options"] is not JsonArray options)
        {
            return;
        }
        foreach (var option in options.OfType<JsonObject>())
        {
            RemoveFields(option,
                "money_before",
                "expected_money_after",
                "availability_status",
                "availability_block_reasons",
                "projection_fingerprint");
        }
    }

    private static void RemoveFields(JsonNode value, params string[] fields)
    {
        if (value is not JsonObject objectValue)
            return;
        foreach (var field in fields)
            objectValue.Remove(field);
    }
}
