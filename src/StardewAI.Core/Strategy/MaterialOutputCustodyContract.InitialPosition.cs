using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Strategy;

namespace StardewAI.Core.Strategy;

public static partial class MaterialOutputCustodyContract
{
    internal static void BindInitialPosition(MaterialReservation row, StrategyCommitmentLedger ledger,
        StrategyCommitmentHistoryEntry creation)
    {
        var initial = new MaterialOutputCustodyInitialPosition
        {
            NodeId = row.NodeId, SlotIndex = row.SlotIndex, SourceStateHash = row.SourceStateHash,
            ReservationRevision = row.Revision
        };
        initial.BindingSha256 = ComputeInitialPositionSha256(row, ledger, initial);
        row.OutputCustody!.InitialPosition = initial;
        creation.OutputCustodyInitialPositionSha256 = initial.BindingSha256;
    }

    public static string ComputeInitialPositionSha256(MaterialReservation row, StrategyCommitmentLedger ledger,
        MaterialOutputCustodyInitialPosition initial)
    {
        var custody = row.OutputCustody ?? throw new ArgumentException("Output custody is required.", nameof(row));
        var json = JsonSerializer.Serialize(new
        {
            initial.SchemaVersion, initial.NodeId, initial.SlotIndex, initial.SourceStateHash, initial.ReservationRevision,
            ledger.LedgerId, ledger.SaveId, ledger.PlayerId, row.ReservationId, row.SourceDecisionId,
            row.GoalId, row.OwnerPlayerId, row.QualifiedItemId, row.Quantity, row.Purpose,
            custody.AcquisitionPortfolioId, custody.AcquisitionRouteSourceDecisionId, custody.ConsumerOptionId,
            custody.FreshTerminalReceiptSha256, custody.CreatedLedgerRevision
        });
        return CustodyTextHash(json);
    }

    private static bool InitialPositionBound(MaterialReservation row, StrategyCommitmentLedger ledger,
        StrategyCommitmentHistoryEntry creation)
    {
        var initial = row.OutputCustody!.InitialPosition;
        return initial is not null && initial.SchemaVersion == "material_output_custody_initial_position.v1" && initial.SlotIndex >= 0 &&
            initial.NodeId == "player:" + ledger.PlayerId && initial.ReservationRevision > 0 &&
            initial.ReservationRevision == creation.CommitmentRevision &&
            !string.IsNullOrWhiteSpace(initial.SourceStateHash) && initial.SourceStateHash == creation.SourceStateHash &&
            initial.BindingSha256 == creation.OutputCustodyInitialPositionSha256 && IsLowerSha256(initial.BindingSha256) &&
            initial.BindingSha256 == ComputeInitialPositionSha256(row, ledger, initial);
    }

    private static string CustodyTextHash(string json)
    {
        using var sha = SHA256.Create();
        return string.Concat(sha.ComputeHash(Encoding.UTF8.GetBytes(json)).Select(value => value.ToString("x2")));
    }
}
