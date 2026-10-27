using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherResumeSession(
    MoneyRevenueCropNativeProductionRunPlanRequest RunPlanRequest,
    MoneyRevenueCropNativeProductionRunPlan RunPlan,
    CropTeacherTransitionLedger TransitionLedger,
    string SourceTransitionLedgerPath,
    string TransitionLedgerPath,
    string NativeRunPlanPath,
    int DiscardedVolatileHarvestReceiptCount);

public static class CropTeacherResumeLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(
        JsonSerializerDefaults.Web);

    public static CropTeacherResumeSession Load(
        string sourceLedgerPath,
        string resumeLedgerPath)
    {
        var sourceLedger = CropTeacherTransitionLedger.OpenExisting(
            sourceLedgerPath);
        var runPlanPath = Path.GetFullPath(sourceLedger.RunPlanPath);
        var persistedPlan = Read<MoneyRevenueCropNativeProductionRunPlan>(
            runPlanPath,
            "crop_teacher_native_run_plan_invalid");
        var rebuiltPlan = MoneyRevenueCropNativeProductionRunPlanBuilder.Build(
            persistedPlan.RequestPath);
        if (!JsonNode.DeepEquals(
                JsonSerializer.SerializeToNode(persistedPlan, JsonOptions),
                JsonSerializer.SerializeToNode(rebuiltPlan, JsonOptions)))
        {
            throw new InvalidDataException(
                "crop_teacher_native_run_plan_not_reproducible");
        }

        var requestPath = Path.GetFullPath(rebuiltPlan.RequestPath);
        var request = Read<MoneyRevenueCropNativeProductionRunPlanRequest>(
            requestPath,
            "crop_teacher_run_plan_request_invalid");
        var ledger = CropTeacherTransitionLedger.ForkForResume(
            sourceLedger,
            resumeLedgerPath,
            out var discardedVolatileHarvestReceiptCount);
        return new CropTeacherResumeSession(
            request,
            rebuiltPlan,
            ledger,
            Path.GetFullPath(sourceLedger.LedgerPath),
            Path.GetFullPath(ledger.LedgerPath),
            runPlanPath,
            discardedVolatileHarvestReceiptCount);
    }

    private static T Read<T>(string path, string error)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(error, path);
        }

        return JsonSerializer.Deserialize<T>(
                File.ReadAllText(path, Encoding.UTF8),
                JsonOptions) ?? throw new InvalidDataException(error);
    }
}
