using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static Task<CropTeacherControlQueueBuildResult> BuildCropTeacherControlQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step,
        MoneyRevenueCropNativeProductionRunPlan runPlan, MoneyRevenueCropNativeProductionRunPlanRequest runPlanRequest,
        string beforeSnapshotPath, JsonObject beforeSnapshot, string stateHash,
        CropTeacherSeedPurchaseSupport? seedSupport = null, CropTeacherSeedTransferSupport? transferSupport = null) =>
        BuildCropTeacherControlQueueCoreAsync(http, options, iteration, step, runPlan, runPlanRequest,
            beforeSnapshotPath, beforeSnapshot, stateHash, seedSupport, transferSupport);
}
