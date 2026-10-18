using System.Text.Json;
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
                    NormalizePersistentEnvelope(
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
            "inventory" or "money" or "shop_currency_balances" ||
        domain == "farm" && field == "material_inventory_graph";
}
