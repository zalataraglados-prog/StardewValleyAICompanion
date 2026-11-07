using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

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
    public IReadOnlyDictionary<string, string> InputHashes => Source.InputHashes;
    public ReservationPortfolioCommitRequest MarkerRequest => Source.MarkerRequest;
    public void ValidateInputsUnchanged() => Source.ValidateInputsUnchanged();

    public CropTeacherSeedPlantingMarker VerifyMarkerReply(ReservationPortfolioCommitResult reply) =>
        new(this, Source.VerifyCommit(MarkerRequest, reply));

    public MoneyRevenueCropNativeProductionActionBatch ValidateExecutionPreflight(
        MoneyRevenueCropNativeProductionActionBatch requested, string? reservationId = null)
    {
        ValidateInputsUnchanged();
        var preview = new ReservationPortfolioLedgerService().Commit(BaseLedger, Source.Before, MarkerRequest, Source.Ledger.UpdatedAt);
        var marker = VerifyMarkerReply(preview);
        var actions = marker.BuildActions(requested, reservationId);
        var queue = new ActionQueueCompiler().Compile(new SmallModelActionEnvelope
        {
            ModelOutputId = "crop_seed_planting_readonly_preflight", SourceModel = "deterministic.preflight",
            StateHash = Source.Before.StateHash, GoalId = "earn_money",
            Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer), Actions = actions
        }, Source.Before, marker.Ledger);
        Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items.Length == actions.Length &&
                queue.Items.All(item => item.BlockingReasons.Length == 0 && item.MissingStateFactors.Length == 0),
            "Owned planting original compiler preflight rejected: " + string.Join(",", queue.CompilerDiagnostics
                .Concat(queue.Items.SelectMany(item => item.BlockingReasons.Concat(item.MissingStateFactors))).Distinct()));
        var parameters = actions[0].Parameters;
        Require(CropSeedPlantingBinding.TryInventory(parameters, out var inventory), "Owned planting preflight inventory binding is missing.");
        var crop = Source.Ledger.CropPlantingCommitments.Single(row => row.CommitmentId == CommitmentId);
        CropSeedPlantingGrowthPreflight.Validate(Source.Before, marker.BoundBatch, inventory!.InventorySlotIndex!.Value, crop.SeedId, crop.BaseGrowDays);
        ValidateInputsUnchanged();
        return marker.BoundBatch;
    }
}
