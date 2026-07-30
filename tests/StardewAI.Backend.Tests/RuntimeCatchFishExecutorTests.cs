using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class RuntimeCatchFishExecutorTests
{
    [Fact]
    public void ActionFeatureVectorCarriesNormalizedInputAndObservedOutput()
    {
        var json = JsonSerializer.Serialize(new ActionFeatureVector
        {
            NormalizedParameters = new[] { new SmallModelActionParameter { Name = "outcome_distribution_json", Value = "[{\"qualified_item_id\":\"(O)152\"}]" } },
            PrimitiveVerificationReasons = new[] { "observed_qualified_item_id=(O)152" },
            RequestedEffect = "fishing.catch;outcome_distribution_complete=true",
            ObservedEffect = "rod_state=fishCaught=False",
            ChangedFacts = new[] { new SimulatedFactChange { Path = "fishing.caught_qualified_item_id", Before = string.Empty, After = "(O)152" } }
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("outcome_distribution_json", root.GetProperty("normalized_parameters")[0].GetProperty("name").GetString());
        Assert.Equal("(O)152", root.GetProperty("changed_facts")[0].GetProperty("after").GetString());
        Assert.Contains("(O)152", root.GetProperty("primitive_verification_reasons")[0].GetString());
    }

    [Fact]
    public void CatchFishRequestCarriesNormalizedExecutorFields()
    {
        var json = JsonSerializer.Serialize(new TrainingExecutionRequest
        {
            OptionId = "executor.catch_fish",
            MaxMovementTiles = 31,
            LocationId = "Mountain",
            StandTileX = 42,
            StandTileY = 18,
            BobberTileX = 42,
            BobberTileY = 15,
            RodSlotIndex = 2,
            RuleKey = "distribution:Mountain|2|42|18|42|15|0|3",
            OutcomeDistributionComplete = true,
            OutcomeDistributionJson = "[{\"source_kind\":\"rule\",\"source_key\":\"Data/Locations:Mountain:158\",\"outcome_index\":0,\"item_id\":\"158\",\"qualified_item_id\":\"(O)158\",\"chance_preview\":0.5,\"probability_status\":\"local_preview\"}]",
            PossibleQualifiedItemIdsJson = "[\"(O)158\"]",
            OutcomeProbabilityStatus = "all_local_previews_known"
        }, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        Assert.Equal("executor.catch_fish", root.GetProperty("option_id").GetString());
        Assert.Equal(31, root.GetProperty("max_movement_tiles").GetInt32());
        Assert.Equal("Mountain", root.GetProperty("location_id").GetString());
        Assert.Equal(42, root.GetProperty("stand_tile_x").GetInt32());
        Assert.Equal(18, root.GetProperty("stand_tile_y").GetInt32());
        Assert.Equal(42, root.GetProperty("bobber_tile_x").GetInt32());
        Assert.Equal(15, root.GetProperty("bobber_tile_y").GetInt32());
        Assert.Equal(2, root.GetProperty("rod_slot_index").GetInt32());
        Assert.StartsWith("distribution:", root.GetProperty("rule_key").GetString(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, root.GetProperty("expected_qualified_item_id").GetString());
        Assert.True(root.GetProperty("outcome_distribution_complete").GetBoolean());
        Assert.Contains("(O)158", root.GetProperty("outcome_distribution_json").GetString());
        Assert.Equal("[\"(O)158\"]", root.GetProperty("possible_qualified_item_ids_json").GetString());
        Assert.Equal("all_local_previews_known", root.GetProperty("outcome_probability_status").GetString());
    }

    [Fact]
    public void RuntimeCatchFishBranchIsFailClosedAndDoesNotUseForbiddenCatchMutation()
    {
        var source = RuntimeHarnessSources.All;
        Assert.True(
            RuntimeTestHarnessDispatchCatalog.IsSupported(
                "executor.catch_fish"));
        Assert.Contains("StartCatchFish(pending);", source);
        Assert.Contains("ValidateCatchFishStart", source);
        Assert.Contains("ValidateCatchFishContinuity", source);
        Assert.Contains("catch_fish_bobber_tile_mismatch_after_cast", source);
        Assert.Contains("CatchFishPostStateVerified", source);
        Assert.Contains("CatchFishCastDiagnostic", source);
        Assert.Contains("!rod.pullingOutOfWater", source);
        Assert.Contains("catch_fish_ended_without_verified_catch", source);
        Assert.DoesNotContain("GetMethod(\"startCasting\"", source, StringComparison.Ordinal);
        Assert.Contains("UpdateTicking += OnExecutorUpdateTicking", source);
        Assert.Contains("CancelCatchFish(active);", source);
        Assert.Contains("fishing.caught_qualified_item_id", source);
        Assert.Contains("catch_fish_expected_item_must_be_unconstrained", source);
        Assert.Contains("catch_fish_outcome_distribution_incomplete", source);
        Assert.Contains("TryValidateFishingOutcomeDistribution", source);
        Assert.Contains("fishing.planned_outcome_distribution_json", source);
        Assert.Contains("candidate_item_match=unconstrained", source);
        Assert.Contains("catch_fish_observed_outcome_not_in_compiled_distribution", source);
        Assert.Contains("observed_outcome_in_compiled_distribution", source);
        Assert.Contains("CompleteObservedBlockedCatchFish", source);
        Assert.Contains("debug.setup_fish_frenzy", source);
        Assert.Contains("location.fishFrenzyFish.Value = fish.QualifiedItemId", source);
        Assert.Contains("location.fishSplashPoint.Value = tile", source);
        Assert.Contains("Helper.Reflection.GetField<int>(location, \"fishSplashPointTime\")", source);
        Assert.Contains("frenzyTimeField.SetValue(Game1.timeOfDay)", source);
        Assert.Contains("debug.setup_fish_pond", source);
        Assert.Contains("farm.buildStructure(pond, topLeft, Game1.player, skipSafetyChecks: false)", source);
        Assert.Contains("farm.isBuildable", source);
        Assert.Contains("isThereAnythingtoPreventConstruction", source);
        Assert.Contains("pond.currentOccupants.Value = 1", source);
        Assert.DoesNotContain("pond.CatchFish()", source, StringComparison.Ordinal);
        Assert.Contains("debug.setup_mine_fishing_floor", source);
        Assert.Contains("Game1.enterMine(request.MineLevel.Value)", source);
        Assert.Contains("mine.getMineArea() == MineShaft.lavaArea", source);
        Assert.Contains("predictedFishCenter + 32f - bar.bobberBarHeight / 2f", source);
        Assert.Contains("MathF.Sqrt(2f * acceleration * MathF.Abs(positionError))", source);
        Assert.Contains("MathF.Sign(positionError) * MathF.Min(5f, reachableRelativeSpeed)", source);
        Assert.Contains("bar.bobberBarSpeed > desiredBarSpeed", source);
        Assert.Contains("catch_fish_minigame_diagnostic:", source);
        Assert.Contains("ApplyCatchFishUseToolInput(activeCatchFish, out var castInputReason)", source);
        Assert.Contains("TryApplySmapiLeftButtonOverride", source);
        Assert.Contains("SButton.MouseLeft", source);
        Assert.Contains("catch_fish_smapi_input_override_unavailable", source);
        Assert.Contains("projectedCastingPower >= active.DesiredCastingPower", source);
        Assert.Contains("bobber_bar_success_observed", source);
        Assert.Contains("catch_fish_bobber_bar_success_not_observed", source);
        Assert.Contains("vanilla_junk_or_special_without_bobber_bar", source);
        Assert.Contains("CatchFishFullyIdle", source);
        Assert.Contains("action_idle_cleanup_complete", source);
        Assert.Contains("fishing.target_casting_power", source);
        Assert.Contains("fishing.observed_peak_casting_power", source);
        Assert.Contains("fishing.observed_release_casting_power", source);
        Assert.Contains("fishing.max_cast_requested", source);
        Assert.Contains("fishing.max_cast_observed", source);
        Assert.Contains("fishing.hook_attempt_count", source);
        Assert.Contains("fishing.bobber_bar_tick_count", source);
        Assert.Contains("fishing.bobber_bar_in_bar_ratio", source);
        Assert.Contains("fishing.terminal_progress", source);
        Assert.Contains("fishing.terminal_result", source);
        Assert.Contains("ApplyBobberBarInput(shouldPress, out reason)", source);
        Assert.DoesNotContain("ControlledInputState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.input =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.oldMouseState =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Mouse.SetState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendInput", source, StringComparison.Ordinal);
        Assert.DoesNotContain("active.Rod.castingPower =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("active.Rod.isTimingCast =", source, StringComparison.Ordinal);
        Assert.Contains("activeCatchFish = null;", source);
        Assert.DoesNotContain("active.Rod.tickUpdate(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bar.update(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("getFish(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("pullFishFromWater(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("whichFish =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventoryBool(ItemRegistry.Create", source, StringComparison.Ordinal);
        var bridgeSource = FishingReadAdapterSources.All;
        Assert.Contains("vanilla_secret_note_or_item", bridgeSource);
        Assert.Contains("Utility.GetUnseenSecretNotes", bridgeSource);
        Assert.Contains("internal_name = item.Name", bridgeSource);
        Assert.Contains("ItemRegistry.GetData(specialQualifiedItemId)", bridgeSource);
        Assert.Contains("baitName.Contains(specialFishInternalName, StringComparison.Ordinal)", bridgeSource);
        Assert.Contains("bait_preserved_parent_sheet_index", bridgeSource);
        Assert.Contains("specific_bait_name_condition_matched", bridgeSource);
        Assert.DoesNotContain("baitBonus = mineArea switch", bridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("baitName.Contains(\"Lava Eel\"", bridgeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("tryToCreateUnseenSecretNote", bridgeSource, StringComparison.Ordinal);
        var routeSource = ShopAccessReadAdapterSources.All;
        Assert.Contains("pathfinding: true", routeSource);
        var smokeSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "Invoke-RuntimeFishingDailyPlanSmoke.ps1"));
        Assert.Contains("profile=fishing", smokeSource);
        Assert.DoesNotContain("profile=full", smokeSource, StringComparison.Ordinal);
        Assert.Contains("FishPondQualifiedItemId", smokeSource);
        Assert.Contains("special_catch_without_bobber_bar_observed", smokeSource);
        Assert.Contains("fishPondBeforeCount - 1", smokeSource);
        Assert.Contains("MineFishingLevel", smokeSource);
        Assert.Contains("MineFishingMaxAttempts", smokeSource);
        Assert.Contains("--iterations $attemptIterations", smokeSource);
        Assert.Contains("Test-MineArea80Prerequisites", smokeSource);
        Assert.Contains("$selectedRod.upgrade_level -eq 4", smokeSource);
        Assert.Contains("$selectedRod.attachment_slot_count -ge 3", smokeSource);
        Assert.Contains("specific_bait_name_condition_matched", smokeSource);
        Assert.Contains("selectedRod.bait.internal_name", smokeSource);
        Assert.Contains("$attemptRecords", smokeSource);
        Assert.Contains("Resolve-CompactTrainingAttempts", smokeSource);
        Assert.Contains("$queueRecordsById[$queueId]", smokeSource);
        Assert.Contains("RunSparseMappingFixture", smokeSource);
        Assert.Contains("row 3 mapped to iteration", smokeSource);
        Assert.Contains("[System.IO.File]::ReadLines($DatasetPath)", smokeSource);
        Assert.Contains("Get-Content -LiteralPath $executionPath -Raw | ConvertFrom-Json", smokeSource);
        Assert.Contains("[string]$_.effective_queue_id -eq $winningAttempt.queue_id", smokeSource);
        Assert.DoesNotContain("foreach ($executionFile in $executionFiles)", smokeSource, StringComparison.Ordinal);
        Assert.DoesNotContain("$rowIndex", smokeSource, StringComparison.Ordinal);
        Assert.Contains("observed_caught_qualified_item_ids_in_attempt_order", smokeSource);
        Assert.Contains("winning_execution_path", smokeSource);
        Assert.DoesNotContain("$executionFiles[-1]", smokeSource, StringComparison.Ordinal);
        Assert.Contains("Mine area-80 smoke requires observed $requiredMineCatch", smokeSource);
        Assert.Contains("bobber_bar_success_observed", smokeSource);
        Assert.Contains("special_catch_without_bobber_bar_observed", smokeSource);
        Assert.Contains("[ValidateSet(\"none\", \"ordinary_quest\", \"special_order\")]", smokeSource);
        Assert.Contains("\"quest.advance\"", smokeSource);
        Assert.Contains("quest_acquisition_source_step", smokeSource);
        Assert.Contains("task_progress_after", smokeSource);
        Assert.Contains("(O)CaveJelly", smokeSource);
        Assert.Contains("(O)172", smokeSource);
        Assert.Contains("player.increaseBackpackSize(36 - player.MaxItems)", source);
        Assert.DoesNotContain("player.MaxItems = 36", source, StringComparison.Ordinal);
        Assert.Contains("new FishingRod(4)", source);
        Assert.Contains("item.UpgradeLevel == 4 && item.AttachmentSlotsCount >= 3", source);
        Assert.Contains("rod.AttachmentSlotsCount = Math.Max(rod.AttachmentSlotsCount, 3)", source);
        Assert.Contains("ItemRegistry.GetObjectTypeDefinition().CreateFlavoredBait(ItemRegistry.Create<StardewValley.Object>(\"(O)162\"))", source);
        Assert.Contains("bait.Stack = 999", source);
        Assert.DoesNotContain("ItemRegistry.Create<StardewValley.Object>(\"(O)SpecificBait\", 999)", source, StringComparison.Ordinal);
        Assert.DoesNotContain("bait.preservedParentSheetIndex.Value = \"162\"", source, StringComparison.Ordinal);
        Assert.Contains("ItemRegistry.Create<StardewValley.Object>(\"(O)856\")", source);
        Assert.Contains("ItemRegistry.Create<StardewValley.Object>(\"(O)695\")", source);
        Assert.Contains("fishing.fixture.backpack_empty_slots", source);
        Assert.Contains("fishing.fixture.selected_rod_qualified_item_id", source);
        Assert.Contains("fishing.fixture.specific_bait_target_item_id", source);
        Assert.Contains("fishing.fixture.bait_internal_name", source);
        Assert.Contains("fishing.fixture.lava_eel_native_name_condition", source);
        Assert.Contains("fishing.fixture.curiosity_lure_equipped", source);
        Assert.Contains("fishing.fixture.cork_bobber_equipped", source);
        Assert.Contains("fishing.fixture.stamina", source);
        Assert.Contains("native Game1.enterMine fixture transition handles MineShaft entry", smokeSource);
        Assert.Contains("setup_route_skip_reason", smokeSource);
    }

    [Fact]
    public void LiveLoopMapsCompiledFishingParametersIntoRuntimeRequest()
    {
        var source = LiveTrainingLoopSources.All;
        Assert.Contains("ReadQueueParameterString(item, \"location_id\")", source);
        Assert.Contains("ReadQueueParameterInt(item, \"stand_tile_x\")", source);
        Assert.Contains("ReadQueueParameterInt(item, \"bobber_tile_x\")", source);
        Assert.Contains("--required-verified-actions", source);
        Assert.Contains("attemptOrdinal <= options.MaxAttempts", source);
        Assert.Contains("--max-attempts", source);
        Assert.Contains("verifiedTargetMet ? \"ok\" : \"incomplete\"", source);
        Assert.Contains("Environment.ExitCode = 2", source);
        Assert.Contains("runtime_test_harness_unverified", source);
        Assert.Contains("ReadChangedFactDouble(execution, \"fishing.target_casting_power\")", source);
        Assert.Contains("ReadChangedFactDouble(execution, \"fishing.hook_attempt_count\")", source);
        Assert.Contains("ReadQueueParameterInt(item, \"rod_slot_index\")", source);
        Assert.Contains("ReadQueueParameterInt(item, \"max_movement_tiles\")", source);
        Assert.Contains("executionRequest.MaxMovementTiles = maxMovementTiles.Value", source);
        Assert.Contains("ReadQueueParameterString(item, \"rule_key\")", source);
        Assert.Contains("ReadQueueParameterString(item, \"outcome_distribution_json\")", source);
        Assert.Contains("executionRequest.OutcomeDistributionComplete = fishingOutcomeDistributionComplete", source);
        Assert.Contains("executionRequest.OutcomeDistributionJson = fishingOutcomeDistributionJson", source);
        Assert.Contains("executionRequest.PossibleQualifiedItemIdsJson = fishingPossibleQualifiedItemIdsJson", source);
        Assert.Contains("NormalizedParameters = item?[\"normalized_command\"]?[\"parameters\"]", source);
        Assert.Contains("ChangedFacts = execution[\"changed_facts\"]", source);
        Assert.Contains("beforeSnapshot.ToJsonString(JsonlOptions)", source);
        Assert.Contains("iteration == 1 && !string.IsNullOrWhiteSpace(options.SnapshotFile)", source);
        Assert.Contains("Timeout = TimeSpan.FromSeconds(180)", source);
        Assert.Contains("PostJsonStringAsync(executorHttp, options.ExecutorUrl", source);
        var deploySource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "Deploy-RuntimeTestHarnessToRuntime.ps1"));
        Assert.Contains("dotnet build", deploySource);
        Assert.Contains("-c Debug", deploySource);
        var smokeSource = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "scripts", "Invoke-RuntimeFishingDailyPlanSmoke.ps1"));
        Assert.Contains("compiled_catch_queue_item_id", smokeSource);
        Assert.Contains("compiled_expected_qualified_item_id", smokeSource);
        Assert.Contains("observed_catch_in_compiled_distribution", smokeSource);
        Assert.Contains("possible_qualified_item_ids_json", smokeSource);
        Assert.Contains("FishFrenzyQualifiedItemId", smokeSource);
        Assert.Contains("Fish frenzy priority was not preserved", smokeSource);
        Assert.DoesNotContain("compiled_catch_item_id", smokeSource, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCatchFishUsesSmapiOverrideWithoutReplacingGlobalOrOsInput()
    {
        var source = RuntimeHarnessSources.All;
        Assert.Contains("inputType.GetMethod(", source);
        Assert.Contains("\"OverrideButton\"", source);
        Assert.Contains("types: new[] { typeof(SButton), typeof(bool) }", source);
        Assert.Contains("TryApplySmapiButtonOverride(SButton.MouseLeft, pressed, out reason)", source);
        Assert.Contains("smapiOverrideButtonMethod.Invoke(input, new object[] { button, pressed });", source);
        Assert.Contains("ApplyBobberBarInput(shouldPress, out reason)", source);
        Assert.Contains("ReleaseSmapiLeftButtonOverride();", source);
        Assert.Contains("Fishing input dispatch failed once and was blocked", source);
        Assert.DoesNotContain("ControlledInputState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.input =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Mouse.SetState", source, StringComparison.Ordinal);
        Assert.DoesNotContain("SendInput", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentBridgeCollectsRequestedSnapshotsOnTheGameThread()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "ModEntry.cs"));
        Assert.Contains("pendingSnapshotRequests.Enqueue", source);
        Assert.Contains("ProcessPendingSnapshotRequests();", source);
        Assert.Contains("private void ProcessPendingSnapshotRequests()", source);
        Assert.Contains("TaskCreationOptions.RunContinuationsAsynchronously", source);
        Assert.Contains("item => (Profile: item.Profile.ToLowerInvariant(), item.ForceRefresh)", source);
        Assert.Contains("snapshot = !group.Key.ForceRefresh &&", source);
        Assert.Contains("RefreshSnapshotCache(group.Key.Profile, publishSnapshotEvent: true)", source);
        Assert.DoesNotContain("return RefreshSnapshotCache(profile, publishSnapshotEvent: true);", source, StringComparison.Ordinal);
        Assert.Contains("if (profile is \"fishing\")", source);
        Assert.Contains("domains.Add(\"quests_progress\")", source);
        Assert.Contains("domains.Add(\"modded_state\")", source);
    }

    [Fact]
    public void RuntimeCatchFishHooksEachNibbleOnlyOnceAndRejectsJunkBobberBarMix()
    {
        var source = RuntimeHarnessSources.All;
        Assert.Contains("if (!active.HookIssuedForNibble)", source);
        Assert.Contains("active.HookIssuedForNibble = true;", source);
        Assert.Contains("active.HookAttemptCount++;", source);
        Assert.Contains("active.Rod.DoFunction(Game1.currentLocation", source);
        Assert.Contains("active.Rod.pullingOutOfWater && !active.SawBobberBar", source);
        Assert.Contains("active.SawJunkOrSpecialPullWithoutBobberBar = true;", source);
        Assert.Contains("catch_fish_junk_or_special_pull_then_bobber_bar", source);
        Assert.Contains("hook_attempt_count=", source);
        Assert.DoesNotContain("while (active.Rod.isNibbling)", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeCatchFishRecordsActualReleasePowerOnlyAtCastTransition()
    {
        var source = RuntimeHarnessSources.All;
        Assert.Contains("active.WasTimingCastLastTick && !active.Rod.isTimingCast && active.Rod.isCasting", source);
        Assert.Contains("active.ObservedReleaseCastingPower = active.Rod.castingPower;", source);
        Assert.Contains("active.WasTimingCastLastTick = active.Rod.isTimingCast;", source);
        Assert.DoesNotContain("ObservedReleaseCastingPower = Math.Max", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ObservedReleaseCastingPower = active.DesiredCastingPower", source, StringComparison.Ordinal);
    }

    [Fact]
    public void LiveLoopReportsIncompleteWhenRequiredVerifiedActionsAreUnmet()
    {
        var source = LiveTrainingLoopSources.All;
        Assert.Contains("MaxAttempts", source);
        Assert.Contains("attemptsStarted++", source);
        Assert.Contains("verifiedActions >= options.RequiredVerifiedActions", source);
        Assert.Contains("var loopStatus = verifiedTargetMet ? \"ok\" : \"incomplete\";", source);
        Assert.Contains("Environment.ExitCode = 2;", source);
        Assert.Contains("stage=", source);
        Assert.Contains("\"incomplete\"", source);
        Assert.DoesNotContain("status = \"ok\"", source, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate repository root.");
    }
}
