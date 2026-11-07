using System.Globalization;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedTransferSupport
{
    private readonly CropTeacherSeedSupportSource source;
    private readonly ReservationPortfolioCommitRequest preparation;

    private CropTeacherSeedTransferSupport(string runRequestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string supportStepId, string reservationId, string? priorVerifiedSnapshotPath)
    {
        source = new(runRequestPath, snapshotPath, ledgerPath, commitmentId, supportStepId, priorVerifiedSnapshotPath);
        var matches = source.Supply.Commitments.Single().Slots.Where(row => row.ReservationId == reservationId &&
            row.InventoryKind == "chest").ToArray();
        Require(matches.Length == 1, "Crop seed transfer requires one owned chest reservation.");
        var slot = matches[0];
        var (intent, projection) = source.Supply.ProjectTransfer(slot);
        var changes = projection.DestinationSlotChanges.OrderBy(row => row.SlotIndex).ToArray();
        Require(changes.Length > 0 && changes.All(row => row.StackAfter > row.StackBefore) &&
                changes.Sum(row => (long)row.StackAfter - row.StackBefore) == slot.Quantity,
            "Crop seed transfer destination allocation is not conservative.");
        preparation = source.MarkerRequest;
        var claim = source.Ledger.MaterialReservations.Single(row => row.ReservationId == reservationId);
        Require(claim.OutputCustody is null, "Crop seed transfer cannot reinterpret protected output custody.");
        var moves = new List<ReservationPortfolioMaterialRelocation>();
        var partitions = new List<MaterialReservationUpsertRequest>();
        for (var index = 0; index < changes.Length; index++)
        {
            var change = changes[index];
            var id = index == 0 ? reservationId : reservationId + ":transfer-split:" + supportStepId + ":" +
                change.SlotIndex.ToString(CultureInfo.InvariantCulture);
            Require(index == 0 || !source.Ledger.MaterialReservations.Any(row => row.ReservationId == id) &&
                    !source.Ledger.CurrencyReservations.Any(row => row.ReservationId == id) &&
                    !source.Ledger.History.Any(row => row.CommitmentId == id),
                "Crop seed transfer partition identity already exists.");
            var quantity = change.StackAfter - change.StackBefore;
            if (changes.Length > 1)
                partitions.Add(new MaterialReservationUpsertRequest
                {
                    StateHash = preparation.StateHash, ExpectedLedgerRevision = preparation.ExpectedLedgerRevision,
                    ReservationId = id, SourceDecisionId = claim.SourceDecisionId, GoalId = claim.GoalId,
                    NodeId = claim.NodeId, SlotIndex = claim.SlotIndex, QualifiedItemId = claim.QualifiedItemId,
                    Quantity = quantity, Purpose = claim.Purpose
                });
            moves.Add(new ReservationPortfolioMaterialRelocation
            {
                MaterialReservationId = id, SourceNodeId = slot.NodeId, SourceSlotIndex = slot.SlotIndex,
                DestinationNodeId = intent.DestinationNodeId, DestinationSlotIndex = change.SlotIndex,
                QualifiedItemId = slot.QualifiedItemId, Quantity = quantity
            });
        }
        // The original ID sorts first: shrink it before adding conserved partitions in the same atomic commit.
        preparation.MaterialClaims = partitions.ToArray();
        preparation.MaterialTransferPreparation = new MaterialTransferPreparationRequest
        {
            RouteSourceDecisionId = claim.SourceDecisionId, Intent = intent, MaterialRelocations = moves.ToArray()
        };
        var preview = new ReservationPortfolioLedgerService().Commit(source.Ledger, source.Before, preparation, source.Ledger.UpdatedAt);
        Require(preview.Accepted && preview.Ledger?.PreparedMaterialTransfer is not null,
            "Crop seed transfer preparation preflight failed: " + string.Join(",", preview.Errors));
        source.ValidateInputsUnchanged();
    }

    public static CropTeacherSeedTransferSupport Open(string runRequestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string supportStepId, string reservationId, string? priorVerifiedSnapshotPath = null) =>
        new(runRequestPath, snapshotPath, ledgerPath, commitmentId, supportStepId, reservationId, priorVerifiedSnapshotPath);

    public string CommitmentId => source.CommitmentId;
    public string SnapshotPath => source.SnapshotPath;
    public IReadOnlyDictionary<string, string> InputHashes => source.InputHashes;
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(source.Ledger);
    public ReservationPortfolioCommitRequest PreparationRequest
    {
        get { source.ValidateInputsUnchanged(); return CropTeacherSeedSupportSource.Copy(preparation); }
    }

    public CropTeacherSeedTransferMarker VerifyPreparationReply(ReservationPortfolioCommitResult result)
    {
        var ledger = source.VerifyCommit(PreparationRequest, result);
        Require(ledger.PreparedMaterialTransfer is not null, "Crop seed transfer preparation reply is incomplete.");
        return new(this, ledger);
    }

    internal void ValidateInputsUnchanged() => source.ValidateInputsUnchanged();
    internal CropTeacherSeedSupportSource Source => source;
}
