using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal sealed record AcquisitionStrategyLedgerState(
    StrategyCommitmentLedger Ledger,
    long ActorPlayerId);

internal static class AcquisitionStrategyLedgerReader
{
    public static AcquisitionStrategyLedgerState Read(
        string path,
        JsonElement snapshot)
    {
        StrategyCommitmentLedger ledger;
        try
        {
            ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(
                    File.ReadAllText(path),
                    JsonDefaults.Options)
                ?? throw new InvalidDataException(
                    "Strategy commitment ledger is empty.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "Strategy commitment ledger JSON is invalid.",
                ex);
        }

        var saveId = ReadIdentity(snapshot, "save_id");
        var playerId = ReadIdentity(snapshot, "player_id");
        Require(long.TryParse(playerId, out var actorPlayerId),
            "Snapshot player identity is not a native numeric player ID.");
        var materialReservations = ledger.MaterialReservations ??
            throw new InvalidDataException(
                "Strategy commitment material reservations are missing.");
        var currencyReservations = ledger.CurrencyReservations ??
            throw new InvalidDataException(
                "Strategy commitment currency reservations are missing.");
        Require(ledger.SchemaVersion == "strategy_commitment_ledger.v1" &&
                !string.IsNullOrWhiteSpace(ledger.LedgerId) &&
                ledger.SaveId == saveId &&
                ledger.PlayerId == playerId &&
                ledger.Revision >= 0 &&
                !string.IsNullOrWhiteSpace(ledger.SourceStateHash) &&
                ledger.CropPlantingCommitments is not null &&
                ledger.MachineRelocationIntents is not null &&
                ledger.MachineSupportIntents is not null &&
                ledger.History is not null,
            "Strategy commitment ledger metadata or identity is invalid.");

        ValidateMaterialReservations(
            materialReservations,
            actorPlayerId);
        ValidateCurrencyReservations(
            currencyReservations,
            actorPlayerId);
        var allIds = materialReservations
            .Select(row => row.ReservationId)
            .Concat(currencyReservations.Select(row => row.ReservationId))
            .ToArray();
        Require(allIds.Distinct(StringComparer.Ordinal).Count() == allIds.Length,
            "Strategy commitment reservation IDs are not globally unique.");
        return new AcquisitionStrategyLedgerState(ledger, actorPlayerId);
    }

    private static void ValidateMaterialReservations(
        MaterialReservation[] rows,
        long actorPlayerId)
    {
        Require(rows.Select(row => row.ReservationId)
                .Distinct(StringComparer.Ordinal).Count() == rows.Length,
            "Material reservation IDs are not unique.");
        Require(rows.All(row => row is not null &&
                !string.IsNullOrWhiteSpace(row.ReservationId) &&
                row.Revision > 0 &&
                ValidStatus(row.Status) &&
                !string.IsNullOrWhiteSpace(row.SourceDecisionId) &&
                !string.IsNullOrWhiteSpace(row.SourceStateHash) &&
                !string.IsNullOrWhiteSpace(row.GoalId) &&
                row.OwnerPlayerId == actorPlayerId &&
                !string.IsNullOrWhiteSpace(row.NodeId) &&
                row.SlotIndex >= 0 &&
                !string.IsNullOrWhiteSpace(row.QualifiedItemId) &&
                row.Quantity > 0 &&
                !string.IsNullOrWhiteSpace(row.Purpose) &&
                (row.Status != StrategyCommitmentStatuses.Cancelled ||
                 !string.IsNullOrWhiteSpace(row.CancelReason))),
            "A material reservation contract is invalid.");
    }

    private static void ValidateCurrencyReservations(
        CurrencyReservation[] rows,
        long actorPlayerId)
    {
        Require(rows.Select(row => row.ReservationId)
                .Distinct(StringComparer.Ordinal).Count() == rows.Length,
            "Currency reservation IDs are not unique.");
        Require(rows.All(row => row is not null &&
                !string.IsNullOrWhiteSpace(row.ReservationId) &&
                row.Revision > 0 &&
                ValidStatus(row.Status) &&
                !string.IsNullOrWhiteSpace(row.SourceDecisionId) &&
                !string.IsNullOrWhiteSpace(row.SourceStateHash) &&
                !string.IsNullOrWhiteSpace(row.GoalId) &&
                row.OwnerPlayerId == actorPlayerId &&
                NativeShopCurrencies.TryGetKey(
                    row.CurrencyId,
                    out var expectedKey) &&
                row.CurrencyKey == expectedKey &&
                row.Amount > 0 &&
                !string.IsNullOrWhiteSpace(row.Purpose) &&
                (row.Status != StrategyCommitmentStatuses.Cancelled ||
                 !string.IsNullOrWhiteSpace(row.CancelReason))),
            "A native currency reservation contract is invalid.");
    }

    private static bool ValidStatus(string status) =>
        status == StrategyCommitmentStatuses.Active ||
        status == StrategyCommitmentStatuses.Cancelled ||
        status == StrategyCommitmentStatuses.Completed;

    private static string ReadIdentity(JsonElement snapshot, string field)
    {
        var topValue = string.Empty;
        var stateValue = string.Empty;
        Require(snapshot.TryGetProperty(field, out var topEnvelope) &&
                TryReadEnvelopeString(topEnvelope, out topValue) &&
                snapshot.TryGetProperty("state", out var state) &&
                state.ValueKind == JsonValueKind.Object &&
                state.TryGetProperty("identity", out var identity) &&
                identity.ValueKind == JsonValueKind.Object &&
                identity.TryGetProperty(field, out var stateEnvelope) &&
                TryReadEnvelopeString(stateEnvelope, out stateValue) &&
                topValue == stateValue,
            "Snapshot " + field + " identity is missing or drifted.");
        return topValue;
    }

    private static bool TryReadEnvelopeString(
        JsonElement envelope,
        out string value)
    {
        value = string.Empty;
        if (envelope.ValueKind != JsonValueKind.Object ||
            !envelope.TryGetProperty("status", out var status) ||
            status.ValueKind != JsonValueKind.String ||
            status.GetString() != "available" ||
            !envelope.TryGetProperty("value", out var fieldValue) ||
            fieldValue.ValueKind != JsonValueKind.String)
        {
            return false;
        }
        value = fieldValue.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
