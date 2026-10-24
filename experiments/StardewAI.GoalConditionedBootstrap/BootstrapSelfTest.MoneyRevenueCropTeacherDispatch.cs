using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void VerifyMoneyRevenueCropTeacherDispatch()
    {
        var root = TempArtifactRoot("money-crop-teacher-dispatch");
        var sourcePath = Path.Combine(root, "source-snapshot.json");
        var layoutPath = Path.Combine(root, "farm-layout-snapshot.json");
        var capturePath = Path.Combine(root, "capture-manifest.json");
        Write(sourcePath, MoneyCropTeacherSourceSnapshot());
        var layout = MoneyCropTeacherLayoutSnapshot();
        Write(layoutPath, layout);
        Write(capturePath, new PlanningHandoffCaptureManifest
        {
            SchemaVersion = "planning_handoff_capture_manifest.v1",
            Status = "farm_layout_capture_complete",
            SourceStateHash = MoneyCropTeacherSourceSnapshot().StateHash,
            SourceSnapshotPath = sourcePath,
            SourceSnapshotSha256 =
                CurrentTeacherFrontierSupport.HashFile(sourcePath),
            MapWidth = 4,
            MapHeight = 3,
            LayoutChunkSize = 32,
            LayoutSnapshotCount = 1,
            LayoutSnapshots = new[]
            {
                new PlanningHandoffLayoutSnapshotBinding
                {
                    Path = layoutPath,
                    Sha256 = CurrentTeacherFrontierSupport.HashFile(
                        layoutPath),
                    StateHash = layout.StateHash,
                    MinX = 0,
                    MinY = 0,
                    MaxX = 3,
                    MaxY = 2,
                    TileCount = 12
                }
            },
            PlanningHandoff = JsonSerializer.SerializeToElement(new
            {
                schema_version = "planning_goal_handoff.v1",
                handoff_kind = "crop_production_teacher",
                required_snapshot_profiles = new[] { "farm_layout" },
                automatic_dispatch_required = true
            }, JsonDefaults.Options)
        });

        var output = Path.Combine(root, "dispatch");
        MoneyRevenueCropTeacherDispatchManifest manifest;
        try
        {
            manifest = MoneyRevenueCropTeacherDispatchBuilder.Build(
                capturePath,
                output,
                "teacher.money.crop.dispatch.v1",
                "2026-10-24T00:00:00Z");
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Money crop Teacher dispatch self-test failed. " + ex,
                ex);
        }
        var runPlan = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropNativeProductionRunPlan>(
                manifest.NativeRunPlanPath,
                "Money crop Teacher dispatch run plan");
        Require(manifest.SchemaVersion ==
                    "money_revenue_crop_teacher_dispatch_manifest.v1" &&
                manifest.Status ==
                    "native_crop_production_run_plan_ready" &&
                manifest.SourceStateHash ==
                    MoneyCropTeacherSourceSnapshot().StateHash &&
                manifest.CoveredCommitmentIds.Length == 1 &&
                manifest.ActionCount > 0 &&
                manifest.NativeRunPlanSha256 ==
                    CurrentTeacherFrontierSupport.HashFile(
                        manifest.NativeRunPlanPath) &&
                runPlan.NativeProductionRunReady &&
                runPlan.SourceStateHash == manifest.SourceStateHash &&
                File.Exists(Path.Combine(
                    output,
                    "teacher-dispatch-manifest.json")),
            "Money crop Teacher handoff did not produce one exact native run plan.");

        var driftedCapturePath = Path.Combine(
            root,
            "capture-manifest-drifted.json");
        var driftedCapture = JsonSerializer.Deserialize<
            PlanningHandoffCaptureManifest>(
                File.ReadAllText(capturePath),
                JsonDefaults.Options)!;
        driftedCapture.LayoutSnapshots[0].StateHash = new string('f', 64);
        Write(driftedCapturePath, driftedCapture);
        var rejected = false;
        try
        {
            _ = MoneyRevenueCropTeacherDispatchBuilder.Build(
                driftedCapturePath,
                Path.Combine(root, "drifted-dispatch"),
                "teacher.money.crop.dispatch.drifted",
                "2026-10-24T00:00:00Z");
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "A crop Teacher handoff with a drifted layout state hash was admitted.");
    }
}
