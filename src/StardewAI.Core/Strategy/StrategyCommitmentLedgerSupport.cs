using System;
using System.Collections.Generic;
using System.Linq;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Strategy;

internal static class StrategyCommitmentLedgerSupport
{
    internal static List<string> ValidateCommon(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        string stateHash,
        int expectedRevision)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(stateHash) ||
            !string.Equals(stateHash, snapshot.StateHash, StringComparison.Ordinal))
        {
            errors.Add("state_hash_mismatch");
        }
        if (expectedRevision != (current?.Revision ?? 0))
        {
            errors.Add("ledger_revision_conflict");
        }
        if (string.IsNullOrWhiteSpace(SaveId(snapshot)) ||
            string.IsNullOrWhiteSpace(PlayerId(snapshot)))
        {
            errors.Add("snapshot_identity_unavailable");
        }
        if (current is not null &&
            (!string.Equals(current.SaveId, SaveId(snapshot), StringComparison.Ordinal) ||
             !string.Equals(current.PlayerId, PlayerId(snapshot), StringComparison.Ordinal)))
        {
            errors.Add("ledger_identity_mismatch");
        }
        return errors;
    }

    internal static StrategyCommitmentLedger CloneOrCreate(
        StrategyCommitmentLedger? current,
        SnapshotEnvelope snapshot,
        string updatedAt) => new()
        {
            LedgerId = current?.LedgerId ??
                "strategy-ledger:" + SaveId(snapshot) + ":" + PlayerId(snapshot),
            SaveId = SaveId(snapshot),
            PlayerId = PlayerId(snapshot),
            Revision = current?.Revision ?? 0,
            UpdatedAt = updatedAt,
            SourceStateHash = snapshot.StateHash,
            CropPlantingCommitments = current?.CropPlantingCommitments
                .Select(CloneCrop)
                .ToArray() ?? Array.Empty<CropPlantingCommitment>(),
            MaterialReservations = current?.MaterialReservations
                .Select(CloneMaterial)
                .ToArray() ?? Array.Empty<MaterialReservation>(),
            History = current?.History
                .Select(CloneHistory)
                .ToArray() ?? Array.Empty<StrategyCommitmentHistoryEntry>()
        };

    internal static CropPlantingCommitment CloneCrop(CropPlantingCommitment row) =>
        CloneCrop(row, row.Status, row.Revision, row.CancelReason);

    internal static CropPlantingCommitment CloneCrop(
        CropPlantingCommitment row,
        string status,
        int revision,
        string cancelReason) => new()
        {
            CommitmentId = row.CommitmentId,
            Revision = revision,
            Status = status,
            SourceDecisionId = row.SourceDecisionId,
            SourceStateHash = row.SourceStateHash,
            LocationContext = row.LocationContext,
            SeedId = row.SeedId,
            HarvestItemId = row.HarvestItemId,
            HarvestItemQualifiedId = row.HarvestItemQualifiedId,
            HarvestContextTags = (row.HarvestContextTags ?? Array.Empty<string>()).ToArray(),
            TileCount = row.TileCount,
            PlantingYear = row.PlantingYear,
            PlantingSeason = row.PlantingSeason,
            PlantingDayOfMonth = row.PlantingDayOfMonth,
            PlantingTotalDay = row.PlantingTotalDay,
            BaseGrowDays = row.BaseGrowDays,
            FirstHarvestTotalDay = row.FirstHarvestTotalDay,
            RegrowDays = row.RegrowDays,
            LastInSeasonHarvestTotalDay = row.LastInSeasonHarvestTotalDay,
            MinimumUnitsPerWave = row.MinimumUnitsPerWave,
            ProjectionStatus = row.ProjectionStatus,
            ProjectionCondition = row.ProjectionCondition,
            CancelReason = cancelReason
        };

    internal static MaterialReservation CloneMaterial(MaterialReservation row) => new()
    {
        ReservationId = row.ReservationId,
        Revision = row.Revision,
        Status = row.Status,
        SourceDecisionId = row.SourceDecisionId,
        SourceStateHash = row.SourceStateHash,
        GoalId = row.GoalId,
        OwnerPlayerId = row.OwnerPlayerId,
        NodeId = row.NodeId,
        SlotIndex = row.SlotIndex,
        QualifiedItemId = row.QualifiedItemId,
        Quantity = row.Quantity,
        Purpose = row.Purpose,
        CancelReason = row.CancelReason
    };

    internal static void Advance(
        StrategyCommitmentLedger ledger,
        SnapshotEnvelope snapshot,
        string updatedAt)
    {
        ledger.Revision++;
        ledger.SourceStateHash = snapshot.StateHash;
        ledger.UpdatedAt = updatedAt;
    }

    internal static void AppendHistory(
        StrategyCommitmentLedger ledger,
        string commitmentId,
        int commitmentRevision,
        string sourceDecisionId,
        string operation,
        string recordedAt,
        string reason)
    {
        ledger.History = ledger.History.Append(new StrategyCommitmentHistoryEntry
        {
            LedgerRevision = ledger.Revision,
            CommitmentId = commitmentId,
            CommitmentRevision = commitmentRevision,
            Operation = operation,
            SourceDecisionId = sourceDecisionId,
            SourceStateHash = ledger.SourceStateHash,
            RecordedAt = recordedAt,
            Reason = reason
        }).ToArray();
    }

    internal static string SaveId(SnapshotEnvelope snapshot) =>
        snapshot.SaveId.Value ?? ReadStateFieldString(snapshot, "identity", "save_id");

    internal static string PlayerId(SnapshotEnvelope snapshot) =>
        snapshot.PlayerId.Value ?? ReadStateFieldString(snapshot, "identity", "player_id");

    private static StrategyCommitmentHistoryEntry CloneHistory(StrategyCommitmentHistoryEntry row) => new()
    {
        LedgerRevision = row.LedgerRevision,
        CommitmentId = row.CommitmentId,
        CommitmentRevision = row.CommitmentRevision,
        Operation = row.Operation,
        SourceDecisionId = row.SourceDecisionId,
        SourceStateHash = row.SourceStateHash,
        RecordedAt = row.RecordedAt,
        Reason = row.Reason
    };
}
