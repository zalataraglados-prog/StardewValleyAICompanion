using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NativeSocialRuntimeSmokeSourceGuardTests
{
    private static readonly string SocialSmokeSource = File.ReadAllText(
        FindRepositoryFile("scripts", "Invoke-RuntimeNativeSocialSmoke.ps1"));
    private static readonly string NpcAdapterSource = File.ReadAllText(
        FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "NpcReadAdapter.cs"));

    [Fact]
    public void SmokeScriptUsesEOnlyRuntimePaths()
    {
        var source = SocialSmokeSource;

        Assert.Contains("E:\\StardewValleyAICompanion-runtime", source, StringComparison.Ordinal);
        Assert.Contains("$RuntimeRoot", source, StringComparison.Ordinal);
        Assert.Contains("$savesPath", source, StringComparison.Ordinal);
        Assert.Contains("$runtimeGameDir", source, StringComparison.Ordinal);

        Assert.DoesNotContain("I:\\StardewValleyAICompanion", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("C:\\Program Files", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("$env:APPDATA", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SmokeScriptLaunchesHiddenWithAudioNull()
    {
        var source = SocialSmokeSource;

        Assert.Contains("WindowStyle Hidden", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("SDL_AUDIODRIVER", source, StringComparison.Ordinal);
        Assert.Contains("ALSOFT_DRIVERS", source, StringComparison.Ordinal);
        Assert.Contains("\"dummy\"", source, StringComparison.Ordinal);
        Assert.Contains("\"null\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasPortConflictGuard()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Test-PortConflictGuard", source, StringComparison.Ordinal);
        Assert.Contains("Get-NetTCPConnection", source, StringComparison.Ordinal);
        Assert.Contains("8765", source, StringComparison.Ordinal);
        Assert.Contains("8767", source, StringComparison.Ordinal);
        Assert.Contains("$BackendPort", source, StringComparison.Ordinal);
        Assert.Contains("port_conflict_detected", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasExactProcessCleanup()
    {
        var source = SocialSmokeSource;

        Assert.Contains("KeepGameRunning", source, StringComparison.Ordinal);
        Assert.Contains("Stop-Process", source, StringComparison.Ordinal);
        Assert.Contains("HasExited", source, StringComparison.Ordinal);
        Assert.Contains("$gameProcess.Id", source, StringComparison.Ordinal);
        Assert.Contains("$backendProcess.Id", source, StringComparison.Ordinal);

        Assert.DoesNotContain("Stop-Process -Name", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Get-Process Stardew", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SmokeScriptPreservesEnvironmentVariables()
    {
        var source = SocialSmokeSource;

        Assert.Contains("previousEnv", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TEST_SAVES", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_SAVE_ISOLATION_PATH", source, StringComparison.Ordinal);
        Assert.Contains("$env:STARDEWAI_TRAINING_MODE", source, StringComparison.Ordinal);
        Assert.Contains("$env:SDL_AUDIODRIVER", source, StringComparison.Ordinal);
        Assert.Contains("$env:ALSOFT_DRIVERS", source, StringComparison.Ordinal);
        Assert.Contains("$env:ASPNETCORE_URLS", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsFindLegalSocialTalkCandidate()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Find-LegalSocialTalkCandidate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("simple_non_villager_npc", source, StringComparison.Ordinal);
        Assert.DoesNotContain("current_route_window_complete", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_npc_not_in_player_location", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_special_npc_check_action_branch_unsupported", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsFindLegalSocialGiftCandidate()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Find-LegalSocialGiftCandidate", source, StringComparison.Ordinal);
        Assert.DoesNotContain("can_receive_gifts", source, StringComparison.Ordinal);
        Assert.DoesNotContain("protected_from_auto_sell", source, StringComparison.Ordinal);
        Assert.DoesNotContain("object_quest_item", source, StringComparison.Ordinal);
        Assert.DoesNotContain("can_be_given_as_gift", source, StringComparison.Ordinal);
        Assert.DoesNotContain("special_item", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_special_switch_item_branch_unsupported", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_daily_limit_exhausted", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_weekly_limit_exhausted", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsManualSocialExecutionRequestBuilder()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Build-SocialTalkExecutionRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Build-SocialGiftExecutionRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Compute-StandTile", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsDirectSocialTrainingExecute()
    {
        var source = SocialSmokeSource;

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("\"executor.social_interact\"", StringComparison.Ordinal) &&
                (line.Contains("schema_version", StringComparison.Ordinal) ||
                 line.Contains("training_execution_request", StringComparison.Ordinal) ||
                 line.Contains("queue_item_id", StringComparison.Ordinal)))
            {
                Assert.Fail("Script contains a hand-written request line with option_id = executor.social_interact: " + line.Trim());
            }
        }
    }

    [Fact]
    public void SmokeScriptHasNoDirectNpcOrFriendshipOrOutcomeMutation()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("changeFriendship", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("set_Friendship", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("GiftsToday =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GiftsThisWeek =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("talkedToToday =", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".receiveGift(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("tryToReceiveActiveObject(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", source, StringComparison.Ordinal);
        Assert.DoesNotContain("teleport", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasProductionChainArtifactsWithCorrectFieldPaths()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ranking-response-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-ranking-response.json", source, StringComparison.Ordinal);
        Assert.Contains("gift-ranking-response.json", source, StringComparison.Ordinal);
        Assert.Contains("plan-execution-episode-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-episode.json", source, StringComparison.Ordinal);
        Assert.Contains("gift-episode.json", source, StringComparison.Ordinal);

        Assert.Contains("daily-plan-response-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("compiled-queue-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("execution-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-feature-rows.jsonl", source, StringComparison.Ordinal);
        Assert.Contains("gift-feature-rows.jsonl", source, StringComparison.Ordinal);

        Assert.Contains("gift-failed.json", source, StringComparison.Ordinal);

        Assert.DoesNotContain("gift-skipped.json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("passed_talk_only", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesCorrectPlanStepKindMapping()
    {
        var source = SocialSmokeSource;

        Assert.Contains("move_to_tile", source, StringComparison.Ordinal);
        Assert.Contains("move_to_social_stand", source, StringComparison.Ordinal);
        Assert.Contains("social_interact", source, StringComparison.Ordinal);

        Assert.DoesNotContain("executor.move_to_social_stand", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".plan_steps", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".candidates", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesCorrectQueueOptionIdMapping()
    {
        var source = SocialSmokeSource;

        Assert.Contains("executor.move_to_tile", source, StringComparison.Ordinal);
        Assert.Contains("executor.social_interact", source, StringComparison.Ordinal);

        Assert.DoesNotContain("executor.move_to_social_stand", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDeploysModsBeforeLaunch()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Deploy-TransparentBridgeToRuntime.ps1", source, StringComparison.Ordinal);
        Assert.Contains("Deploy-RuntimeTestHarnessToRuntime.ps1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptTalksBeforeGiftWithProductionRecoveryClose()
    {
        var source = SocialSmokeSource;

        Assert.Contains("social.talk_npc", source, StringComparison.Ordinal);
        Assert.Contains("social.gift_npc", source, StringComparison.Ordinal);
        Assert.Contains("recovery.stabilize_day", source, StringComparison.Ordinal);
        Assert.Contains("close-dialogue", source, StringComparison.Ordinal);

        Assert.Contains("executor.close_menu", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFailClosedWhenGiftFails()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift LiveTrainingLoop returned exit code", source, StringComparison.Ordinal);
        Assert.Contains("gift-failed.json", source, StringComparison.Ordinal);
        Assert.Contains("full talk+gift smoke requires both phases", source, StringComparison.Ordinal);

        Assert.DoesNotContain("passed_talk_only", source, StringComparison.Ordinal);
        Assert.DoesNotContain("passed_all", source, StringComparison.Ordinal);
        Assert.DoesNotContain("gift-skipped.json", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"skipped\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresEpisodeRankingAndFeatureRowArtifacts()
    {
        var source = SocialSmokeSource;

        Assert.Contains("plan-execution-episode-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("ranking-response-0001.json", source, StringComparison.Ordinal);
        Assert.Contains("talk-feature-rows.jsonl", source, StringComparison.Ordinal);
        Assert.Contains("gift-feature-rows.jsonl", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresRankingProvenanceForCandidates()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ranked_event_candidates", source, StringComparison.Ordinal);
        Assert.Contains("social_talk_current", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_current", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasRouteGraphBfsTraversal()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Invoke-RouteGraphBfsToNpc", source, StringComparison.Ordinal);
        Assert.Contains("route_graph", source, StringComparison.Ordinal);
        Assert.Contains("locations.route_graph", source, StringComparison.Ordinal);
        Assert.Contains("Queue", source, StringComparison.Ordinal);
        Assert.Contains("traverse_connector", source, StringComparison.Ordinal);

        Assert.DoesNotContain("teleport", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("directWarp", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SmokeScriptExecutesProductionSocialRouteCandidateBeforeLegacyTraversalFallback()
    {
        var source = SocialSmokeSource;

        var productionStage = source.IndexOf("Verify-ProductionSocialRouteStepArtifacts", StringComparison.Ordinal);
        var fallbackStage = source.IndexOf("Invoke-RouteGraphBfsToNpc", productionStage, StringComparison.Ordinal);
        Assert.True(productionStage >= 0);
        Assert.True(fallbackStage > productionStage);
        Assert.Contains("production-route-loop", source, StringComparison.Ordinal);
        Assert.Contains("kind -eq \"route_connector_tile\"", source, StringComparison.Ordinal);
        Assert.Contains("continuation.option_id", source, StringComparison.Ordinal);
        Assert.Contains("continuation.npc_name", source, StringComparison.Ordinal);
        Assert.Contains("continuation.target_location", source, StringComparison.Ordinal);
        Assert.Contains("social_route.position_source", source, StringComparison.Ordinal);
        Assert.Contains("social_route.future_schedule_projection", source, StringComparison.Ordinal);
        Assert.Contains("fresh_snapshot_replan_required=true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSocialRouteSmokeRequiresCompiledAndVerifiedConnectorArtifacts()
    {
        var source = SocialSmokeSource;

        Assert.Contains("production-route-ranking-response.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-daily-plan-response.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-compiled-queue.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-execution.json", source, StringComparison.Ordinal);
        Assert.Contains("production-route-episode.json", source, StringComparison.Ordinal);
        Assert.Contains("Production social route plan must compile exactly one executor.traverse_connector queue item", source, StringComparison.Ordinal);
        Assert.Contains("Production social route execution must contain one applied/verified traverse_connector result", source, StringComparison.Ordinal);
        Assert.Contains("Production social route arrival mismatch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionSocialRouteOnlyModeReportsItsBoundedRuntimeScope()
    {
        var source = SocialSmokeSource;

        Assert.Contains("[switch] $ProductionRouteOnly", source, StringComparison.Ordinal);
        Assert.Contains("one_production_social_route_connector_then_fresh_snapshot", source, StringComparison.Ordinal);
        Assert.Contains("full_multi_connector_pursuit_verified = $false", source, StringComparison.Ordinal);
        Assert.Contains("future_schedule_projection = \"not_used\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialContinuationDialogueRecoveryFlagReachesRuntimeSafetyGate()
    {
        var loopSource = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.LiveTrainingLoop", "Program.cs"));
        var runtimeSource = RuntimeHarnessSources.All;

        Assert.Contains("social_continuation_dialogue_recovery", loopSource, StringComparison.Ordinal);
        Assert.Contains("executionRequest.SocialContinuationDialogueRecovery", loopSource, StringComparison.Ordinal);
        Assert.Contains("request.SocialContinuationDialogueRecovery", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("allowSpeakerlessSocialContinuation", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("currentBox.characterDialogue?.speaker?.Name ?? string.Empty", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("string.Equals(currentSpeakerName, advance.InitialSpeakerName", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("dialogueBox.responses", runtimeSource, StringComparison.Ordinal);
        Assert.Contains("Game1.eventUp", runtimeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void CollisionGridTreatsLockedFriendshipTouchDoorsAsDynamicObstacles()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "ShopAccessReadAdapter.cs"));

        Assert.Contains("ReadFriendshipDoorGate(location, touchAction)", source, StringComparison.Ordinal);
        Assert.Contains("friendshipDoor is { AllowedNow: false }", source, StringComparison.Ordinal);
        Assert.Contains("friendship_door_allowed_now", source, StringComparison.Ordinal);
        Assert.Contains("getFriendshipHeartLevelForNPC(name) >= 2", source, StringComparison.Ordinal);
        Assert.Contains("location.IsGreenRainingHere()", source, StringComparison.Ordinal);
        Assert.Contains("Game1.year == 1", source, StringComparison.Ordinal);
        Assert.Contains("Sebastian", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesSocialExecutionFieldsDirectly()
    {
        var source = SocialSmokeSource;

        Assert.Contains("social_native_handled", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_name", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_location_before", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("social_player_tile_x_before", source, StringComparison.Ordinal);
        Assert.Contains("social_player_tile_y_before", source, StringComparison.Ordinal);
        Assert.Contains("social_player_facing_before", source, StringComparison.Ordinal);
        Assert.Contains("social_dialogue_open_before", source, StringComparison.Ordinal);
        Assert.Contains("social_dialogue_open_after", source, StringComparison.Ordinal);
        Assert.Contains("social_talked_to_today_before", source, StringComparison.Ordinal);
        Assert.Contains("social_talked_to_today_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_stack_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_stack_after", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_before", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_this_week_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_this_week_after", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_item_id_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gift_item_id_after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresGiftStackDecreaseByOne()
    {
        var source = SocialSmokeSource;

        Assert.Contains("gift_stack_before", source, StringComparison.Ordinal);
        Assert.Contains("gift_stack_after", source, StringComparison.Ordinal);
        Assert.Contains("stack did not decrease by exactly 1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresFriendshipNonNullBeforeAfter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("friendship_before", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_before", source, StringComparison.Ordinal);
        Assert.Contains("social_friendship_points_after", source, StringComparison.Ordinal);
        Assert.Contains("missing friendship before", source, StringComparison.Ordinal);
        Assert.Contains("missing friendship after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresGiftCounterIncrements()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gifts today did not increment by 1", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_before", source, StringComparison.Ordinal);
        Assert.Contains("social_gifts_today_after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptNeverReturnsPassedTalkOnlyOrSkipped()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("passed_talk_only", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"skipped\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("gift-skipped.json", source, StringComparison.Ordinal);

        Assert.Contains("\"passed\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesProductionRecoveryCloseNotHandWrittenRequest()
    {
        var source = SocialSmokeSource;

        Assert.Contains("recovery.stabilize_day", source, StringComparison.Ordinal);
        Assert.Contains("close-dialogue-loop", source, StringComparison.Ordinal);

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("executor.close_menu", StringComparison.Ordinal) &&
                line.Contains("option_id", StringComparison.Ordinal) &&
                line.Contains("\"executor.close_menu\"", StringComparison.Ordinal))
            {
                Assert.Fail("Script contains a hand-written close request with option_id = executor.close_menu: " + line.Trim());
            }
        }
    }

    [Fact]
    public void SmokeScriptStartsAspNetCoreBackend()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ASPNETCORE_URLS", source, StringComparison.Ordinal);
        Assert.Contains("BackendPort", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.Backend.csproj", source, StringComparison.Ordinal);
        Assert.Contains("$backendUrl/health", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasBackendProcessCleanup()
    {
        var source = SocialSmokeSource;

        Assert.Contains("$backendProcess", source, StringComparison.Ordinal);
        Assert.Contains("Stop-Process -Id $backendProcess.Id", source, StringComparison.Ordinal);
        Assert.Contains("$backendProcess.HasExited", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesMoveBeforeSocialUsingActualFieldPaths()
    {
        var source = SocialSmokeSource;

        Assert.Contains("kind", source, StringComparison.Ordinal);
        Assert.Contains("step_id", source, StringComparison.Ordinal);
        Assert.Contains("move_to_tile", source, StringComparison.Ordinal);
        Assert.Contains("move_to_social_stand", source, StringComparison.Ordinal);
        Assert.Contains("source_action_id", source, StringComparison.Ordinal);
        Assert.Contains("executor.move_to_tile", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductionCompileChainRequiresBackendAndLiveTrainingLoop()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("Build-SocialTalkExecutionRequest", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Build-SocialGiftExecutionRequest", source, StringComparison.Ordinal);

        Assert.Contains("dotnet run", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.Backend", source, StringComparison.Ordinal);
        Assert.Contains("StardewAI.LiveTrainingLoop", source, StringComparison.Ordinal);
        Assert.Contains("--use-daily-plan", source, StringComparison.Ordinal);
        Assert.Contains("social.talk_npc", source, StringComparison.Ordinal);
        Assert.Contains("social.gift_npc", source, StringComparison.Ordinal);
    }

    [Fact]
    public void DailyPlanCompilerMapsSocialTalkToSocialInteractPlanStep()
    {
        var compilerSource = DailyPlanCompilerSources.All;

        Assert.Contains("social_talk_current", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_gift_current", compilerSource, StringComparison.Ordinal);
        Assert.Contains("SocialInteractionSteps", compilerSource, StringComparison.Ordinal);
        Assert.Contains("\"social_interact\"", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_action_kind", compilerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void ActionQueueCompilerValidatesSocialInteractWithStandTileAdjacency()
    {
        var compilerSource = ActionQueueCompilerSources.All;

        Assert.Contains("ValidateSocialInteractPlan", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_stand_not_adjacent_to_npc", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_candidate_stand_npc_mismatch", compilerSource, StringComparison.Ordinal);
        Assert.Contains("executor.social_interact", compilerSource, StringComparison.Ordinal);
        Assert.Contains("social_action_kind", compilerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveTrainingLoopSavesRankingResponse()
    {
        var loopSource = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.LiveTrainingLoop", "Program.cs"));

        Assert.Contains("ranking-response-", loopSource, StringComparison.Ordinal);
        Assert.Contains("replan-ranking-response-", loopSource, StringComparison.Ordinal);
        Assert.Contains("WriteAllTextAsync", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("NextRankingIndex", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("IncrementRankingIndex", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("_rankingIndex", loopSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesAvailableCandidateSelectionNotRankDefault()
    {
        var source = SocialSmokeSource;

        Assert.Contains(".available -eq", source, StringComparison.Ordinal);
        Assert.Contains("candidate_id", source, StringComparison.Ordinal);
        Assert.Contains("timeline_status -eq \"blocked\"", source, StringComparison.Ordinal);
        Assert.Contains("option_id -eq \"social.talk_npc\"", source, StringComparison.Ordinal);
        Assert.Contains("option_id -eq \"social.gift_npc\"", source, StringComparison.Ordinal);
        Assert.Contains("daily-plan-max-candidates 1", source, StringComparison.Ordinal);

        Assert.DoesNotContain(".rank -ge 0", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptCrossChecksCandidateIdThroughPlanQueueExecution()
    {
        var source = SocialSmokeSource;

        Assert.Contains("planCandidateId", source, StringComparison.Ordinal);
        Assert.Contains("moveStepCandidateId", source, StringComparison.Ordinal);
        Assert.Contains("source_action_id", source, StringComparison.Ordinal);
        Assert.Contains("queue_item_id", source, StringComparison.Ordinal);
        Assert.Contains("candidate_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptValidatesTalkManhattanAdjacencyAndFacing()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Manhattan-adjacent", source, StringComparison.Ordinal);
        Assert.Contains("player_facing_before", source, StringComparison.Ordinal);
        Assert.Contains("does not point toward NPC", source, StringComparison.Ordinal);
        Assert.Contains("talked_to_before", source, StringComparison.Ordinal);
        Assert.Contains("talked_to_after", source, StringComparison.Ordinal);
        Assert.Contains("social_menu_open_before", source, StringComparison.Ordinal);
        Assert.Contains("social_current_dialogue_count_before", source, StringComparison.Ordinal);
        Assert.Contains("social_current_dialogue_key_before", source, StringComparison.Ordinal);
        Assert.Contains("social_current_dialogue_speaker_name_before", source, StringComparison.Ordinal);
        Assert.Contains("post-talk dialogue/menu signal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptValidatesGiftStackNullOrDecrementAndWeekCounter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("item_stack_before", source, StringComparison.Ordinal);
        Assert.Contains("gift_updates_normal_limits", source, StringComparison.Ordinal);
        Assert.Contains("gifts_week_before", source, StringComparison.Ordinal);
        Assert.Contains("gifts_week_after", source, StringComparison.Ordinal);
        Assert.Contains("should be null when exactly one item consumed", source, StringComparison.Ordinal);
        Assert.Contains("Gifts this week did not increment", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDerivesSummaryFlagsFromEvidenceNotHardcoded()
    {
        var source = SocialSmokeSource;

        Assert.Contains("giftStackDecreased", source, StringComparison.Ordinal);
        Assert.Contains("talkArtifacts.HasRankedCandidate", source, StringComparison.Ordinal);
        Assert.Contains("giftVerification.HasRankedCandidate", source, StringComparison.Ordinal);

        Assert.DoesNotContain("gift_stack_decreased_by_one = $true", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptIngestsSnapshotBeforePreTalkRankProbe()
    {
        var source = SocialSmokeSource;

        Assert.Contains("bridge-snapshot-ingested.json", source, StringComparison.Ordinal);
        Assert.Contains("/api/v1/snapshots", source, StringComparison.Ordinal);
        Assert.Contains("pre-talk-rank-probe.json", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDoesNotUseDirectCoordinateTeleportForMovement()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("teleport", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("setPosition", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("debug.visible_walk", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGiftLegalityDelegatedToProductionSocialCandidateBuilder()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("is_divorced", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("social_gift_divorced", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_gift_weekly_limit", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_can_receive_gifts_incomplete", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptReliesOnProductionRouteGraph()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("directWarp", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("teleport", source, StringComparison.OrdinalIgnoreCase);

        Assert.Contains("route_graph", source, StringComparison.Ordinal);
        Assert.Contains("locations.route_graph", source, StringComparison.Ordinal);
        Assert.Contains("executor.traverse_connector", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptBfsTraversalSavesAllEdgeRequestsAndResults()
    {
        var source = SocialSmokeSource;

        Assert.Contains("route-graph-bfs-edge", source, StringComparison.Ordinal);
        Assert.Contains("-request.json", source, StringComparison.Ordinal);
        Assert.Contains("-result.json", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-success.json", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-failed.json", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-no-npc.json", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptBfsFailsClosedWhenNoRouteExists()
    {
        var source = SocialSmokeSource;

        Assert.Contains("No bounded transparent route", source, StringComparison.Ordinal);
        Assert.Contains("failed_closed", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRecordsLocationStateHashBeforeAfter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("before_location", source, StringComparison.Ordinal);
        Assert.Contains("after_talk_location", source, StringComparison.Ordinal);
        Assert.Contains("after_gift_location", source, StringComparison.Ordinal);
        Assert.Contains("before_state_hash", source, StringComparison.Ordinal);
        Assert.Contains("after_talk_state_hash", source, StringComparison.Ordinal);
        Assert.Contains("after_gift_state_hash", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesPowerShell51CompatibleSyntax()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("??", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesCorrectRouteGraphEdgeFieldNames()
    {
        var source = SocialSmokeSource;

        Assert.Contains(".resolved", source, StringComparison.Ordinal);
        Assert.Contains(".from_location", source, StringComparison.Ordinal);
        Assert.Contains(".from_x", source, StringComparison.Ordinal);
        Assert.Contains(".from_y", source, StringComparison.Ordinal);
        Assert.Contains(".kind", source, StringComparison.Ordinal);
        Assert.Contains(".target_location", source, StringComparison.Ordinal);

        Assert.DoesNotContain("source_location_id", source, StringComparison.Ordinal);
        Assert.DoesNotContain("target_location_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresExactTraverseConnectorRequestFields()
    {
        var source = SocialSmokeSource;

        Assert.Contains("target_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("target_tile_y", source, StringComparison.Ordinal);
        Assert.Contains("connector_kind", source, StringComparison.Ordinal);
        Assert.Contains("expected_target_location", source, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("expected_arrival_tile_y", source, StringComparison.Ordinal);

        Assert.DoesNotContain("\"placeholder\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDoesNotDefaultMissingKindToWarp()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("\"warp\"", source, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("edge.kind", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresAppliedAndVerifiedTraverseResult()
    {
        var source = SocialSmokeSource;

        Assert.Contains("result not applied/verified", source, StringComparison.Ordinal);
        Assert.Contains("primitive_verification_status", source, StringComparison.Ordinal);
        Assert.Contains("\"applied\"", source, StringComparison.Ordinal);
        Assert.Contains("\"verified\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresArrivalTileVerificationAfterTraverse()
    {
        var source = SocialSmokeSource;

        Assert.Contains("arrival tile mismatch", source, StringComparison.Ordinal);
        Assert.Contains("route-graph-bfs-bad-arrival-tile.json", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFiltersRouteEdgesByResolvedContract()
    {
        var source = SocialSmokeSource;

        Assert.Contains(".resolved -eq", source, StringComparison.Ordinal);
        Assert.Contains(".from_location", source, StringComparison.Ordinal);
        Assert.Contains(".target_location", source, StringComparison.Ordinal);
        Assert.Contains(".from_x", source, StringComparison.Ordinal);
        Assert.Contains(".from_y", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsEdgeConnectorKindFieldAlias()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("edgeData.connector_kind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edge.connector_kind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.branch", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRejectsWrongTileCoordinateAliases()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("edgeData.target_tile_x", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.target_tile_y", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.warp_tile_x", source, StringComparison.Ordinal);
        Assert.DoesNotContain("edgeData.warp_tile_y", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptUsesEdgeFromCoordinatesForTargetTile()
    {
        var source = SocialSmokeSource;

        Assert.Contains("target_tile_x =", source, StringComparison.Ordinal);
        Assert.Contains("target_tile_y =", source, StringComparison.Ordinal);
        if (source.Contains("from_x", StringComparison.Ordinal) && source.Contains("target_tile_x", StringComparison.Ordinal))
        {
            Assert.Contains("edgeData.target_x", source, StringComparison.Ordinal);
            Assert.Contains("edgeData.target_y", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void SmokeScriptRequiresEdgeKindNotBeNullOrEmpty()
    {
        var source = SocialSmokeSource;

        Assert.Contains("missing or empty kind", source, StringComparison.Ordinal);
        Assert.Contains("has empty kind", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasGetParameterValueHelper()
    {
        var source = SocialSmokeSource;

        Assert.Contains("function Get-ParameterValue", source, StringComparison.Ordinal);
        Assert.Contains("Required parameter", source, StringComparison.Ordinal);
        Assert.Contains("Duplicate parameter", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptHasGetCandidateIdFromPreconditionsHelper()
    {
        var source = SocialSmokeSource;

        Assert.Contains("function Get-CandidateIdFromPreconditions", source, StringComparison.Ordinal);
        Assert.Contains("candidate_id:*", source, StringComparison.Ordinal);
        Assert.Contains("No candidate_id precondition", source, StringComparison.Ordinal);
        Assert.Contains("Multiple candidate_id preconditions", source, StringComparison.Ordinal);
        Assert.Contains("Get-CandidateIdFromPreconditions -Preconditions", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptReadsNpcNameSlotAndStackFromParameters()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"npc_name\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"slot_index\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"qualified_item_id\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterInt -Parameters $selectedCandidate.parameters -Name \"item_stack_before\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterValue -Parameters $selectedCandidate.parameters -Name \"gift_updates_normal_limits\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterInt -Parameters $selectedCandidate.parameters -Name \"npc_tile_x\"", source, StringComparison.Ordinal);
        Assert.Contains("Get-ParameterInt -Parameters $selectedCandidate.parameters -Name \"stand_tile_x\"", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptAcceptsTileZero()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("-eq 0", source.Split(new[] { "throw" }, StringSplitOptions.None)
            .Where(s => s.Contains("tile")).DefaultIfEmpty(string.Empty).First(), StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptAcceptsOneItemGiftNullAfter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("stack should be null when exactly one item consumed", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Gift execution missing gift_stack_after", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresMultiItemStackDecrement()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift stack after must be non-null when before=", source, StringComparison.Ordinal);
        Assert.Contains("stack did not decrease by exactly 1", source, StringComparison.Ordinal);
        Assert.Contains("stack before must be >= 1", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptValidatesExactFacingTowardNpc()
    {
        var source = SocialSmokeSource;

        Assert.Contains("does not point toward NPC", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 0", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 1", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 2", source, StringComparison.Ordinal);
        Assert.Contains("$expectedFacing = 3", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresExactlyOneSocialResult()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Expected exactly 1 verified applied social_interact result", source, StringComparison.Ordinal);
        Assert.Contains("Expected exactly 1 social_interact plan step", source, StringComparison.Ordinal);
        Assert.Contains("Expected exactly 1 executor.social_interact queue item", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveTrainingLoopRankingNonOverwrittenPaths()
    {
        var loopSource = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.LiveTrainingLoop", "Program.cs"));

        Assert.Contains("ranking-response-\" + iteration.ToString(\"D4\") + \".json\"", loopSource, StringComparison.Ordinal);
        Assert.Contains("replan-ranking-response-\" + iteration.ToString(\"D4\")", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ranking-response-0000", loopSource, StringComparison.Ordinal);
        Assert.DoesNotContain("ranking-response-0001.json", loopSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresGiftSlotAndItemIdNonNull()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift execution missing gift_slot_before", source, StringComparison.Ordinal);
        Assert.Contains("Gift candidate missing qualified_item_id", source, StringComparison.Ordinal);
        Assert.Contains("Gift execution missing gift_item_id_before", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptPreservesGiftCountersWhenNotNormalLimits()
    {
        var source = SocialSmokeSource;

        Assert.Contains("gift_updates_normal_limits", source, StringComparison.Ordinal);
        Assert.Contains("candidateGiftUpdatesNormal", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresFullSnapshotProfile()
    {
        var source = SocialSmokeSource;

        Assert.Contains("?profile=full", source, StringComparison.Ordinal);
        Assert.DoesNotContain("\"/api/v1/snapshot\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/snapshot`\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("api/v1/snapshot'", source, StringComparison.Ordinal);

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("/api/v1/snapshot", StringComparison.Ordinal) &&
                !line.Contains("?profile=full", StringComparison.Ordinal) &&
                !line.Contains("?profile=fishing", StringComparison.Ordinal) &&
                !line.Contains("?profile=route", StringComparison.Ordinal) &&
                !line.Contains("?profile=light", StringComparison.Ordinal) &&
                !line.Contains("?profile=machine", StringComparison.Ordinal) &&
                !line.Contains("?profile=training_machine", StringComparison.Ordinal) &&
                !line.Contains("/api/v1/snapshots", StringComparison.Ordinal))
            {
                Assert.Fail("Script uses a bare /api/v1/snapshot URL without a profile parameter: " + line.Trim());
            }
        }
    }

    [Fact]
    public void WaitWorldSnapshotRequiresFullDomainSet()
    {
        var source = SocialSmokeSource;

        Assert.Contains("requiredDomains", source, StringComparison.Ordinal);
        Assert.Contains("missingDomains", source, StringComparison.Ordinal);
        Assert.Contains("\"player\"", source, StringComparison.Ordinal);
        Assert.Contains("\"farm\"", source, StringComparison.Ordinal);
        Assert.Contains("\"current_location\"", source, StringComparison.Ordinal);
        Assert.Contains("\"locations\"", source, StringComparison.Ordinal);
        Assert.Contains("\"npcs\"", source, StringComparison.Ordinal);
        Assert.Contains("\"quests\"", source, StringComparison.Ordinal);
        Assert.Contains("\"world_progress\"", source, StringComparison.Ordinal);
        Assert.Contains("\"mods\"", source, StringComparison.Ordinal);
        Assert.Contains("\"modded_state\"", source, StringComparison.Ordinal);
        Assert.Contains("\"fishing\"", source, StringComparison.Ordinal);
        Assert.Contains("\"mining\"", source, StringComparison.Ordinal);
        Assert.Contains("$missingDomains -join", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterEnumeratesAllLocationsWithForEachLocationIncludeInteriorsAndGenerated()
    {
        var source = NpcAdapterSource;

        Assert.Contains("Utility.ForEachLocation", source, StringComparison.Ordinal);
        Assert.Contains("includeInteriors: true", source, StringComparison.Ordinal);
        Assert.Contains("includeGenerated: true", source, StringComparison.Ordinal);
        Assert.Contains("CollectAllLoadedNpcs", source, StringComparison.Ordinal);

        Assert.DoesNotContain("= Game1.currentLocation.characters", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterProvenanceNamesForEachLocationAndConcreteSources()
    {
        var source = NpcAdapterSource;

        Assert.Contains("Utility.ForEachLocation(includeInteriors:true, includeGenerated:true)", source, StringComparison.Ordinal);
        Assert.Contains("Game1.locations, instanced interiors, MineShaft.activeMines, VolcanoDungeon.activeLevels", source, StringComparison.Ordinal);

        Assert.Contains("gift_tastes", source, StringComparison.Ordinal);
        Assert.Contains("getGiftTasteForThisItem", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterCurrentInstanceLoadedChecksActualCharacterCollection()
    {
        var source = NpcAdapterSource;

        Assert.Contains("npcCurrentLocation.characters.Any", source, StringComparison.Ordinal);
        Assert.Contains("instanceLoaded", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReferenceEquals(npc.currentLocation, Game1.currentLocation)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void NpcReadAdapterVisibleOnScreenFalseForNonCurrentLocation()
    {
        var source = NpcAdapterSource;

        Assert.Contains("isCurrentLocation", source, StringComparison.Ordinal);
        Assert.Contains("isCurrentLocation && Utility.isOnScreen", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialCandidateBuilderRemoteStandSkippingGuard()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.Core", "OptionRegistry", "SocialCandidateBuilder.cs"));

        Assert.Contains("npcLocationId", source, StringComparison.Ordinal);
        Assert.Contains("social_npc_not_in_player_location_stand_skipped", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CollectAllLoadedNpcsUsesReferenceIdentityNotCompositeStringKey()
    {
        var source = NpcAdapterSource;

        Assert.Contains("Utility.ForEachLocation", source, StringComparison.Ordinal);
        Assert.Contains("ReferenceEqualityComparer.Instance", source, StringComparison.Ordinal);
        Assert.Contains("seen.Add(npc)", source, StringComparison.Ordinal);

        Assert.DoesNotContain("npc.Name + \"|\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("HashSet<string>", source, StringComparison.Ordinal);

        Assert.Contains("var firstSeen = new List<NPC>()", source, StringComparison.Ordinal);
        Assert.Contains("firstSeen.Add(npc)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptDeterministicRouteReachableNpcFilter()
    {
        var source = SocialSmokeSource;

        Assert.Contains("ordinaryNpcs", source, StringComparison.Ordinal);
        Assert.Contains("reachableLocations", source, StringComparison.Ordinal);
        Assert.Contains("route_reachable", source, StringComparison.Ordinal);
        Assert.Contains("npcs-considered-for-route.json", source, StringComparison.Ordinal);
        Assert.Contains("vanilla_social_query_supported", source, StringComparison.Ordinal);
        Assert.Contains("can_socialize_complete", source, StringComparison.Ordinal);
        Assert.Contains("is_sleeping -ne", source, StringComparison.Ordinal);
        Assert.Contains("is_invisible -ne", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RejectsSuffixedRunIdPatterns()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("$RunId.route-bfs", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$RunId.talk", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$RunId.close-dialogue", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$RunId.gift", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RequiresAllExecutionPhasesUseExactBaseRunId()
    {
        var source = SocialSmokeSource;

        Assert.Contains("run_id = \"$RunId\"", source, StringComparison.Ordinal);

        Assert.Contains("$talkRunId = \"$RunId\"", source, StringComparison.Ordinal);
        Assert.Contains("--run-id $talkRunId", source, StringComparison.Ordinal);

        Assert.Contains("$closeRunId = \"$RunId\"", source, StringComparison.Ordinal);
        Assert.Contains("--run-id $closeRunId", source, StringComparison.Ordinal);

        Assert.Contains("$giftRunId = \"$RunId\"", source, StringComparison.Ordinal);
        Assert.Contains("--run-id $giftRunId", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HasSeparatePhaseRootDirectories()
    {
        var source = SocialSmokeSource;

        Assert.Contains("$talkLoopRoot = Join-Path $runDirectory \"talk-loop\"", source, StringComparison.Ordinal);
        Assert.Contains("$giftLoopRoot = Join-Path $runDirectory \"gift-loop\"", source, StringComparison.Ordinal);
        Assert.Contains("--root (Join-Path $runDirectory \"close-dialogue-loop\")", source, StringComparison.Ordinal);

        Assert.DoesNotContain("$talkLoopRoot = $runDirectory", source, StringComparison.Ordinal);
        Assert.DoesNotContain("$giftLoopRoot = $runDirectory", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceIdentityHashSetRetainsDistinctInstancesCollapsesSameReference()
    {
        var a = new NpcReferenceTestProxy { Name = "Robin", Location = "Farm", TileX = 5, TileY = 10 };
        var b = new NpcReferenceTestProxy { Name = "Robin", Location = "Farm", TileX = 5, TileY = 10 };

        var set = new HashSet<NpcReferenceTestProxy>(ReferenceEqualityComparer.Instance);
        Assert.True(set.Add(a));
        Assert.True(set.Add(b));

        Assert.Equal(2, set.Count);
        Assert.Contains(a, set);
        Assert.Contains(b, set);

        Assert.False(set.Add(a));
        Assert.Equal(2, set.Count);
    }

    private sealed class NpcReferenceTestProxy
    {
        public string Name { get; init; } = "";
        public string Location { get; init; } = "";
        public int TileX { get; init; }
        public int TileY { get; init; }
    }

    [Fact]
    public void SmokeScriptDoesNotRequireSocialPlanOnSocialInteract()
    {
        var source = SocialSmokeSource;

        Assert.DoesNotContain("social_plan is null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.action_kind", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.requested_npc_name", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.requested_slot_index", source, StringComparison.Ordinal);
        Assert.DoesNotContain("social_plan.requested_qualified_item_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptStillRequiresNormalizedTalkParameters()
    {
        var source = SocialSmokeSource;

        Assert.Contains("npc_name", source, StringComparison.Ordinal);
        Assert.Contains("social_action_kind", source, StringComparison.Ordinal);
        Assert.Contains("target_location", source, StringComparison.Ordinal);
        Assert.Contains("npc_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("npc_tile_y", source, StringComparison.Ordinal);
        Assert.Contains("stand_tile_x", source, StringComparison.Ordinal);
        Assert.Contains("stand_tile_y", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptStillRequiresNormalizedGiftParameters()
    {
        var source = SocialSmokeSource;

        Assert.Contains("slot_index", source, StringComparison.Ordinal);
        Assert.Contains("qualified_item_id", source, StringComparison.Ordinal);
        Assert.Contains("item_stack_before", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesNormalizedParametersMatchCandidate()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Queue social item npc_name mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item action_kind", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item target_location mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Queue social item missing npc_tile_x", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptVerifiesNormalizedParametersMatchCandidateGift()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Gift queue npc_name mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue action_kind", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue target_location mismatch", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue social item missing slot_index", source, StringComparison.Ordinal);
        Assert.Contains("Gift queue social item missing qualified_item_id", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptReadsActiveMenuAsObjectNotScalarString()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Get-SnapshotObject", source, StringComparison.Ordinal);

        var lines = source.Split('\n');
        foreach (var line in lines)
        {
            if (line.Contains("Get-SnapshotString", StringComparison.Ordinal) &&
                line.Contains("active_menu", StringComparison.Ordinal))
            {
                Assert.Fail("Get-SnapshotString used with active_menu: " + line.Trim());
            }
        }
    }

    [Fact]
    public void SmokeScriptChecksActiveMenuIsOpenViaPropertyNotStringCompare()
    {
        var source = SocialSmokeSource;

        Assert.Contains("activeMenu.is_open", source, StringComparison.Ordinal);
        Assert.Contains("afterCloseActiveMenu.is_open", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFailsClosedOnMissingActiveMenuAfterTalk()
    {
        var source = SocialSmokeSource;

        Assert.Contains("active_menu missing or null after talk", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open missing or null after talk", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open must be boolean after talk", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptFailsClosedOnMissingActiveMenuAfterClose()
    {
        var source = SocialSmokeSource;

        Assert.Contains("active_menu missing or null after close", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open missing or null after close", source, StringComparison.Ordinal);
        Assert.Contains("active_menu.is_open must be boolean after close", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptGetSnapshotObjectHasNoDefaultParameter()
    {
        var source = SocialSmokeSource;

        var functionBody = ExtractFunctionBody(source, "Get-SnapshotObject");
        Assert.DoesNotContain("$Default", functionBody, StringComparison.Ordinal);
        Assert.DoesNotContain("[int]", functionBody, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptActiveMenuIsOpenCheckedWithIsNotBoolGuard()
    {
        var source = SocialSmokeSource;

        Assert.Contains("is_open -isnot [bool]", source, StringComparison.Ordinal);
        Assert.Contains("-isnot [bool]", source, StringComparison.Ordinal);
    }

    [Fact]
    public void PlayerAdapterEncodesKnownNoSpouseAsAvailableCanonicalEmptyString()
    {
        var source = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.cs"));

        Assert.Contains("Context.IsWorldReady && player is not null ? (player.spouse ?? string.Empty) : null", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Context.IsWorldReady ? player?.spouse : null", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SmokeScriptRequiresReadableNonNullSpouseProof()
    {
        var source = SocialSmokeSource;

        Assert.Contains("Snapshot after wait does not have readable player.spouse", source, StringComparison.Ordinal);
        Assert.Contains("known no-spouse state as null instead of canonical empty string", source, StringComparison.Ordinal);
    }

    private static string ExtractFunctionBody(string source, string functionName)
    {
        var lines = source.Split('\n');
        var inside = false;
        var braceDepth = 0;
        var body = new System.Collections.Generic.List<string>();
        foreach (var line in lines)
        {
            if (!inside)
            {
                if (line.TrimStart().StartsWith("function " + functionName, StringComparison.Ordinal))
                {
                    inside = true;
                }
                continue;
            }

            body.Add(line);
            braceDepth += line.Count(c => c == '{') - line.Count(c => c == '}');
            if (braceDepth <= 0)
            {
                break;
            }
        }

        return string.Join("\n", body);
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Could not locate repository file", Path.Combine(parts));
    }
}
