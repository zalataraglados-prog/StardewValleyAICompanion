using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<CropTeacherControlQueueBuildResult> BuildCropTeacherManagedControlQueueAsync(HttpClient http,
        LiveTrainingOptions options, int iteration, CropTeacherRunStep step,
        MoneyRevenueCropNativeProductionRunPlan runPlan, MoneyRevenueCropNativeProductionRunPlanRequest runPlanRequest,
        string beforeSnapshotPath, JsonObject beforeSnapshot, string stateHash, CropTeacherManagedSeedScopes scopes)
    {
        scopes.ValidateStep(step);
        return await BuildCropTeacherControlQueueCoreAsync(http, options, iteration, step, runPlan, runPlanRequest,
            beforeSnapshotPath, beforeSnapshot, stateHash, scopes.Purchase, scopes.Transfer, scopes.Planting);
    }
}
