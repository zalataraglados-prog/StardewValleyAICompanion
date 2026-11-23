using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    internal static VerifiedNativeExecutionQueue ReadOwnedCropPrefix(MoneyRevenueCropNativeLifecycleTransitionRequest request)
    {
        var product = request.ExecutorVersion == PolicyTrajectoryVersionPins.ProductExecutor;
        Require(product || request.ExecutorVersion == PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            "Owned crop prefix requires its original native executor.");
        var native = ReadQueueCore(request.QueuePath, request.BeforeSnapshotPath, request.ExecutionReceiptPath,
            request.AfterSnapshotPath, request.RunId, request.SelectedCandidateId, NativeProductBoundary.NativeAction,
            request.ExecutorVersion, ownedCropPrefix: request);
        if (product) RequireOriginalProductWire(native);
        else Require(native.Actions.All(frame => new Uri(frame.Endpoint).AbsolutePath == "/api/v1/training/execute"),
            "Owned crop prefix requires its original Runtime request.");
        foreach (var frame in native.Actions.Where(frame => frame.Result.Status == "blocked"))
        {
            if (frame.Item.OptionId == "executor.harvest_crop")
            {
                CropSeedResourceBoundary.VerifyUnconsumedCropAction(frame.Before, frame.After, "Owned harvest prefix",
                    requireSeedInventory: false);
                continue;
            }
            Require(frame.Request.TargetTileX.HasValue && frame.Request.TargetTileY.HasValue,
                "Owned watering prefix lost its original target tile.");
            CropSeedResourceBoundary.VerifyWatering(frame.Before, frame.After, frame.Request.LocationId,
                new(frame.Request.TargetTileX!.Value, frame.Request.TargetTileY!.Value), applied: false);
            if (native.Actions.Length == 1)
                CropSeedResourceBoundary.VerifyRefillControlWaterDelta(frame.Before, frame.After);
        }
        return native;
    }

    private static void ValidateOwnedCropPrefix(ActionQueueEnvelope queue, SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope receipt, SnapshotEnvelope after, MoneyRevenueCropNativeLifecycleTransitionRequest request)
    {
        var kinds = queue.Items.Select(item => item.OptionId).Distinct(StringComparer.Ordinal).ToArray();
        Require(!receipt.Success && request.TransitionKind == "crop_actions" &&
            kinds is ["executor.water_crop"] or ["executor.harvest_crop"], "Invalid incomplete owned crop queue.");
        if (kinds is ["executor.water_crop"] && receipt.StepResults is [{ Status: "blocked" }])
            CropTeacherAppliedPrefixReceipt.ValidateRefillControl(queue, before, receipt, after, request);
        else
            _ = CropTeacherAppliedPrefixReceipt.Validate(queue, before, receipt, after, request,
                kinds.Single()["executor.".Length..], "Owned native crop prefix");
    }
}
