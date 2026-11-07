using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class CropTeacherSeedTransferMarker
{
    private readonly CropTeacherSeedTransferSupport scope;
    private readonly StrategyCommitmentLedger ledger;

    internal CropTeacherSeedTransferMarker(CropTeacherSeedTransferSupport scope, StrategyCommitmentLedger ledger)
    {
        this.scope = scope;
        this.ledger = ledger;
    }

    public StrategyCommitmentLedger Ledger => CropTeacherSeedSupportSource.Copy(ledger);

    public string ValidateQueue(string path) => ReadQueue(path).Hash;

    private (string Hash, ActionQueueEnvelope Queue) ReadQueue(string path)
    {
        scope.ValidateInputsUnchanged();
        var hash = CurrentTeacherFrontierSupport.HashFile(path);
        var queue = CurrentTeacherFrontierSupport.Read<ActionQueueEnvelope>(path, "owned seed transfer queue");
        Require(queue.StateHash == scope.Source.Before.StateHash && queue.GoalId == "earn_money" &&
                queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
                item.BlockingReasons.Length == 0 && item.MissingStateFactors.Length == 0 &&
                item.NormalizedCommand.Steps is [{ StepType: "transfer_material" }] &&
                ExecutionTargetProfiles.IsSupported(queue.ExecutionMode) &&
                EqualJson(queue.Actor, ExecutionTargetProfiles.CreateActor(queue.ExecutionMode)) &&
                item.NormalizedCommand.ExecutionMode == queue.ExecutionMode &&
                EqualJson(item.NormalizedCommand.Actor, queue.Actor) &&
                new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, queue.StateHash).Ready,
            "Owned seed transfer queue exceeds its prepared scope.");
        Require(CurrentTeacherFrontierSupport.HashFile(path) == hash, "Owned seed transfer queue changed while binding.");
        return (hash, queue);
    }

    public CropTeacherSeedTransferSettlement VerifyTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string proofPath, string preparedQueueSha256)
    {
        var bound = ReadQueue(transition.QueuePath);
        return new(scope, ledger, transition, proofPath, bound.Queue, bound.Hash, preparedQueueSha256, null);
    }

    public CropTeacherSeedTransferSettlement ResumeTransition(MoneyRevenueCropNativeLifecycleTransitionRequest transition,
        string proofPath, string preparedQueueSha256, string expectedProofSha256)
    {
        Require(IsLowerSha256(expectedProofSha256), "Owned seed transfer retained proof hash is invalid.");
        var bound = ReadQueue(transition.QueuePath);
        return new(scope, ledger, transition, proofPath, bound.Queue, bound.Hash, preparedQueueSha256, expectedProofSha256);
    }

    public SmallModelAction BuildAction(int standTileX, int standTileY)
    {
        scope.ValidateInputsUnchanged();
        return new SmallModelAction
        {
            ActionId = ledger.PreparedMaterialTransfer!.PortfolioId, OptionId = "executor.transfer_material",
            Parameters = CropSeedMaterialTransferBinding.ToParameters(ledger, scope.CommitmentId).Concat(new[]
            {
                new SmallModelActionParameter { Name = "stand_tile_x", Value = standTileX.ToString(CultureInfo.InvariantCulture) },
                new SmallModelActionParameter { Name = "stand_tile_y", Value = standTileY.ToString(CultureInfo.InvariantCulture) }
            }).ToArray()
        };
    }
}
