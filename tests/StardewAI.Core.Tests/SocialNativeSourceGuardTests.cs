using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class SocialNativeSourceGuardTests
{
    [Fact]
    public void SocialExecutorSourceContainsOnlyCheckActionEntryPoint()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("executor.social_interact", source, StringComparison.Ordinal);

        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");
        var buildSocialSource = Slice(source, "private static TrainingExecutionResult BuildSocialInteractResult", "private static string SocialInteractRequestedEffect");

        Assert.Contains("Game1.currentLocation.checkAction(", socialSource, StringComparison.Ordinal);

        Assert.DoesNotContain(".receiveGift(", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".tryToReceiveActiveObject(", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("changeFriendship", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("friendshipEntry.GiftsToday =", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("friendshipEntry.GiftsThisWeek =", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("reduceActiveItemByOne", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Player.Position =", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("teleport", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("npc.TilePoint =", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("npcTile.X =", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("npcTile.Y =", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("npc.Position =", socialSource, StringComparison.Ordinal);

        Assert.Contains("Game1.currentLocation.checkAction(", socialSource, StringComparison.Ordinal);
        var checkActionCount = source.Split("Game1.currentLocation.checkAction(", StringSplitOptions.None).Length - 1;
        Assert.True(checkActionCount >= 1, "Must contain at least one Game1.currentLocation.checkAction( call");

        Assert.DoesNotContain("TODO", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FIXME", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("not_implemented", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("placeholder", socialSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SocialExecutorRecordsTimestampsAndGameTicks()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("startTicks = Game1.ticks", socialSource, StringComparison.Ordinal);
        Assert.Contains("endTicks = Game1.ticks", socialSource, StringComparison.Ordinal);
        Assert.Contains("Math.Max(0, endTicks - startTicks)", socialSource, StringComparison.Ordinal);
        Assert.Contains("actualTicks", socialSource, StringComparison.Ordinal);
        Assert.Contains("startedAt = DateTimeOffset.UtcNow.ToString", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorRecordsFacingAfterCheckAction()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("afterFacing = Game1.player.FacingDirection", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorRecordsNpcAndPlayerLocationBeforeAndAfter()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("beforeNpcLocation", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterNpcLocation", socialSource, StringComparison.Ordinal);
        Assert.Contains("beforeNpcTile", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterNpcTile", socialSource, StringComparison.Ordinal);
        Assert.Contains("beforePlayerTile", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterPlayerTile", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterNpcMember = Game1.currentLocation.characters", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorRecordsDialogueCountAndTranslationKey()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("beforeDialogueCount = beforeCurrentDialogue?.Count ?? 0", socialSource, StringComparison.Ordinal);
        Assert.Contains("beforeDialogueKey = beforeCurrentDialogue is not null && beforeCurrentDialogue.Count", socialSource, StringComparison.Ordinal);
        Assert.Contains(".TranslationKey", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterDialogueCount", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterDialogueKey", socialSource, StringComparison.Ordinal);
        Assert.Contains("beforeDialogueSpeakerName", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterDialogueSpeakerName", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorValidatesLocationIdAndNpcCoordinates()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("request.LocationId", socialSource, StringComparison.Ordinal);
        Assert.Contains("string.IsNullOrWhiteSpace(request.LocationId)", socialSource, StringComparison.Ordinal);
        Assert.Contains("social_location_id_required", socialSource, StringComparison.Ordinal);
        Assert.Contains("social_location_id_mismatch", socialSource, StringComparison.Ordinal);
        Assert.Contains("SocialObservedNpcTileX", socialSource, StringComparison.Ordinal);
        Assert.Contains("SocialObservedNpcTileY", socialSource, StringComparison.Ordinal);
        Assert.Contains("social_npc_coordinates_required", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorUsesDecompiledBackedNpcValidation()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("npc.IsVillager", socialSource, StringComparison.Ordinal);
        Assert.Contains("npc.IsMonster", socialSource, StringComparison.Ordinal);
        Assert.Contains("npc.IsInvisible", socialSource, StringComparison.Ordinal);
        Assert.Contains("npc.isSleeping.Value", socialSource, StringComparison.Ordinal);
        Assert.Contains("npc.CanSocialize", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorUsesBoundingRectangleIntersection()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("actionTargetRectangle", socialSource, StringComparison.Ordinal);
        Assert.Contains("npc.GetBoundingBox().Intersects(actionTargetRectangle)", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("playerStandingRect", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("npcBoundingBox.Intersects(playerStandingRect)", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorHandlesStardropTeaExceptions()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("(O)StardropTea", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("(O)434", socialSource, StringComparison.Ordinal);
        Assert.Contains("isStardropTea = string.Equals(item.QualifiedItemId, \"(O)StardropTea\"", socialSource, StringComparison.Ordinal);
        Assert.Contains("isSpouse = string.Equals(Game1.player.spouse", socialSource, StringComparison.Ordinal);
        Assert.Contains("dailyExhausted && !isStardropTea", socialSource, StringComparison.Ordinal);
        Assert.Contains("weeklyExhausted && !isSpouse && !isBirthday && !isStardropTea", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorValidatesActiveObjectBeforeCheckAction()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("player.ActiveObject is null ||", socialSource, StringComparison.Ordinal);
        Assert.Contains("ActiveObject.QualifiedItemId !=", socialSource, StringComparison.Ordinal);
        Assert.Contains("social_gift_active_object_not_selected", socialSource, StringComparison.Ordinal);
        Assert.Contains("CurrentToolIndex = beforeSelectedSlot", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorExactOneItemConsumption()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("afterGiftStack.Value == beforeGiftStack.Value - 1", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorGiftRequiresSocialEffect()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("hasDialogueChange || hasFriendshipChange || hasGiftCounterChange", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorTalkRequiresSocialTransition()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("hasTalkChange || hasDialogueChange || hasFriendshipChange", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialBlockedResultContainsTrainingImpactScope()
    {
        var source = RuntimeHarnessSources.All;
        var blockedSource = Slice(source, "private static TrainingExecutionResult BuildSocialBlockedResult", "private static TrainingExecutionResult BuildSocialInteractResult");

        Assert.Contains("TrainingImpactScope = \"executor_calibration\"", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialPlayerTileXBefore", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialPlayerTileYBefore", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialNpcVisibleBefore", blockedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialInteractResultSerializationRoundTrip()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.test",
            QueueId = "queue.test",
            QueueItemId = "item.test",
            BeforeStateHash = "hash.before",
            OptionId = "executor.social_interact",
            Status = "applied",
            FeedbackAvailable = true,
            StartedAt = "2026-07-14T00:00:00.0000000+00:00",
            CompletedAt = "2026-07-14T00:00:01.0000000+00:00",
            ActualTicks = 3,
            PrimitiveKind = "social_interact",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "native_talk_handled", "observable_social_transition" },
            RequestedEffect = "social.kind=talk;npc=Abigail",
            ObservedEffect = "location=Town;player.tile=10,11",
            FailureCategory = string.Empty,
            TrainingImpactScope = string.Empty,
            SocialNpcName = "Abigail",
            SocialNpcLocationBefore = "Town",
            SocialNpcLocationAfter = "Town",
            SocialNpcTileXBefore = 10,
            SocialNpcTileYBefore = 10,
            SocialNpcTileXAfter = 10,
            SocialNpcTileYAfter = 10,
            SocialNpcVisibleBefore = true,
            SocialNpcVisibleAfter = true,
            SocialNpcSleepingBefore = false,
            SocialNpcSleepingAfter = false,
            SocialNpcOrdinaryBefore = true,
            SocialNpcOrdinaryAfter = true,
            SocialNpcPresentBefore = true,
            SocialNpcPresentAfter = true,
            SocialPlayerTileXBefore = 11,
            SocialPlayerTileYBefore = 10,
            SocialPlayerTileXAfter = 11,
            SocialPlayerTileYAfter = 10,
            SocialPlayerFacingBefore = 3,
            SocialPlayerFacingAfter = 3,
            SocialPlayerSelectedSlotBefore = 0,
            SocialPlayerSelectedSlotAfter = 0,
            SocialActionKind = "talk",
            SocialNativeHandled = true,
            SocialFriendshipPointsBefore = 250,
            SocialFriendshipPointsAfter = 270,
            SocialTalkedToTodayBefore = false,
            SocialTalkedToTodayAfter = true,
            SocialGiftsTodayBefore = 0,
            SocialGiftsTodayAfter = 0,
            SocialGiftsThisWeekBefore = 0,
            SocialGiftsThisWeekAfter = 0,
            SocialMenuOpenBefore = false,
            SocialMenuOpenAfter = true,
            SocialMenuTypeBefore = "none",
            SocialMenuTypeAfter = "DialogueBox",
            SocialDialogueOpenBefore = false,
            SocialDialogueOpenAfter = true,
            SocialCurrentDialogueCountBefore = 0,
            SocialCurrentDialogueCountAfter = 1,
            SocialCurrentDialogueKeyBefore = string.Empty,
            SocialCurrentDialogueKeyAfter = "Characters\\Dialogue\\Abigail:Wed",
            SocialCurrentDialogueSpeakerNameBefore = string.Empty,
            SocialCurrentDialogueSpeakerNameAfter = "Abigail",
            SocialFriendshipRowExistsBefore = true,
            SocialFriendshipRowExistsAfter = true,
            SocialGiftItemIdBefore = string.Empty,
            SocialGiftItemIdAfter = string.Empty,
            SocialGiftStackBefore = null,
            SocialGiftStackAfter = null,
            SocialGiftQualityBefore = null,
            SocialGiftQualityAfter = null,
            SocialGiftSlotBefore = null,
            SocialGiftSlotAfter = null
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.Equal("executor.social_interact", roundTrip.OptionId);
        Assert.Equal("social_interact", roundTrip.PrimitiveKind);
        Assert.Equal("Abigail", roundTrip.SocialNpcName);
        Assert.Equal("Town", roundTrip.SocialNpcLocationBefore);
        Assert.Equal("Town", roundTrip.SocialNpcLocationAfter);
        Assert.Equal(10, roundTrip.SocialNpcTileXBefore);
        Assert.Equal(10, roundTrip.SocialNpcTileYBefore);
        Assert.Equal(10, roundTrip.SocialNpcTileXAfter);
        Assert.Equal(10, roundTrip.SocialNpcTileYAfter);
        Assert.True(roundTrip.SocialNpcVisibleBefore);
        Assert.True(roundTrip.SocialNpcVisibleAfter);
        Assert.False(roundTrip.SocialNpcSleepingBefore);
        Assert.False(roundTrip.SocialNpcSleepingAfter);
        Assert.True(roundTrip.SocialNpcOrdinaryBefore);
        Assert.True(roundTrip.SocialNpcOrdinaryAfter);
        Assert.True(roundTrip.SocialNpcPresentBefore);
        Assert.True(roundTrip.SocialNpcPresentAfter);
        Assert.Equal(11, roundTrip.SocialPlayerTileXBefore);
        Assert.Equal(10, roundTrip.SocialPlayerTileYBefore);
        Assert.Equal(11, roundTrip.SocialPlayerTileXAfter);
        Assert.Equal(10, roundTrip.SocialPlayerTileYAfter);
        Assert.Equal(3, roundTrip.SocialPlayerFacingBefore);
        Assert.Equal(3, roundTrip.SocialPlayerFacingAfter);
        Assert.Equal(0, roundTrip.SocialPlayerSelectedSlotBefore);
        Assert.Equal(0, roundTrip.SocialPlayerSelectedSlotAfter);
        Assert.Equal("talk", roundTrip.SocialActionKind);
        Assert.True(roundTrip.SocialNativeHandled);
        Assert.Equal(250, roundTrip.SocialFriendshipPointsBefore);
        Assert.Equal(270, roundTrip.SocialFriendshipPointsAfter);
        Assert.False(roundTrip.SocialTalkedToTodayBefore);
        Assert.True(roundTrip.SocialTalkedToTodayAfter);
        Assert.Equal(0, roundTrip.SocialCurrentDialogueCountBefore);
        Assert.Equal(1, roundTrip.SocialCurrentDialogueCountAfter);
        Assert.Equal("Characters\\Dialogue\\Abigail:Wed", roundTrip.SocialCurrentDialogueKeyAfter);
        Assert.Equal("Abigail", roundTrip.SocialCurrentDialogueSpeakerNameAfter);
        Assert.Equal(3, roundTrip.ActualTicks);

        Assert.Contains("\"social_npc_location_before\":\"Town\"", json);
        Assert.Contains("\"social_npc_location_after\":\"Town\"", json);
        Assert.Contains("\"social_npc_tile_x_before\":10", json);
        Assert.Contains("\"social_npc_tile_x_after\":10", json);
        Assert.Contains("\"social_npc_tile_y_before\":10", json);
        Assert.Contains("\"social_npc_tile_y_after\":10", json);
        Assert.Contains("\"social_npc_visible_before\":true", json);
        Assert.Contains("\"social_npc_visible_after\":true", json);
        Assert.Contains("\"social_npc_sleeping_before\":false", json);
        Assert.Contains("\"social_npc_sleeping_after\":false", json);
        Assert.Contains("\"social_npc_ordinary_before\":true", json);
        Assert.Contains("\"social_npc_ordinary_after\":true", json);
        Assert.Contains("\"social_npc_present_before\":true", json);
        Assert.Contains("\"social_npc_present_after\":true", json);
        Assert.Contains("\"social_player_tile_x_before\":11", json);
        Assert.Contains("\"social_player_tile_y_before\":10", json);
        Assert.Contains("\"social_player_tile_x_after\":11", json);
        Assert.Contains("\"social_player_tile_y_after\":10", json);
        Assert.Contains("\"social_current_dialogue_count_before\":0", json);
        Assert.Contains("\"social_current_dialogue_count_after\":1", json);
        Assert.Contains("\"social_current_dialogue_key_before\":\"\"", json);
        Assert.Contains("\"social_current_dialogue_key_after\":\"Characters\\\\Dialogue\\\\Abigail:Wed\"", json);
        Assert.Contains("\"social_current_dialogue_speaker_name_before\":\"\"", json);
        Assert.Contains("\"social_current_dialogue_speaker_name_after\":\"Abigail\"", json);
        Assert.Contains("\"social_gift_quality_after\":null", json);
        Assert.Contains("\"actual_ticks\":3", json);
    }

    [Fact]
    public void SocialInteractResultNullRowSemantics()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.null",
            QueueId = "queue.null",
            QueueItemId = "item.null",
            BeforeStateHash = "hash.before",
            OptionId = "executor.social_interact",
            Status = "blocked",
            SocialNpcName = "UnknownNpc",
            SocialFriendshipRowExistsBefore = false,
            SocialFriendshipRowExistsAfter = false,
            SocialFriendshipPointsBefore = null,
            SocialFriendshipPointsAfter = null,
            SocialGiftsTodayBefore = null,
            SocialGiftsTodayAfter = null,
            SocialGiftsThisWeekBefore = null,
            SocialGiftsThisWeekAfter = null
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.False(roundTrip.SocialFriendshipRowExistsBefore);
        Assert.False(roundTrip.SocialFriendshipRowExistsAfter);
        Assert.Null(roundTrip.SocialFriendshipPointsBefore);
        Assert.Null(roundTrip.SocialFriendshipPointsAfter);
        Assert.Null(roundTrip.SocialGiftsTodayBefore);
        Assert.Null(roundTrip.SocialGiftsTodayAfter);
        Assert.Null(roundTrip.SocialGiftsThisWeekBefore);
        Assert.Null(roundTrip.SocialGiftsThisWeekAfter);
        Assert.Contains("\"social_friendship_row_exists_before\":false", json);
    }

    [Fact]
    public void SocialExecutorStackOneToNullVerification()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("afterGiftStack = afterItem?.Stack;", socialSource, StringComparison.Ordinal);
        Assert.Contains("!afterGiftStack.HasValue", socialSource, StringComparison.Ordinal);
        Assert.Contains("beforeGiftStack.Value == 1", socialSource, StringComparison.Ordinal);
        Assert.Contains("afterGiftStack.Value == beforeGiftStack.Value - 1", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorPreservesNpcOnTileMismatch()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("BuildSocialBlockedResult(request, true, npc, \"social_interact\", \"social_npc_moved_from_observed_tile\")", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialBlockedResultPopulatesFriendshipAndGiftFields()
    {
        var source = RuntimeHarnessSources.All;
        var blockedSource = Slice(source, "private static TrainingExecutionResult BuildSocialBlockedResult", "private static TrainingExecutionResult BuildSocialInteractResult");

        Assert.Contains("friendshipData.TryGetValue(npcName, out var beforeFriendshipEntry", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialFriendshipPointsBefore = beforeFriendshipRowExists == true ? beforePoints : null", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialFriendshipPointsAfter = beforeFriendshipRowExists == true ? beforePoints : null", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialGiftsTodayBefore = beforeFriendshipRowExists == true ? beforeGiftsToday : null", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialGiftsThisWeekBefore = beforeFriendshipRowExists == true ? beforeGiftsThisWeek : null", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialFriendshipRowExistsBefore = beforeFriendshipRowExists", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialGiftItemIdBefore = beforeGiftItemId", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialGiftStackBefore = beforeGiftStack", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialCurrentDialogueCountBefore = beforeDialogueCount", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialCurrentDialogueKeyBefore = beforeDialogueKey", blockedSource, StringComparison.Ordinal);
        Assert.Contains("ActualTicks = 0", blockedSource, StringComparison.Ordinal);
        Assert.Contains("SocialNativeHandled = false", blockedSource, StringComparison.Ordinal);
        Assert.Contains("TrainingImpactScope = \"executor_calibration\"", blockedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialExecutorDispatcher()
    {
        var source = RuntimeHarnessSources.All;

        Assert.Contains("executor.social_interact", source, StringComparison.Ordinal);
        Assert.Contains("ExecuteSocialInteract(pending.Request)", source, StringComparison.Ordinal);

        var optionRegistrySource = File.ReadAllText(FindRepositoryFile("src", "StardewAI.Core", "OptionRegistry", "OptionRegistry.cs"));
        Assert.DoesNotContain("social_native_executor_not_implemented", optionRegistrySource, StringComparison.Ordinal);
        Assert.DoesNotContain("social_runtime_executor_not_implemented", optionRegistrySource, StringComparison.Ordinal);

        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.social_interact"));
    }

    [Fact]
    public void SocialBlockedResultWorldNotReadySourceIsSafe()
    {
        var source = RuntimeHarnessSources.All;
        var blockedSource = Slice(source, "private static TrainingExecutionResult BuildSocialBlockedResult", "private static TrainingExecutionResult BuildSocialInteractResult");

        Assert.Contains("Context.IsWorldReady", blockedSource, StringComparison.Ordinal);
        Assert.Contains("safePlayer = Context.IsWorldReady ? Game1.player : null", blockedSource, StringComparison.Ordinal);
        Assert.Contains("safeLocation = Context.IsWorldReady ? Game1.currentLocation : null", blockedSource, StringComparison.Ordinal);
        Assert.Contains("allReasons.Insert(0, \"world_not_ready\")", blockedSource, StringComparison.Ordinal);
        Assert.Contains("safePlayer?.TilePoint", blockedSource, StringComparison.Ordinal);
        Assert.Contains("safePlayer?.FacingDirection", blockedSource, StringComparison.Ordinal);
        Assert.Contains("safePlayer?.CurrentToolIndex", blockedSource, StringComparison.Ordinal);
        Assert.Contains("safeLocation?.NameOrUniqueName", blockedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.TilePoint", blockedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.FacingDirection", blockedSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.CurrentToolIndex", blockedSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialInteractResultAfterNpcPresentIsBooleanNotTernary()
    {
        var source = RuntimeHarnessSources.All;
        var resultSource = Slice(source, "private static TrainingExecutionResult BuildSocialInteractResult", "private static string SocialInteractRequestedEffect");

        Assert.Contains("SocialNpcPresentAfter = afterNpcPresent", resultSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SocialNpcPresentAfter = afterNpcPresent ? true : null", resultSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialObservedEffectWorldNotReadyIsSafe()
    {
        var source = RuntimeHarnessSources.All;
        var effectSource = Slice(source, "private static string SocialInteractObservedEffect()", "private TrainingExecutionResult ExecuteChooseDialogueResponse");

        Assert.Contains("Context.IsWorldReady", effectSource, StringComparison.Ordinal);
        Assert.Contains("safePlayer = Context.IsWorldReady ? Game1.player : null", effectSource, StringComparison.Ordinal);
        Assert.Contains("safeLocation = Context.IsWorldReady ? Game1.currentLocation : null", effectSource, StringComparison.Ordinal);
        Assert.Contains("none", effectSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialRectangleReasonIsRenamed()
    {
        var source = RuntimeHarnessSources.All;
        var socialSource = Slice(source, "private TrainingExecutionResult ExecuteSocialInteract", "private static TrainingExecutionResult BuildSocialBlockedResult");

        Assert.Contains("social_npc_not_intersecting_action_target_rectangle", socialSource, StringComparison.Ordinal);
        Assert.DoesNotContain("social_player_not_within_npc_bounding_rectangle", socialSource, StringComparison.Ordinal);
    }

    [Fact]
    public void SocialNpcPresentFieldsSerialize()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.presence",
            QueueId = "queue.presence",
            QueueItemId = "item.presence",
            BeforeStateHash = "hash.presence",
            OptionId = "executor.social_interact",
            Status = "applied",
            SocialNpcName = "Abigail",
            SocialNpcPresentBefore = true,
            SocialNpcPresentAfter = true,
            SocialNpcLocationBefore = "Town",
            SocialNpcLocationAfter = "Town",
            SocialNpcTileXBefore = 10,
            SocialNpcTileYBefore = 10,
            SocialNpcTileXAfter = 10,
            SocialNpcTileYAfter = 10,
            SocialNpcVisibleBefore = true,
            SocialNpcVisibleAfter = true,
            SocialNpcSleepingBefore = false,
            SocialNpcSleepingAfter = false,
            SocialNpcOrdinaryBefore = true,
            SocialNpcOrdinaryAfter = true,
            SocialPlayerTileXBefore = 11,
            SocialPlayerTileYBefore = 10,
            SocialPlayerTileXAfter = 11,
            SocialPlayerTileYAfter = 10,
            SocialActionKind = "talk",
            SocialNativeHandled = true
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.True(roundTrip.SocialNpcPresentBefore);
        Assert.True(roundTrip.SocialNpcPresentAfter);
        Assert.Contains("\"social_npc_present_before\":true", json);
        Assert.Contains("\"social_npc_present_after\":true", json);
        Assert.Contains("\"social_npc_location_before\":\"Town\"", json);
        Assert.Contains("\"social_npc_location_after\":\"Town\"", json);
    }

    [Fact]
    public void SocialNpcPresentNullBeforeResolution()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.null.resolved",
            QueueId = "queue.null.resolved",
            QueueItemId = "item.null.resolved",
            BeforeStateHash = "hash.null",
            OptionId = "executor.social_interact",
            Status = "blocked",
            SocialNpcName = "UnknownNpc",
            SocialNpcPresentBefore = null,
            SocialNpcPresentAfter = null,
            SocialNpcLocationBefore = string.Empty,
            SocialNpcLocationAfter = string.Empty,
            SocialNpcTileXBefore = null,
            SocialNpcTileYBefore = null,
            SocialNpcTileXAfter = null,
            SocialNpcTileYAfter = null,
            SocialNpcVisibleBefore = null,
            SocialNpcVisibleAfter = null,
            SocialNpcSleepingBefore = null,
            SocialNpcSleepingAfter = null,
            SocialNpcOrdinaryBefore = null,
            SocialNpcOrdinaryAfter = null,
            SocialPlayerTileXBefore = null,
            SocialPlayerTileYBefore = null,
            SocialPlayerTileXAfter = null,
            SocialPlayerTileYAfter = null,
            SocialActionKind = "talk",
            SocialNativeHandled = false
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.Null(roundTrip.SocialNpcPresentBefore);
        Assert.Null(roundTrip.SocialNpcPresentAfter);
        Assert.Contains("\"social_npc_present_before\":null", json);
        Assert.Contains("\"social_npc_present_after\":null", json);
        Assert.Contains("\"social_npc_location_before\":\"\"", json);
        Assert.Contains("\"social_npc_location_after\":\"\"", json);
        Assert.Contains("\"social_npc_tile_x_before\":null", json);
        Assert.Contains("\"social_npc_tile_y_before\":null", json);
    }

    [Fact]
    public void SocialNpcAbsentAfterNotCopiedFromBefore()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.absent",
            QueueId = "queue.absent",
            QueueItemId = "item.absent",
            BeforeStateHash = "hash.absent",
            OptionId = "executor.social_interact",
            Status = "applied",
            SocialNpcName = "Abigail",
            SocialNpcPresentBefore = true,
            SocialNpcPresentAfter = false,
            SocialNpcLocationBefore = "Town",
            SocialNpcLocationAfter = string.Empty,
            SocialNpcTileXBefore = 10,
            SocialNpcTileYBefore = 10,
            SocialNpcTileXAfter = null,
            SocialNpcTileYAfter = null,
            SocialNpcVisibleBefore = true,
            SocialNpcVisibleAfter = null,
            SocialNpcSleepingBefore = false,
            SocialNpcSleepingAfter = null,
            SocialNpcOrdinaryBefore = true,
            SocialNpcOrdinaryAfter = null,
            SocialPlayerTileXBefore = 11,
            SocialPlayerTileYBefore = 10,
            SocialPlayerTileXAfter = 11,
            SocialPlayerTileYAfter = 10,
            SocialActionKind = "talk",
            SocialNativeHandled = true,
            SocialCurrentDialogueCountBefore = 0,
            SocialCurrentDialogueCountAfter = null
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.True(roundTrip.SocialNpcPresentBefore);
        Assert.False(roundTrip.SocialNpcPresentAfter);
        Assert.Equal("Town", roundTrip.SocialNpcLocationBefore);
        Assert.Equal(string.Empty, roundTrip.SocialNpcLocationAfter);
        Assert.Equal(10, roundTrip.SocialNpcTileXBefore);
        Assert.Equal(10, roundTrip.SocialNpcTileYBefore);
        Assert.Null(roundTrip.SocialNpcTileXAfter);
        Assert.Null(roundTrip.SocialNpcTileYAfter);
        Assert.True(roundTrip.SocialNpcVisibleBefore);
        Assert.Null(roundTrip.SocialNpcVisibleAfter);
        Assert.False(roundTrip.SocialNpcSleepingBefore);
        Assert.Null(roundTrip.SocialNpcSleepingAfter);
        Assert.True(roundTrip.SocialNpcOrdinaryBefore);
        Assert.Null(roundTrip.SocialNpcOrdinaryAfter);
        Assert.Null(roundTrip.SocialCurrentDialogueCountAfter);

        Assert.Contains("\"social_npc_present_before\":true", json);
        Assert.Contains("\"social_npc_present_after\":false", json);
        Assert.Contains("\"social_npc_location_before\":\"Town\"", json);
        Assert.Contains("\"social_npc_location_after\":\"\"", json);
        Assert.Contains("\"social_npc_tile_x_after\":null", json);
        Assert.Contains("\"social_npc_tile_y_after\":null", json);
        Assert.Contains("\"social_npc_visible_after\":null", json);
        Assert.Contains("\"social_npc_sleeping_after\":null", json);
        Assert.Contains("\"social_npc_ordinary_after\":null", json);
        Assert.Contains("\"social_current_dialogue_count_after\":null", json);
    }

    [Fact]
    public void DialogueSpeakerComparisonUsesStringEqualsNotReferenceEquals()
    {
        var source = RuntimeHarnessSources.All;
        var dialogueAdvanceSource = Slice(source, "private void TickDialogueAdvanceCore", "private static TrainingExecutionResult DialogueAdvanceResult");

        Assert.DoesNotContain("ReferenceEquals(currentBox.characterDialogue?.speaker?.Name", dialogueAdvanceSource, StringComparison.Ordinal);
        Assert.Contains("string.Equals(currentSpeakerName, advance.InitialSpeakerName, StringComparison.Ordinal)", dialogueAdvanceSource, StringComparison.Ordinal);
        Assert.Contains("StringComparison.Ordinal", dialogueAdvanceSource, StringComparison.Ordinal);
    }

    private static string Slice(string source, string startMarker, string endMarker)
    {
        var start = source.IndexOf(startMarker, StringComparison.Ordinal);
        Assert.True(start >= 0, "Missing source marker: " + startMarker);
        var end = source.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(end > start, "Missing source marker: " + endMarker);
        return source[start..end];
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
