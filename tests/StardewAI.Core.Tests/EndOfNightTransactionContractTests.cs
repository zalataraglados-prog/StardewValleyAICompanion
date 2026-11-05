using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class EndOfNightTransactionContractTests
{
    [Fact]
    public void ExecutionResultCarriesNightTransactionLineage()
    {
        var result = new TrainingExecutionResult
        {
            SourcePlanningOptionId = "exploration.visit_location",
            EndOfNightTransactionId = "run.night.1",
            EndOfNightTransactionStatus = "awaiting_planned_profession_selection",
            EndOfNightTransactionEvidencePath = "evidence/night.json"
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(result));
        var root = json.RootElement;
        Assert.Equal(
            "exploration.visit_location",
            root.GetProperty("source_planning_option_id").GetString());
        Assert.Equal(
            "run.night.1",
            root.GetProperty("end_of_night_transaction_id").GetString());
        Assert.Equal(
            "awaiting_planned_profession_selection",
            root.GetProperty("end_of_night_transaction_status").GetString());
        Assert.Equal(
            "evidence/night.json",
            root.GetProperty("end_of_night_transaction_evidence_path").GetString());
    }

    [Fact]
    public void ExecutionRequestCarriesNativeFarmEventFixtureSelection()
    {
        var request = new TrainingExecutionRequest
        {
            SourcePlanningOptionId = "exploration.visit_location",
            DebugEndOfNightOrdinaryLevelCount = 2,
            DebugEndOfNightFarmEvent = "day_31_earthquake",
            DebugEndOfNightNegativeProfile =
                "unknown_menu_transition_timeout"
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(request));
        var root = json.RootElement;
        Assert.Equal(
            "exploration.visit_location",
            root.GetProperty("source_planning_option_id").GetString());
        Assert.Equal(
            2,
            root.GetProperty("debug_end_of_night_ordinary_level_count")
                .GetInt32());
        Assert.Equal(
            "day_31_earthquake",
            root.GetProperty("debug_end_of_night_farm_event").GetString());
        Assert.Equal(
            "unknown_menu_transition_timeout",
            root.GetProperty("debug_end_of_night_negative_profile").GetString());
    }

    [Fact]
    public void RuntimeTracksOneNightWithoutAddingASecondProfessionExecutor()
    {
        var root = FindRepositoryRoot();
        var sleep = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Sleep.cs"));
        var sleepState = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.State.RecoveryShipping.cs"));
        var tracker = string.Concat(
            File.ReadAllText(Path.Combine(
                root,
                "tools",
                "StardewAI.RuntimeTestHarness",
                "ModEntry.EndOfNightTransaction.cs")),
            File.ReadAllText(Path.Combine(
                root,
                "tools",
                "StardewAI.RuntimeTestHarness",
                "ModEntry.EndOfNightTransaction.Observation.cs")));
        var farming = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.Farming.cs"));
        var runtime = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.cs"));
        var fullShipmentFixture = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.FullShipmentFixture.cs"));
        var levelUpFixture = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.LevelUpFixture.cs"));
        var partnershipFixture = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.PartnershipFixture.cs"));
        var nativeStoryHandoffFixture = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.RuntimeTestHarness",
            "ModEntry.NativeStoryHandoffFixture.cs"));
        var compoundSmoke = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeFullShipmentTerminalSmoke.ps1"));
        var positiveMatrix = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeEndOfNightPositiveMatrix.ps1"));
        var storyHandoffMatrix = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeEndOfNightStoryHandoffMatrix.ps1"));
        var negativeMatrix = File.ReadAllText(Path.Combine(
            root,
            "scripts",
            "Invoke-RuntimeEndOfNightNegativeMatrix.ps1"));
        var runtimeExecution = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecution.cs"));
        var runtimeExecutionRequest = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.LiveTrainingLoop",
            "Program.RuntimeExecutionRequest.cs"));
        var noProgressBackoff = File.ReadAllText(Path.Combine(
            root,
            "tools",
            "StardewAI.LiveTrainingLoop",
            "NoProgressBackoffPolicy.cs"));

        Assert.Contains("BeginEndOfNightTransaction(activeSleep)", sleep,
            StringComparison.Ordinal);
        Assert.Contains("HasPendingNativeEndOfNightTransition", sleep,
            StringComparison.Ordinal);
        var beginTransaction = Slice(
            tracker,
            "private void BeginEndOfNightTransaction(",
            "private bool HasPendingNativeEndOfNightTransition(");
        Assert.Contains("RequiresNativeTransitionControl(", beginTransaction,
            StringComparison.Ordinal);
        Assert.Contains("previous.SawSafeNewDayReadyState", beginTransaction,
            StringComparison.Ordinal);
        Assert.True(
            beginTransaction.IndexOf(
                "end_of_night_followup_not_observed_before_next_sleep",
                StringComparison.Ordinal) <
            beginTransaction.IndexOf(
                "endOfNightTransaction = new EndOfNightTransaction",
                StringComparison.Ordinal));
        Assert.Contains("BlockEndOfNightTransactionLocked(", beginTransaction,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Status = \"complete\"", beginTransaction,
            StringComparison.Ordinal);
        var nativeControlGuard = Slice(
            tracker,
            "private bool HasPendingNativeEndOfNightTransition(",
            "private void MarkEndOfNightSleepConfirmed(");
        Assert.Contains("RequiresNativeTransitionControl(", nativeControlGuard,
            StringComparison.Ordinal);
        Assert.Contains("transaction.SawSafeNewDayReadyState", nativeControlGuard,
            StringComparison.Ordinal);
        var pauseEvidence = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness",
            "ModEntry.PlanningPauseApi.cs"));
        Assert.Contains("!HasPendingNativeEndOfNightTransition(out _)",
            pauseEvidence, StringComparison.Ordinal);
        Assert.Contains("YieldEndOfNightForPlannedProfession(sleep)", sleep,
            StringComparison.Ordinal);
        Assert.Contains("MarkEndOfNightSleepConfirmed(sleep)", sleep,
            StringComparison.Ordinal);
        Assert.Contains(
            "post_sleep_profession_choice_requires_planned_selection",
            sleep,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.professions.Add", sleep,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.professions.Add", tracker,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            Count(farming, "private TrainingExecutionResult ExecuteLevelUpMenu("));

        Assert.Contains("Game1.farmEvent is not null", tracker,
            StringComparison.Ordinal);
        Assert.True(
            tracker.IndexOf(
                "if (Game1.currentMinigame is not null)",
                StringComparison.Ordinal) <
            tracker.IndexOf(
                "if (Game1.farmEvent is not null)",
                StringComparison.Ordinal));
        Assert.Contains("Game1.activeClickableMenu is ShippingMenu", tracker,
            StringComparison.Ordinal);
        Assert.Contains("Game1.activeClickableMenu is SaveGameMenu", tracker,
            StringComparison.Ordinal);
        Assert.Contains("transaction.SawNativeSaveCommit", tracker,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.Date.TotalDays == transaction.ExpectedTotalDays",
            tracker,
            StringComparison.Ordinal);
        Assert.Contains("ready_for_safe_next_day_action", tracker,
            StringComparison.Ordinal);
        Assert.Contains("saw_safe_new_day_ready_state", tracker,
            StringComparison.Ordinal);
        Assert.DoesNotContain("saw_safe_new_day_control", tracker,
            StringComparison.Ordinal);
        Assert.Contains("safe_next_day_action:", tracker,
            StringComparison.Ordinal);
        Assert.Contains("SafeNextDayPlanningOptionId", tracker,
            StringComparison.Ordinal);
        Assert.Contains("SafeNextDayQueueItemId", tracker,
            StringComparison.Ordinal);
        var completionObserver = Slice(
            tracker,
            "private void ObserveCompletedExecutionForEndOfNight(",
            "private object ReadEndOfNightTransactionSnapshot()");
        Assert.DoesNotContain("Game1.", completionObserver,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Context.", completionObserver,
            StringComparison.Ordinal);
        Assert.Contains("transaction.TerminalResultAttached", tracker,
            StringComparison.Ordinal);
        Assert.Contains(
            "CaptureEndOfNightWorldStateLocked(transaction);",
            tracker,
            StringComparison.Ordinal);
        Assert.Contains("EndOfNightTransactionPolicy", tracker,
            StringComparison.Ordinal);
        Assert.Contains(
            "EndOfNightTransactionPolicy.MatchesActiveSleepRequest(",
            tracker,
            StringComparison.Ordinal);
        Assert.Contains(
            "EndOfNightTransactionPolicy.MaximumOverallTicks +",
            sleepState,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Math.Max(30000", sleepState,
            StringComparison.Ordinal);
        Assert.Contains(
            "OptionCapabilityRegistrySource.TryGet(",
            completionObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "ProductExecutorCapabilityCatalog.IsSupported(result.OptionId)",
            completionObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "planningCapability.CandidateStatus !=",
            completionObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "planningCapability.PlayerConfirmationRequired",
            completionObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "OptionInvocationPolicy.PolicyOrAutonomous",
            completionObserver,
            StringComparison.Ordinal);
        Assert.Contains(
            "selectedQueueDecision.CandidateAt(selectedQueueIndex).OptionId",
            runtimeExecution,
            StringComparison.Ordinal);
        Assert.Contains(
            "ReadString(item, \"runtime_priority_interrupt\")",
            runtimeExecution,
            StringComparison.Ordinal);
        Assert.Contains(
            "SourcePlanningOptionId = sourcePlanningOptionId",
            runtimeExecutionRequest,
            StringComparison.Ordinal);
        Assert.Contains("if (transaction.SleepConfirmed)", tracker,
            StringComparison.Ordinal);
        Assert.Contains("end_of_night_transaction_overall_timeout", tracker,
            StringComparison.Ordinal);
        Assert.Contains("end_of_night_transition_timeout:", tracker,
            StringComparison.Ordinal);
        Assert.Contains("new QuestLog()", tracker,
            StringComparison.Ordinal);
        Assert.Contains(
            "EndOfNightUnknownMenuTransitionTimeoutProfile",
            tracker,
            StringComparison.Ordinal);
        Assert.Contains("EndOfNightOverallTimeoutProfile", tracker,
            StringComparison.Ordinal);
        Assert.Contains("debug_negative_fixture_injected", tracker,
            StringComparison.Ordinal);
        var negativeFixture = Slice(
            tracker,
            "private static bool TryInjectEndOfNightNegativeFixture(",
            "private static bool TryObserveSleepTransitionBudget(");
        var activeSleepGuard = negativeFixture.IndexOf(
            "if (sleep is not null ||",
            StringComparison.Ordinal);
        var nativeMenuInjection = negativeFixture.IndexOf(
            "Game1.activeClickableMenu = new QuestLog();",
            StringComparison.Ordinal);
        Assert.True(activeSleepGuard >= 0);
        Assert.True(nativeMenuInjection > activeSleepGuard);
        Assert.Contains(
            "SanitizeFileName(transaction.TransactionId)",
            tracker,
            StringComparison.Ordinal);
        Assert.DoesNotContain("skipEvent", tracker,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.eventUp = false", tracker,
            StringComparison.Ordinal);
        Assert.Contains(
            "/api/v1/training/end-of-night-transaction",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "var hasPendingNativeEndOfNightTransition =",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "HasPendingNativeEndOfNightTransition(out _);",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "!hasPendingNativeEndOfNightTransition &&",
            runtime,
            StringComparison.Ordinal);
        Assert.True(
            runtime.IndexOf(
                "var hasPendingNativeEndOfNightTransition =",
                StringComparison.Ordinal) <
            runtime.IndexOf(
                "var shouldPauseForExecutorIdle =",
                StringComparison.Ordinal));
        Assert.True(
            runtime.IndexOf(
                "ObserveEndOfNightTransaction(activeSleep);",
                StringComparison.Ordinal) <
            runtime.IndexOf(
                "TickTileMove(e);",
                StringComparison.Ordinal));
        Assert.Contains(
            "TryPreparePendingProfessionLevel(",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "PreparePendingSkillLevel(",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Farmer.getBaseExperienceForLevel(level)",
            levelUpFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "LevelUpMenu.RevalidateHealth(player)",
            levelUpFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "menu.getImmediateProfessionPerk(",
            levelUpFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            ".Where(candidateSkill => candidateSkill != skill)",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "if (request.ProfessionChoiceId is < 0 or > 29)",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.True(
            fullShipmentFixture.IndexOf(
                "if (request.ProfessionChoiceId is < 0 or > 29)",
                StringComparison.Ordinal) <
            fullShipmentFixture.IndexOf(
                "Game1.player.newLevels.Clear();",
                StringComparison.Ordinal));
        Assert.True(
            fullShipmentFixture.IndexOf(
                "var result = ExecutePrepareNativeSleepFixture(",
                StringComparison.Ordinal) <
            fullShipmentFixture.IndexOf(
                "Game1.player.newLevels.Clear();",
                StringComparison.Ordinal));
        Assert.Contains(
            "Utility.pickFarmEvent_day_31",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "spouseFriendship.WeddingDate is not null",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "farmer.isEngaged()",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "farmer.hasCurrentOrPendingRoommate()",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.True(
            fullShipmentFixture.IndexOf(
                "HasDay31WeddingConflict()",
                StringComparison.Ordinal) <
            fullShipmentFixture.IndexOf(
                "var result = ExecutePrepareNativeSleepFixture(",
                StringComparison.Ordinal));
        Assert.Contains("Game1.currentSeason = \"summer\"",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.dayOfMonth = 2",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new SoundInTheNightEvent",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Game1.farmEvent =",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new LevelUpMenu",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "pendingEndOfNightNegativeProfile = string.Empty;",
            partnershipFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "pendingEndOfNightNegativeProfileRunId = string.Empty;",
            partnershipFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "pendingEndOfNightNegativeProfileRunId,",
            tracker,
            StringComparison.Ordinal);
        Assert.Contains(
            "debug.prepare_native_story_handoff",
            runtime,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_kent_post_save",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.dayOfMonth = 2", nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.stats.DaysPlayed = 114", nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.stats.DaysPlayed = 113", nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.stats.DaysPlayed = 25", nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.stats.DaysPlayed = 26", nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_cat_adoption_not_first_eligible_farm_event:",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "projectedWakeTotalMoneyEarned",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "GameLocation.IsValidLocationEvent(pair.Key, pair.Value)",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "item.sellToStorePrice(-1L)",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_cat_adoption_unsafe_question",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Data\\\\Events\\\\Farm",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "farm.checkEventPrecondition(pair.Key)",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"/broadcastEvent/\"",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"/catQuestion\"",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Pet.TryGetData(\"Cat\"",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "1590166/m 1000/t 600 930/d Mon Tue Thu Sat Sun/w sunny/h cat/H",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "events.TryGetValue(",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "var preparedTotalMoneyEarned = Math.Max(",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.player.totalMoneyEarned = projectedWakeTotalMoneyEarned;",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.dayOfMonth = 25",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains(
            "Game1.weatherForTomorrow = Game1.weather_sunny",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "Game1.player.totalMoneyEarned >= 1000",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "new Event(",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            ".startEvent(",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "eventsSeen.Add",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "eventUp = true",
            nativeStoryHandoffFixture,
            StringComparison.Ordinal);
        Assert.Contains("PendingFarmEvent", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "Owner composite requires visible positive shipping",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("$matrixScenario = $NoShipment -or",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$sleepFixtureRequest.debug_end_of_night_ordinary_level_count",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("farm_event:SoundInTheNightEvent", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("NoShipment", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "-ExpectedTerminalBinCount $(if ($NoShipment) { 0 } else { 1 })",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "-ExpectedShippedCount $(if ($NoShipment) { 153 } else { 154 })",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "-ExpectedMissingCount $(if ($NoShipment) { 1 } else { 0 })",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "-ExpectedTerminalShippedCount $(if ($NoShipment) { 0 } else { 1 })",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "-ExpectedAchievement (-not $NoShipment)",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("saw_save_menu", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("start_total_days = [int]$tracker.start_total_days",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("expected_total_days = [int]$tracker.expected_total_days",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("([int]$tracker.start_total_days + 1)",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("money_before_settlement", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("profession_skill_id", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("profession_level", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("profession_title", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("Get-TerminalBinSettlementValue",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("total_sell_to_store_price",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "$expectedSettlementIncome = [int]$item.sell_to_store_price",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.True(
            compoundSmoke.IndexOf(
                "$sleepReadySnapshot = Wait-WorldSnapshot",
                StringComparison.Ordinal) <
            compoundSmoke.IndexOf(
                "$expectedSettlementIncome = Get-TerminalBinSettlementValue",
                StringComparison.Ordinal));
        Assert.Contains("owner_visual_gate_passed = $false", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("\"CLIENT_FPS\"", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$env:CLIENT_FPS = if ($VisibleGame) { \"60\" } else { \"0\" }",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.True(
            compoundSmoke.IndexOf(
                "$env:CLIENT_FPS = if ($VisibleGame)",
                StringComparison.Ordinal) <
            compoundSmoke.IndexOf(
                "$game = Start-Process -FilePath $smapi",
                StringComparison.Ordinal));
        Assert.Contains("Get-VerifiedEndOfNightEvidence", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("native_stage_one_settlement_date_prepared",
            fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.year = 2;", fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.currentSeason = \"fall\";", fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.dayOfMonth = 1;", fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains("Game1.stats.DaysPlayed = 169;", fullShipmentFixture,
            StringComparison.Ordinal);
        Assert.Contains("\"end_of_night_transaction.v1\"", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "schema_version = \"end_of_night_transaction.v1\"",
            tracker,
            StringComparison.Ordinal);
        Assert.DoesNotContain("end-of-night_transaction.v1", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("transaction_evidence_sha256", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Equal(
            3,
            Count(compoundSmoke, "transaction_evidence_sha256 ="));
        Assert.Equal(
            3,
            Count(compoundSmoke, "transaction_evidence_path ="));
        Assert.DoesNotContain("            evidence_sha256 =", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("Read-LogicalJsonArtifact", compoundSmoke,
            StringComparison.Ordinal);
        var logicalReader = File.ReadAllText(Path.Combine(
            root, "scripts", "lib", "RuntimeLogicalJson.ps1"));
        Assert.Contains("Read-RuntimeLogicalJsonArtifact", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "ContentAddressedJsonArtifactReader]::ReadAllText",
            logicalReader,
            StringComparison.Ordinal);
        Assert.Contains("$readerType =", logicalReader,
            StringComparison.Ordinal);
        Assert.Contains("-as [type]", logicalReader,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"--iterations\", $MaxLoopAttempts.ToString()",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("-MaxLoopAttempts 8", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("$report.attempts_started", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("$loopExitCode = $LASTEXITCODE", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("$expectedBlockedExit", compoundSmoke,
            StringComparison.Ordinal);
        Assert.DoesNotContain("function Wait-ProfessionChoiceSnapshot", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$professionReadySnapshot = $sleep.after",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "StageOneMandatoryMenuRecovery.ProfessionInputSettleKind",
            File.ReadAllText(Path.Combine(
                root,
                "src",
                "StardewAI.Core",
                "OptionRegistry",
                "CandidateOptionAvailabilityEvaluator.Recovery.cs")),
            StringComparison.Ordinal);
        Assert.Contains(
            "recovery_wait_for_level_up_profession_input_",
            noProgressBackoff,
            StringComparison.Ordinal);
        Assert.Contains(
            "preferredProfessionCandidateId",
            File.ReadAllText(Path.Combine(
                root,
                "tools",
                "StardewAI.LiveTrainingLoop",
                "Program.StageOneMandatoryMenuRecovery.cs")),
            StringComparison.Ordinal);
        Assert.Contains("\"blocked\" -in $AllowedExecutionStatuses",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("if ($MaxLoopAttempts -eq 1)", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$arguments.Add(\"--emit-queue-execution-receipt\")",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "after = Get-Content -LiteralPath $afterPath -Raw | ConvertFrom-Json",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("candidate_binary_sha256", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("candidate_runtime_bundle_sha256", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("candidate_script_sha256", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("project_head_commit", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("project_worktree_dirty", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("project_worktree_status_sha256", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "$safeNextDaySteps = @($nextDay.execution.step_results",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "safe_next_day_planning_option_id",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("saw_safe_new_day_ready_state", compoundSmoke,
            StringComparison.Ordinal);
        Assert.DoesNotContain("saw_safe_new_day_control", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "\"executor.traverse_connector\"",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("PostSaveStoryHandoff", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("Get-ExactFarmEntryConnector", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("debug.prepare_native_story_handoff", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("story-event:63:", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("Get-VerifiedCompletedStoryInterrupt", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "safe_next_day_runtime_priority_interrupt",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "player.story_event.event_id",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "player.story_event.event_seen",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("skipEvent_not_used", compoundSmoke,
            StringComparison.Ordinal);
        Assert.True(
            compoundSmoke.IndexOf(
                "$completedInterrupt = Get-VerifiedCompletedStoryInterrupt",
                StringComparison.Ordinal) <
            compoundSmoke.IndexOf(
                "-Name \"06-native-kent-story-handoff\"",
                StringComparison.Ordinal));
        Assert.Contains("story-event:1590166:", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains("story_event_dialogue_response_mismatch", compoundSmoke,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_question_held_for_model_decision",
            compoundSmoke,
            StringComparison.Ordinal);
        Assert.Equal(7, Count(positiveMatrix, "id = \""));
        Assert.Contains("positive-shipping", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("Level5ProfessionChoiceId", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("Level10ProfessionChoiceId", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains(
            "Level-5 and level-10 profession IDs must form one legal vanilla branch.",
            positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("empty-bin-save", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("ordinary-levels-save", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("profession-and-remaining-save-menus", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("shipping-and-ordinary-levels", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("shipping-and-profession", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("day-31-full-composite", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("& $smokeScript @arguments", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("[switch] $OwnerVisualGate", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("$arguments.VisibleGame = $true", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("$arguments.OwnerComposite = $true", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains("transaction_evidence_sha256", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Contains(
            "Owner visual matrix requires one committed clean-worktree candidate.",
            positiveMatrix,
            StringComparison.Ordinal);
        Assert.True(
            positiveMatrix.IndexOf(
                "git -C $ProjectRoot status",
                StringComparison.Ordinal) <
            positiveMatrix.IndexOf(
                "New-Item -ItemType Directory",
                StringComparison.Ordinal));
        Assert.DoesNotContain("Invoke-LoopStep", positiveMatrix,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI.exe", positiveMatrix,
            StringComparison.Ordinal);
        Assert.Equal(
            2,
            storyHandoffMatrix.Split('\n').Count(line =>
                line.StartsWith("    \"native_", StringComparison.Ordinal)));
        Assert.Contains("native_kent_post_save", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains(
            "native_cat_adoption_unsafe_question",
            storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains("& $smokeScript @arguments", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains("candidate_binary_sha256", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains("PostSaveStoryVisualHoldSeconds = 5",
            storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains("candidate_runtime_bundle_sha256", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains("transaction_evidence_sha256", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains("project_worktree_status_sha256", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.True(
            storyHandoffMatrix.IndexOf(
                "git -C $ProjectRoot status",
                StringComparison.Ordinal) <
            storyHandoffMatrix.IndexOf(
                "New-Item -ItemType Directory",
                StringComparison.Ordinal));
        Assert.DoesNotContain("Invoke-LoopStep", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI.exe", storyHandoffMatrix,
            StringComparison.Ordinal);
        Assert.Contains("unknown_menu_transition_timeout", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("overall_timeout", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("EndOfNightNegativeProfile", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("NegativeVisualHoldSeconds = 5", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("transaction_failure_reason", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("candidate_binary_sha256", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("candidate_runtime_bundle_sha256", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("transaction_evidence_sha256", negativeMatrix,
            StringComparison.Ordinal);
        Assert.Contains("project_worktree_status_sha256", negativeMatrix,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Invoke-LoopStep", negativeMatrix,
            StringComparison.Ordinal);
        Assert.DoesNotContain("StardewModdingAPI.exe", negativeMatrix,
            StringComparison.Ordinal);
    }

    private static int Count(string source, string needle)
    {
        var count = 0;
        var offset = 0;
        while ((offset = source.IndexOf(
                   needle,
                   offset,
                   StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += needle.Length;
        }
        return count;
    }

    private static string Slice(string source, string start, string end)
    {
        var startIndex = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(startIndex >= 0, $"Missing source marker: {start}");
        var endIndex = source.IndexOf(
            end,
            startIndex + start.Length,
            StringComparison.Ordinal);
        Assert.True(endIndex > startIndex, $"Missing source marker: {end}");
        return source[startIndex..endIndex];
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(
                    current.FullName,
                    "StardewValleyAICompanion.sln")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new DirectoryNotFoundException(
            "Repository root was not found.");
    }
}
