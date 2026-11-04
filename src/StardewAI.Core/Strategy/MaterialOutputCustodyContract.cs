using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

public static partial class MaterialOutputCustodyContract
{
    public const string MuseumConsumer = "executor.donate_museum_item";
    public const string CreatedOperation = "material_output_custody_created";

    public static string PortfolioId(string acquisitionPortfolioId, string routeSourceDecisionId,
        string consumerOptionId, string qualifiedItemId) => "output-custody:" +
        string.Join(":", new[] { acquisitionPortfolioId, routeSourceDecisionId, consumerOptionId, qualifiedItemId }
            .Select(Uri.EscapeDataString));

    public static string ReservationId(string custodyPortfolioId) => custodyPortfolioId + ":material";

    public static bool IsAuthorized(MaterialReservation row, StrategyCommitmentLedger ledger,
        string consumerOptionId, string? goalId = null) =>
        TryReadCreation(row, ledger, consumerOptionId, goalId, out var creation) &&
        PositionMatches(row, ledger, creation!, out _);

    public static bool TryGetPositionBinding(MaterialReservation row, StrategyCommitmentLedger ledger, out string bindingSha256)
    {
        bindingSha256 = string.Empty;
        return row.OutputCustody?.InitialPosition is not null &&
            TryReadCreation(row, ledger, MuseumConsumer, null, out var creation) &&
            PositionMatches(row, ledger, creation!, out bindingSha256);
    }

    private static bool TryReadCreation(MaterialReservation row, StrategyCommitmentLedger ledger,
        string consumerOptionId, string? goalId, out StrategyCommitmentHistoryEntry? creationEntry)
    {
        creationEntry = null;
        var custody = row.OutputCustody;
        if (custody is null || row.Status != StrategyCommitmentStatuses.Active || row.Quantity != 1 ||
            row.Revision < 1 || custody.CreatedLedgerRevision < 1 || custody.CreatedLedgerRevision > ledger.Revision ||
            string.IsNullOrWhiteSpace(custody.AcquisitionPortfolioId) ||
            string.IsNullOrWhiteSpace(custody.AcquisitionRouteSourceDecisionId) ||
            string.IsNullOrWhiteSpace(row.GoalId) || string.IsNullOrWhiteSpace(row.QualifiedItemId) ||
            ledger.History is null || goalId is not null && row.GoalId != goalId ||
            consumerOptionId != MuseumConsumer || custody.ConsumerOptionId != consumerOptionId ||
            !IsLowerSha256(custody.FreshTerminalReceiptSha256))
            return false;
        var portfolioId = PortfolioId(custody.AcquisitionPortfolioId, custody.AcquisitionRouteSourceDecisionId,
            custody.ConsumerOptionId, row.QualifiedItemId);
        var creation = ledger.History.Where(entry => entry.CommitmentId == row.ReservationId &&
                entry.SourceDecisionId == portfolioId && entry.Operation == CreatedOperation &&
                entry.LedgerRevision == custody.CreatedLedgerRevision &&
                entry.Reason == custody.FreshTerminalReceiptSha256).ToArray();
        var valid = row.SourceDecisionId == portfolioId && row.ReservationId == ReservationId(portfolioId) &&
            creation.Length == 1 &&
            ledger.History.Count(entry => entry.CommitmentId == portfolioId && entry.SourceDecisionId == portfolioId &&
                entry.Operation == "reservation_portfolio_commit" && entry.LedgerRevision == custody.CreatedLedgerRevision) == 1;
        if (valid) creationEntry = creation[0];
        return valid;
    }

    public static bool IsLowerSha256(string value) => value is { Length: 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    public static bool TryMuseumDonated(SnapshotEnvelope snapshot, string qualifiedItemId, out bool donated)
    {
        donated = false;
        var museum = ReadStateFieldValue(snapshot, "world_progress", "museum");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "world_progress", "museum")) ||
            museum is not { ValueKind: JsonValueKind.Object } ||
            !museum.Value.TryGetProperty("donatable_items", out var rows) || rows.ValueKind != JsonValueKind.Array)
            return false;
        var matches = rows.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            ReadString(row, "qualified_item_id") == qualifiedItemId).ToArray();
        if (matches.Length != 1 || !matches[0].TryGetProperty("donated", out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) return false;
        donated = value.GetBoolean();
        return true;
    }
}
