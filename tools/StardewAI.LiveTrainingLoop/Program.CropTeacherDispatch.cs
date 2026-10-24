using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record CropTeacherDispatchSession(
        PlanningHandoffCaptureResult Capture,
        string TeacherDispatchManifestPath,
        string NativeRunPlanPath,
        MoneyRevenueCropNativeProductionRunPlanRequest RunPlanRequest,
        MoneyRevenueCropNativeProductionRunPlan RunPlan,
        string TransitionLedgerPath,
        CropTeacherTransitionLedger TransitionLedger,
        int ActionCount);

    private static async Task<CropTeacherDispatchSession>
        DispatchCropTeacherAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string sourceSnapshotJson,
            JsonObject planningHandoff,
            string artifactKind)
    {
        if (artifactKind is not ("initial" or "replan"))
        {
            throw new ArgumentOutOfRangeException(nameof(artifactKind));
        }
        var directoryName = artifactKind == "initial"
            ? "planning-handoff-" + iteration.ToString("D4")
            : "planning-handoff-replan-" + iteration.ToString("D4");
        var capture = await PlanningHandoffCapture.CaptureAsync(
            http,
            options.BridgeSnapshotUrl,
            Path.Combine(options.SnapshotDir, directoryName),
            sourceSnapshotJson,
            planningHandoff);
        var teacherDispatchDirectory = Path.Combine(
            Path.GetDirectoryName(capture.ManifestPath)!,
            "teacher-dispatch");
        var teacherDispatch =
            MoneyRevenueCropTeacherDispatchBuilder.Build(
                capture.ManifestPath,
                teacherDispatchDirectory,
                options.RunId + ".crop-teacher." + artifactKind + "." +
                    iteration.ToString("D4"),
                DateTimeOffset.UtcNow.ToString("O"));
        var runPlanRequest = JsonSerializer.Deserialize<
                MoneyRevenueCropNativeProductionRunPlanRequest>(
                    await File.ReadAllTextAsync(
                        teacherDispatch.NativeRunPlanRequestPath),
                    JsonOptions) ?? throw new InvalidDataException(
                        "crop_teacher_run_plan_request_invalid");
        var runPlan = JsonSerializer.Deserialize<
                MoneyRevenueCropNativeProductionRunPlan>(
                    await File.ReadAllTextAsync(
                        teacherDispatch.NativeRunPlanPath),
                    JsonOptions) ?? throw new InvalidDataException(
                        "crop_teacher_native_run_plan_invalid");
        var transitionLedgerPath = Path.Combine(
            teacherDispatchDirectory,
            "crop-teacher-transition-ledger.json");
        var transitionLedger = CropTeacherTransitionLedger.Open(
            transitionLedgerPath,
            teacherDispatch.NativeRunPlanPath);
        return new CropTeacherDispatchSession(
            capture,
            Path.Combine(
                teacherDispatchDirectory,
                "teacher-dispatch-manifest.json"),
            teacherDispatch.NativeRunPlanPath,
            runPlanRequest,
            runPlan,
            transitionLedgerPath,
            transitionLedger,
            teacherDispatch.ActionCount);
    }

    private static JsonObject BuildCropTeacherReplanHandoff(
        string reason,
        string supersededRunPlanPath,
        string supersededRunPlanSha256) => new()
    {
        ["handoff_kind"] = "crop_production_teacher",
        ["required_snapshot_profiles"] = new JsonArray("farm_layout"),
        ["requested_capability"] =
            "replan_crop_production_after_native_drift",
        ["automatic_dispatch_required"] = true,
        ["replan_reason"] = reason,
        ["superseded_run_plan_path"] = supersededRunPlanPath,
        ["superseded_run_plan_sha256"] = supersededRunPlanSha256
    };
}
