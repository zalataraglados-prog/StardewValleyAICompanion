using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedPlantingSupport
{
    internal CropTeacherSeedSupportSource Source { get; }

    private CropTeacherSeedPlantingSupport(string requestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string stepId)
    {
        Source = new(requestPath, snapshotPath, ledgerPath, commitmentId, stepId, null);
        var crop = Source.Supply.Commitments.Single();
        var layout = Source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == commitmentId);
        Require(Source.Ledger.PreparedMaterialTransfer is null && Source.TotalDay == crop.PlantingTotalDay &&
                crop.PlayerQuantity > 0 && layout.Tiles.Distinct().Count() == layout.Tiles.Length,
            "Owned seed planting requires settled player custody, its original planting day and unique layout.");
    }

    public static CropTeacherSeedPlantingSupport Open(string requestPath, string snapshotPath, string ledgerPath,
        string commitmentId, string stepId) => new(requestPath, snapshotPath, ledgerPath, commitmentId, stepId);

    public string CommitmentId => Source.CommitmentId;
    public int TotalDay => Source.TotalDay;
    public string SnapshotPath => Source.SnapshotPath;
    public StrategyCommitmentLedger BaseLedger => CropTeacherSeedSupportSource.Copy(Source.Ledger);
    public ReservationPortfolioCommitRequest MarkerRequest => Source.MarkerRequest;
    public void ValidateInputsUnchanged() => Source.ValidateInputsUnchanged();

    public CropTeacherSeedPlantingMarker VerifyMarkerReply(ReservationPortfolioCommitResult reply) =>
        new(this, Source.VerifyCommit(MarkerRequest, reply));
}
