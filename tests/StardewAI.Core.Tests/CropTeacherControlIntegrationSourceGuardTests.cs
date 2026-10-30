using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

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
            "CaptureCropTeacherTerminalBoundaryStartAsync",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "completedCropTeacherBoundaryLedger",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "pendingCropTeacherDayBoundaryLedger",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_terminal_save_boundary_settled",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_save_boundary_baseline_captured",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "options.RequireNativeSaveBoundary && closingNativeSaveBoundary",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReadString(trainingRepresentative, \"option_id\")",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "DispatchCropTeacherAsync(",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "ShouldDeferCropProductionTeacherHandoffForExclusiveRecovery(",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "planning_handoff_deferred_for_exclusive_recovery",
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
        Assert.Contains(
            "CropTeacherSiteApproachPlanner.SelectApproach(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "CropTeacherWateringRefillRecoveryPlanner.Build(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_watering_refill_approach",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"max_route_repair_clears\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"executor.move_to_tile\"",
            source,
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
            "interruptKind: \"priority_story_event\"",
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
    public void CropTeacherRouteAcceptsBoundedFestivalWaitAndReplans()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.CropTeacherControl.cs");

        Assert.Contains("RequireCropTeacherRouteQueue(queue);", source);
        Assert.Contains("\"executor.traverse_connector\"", source);
        Assert.Contains("\"executor.wait_ticks\"", source);
        Assert.Contains("\"route_wait_reason\"", source);
        Assert.Contains(
            "\"recovery_current_connector_active_festival_transit_closed\"",
            source);
    }

    [Fact]
    public void StoryEventInterruptsCannotSettleCropOrSleepTransitions()
    {
        var program = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs");

        Assert.Contains(
            "cropQueue.ExecutionStepOverride ??\n" +
            "            (!string.IsNullOrWhiteSpace(cropQueue.InterruptKind)\n" +
            "                ? null\n" +
            "                : cropStep);",
            program.Replace("\r\n", "\n"),
            StringComparison.Ordinal);
        Assert.Contains(
            "cropQueue.InterruptKind",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void LateDayCropWorkReusesRecoveryAndPreservesDayBoundarySettlement()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.CropTeacherControl.cs");
        var program = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs");
        var dialogueCheck = source.IndexOf(
            ".HasSafeSpeakerlessDialogue(beforeSnapshot)",
            StringComparison.Ordinal);
        var recoveryCheck = source.IndexOf(
            "GameClockBudgetPolicy.RecoveryWindowStarted(",
            dialogueCheck,
            StringComparison.Ordinal);
        var stageSwitch = source.IndexOf(
            "return step.Stage switch",
            recoveryCheck,
            StringComparison.Ordinal);

        Assert.True(dialogueCheck >= 0);
        Assert.True(recoveryCheck > dialogueCheck);
        Assert.True(stageSwitch > recoveryCheck);
        Assert.Contains(
            "BuildCropTeacherSleepQueueAsync(",
            source[recoveryCheck..stageSwitch],
            StringComparison.Ordinal);
        Assert.Contains(
            "InterruptKind = \"crop_teacher_late_day_recovery\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "Stage = CropTeacherRunStageKinds.NativeSleep",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "cropQueue.ExecutionStepOverride ??",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "CaptureCropTeacherDayBoundaryStartAsync(",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompletedCropRunSettlesOnTheNextVerifiedSharedSleep()
    {
        var program = Read("tools", "StardewAI.LiveTrainingLoop", "Program.cs")
            .Replace("\r\n", "\n");

        Assert.Contains(
            "if (cropTeacherDayBoundaryStart is null &&\n" +
            "            completedCropTeacherBoundaryStart is not null)",
            program,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "cropTeacherDayBoundaryStart is null &&\n" +
            "            closingNativeSaveBoundary &&",
            program,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReadString(trainingRepresentative, \"option_id\"),\n" +
            "                    \"executor.sleep\"",
            program,
            StringComparison.Ordinal);
    }

    [Fact]
    public void SpeakerlessDialogueRecoveryPreemptsCropSettlement()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.CropTeacherControl.cs");
        var eventCheck = source.IndexOf(
            "if (HasPriorityStoryEventInterrupt(beforeSnapshot))",
            StringComparison.Ordinal);
        var recoveryCheck = source.IndexOf(
            ".HasSafeSpeakerlessDialogue(beforeSnapshot)",
            eventCheck,
            StringComparison.Ordinal);
        var stageSwitch = source.IndexOf(
            "return step.Stage switch",
            recoveryCheck,
            StringComparison.Ordinal);

        Assert.True(eventCheck >= 0);
        Assert.True(recoveryCheck > eventCheck);
        Assert.True(stageSwitch > recoveryCheck);
        Assert.Contains(
            "new[] { \"executor.close_menu\" }",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "interruptKind: \"safe_dialogue_recovery\"",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "-compiled-queue.json",
            source,
            StringComparison.Ordinal);
    }

    [Fact]
    public void FullInventoryHarvestReusesStageOneCapacityRecoveryAsInterrupt()
    {
        var source = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.CropTeacherControl.cs");
        var recoveryCheck = source.IndexOf(
            "StageOneInventoryCapacityRecovery.HasFullInventory(",
            StringComparison.Ordinal);
        var stageSwitch = source.IndexOf(
            "return step.Stage switch",
            recoveryCheck,
            StringComparison.Ordinal);

        Assert.True(recoveryCheck >= 0);
        Assert.True(stageSwitch > recoveryCheck);
        Assert.Contains(
            "TryBuildStageOneCollectionTeacherQueueAsync(",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "capacityRecoveryOnly: true",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "allowObjectiveSafeCapacityRecovery: true",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "requireFishingAttemptBlockedForCapacityRecovery: false",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "crop_teacher_harvest_inventory_capacity_recovery",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "-compile-response.json",
            source,
            StringComparison.Ordinal);

        var collectionSource = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneCollectionTeacher.cs");
        Assert.Contains(
            "if ((!capacityRecoveryOnly &&",
            collectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (capacityRecoveryOnly)",
            collectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/planner/options/availability",
            collectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "new EventCandidateRanker().Rank(",
            collectionSource,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CollectionContinuationSeparatesControlTiesFromTeacherLabels()
    {
        var collectionSource = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.StageOneCollectionTeacher.cs");
        Assert.Contains(
            "blocked_authoritatively_tied_top_candidates",
            collectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "stable_controller_tiebreak_only",
            collectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "teacher_preference_label_eligible = false",
            collectionSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "formal_training_authorized = false",
            collectionSource,
            StringComparison.Ordinal);

        var loopSource = Read(
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.cs");
        Assert.Contains(
            "stage_one_collection_control_queue_compiled",
            loopSource,
            StringComparison.Ordinal);
        Assert.Contains(
            "stage_one_collection_teacher_blocked status=",
            loopSource,
            StringComparison.Ordinal);
        Assert.True(
            loopSource.IndexOf(
                "stage_one_collection_teacher_blocked status=",
                StringComparison.Ordinal) <
            loopSource.IndexOf(
                "BuildQueueFromDailyPlanAsync(",
                loopSource.IndexOf(
                    "stage_one_collection_teacher_blocked status=",
                    StringComparison.Ordinal),
                StringComparison.Ordinal));
    }

    [Fact]
    public void RecoveryClassifierRequiresExactSafeSpeakerlessProof()
    {
        var snapshot = JsonNode.Parse("""
        {
          "state": {
            "menus": {
              "active_menu": {
                "status": "available",
                "value": {
                  "is_open": true,
                  "type": "DialogueBox",
                  "last_question_key": null,
                  "is_sleep_prompt": false,
                  "event_up": false,
                  "dialogue_is_question": false,
                  "dialogue_response_count": 0,
                  "dialogue_transitioning": false,
                  "dialogue_character_present": false,
                  "dialogue_speaker_name": null
                }
              }
            }
          }
        }
        """)!.AsObject();

        Assert.True(CropTeacherControlInterruptClassifier
            .HasSafeSpeakerlessDialogue(snapshot));

        snapshot["state"]!["menus"]!["active_menu"]!["value"]![
            "dialogue_is_question"] = true;
        Assert.False(CropTeacherControlInterruptClassifier
            .HasSafeSpeakerlessDialogue(snapshot));

        snapshot["state"]!["menus"]!["active_menu"]!["value"]![
            "dialogue_is_question"] = false;
        ((JsonObject)snapshot["state"]!["menus"]!["active_menu"]![
            "value"]!).Remove("dialogue_speaker_name");
        Assert.False(CropTeacherControlInterruptClassifier
            .HasSafeSpeakerlessDialogue(snapshot));
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
