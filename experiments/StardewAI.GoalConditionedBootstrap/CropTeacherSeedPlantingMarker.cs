using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedPlantingMarker
{
    private readonly CropTeacherSeedPlantingSupport scope;
    private readonly StrategyCommitmentLedger ledger;
    private SmallModelAction[]? actions;
    private MoneyRevenueCropNativeProductionActionBatch? batch;

    internal CropTeacherSeedPlantingMarker(CropTeacherSeedPlantingSupport scope, StrategyCommitmentLedger ledger)
    {
        this.scope = scope;
        this.ledger = ledger;
    }

    public StrategyCommitmentLedger Ledger => CropTeacherSeedSupportSource.Copy(ledger);
    public MoneyRevenueCropNativeProductionActionBatch BoundBatch => CropTeacherSeedSupportSource.Copy(batch ??
        throw new InvalidOperationException("Owned planting batch is not bound."));

    public SmallModelAction[] BuildActions(MoneyRevenueCropNativeProductionActionBatch requestedBatch, string? reservationId = null)
    {
        scope.ValidateInputsUnchanged();
        Require(actions is null, "Owned seed planting marker already has a bound batch.");
        var source = scope.Source;
        var layout = source.RunRequest.LayoutIntents.Single(row => row.CommitmentId == scope.CommitmentId);
        var crop = source.Supply.Commitments.Single();
        Require(requestedBatch.OptionId == "executor.plant_seed" && requestedBatch.CommitmentId == scope.CommitmentId &&
                requestedBatch.LocationId == layout.LocationId && requestedBatch.SeedQualifiedItemId == crop.SeedQualifiedItemId &&
                requestedBatch.Tiles.Length is >= 1 && requestedBatch.Tiles.Length <= CropSeedPlantingInventoryContract.MaximumBatchSize &&
                requestedBatch.Tiles.Distinct().Count() == requestedBatch.Tiles.Length &&
                requestedBatch.Tiles.All(tile => layout.Tiles.Contains(tile)),
            "Owned seed planting batch differs from its original layout/commitment.");
        var slots = crop.Slots.Where(row => row.InventoryKind == "player_inventory" && row.LocationId == layout.LocationId &&
            row.Quantity > 0 && (reservationId is null || row.ReservationId == reservationId)).ToArray();
        Require(slots.Length > 0 && (reservationId is null || slots.Length == 1), "Owned seed planting has no selected player claim.");
        var slot = slots[0];
        var count = Math.Min(slot.Quantity, requestedBatch.Tiles.Length);
        batch = CropTeacherSeedSupportSource.Copy(requestedBatch with { Tiles = requestedBatch.Tiles.Take(count).ToArray() });
        var stepId = scope.MarkerRequest.PortfolioId;
        actions = batch.Tiles.Select((tile, index) => new SmallModelAction
        {
            ActionId = stepId + ":plant:" + index.ToString(CultureInfo.InvariantCulture),
            OptionId = "executor.plant_seed", Rationale = "authoritative_crop_owned_seed_batch",
            Parameters = CropSeedPlantingBinding.ToParameters(ledger, scope.CommitmentId, slot.ReservationId,
                slot.Quality, slot.NativeStack, index, count).Concat(new[]
            {
                Parameter("target_location", batch.LocationId), Parameter("target_tile_x", tile.TileX.ToString(CultureInfo.InvariantCulture)),
                Parameter("target_tile_y", tile.TileY.ToString(CultureInfo.InvariantCulture)), Parameter("action_commitment_id", scope.CommitmentId)
            }).ToArray()
        }).ToArray();
        return CropTeacherSeedSupportSource.Copy(actions);
    }

    public string ValidateQueue(string path) => ReadQueue(path).Hash;

    private (string Hash, ActionQueueEnvelope Queue) ReadQueue(string path)
    {
        scope.ValidateInputsUnchanged();
        var hash = CurrentTeacherFrontierSupport.HashFile(path);
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(path, "owned seed planting queue");
        Require(actions is not null && queue.StateHash == scope.Source.Before.StateHash && queue.GoalId == "earn_money" &&
                queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items.Length == actions.Length &&
                ExecutionTargetProfiles.IsSupported(queue.ExecutionMode) &&
                EqualJson(queue.Actor, ExecutionTargetProfiles.CreateActor(queue.ExecutionMode)),
            "Owned seed planting queue differs from its original bound batch.");
        var readiness = new ActionQueueDispatchReadinessService();
        for (var index = 0; index < actions!.Length; index++)
        {
            var item = queue.Items[index];
            Require(item.SourceActionId == actions[index].ActionId && item.OptionId == "executor.plant_seed" &&
                    item.BlockingReasons.Length == 0 && item.MissingStateFactors.Length == 0 &&
                    item.NormalizedCommand.Steps is [{ StepType: "plant_seed" }] &&
                    item.NormalizedCommand.ExecutionMode == queue.ExecutionMode && EqualJson(item.NormalizedCommand.Actor, queue.Actor) &&
                    EqualJson(item.NormalizedCommand.Parameters, actions[index].Parameters) &&
                    readiness.Evaluate(queue, item, ledger, queue.StateHash).Ready,
                "Owned seed planting queue action/order/claim binding drifted.");
        }
        Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Owned seed planting queue changed while binding.");
        return (hash, queue);
    }

    public CropTeacherSeedPlantingSettlement VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string proofPath, string preparedQueueSha256) => CreateSettlement(transition, proofPath, preparedQueueSha256, null);

    public CropTeacherSeedPlantingSettlement ResumeTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string proofPath, string preparedQueueSha256, string expectedProofSha256)
    {
        Require(IsLowerSha256(expectedProofSha256), "Owned planting retained proof hash is invalid.");
        return CreateSettlement(transition, proofPath, preparedQueueSha256, expectedProofSha256);
    }

    private CropTeacherSeedPlantingSettlement CreateSettlement(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string proofPath, string preparedQueueSha256, string? expectedProofSha256)
    {
        var bound = ReadQueue(transition.QueuePath);
        Require(bound.Hash == preparedQueueSha256 && IsLowerSha256(bound.Hash), "Owned planting queue differs from its prepared hash.");
        return new(scope, ledger, BoundBatch, transition, bound.Queue, bound.Hash, proofPath, expectedProofSha256);
    }

    private static SmallModelActionParameter Parameter(string name, string value) => new() { Name = name, Value = value };
}
