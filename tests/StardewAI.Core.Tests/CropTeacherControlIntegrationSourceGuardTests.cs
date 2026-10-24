namespace StardewAI.Core.Tests;

public sealed class CropTeacherControlIntegrationSourceGuardTests
{
    [Fact]
    public void TeacherDispatchContinuesIntoTheFreshSnapshotControlLoop()
    {
        var program = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs");
        var dispatch = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.CropTeacherDispatch.cs");

        Assert.Contains(
            "activeCropTeacherRunPlan =",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildCropTeacherControlQueueAsync(",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_first_day_complete",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_run_complete",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "CropTeacherTransitionLedger.Open",
            dispatch,
            StringComparison.Ordinal);
        Assert.Contains(
            "RecordVerifiedHarvestResults",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "CheckCropTeacherDayBoundaryAsync",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "RecordVerifiedDayBoundary",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "DispatchCropTeacherAsync(",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "BuildCropTeacherReplanHandoff(",
            dispatch,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_replanned",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "planning_handoff_dispatched_native_run_plan_ready",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TeacherMechanicalQueuesBypassPolicyDecisionLeasesOnly()
    {
        var program = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs");
        var execution = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs");

        Assert.Contains(
            "useDailyPlanDecisionLease = false",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "options.UseDailyPlan && useDailyPlanDecisionLease",
            execution,
            StringComparison.Ordinal);
        Assert.Contains(
            "executionSnapshotProfileOverride",
            execution,
            StringComparison.Ordinal);
    }

    [Fact]
    public void TeacherControlReusesExistingCompilerAndPlannerEntrypoints()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.CropTeacherControl.cs");

        Assert.Contains(
            "BuildQueueFromDailyPlanAsync(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "MoneyRevenueCropSitePreparationPlanBuilder.Build",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/small-model/plan/action-queue/compile",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/small-model/action-queue/compile",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "PlanningHandoffCapture.BuildFarmLayoutSnapshotUrl",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "CropTeacherSitePreparationFrontier\n" +
            "            .SelectNextExecutableStep(plan)",
            source.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "plan.BlockedCount != 0",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "executor.harvest_crop",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "/api/v1/training/execute",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "/api/v1/product/execute",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void EveryCropTeacherStageLetsNativeStoryEventsPreemptMechanicalQueues()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.CropTeacherControl.cs");

        var controlEntry = source.IndexOf(
            "BuildCropTeacherControlQueueAsync(",
            StringComparison.Ordinal);
        var eventCheck = source.IndexOf(
            "if (HasPriorityStoryEventInterrupt(beforeSnapshot))",
            controlEntry,
            StringComparison.Ordinal);
        var stageSwitch = source.IndexOf(
            "return step.Stage switch",
            eventCheck,
            StringComparison.Ordinal);
        var storyBuilder = source.IndexOf(
            "BuildCropTeacherStoryEventQueueAsync(",
            eventCheck,
            StringComparison.Ordinal);
        var seedBuilder = source.IndexOf(
            "BuildCropTeacherSeedQueueAsync(",
            stageSwitch,
            StringComparison.Ordinal);
        var routeBuilder = source.IndexOf(
            "BuildCropTeacherRouteQueueAsync(",
            seedBuilder,
            StringComparison.Ordinal);

        Assert.True(controlEntry >= 0);
        Assert.True(eventCheck > controlEntry);
        Assert.True(storyBuilder > eventCheck);
        Assert.True(stageSwitch > storyBuilder);
        Assert.True(seedBuilder > stageSwitch);
        Assert.True(routeBuilder > seedBuilder);
        Assert.Contains(
            "BuildPriorityStoryEventQueueAsync(",
            source,
            StringComparison.Ordinal);
        Assert.Contains("\"story-event\"", source, StringComparison.Ordinal);
        Assert.Contains(
            "priorityInterrupt: true",
            source,
            StringComparison.Ordinal);

        var seedBodyStart = source.IndexOf(
            "BuildCropTeacherSeedQueueAsync(",
            storyBuilder,
            StringComparison.Ordinal);
        var routeBodyStart = source.IndexOf(
            "BuildCropTeacherRouteQueueAsync(",
            seedBodyStart,
            StringComparison.Ordinal);
        var seedBody = source[seedBodyStart..routeBodyStart];
        Assert.DoesNotContain(
            "HasPriorityStoryEventInterrupt",
            seedBody,
            StringComparison.Ordinal);
    }

    [Fact]
    public void StoryEventInterruptsCannotSettleCropOrSleepTransitions()
    {
        var program = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs");

        Assert.Contains(
            "cropTeacherStepForExecution = cropQueue.PriorityInterrupt\n" +
            "            ? null\n" +
            "            : cropStep;",
            program.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
        Assert.Contains(
            "? \"priority_story_event\"",
            program,
            StringComparison.Ordinal);
    }

    private static string Read(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
            {
                return File.ReadAllText(path);
            }
            directory = directory.Parent;
        }
        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
