using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class SocialTransparentPlanningTests
{
    [Fact]
    public void AvailabilityBuildsCurrentTalkCandidateFromCompleteTransparentFacts()
    {
        var snapshot = CompleteSocialSnapshot();

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("preview_available", option.Status);
        Assert.Contains("social_high_level_direct_executor_disabled_use_daily_plan_compiler", option.BlockingReasons);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.Equal("social_talk_current", candidate.Kind);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "npc_name" && parameter.Value == "Abigail");
    }

    [Fact]
    public void SocialCandidateEnergyCostIsBoundedAndHighLevelDirectActionHardBlocked()
    {
        var snapshotTalk = CompleteSocialSnapshot();
        var optionTalk = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshotTalk, new[] { "social.talk_npc" }).Options);
        var talkCandidate = Assert.Single(optionTalk.SocialCandidates);
        Assert.True(talkCandidate.EstimatedTicks > 0);
        Assert.Equal(0, talkCandidate.EnergyCost);

        var snapshotGift = CompleteSocialSnapshot();
        var optionGift = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshotGift, new[] { "social.gift_npc" }).Options);
        var giftCandidate = Assert.Single(optionGift.SocialCandidates);
        Assert.True(giftCandidate.EstimatedTicks > 0);
        Assert.Equal(0, giftCandidate.EnergyCost);

        var requestTalk = Request(snapshotTalk.StateHash, "social.talk_npc");
        var queueTalk = new ActionQueueCompiler().Compile(requestTalk, snapshotTalk);
        Assert.Equal("blocked", queueTalk.Status);
        Assert.DoesNotContain("social_native_executor_not_implemented", queueTalk.Items[0].BlockingReasons);

        var requestGift = Request(snapshotGift.StateHash, "social.gift_npc");
        requestGift.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" },
            new SmallModelActionParameter { Name = "slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)66" }
        };
        var queueGift = new ActionQueueCompiler().Compile(requestGift, snapshotGift);
        Assert.Equal("blocked", queueGift.Status);
        Assert.DoesNotContain("social_native_executor_not_implemented", queueGift.Items[0].BlockingReasons);
    }

    [Fact]
    public void AvailabilityBuildsGiftCandidateForExactOwnedCompleteTasteItem()
    {
        var snapshot = CompleteSocialSnapshot();

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("preview_available", option.Status);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.Equal("social_gift_current", candidate.Kind);
        Assert.Equal(0, candidate.SlotIndex);
        Assert.Equal("(O)66", candidate.QualifiedItemId);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_friendship_delta" && parameter.Value == "80");
    }

    [Fact]
    public void AvailabilityFailsClosedWhenTasteDataIsIncomplete()
    {
        var snapshot = CompleteSocialSnapshot(giftTasteField: "{\"value\":[],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}");

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("blocked", option.Status);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_gift_taste_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityFailsClosedForUnknownNpc()
    {
        var snapshot = CompleteSocialSnapshot(socialInteractionValue: "[{\"name\":\"ModdedNpc\",\"display_name\":\"Modded\",\"master_data_present\":false,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":10,\"can_socialize\":false,\"can_socialize_complete\":false}]");

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("blocked", option.Status);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_npc_master_data_missing", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsProtectedAndQuestGiftItemsUpstream()
    {
        var inventory = "[" +
            "{\"slot_index\":0,\"item_id\":\"66\",\"qualified_item_id\":\"(O)66\",\"stack\":1,\"quality\":0,\"is_object\":true,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"protected_from_auto_sell\":true}," +
            "{\"slot_index\":1,\"item_id\":\"128\",\"qualified_item_id\":\"(O)128\",\"stack\":1,\"quality\":0,\"is_object\":true,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"object_quest_item\":true}" +
            "]";
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("blocked", option.Status);
        Assert.Equal(2, option.SocialCandidates.Length);
        Assert.All(option.SocialCandidates, candidate => Assert.False(candidate.Available));
    }

    [Fact]
    public void AvailabilityRejectsGiftLimitExhausted()
    {
        var friendship = "[{\"npc_name\":\"Abigail\",\"points\":250,\"heart_level\":1,\"gifts_this_week\":2,\"gifts_today\":1,\"talked_to_today\":false,\"is_divorced\":false}]";
        var snapshot = CompleteSocialSnapshot(friendshipValue: friendship);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("blocked", option.Status);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_gift_daily_limit_exhausted", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsIncompleteRouteToNpc()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[" +
            "{\"tile_x\":11,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":9,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":11,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":9,\"collision_blocked\":true}" +
            "]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("blocked", option.Status);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_no_reachable_adjacent_stand_tile", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsIncompleteCurrentScheduleWindow()
    {
        var socialInteraction = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":10,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":false}]";
        var snapshot = CompleteSocialSnapshot(socialInteractionValue: socialInteraction);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("blocked", option.Status);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_current_route_window_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityDoesNotBlockOnMissingScheduleWhenNpcIsLoaded()
    {
        var socialInteraction = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":10,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"schedule_loaded\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(socialInteractionValue: socialInteraction);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.DoesNotContain(candidate.BlockReasons, reason => reason.StartsWith("social_schedule_missing"));
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "schedule_loaded_for_evidential_provenance" && parameter.Value == "false");
    }

    [Fact]
    public void AvailabilityVerifierRejectsUnavailableSchedules()
    {
        var snapshot = CompleteSocialSnapshot(scheduleField: "{\"value\":null,\"status\":\"unavailable\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":0,\"reason\":\"missing_schedule_inventory\"}");

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("blocked", option.Status);
        Assert.Contains("npcs.schedules", option.MissingStateFactors);
        Assert.NotEmpty(option.SocialCandidates);
        Assert.Contains("npcs.schedules", option.MissingStateFactors);
    }

    [Fact]
    public void AvailabilityPreservesBlockedDiagnosticsInsteadOfErasingRows()
    {
        var snapshot = CompleteSocialSnapshot(giftTasteField: "{\"value\":[],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}");

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });

        var candidate = Assert.Single(Assert.Single(availability.Options).SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_gift_taste_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityAllowsMissingFriendshipRowForOrdinaryGiftBaseline()
    {
        var snapshot = CompleteSocialSnapshot(friendshipValue: "[]");

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });

        var candidate = Assert.Single(Assert.Single(availability.Options).SocialCandidates);
        Assert.True(candidate.Available);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "friendship_row_exists_before" && parameter.Value == "false");
    }

    [Fact]
    public void AvailabilityAppliesStardropTeaGiftLimitException()
    {
        var inventory = "[{\"slot_index\":0,\"item_id\":\"StardropTea\",\"qualified_item_id\":\"(O)StardropTea\",\"display_name\":\"Stardrop Tea\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"context_tags\":[],\"is_empty\":false}]";
        var friendship = "[{\"npc_name\":\"Abigail\",\"points\":250,\"heart_level\":1,\"gifts_this_week\":2,\"gifts_today\":1,\"talked_to_today\":false,\"is_divorced\":false}]";
        var taste = "{\"value\":[{\"npc_name\":\"Abigail\",\"slot_index\":0,\"qualified_item_id\":\"(O)StardropTea\",\"quality\":0,\"taste\":\"stardrop_tea\",\"expected_friendship_delta\":\"250\",\"expected_friendship_delta_complete\":true,\"complete\":true}],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}";
        var snapshot = CompleteSocialSnapshot(friendshipValue: friendship, inventoryValue: inventory, giftTasteField: taste);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.DoesNotContain("social_gift_daily_limit_exhausted", candidate.BlockReasons);
        Assert.DoesNotContain("social_gift_weekly_limit_exhausted", candidate.BlockReasons);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "gift_updates_normal_limits" && parameter.Value == "false");
    }

    [Fact]
    public void AvailabilityDoesNotUniversallyBlockMovingOrControllerNpc()
    {
        var socialInteraction = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":true,\"is_busy\":true,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(socialInteractionValue: socialInteraction);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.DoesNotContain("social_npc_has_controller", candidate.BlockReasons);
        Assert.DoesNotContain("social_npc_busy", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsSpecialGiftSwitchItems()
    {
        var inventory = "[{\"slot_index\":0,\"item_id\":\"809\",\"qualified_item_id\":\"(O)809\",\"display_name\":\"Movie Ticket\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"context_tags\":[],\"is_empty\":false}]";
        var taste = "{\"value\":[{\"npc_name\":\"Abigail\",\"slot_index\":0,\"qualified_item_id\":\"(O)809\",\"quality\":0,\"taste\":\"neutral\",\"expected_friendship_delta\":\"20\",\"expected_friendship_delta_complete\":true,\"complete\":true}],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}";
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory, giftTasteField: taste);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_gift_special_switch_item_branch_unsupported", candidate.BlockReasons);
    }

    [Theory]
    [InlineData("(O)233", "233", "Small Wall-Safe")]
    [InlineData("(O)897", "897", "Void Salmon")]
    [InlineData("(O)71", "71", "Fried Egg")]
    [InlineData("(O)864", "864", "Dragon Tooth")]
    [InlineData("(O)865", "865", "Dragon Tooth II")]
    [InlineData("(O)866", "866", "Dragon Tooth III")]
    [InlineData("(O)867", "867", "Dragon Tooth IV")]
    [InlineData("(O)868", "868", "Dragon Tooth V")]
    [InlineData("(O)869", "869", "Dragon Tooth VI")]
    [InlineData("(O)870", "870", "Dragon Tooth VII")]
    [InlineData("(O)809", "809", "Movie Ticket")]
    [InlineData("(O)458", "458", "Mermaid's Pendant")]
    [InlineData("(O)277", "277", "Wilted Bouquet")]
    [InlineData("(O)460", "460", "Stardrop")]
    public void AvailabilityRejectsAllSpecialSwitchItems(string qualifiedId, string itemId, string displayName)
    {
        var inventory = "[{\"slot_index\":0,\"item_id\":\"" + itemId + "\",\"qualified_item_id\":\"" + qualifiedId + "\",\"display_name\":\"" + displayName + "\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"context_tags\":[],\"is_empty\":false}]";
        var taste = "{\"value\":[{\"npc_name\":\"Abigail\",\"slot_index\":0,\"qualified_item_id\":\"" + qualifiedId + "\",\"quality\":0,\"taste\":\"neutral\",\"expected_friendship_delta\":\"20\",\"expected_friendship_delta_complete\":true,\"complete\":true}],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}";
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory, giftTasteField: taste);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_gift_special_switch_item_branch_unsupported", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsRoommateProposalContextGiftBranch()
    {
        var inventory = "[{\"slot_index\":0,\"item_id\":\"Custom\",\"qualified_item_id\":\"(O)Custom\",\"display_name\":\"Custom Pendant\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"context_tags\":[\"propose_roommate_Krobus\"],\"is_empty\":false}]";
        var taste = "{\"value\":[{\"npc_name\":\"Abigail\",\"slot_index\":0,\"qualified_item_id\":\"(O)Custom\",\"quality\":0,\"taste\":\"neutral\",\"expected_friendship_delta\":\"20\",\"expected_friendship_delta_complete\":true,\"complete\":true}],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}";
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory, giftTasteField: taste);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_gift_roommate_proposal_context_branch_unsupported", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityFailsClosedOnMissingGiftabilityFact()
    {
        var inventory = "[{\"slot_index\":0,\"item_id\":\"66\",\"qualified_item_id\":\"(O)66\",\"display_name\":\"Amethyst\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"context_tags\":[],\"is_empty\":false}]";
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_gift_item_can_be_given_missing_or_malformed", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityFailsClosedOnNonBooleanGiftabilityFact()
    {
        var inventory = "[{\"slot_index\":0,\"item_id\":\"66\",\"qualified_item_id\":\"(O)66\",\"display_name\":\"Amethyst\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"can_be_given_as_gift\":\"yes\",\"context_tags\":[],\"is_empty\":false}]";
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_gift_item_can_be_given_missing_or_malformed", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityFailsClosedOnMissingBaseTagNotGiftable()
    {
        var inventory = "[{\"slot_index\":0,\"item_id\":\"66\",\"qualified_item_id\":\"(O)66\",\"display_name\":\"Amethyst\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"can_be_given_as_gift\":true,\"context_tags\":[],\"is_empty\":false}]";
        var snapshot = CompleteSocialSnapshot(inventoryValue: inventory);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_gift_item_base_tag_not_giftable_missing_or_malformed", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsMalformedRouteActionCoverage()
    {
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: "{\"unsupported_for_route_training_count\":0}");

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsNonObjectCollisionRow()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[null,\"string\",42]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsCollisionRowWithMissingCoordinates()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"collision_blocked\":true}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsCollisionRowWithNonNumericCoordinate()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"tile_x\":\"abc\",\"tile_y\":10,\"collision_blocked\":true}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsCollisionRowOutOfBounds()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"tile_x\":25,\"tile_y\":10,\"collision_blocked\":true}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsNonObjectRouteActionRow()
    {
        var routeActionCoverageValue = "{\"rows\":[null,\"string\",42]}";
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: routeActionCoverageValue);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsRouteActionRowWithMissingCoordinates()
    {
        var routeActionCoverageValue = "{\"rows\":[{\"route_training_blocked\":true}]}";
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: routeActionCoverageValue);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsRouteActionRowOutOfBounds()
    {
        var routeActionCoverageValue = "{\"rows\":[{\"tile_x\":-1,\"tile_y\":5,\"route_training_blocked\":true}]}";
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: routeActionCoverageValue);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsCollisionRowWithNonBooleanDiscriminator()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"tile_x\":10,\"tile_y\":10,\"collision_blocked\":\"yes\"}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsCollisionRowWithMissingDiscriminator()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"tile_x\":10,\"tile_y\":10}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsCollisionRowWithNonBooleanNumericDiscriminator()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"tile_x\":10,\"tile_y\":10,\"collision_blocked\":1}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsRouteActionRowWithNonBooleanDiscriminator()
    {
        var routeActionCoverageValue = "{\"rows\":[{\"tile_x\":10,\"tile_y\":10,\"route_training_blocked\":\"yes\"}]}";
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: routeActionCoverageValue);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsRouteActionRowWithMissingDiscriminator()
    {
        var routeActionCoverageValue = "{\"rows\":[{\"tile_x\":10,\"tile_y\":10}]}";
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: routeActionCoverageValue);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void AvailabilityRejectsRouteActionRowWithNonBooleanNumericDiscriminator()
    {
        var routeActionCoverageValue = "{\"rows\":[{\"tile_x\":10,\"tile_y\":10,\"route_training_blocked\":0}]}";
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: routeActionCoverageValue);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void CollisionBlockedFalseDoesNotBlockRouteEvidence()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"tile_x\":10,\"tile_y\":10,\"collision_blocked\":false}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.DoesNotContain("social_route_collision_grid_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void RouteTrainingBlockedFalseDoesNotBlockRouteEvidence()
    {
        var routeActionCoverageValue = "{\"rows\":[{\"tile_x\":10,\"tile_y\":10,\"branch\":\"Warp\",\"route_training_blocked\":false}]}";
        var snapshot = CompleteSocialSnapshot(routeActionCoverageValue: routeActionCoverageValue);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" }).Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.DoesNotContain("social_route_action_coverage_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void CompilerPreservesSocialEnvelopeAndBlocksNativeExecutor()
    {
        var snapshot = CompleteSocialSnapshot();
        var request = Request(snapshot.StateHash, "social.gift_npc");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" },
            new SmallModelActionParameter { Name = "slot_index", Value = "0" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)66" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("social_requires_daily_plan_compilation", queue.Items[0].BlockingReasons);
        Assert.NotNull(queue.Items[0].NormalizedCommand.SocialPlan);
        var socialPlan = queue.Items[0].NormalizedCommand.SocialPlan!;
        Assert.Equal("gift", socialPlan.ActionKind);
        Assert.Equal("Abigail", socialPlan.RequestedNpcName);
        Assert.Equal(0, socialPlan.RequestedSlotIndex);
        Assert.Contains(socialPlan.TrainingRecordingContract, item => item == "friendship_points_before_after_delta");
        Assert.Empty(queue.Items[0].NormalizedCommand.Steps);
    }

    [Fact]
    public void DailyPlanCompilerCompilesTalkCandidateIntoMoveAndSocialInteract()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.True(socialCandidate.Available);

        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("social_interact", plan.Steps[1].Kind);
        Assert.DoesNotContain(plan.Steps[1].SafetyConstraints, s => s == "social_runtime_executor_not_implemented");
        Assert.Contains(plan.Steps[1].Parameters, p => p.Name == "social_action_kind" && p.Value == "talk");
        Assert.Contains(plan.Steps[1].Parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(plan.Steps[1].Parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.True(plan.Steps[0].TargetTileX.HasValue && plan.Steps[1].TargetTileX.HasValue);
        Assert.NotEqual(plan.Steps[0].TargetTileX, plan.Steps[1].TargetTileX);
    }

    [Fact]
    public void DailyPlanCompilerCompilesGiftCandidateIntoMoveAndSocialInteract()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.True(socialCandidate.Available);

        var prediction = ToPrediction(socialCandidate, "social.gift_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("social_interact", plan.Steps[1].Kind);
        var socialStep = plan.Steps[1];
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_name" && p.Value == "Abigail");
        Assert.Contains(socialStep.Parameters, p => p.Name == "qualified_item_id" && p.Value == "(O)66");
        Assert.Contains(socialStep.Parameters, p => p.Name == "social_action_kind" && p.Value == "gift");
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Equal(10, socialStep.TargetTileX);
        Assert.Equal(10, socialStep.TargetTileY);
        Assert.NotEqual(plan.Steps[0].TargetTileX, socialStep.TargetTileX);
    }

    [Fact]
    public void SocialInteractPlansToExecutorOptionWithCorrectMapping()
    {
        var compiler = new ActionQueueCompiler();
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        var queue = compiler.Compile(plan, snapshot);

        var items = queue.Items;
        Assert.Equal(2, items.Length);
        Assert.Equal("executor.move_to_tile", items[0].OptionId);
        Assert.Equal("executor.social_interact", items[1].OptionId);
        Assert.Equal("pending", items[1].Status);
        Assert.DoesNotContain("social_runtime_executor_not_implemented", items[1].BlockingReasons);
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_action_kind_talk_or_gift_required");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_stand_not_adjacent_to_npc");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_candidate_stand_npc_mismatch");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_candidate_stand_npc_evidence_missing");
        Assert.DoesNotContain(items[1].BlockingReasons, r => r == "social_npc_name_required");

        var socialParams = items[1].NormalizedCommand.Parameters;
        Assert.Contains(socialParams, p => p.Name == "social_action_kind" && p.Value == "talk");
        Assert.Contains(socialParams, p => p.Name == "npc_name" && p.Value == "Abigail");
        Assert.Contains(socialParams, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(socialParams, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Contains(socialParams, p => p.Name == "stand_tile_x");
        Assert.Contains(socialParams, p => p.Name == "stand_tile_y");

        var targetStandX = items[0].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_x")?.Value;
        var targetStandY = items[0].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_y")?.Value;
        var targetNpcX = items[1].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_x")?.Value;
        var targetNpcY = items[1].NormalizedCommand.Parameters
            .FirstOrDefault(p => p.Name == "target_tile_y")?.Value;
        Assert.NotNull(targetNpcX);
        Assert.NotNull(targetNpcY);
        Assert.NotEqual(targetStandX, targetNpcX);
    }

    [Fact]
    public void DirectHighLevelSocialActionCannotBypassCompilation()
    {
        var snapshot = CompleteSocialSnapshot();
        var request = Request(snapshot.StateHash, "social.talk_npc");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        Assert.DoesNotContain("social_native_executor_not_implemented", queue.Items[0].BlockingReasons);
        Assert.Empty(queue.Items[0].NormalizedCommand.Steps);
    }

    [Fact]
    public void SocialCandidateIncludesRouteDistanceInParameters()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);

        Assert.Contains(socialCandidate.Parameters, p => p.Name == "route_distance_tiles");
        Assert.Contains(socialCandidate.Parameters, p => p.Name == "route_distance_ticks");
        Assert.Contains(socialCandidate.Parameters, p => p.Name == "native_interaction_planner_budget_ticks");

        var distanceTiles = int.Parse(socialCandidate.Parameters.First(p => p.Name == "route_distance_tiles").Value);
        var distanceTicks = int.Parse(socialCandidate.Parameters.First(p => p.Name == "route_distance_ticks").Value);
        var plannerBudget = int.Parse(socialCandidate.Parameters.First(p => p.Name == "native_interaction_planner_budget_ticks").Value);
        Assert.True(distanceTiles >= 0);
        Assert.True(distanceTicks >= 0);
        Assert.Equal(120, plannerBudget);
        Assert.Equal(distanceTicks + plannerBudget, socialCandidate.EstimatedTicks);
    }

    [Fact]
    public void AvailabilityDurationIsUnknownForBlockedCandidateNoStandTile()
    {
        var blockedCollision = "{\"width\":20,\"height\":20,\"notable_tiles\":[" +
            "{\"tile_x\":11,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":9,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":11,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":9,\"collision_blocked\":true}" +
            "]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: blockedCollision);
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var candidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_no_reachable_adjacent_stand_tile", candidate.BlockReasons);

        Assert.Equal(-1, candidate.EstimatedTicks);
        var distanceTiles = int.Parse(candidate.Parameters.First(p => p.Name == "route_distance_tiles").Value);
        Assert.True(distanceTiles < 0);
        var distanceTicks = int.Parse(candidate.Parameters.First(p => p.Name == "route_distance_ticks").Value);
        Assert.Equal(-1, distanceTicks);
        Assert.Contains(candidate.Parameters, p => p.Name == "native_interaction_planner_budget_ticks");
        Assert.Contains("estimated_duration_ticks=-1", candidate.ExpectedEffect);
        Assert.Contains("duration_estimate_status=planner_budget_assumption_pending_runtime_calibration", candidate.ExpectedEffect);
    }

    [Fact]
    public void DailyPlanCompilerSkipsWhenSocialCandidateMissingStandTile()
    {
        var blockedCollision = "{\"width\":20,\"height\":20,\"notable_tiles\":[" +
            "{\"tile_x\":11,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":9,\"tile_y\":10,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":11,\"collision_blocked\":true}," +
            "{\"tile_x\":10,\"tile_y\":9,\"collision_blocked\":true}" +
            "]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: blockedCollision);
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.False(socialCandidate.Available);
        Assert.Contains("social_no_reachable_adjacent_stand_tile", socialCandidate.BlockReasons);

        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        Assert.Empty(plan.Steps);
    }

    [Fact]
    public void ExecutorSocialInteractIsRegisteredAndHasCorrectParameters()
    {
        var registry = new StardewAI.Core.OptionRegistry.OptionRegistry();
        var spec = registry.GetRequired("executor.social_interact");

        Assert.NotNull(spec);
        Assert.Equal("social", spec.Domain);
        Assert.Equal(OptionBehaviorCategories.Mechanical, spec.BehaviorCategory);
        Assert.Equal(CompilerResponsibilities.FullActionExpansion, spec.CompilerResponsibility);
        Assert.Equal(TrainingRoles.ExecutorCalibration, spec.TrainingRole);
        Assert.DoesNotContain("social_runtime_executor_not_implemented", spec.SafetyConstraints);
    }

    [Fact]
    public void CompileChainPreservesNpcNameAndActionKindThroughSocialInteractRequest()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        var prediction = ToPrediction(socialCandidate, "social.gift_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        var socialItem = queue.Items.FirstOrDefault(i => i.OptionId == "executor.social_interact");
        Assert.NotNull(socialItem);
        var parameters = socialItem.NormalizedCommand.Parameters;
        Assert.Contains(parameters, p => p.Name == "npc_name" && p.Value == "Abigail");
        Assert.Contains(parameters, p => p.Name == "social_action_kind" && p.Value == "gift");
        Assert.Contains(parameters, p => p.Name == "qualified_item_id" && p.Value == "(O)66");
        Assert.Contains(parameters, p => p.Name == "slot_index" && p.Value == "0");
        Assert.Contains(parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Contains(parameters, p => p.Name == "stand_tile_x");
        Assert.Contains(parameters, p => p.Name == "stand_tile_y");
        Assert.Contains(parameters, p => p.Name == "expected_friendship_delta" && p.Value == "80");

        var npcTargetX = parameters.FirstOrDefault(p => p.Name == "target_tile_x")?.Value;
        var npcTargetY = parameters.FirstOrDefault(p => p.Name == "target_tile_y")?.Value;
        Assert.Equal("10", npcTargetX);
        Assert.Equal("10", npcTargetY);

        var blocking = socialItem.BlockingReasons;
        Assert.DoesNotContain("social_runtime_executor_not_implemented", blocking);
        Assert.DoesNotContain(blocking, r => r == "social_action_kind_talk_or_gift_required");
        Assert.DoesNotContain(blocking, r => r == "social_stand_not_adjacent_to_npc");
        Assert.DoesNotContain(blocking, r => r == "social_candidate_stand_npc_mismatch");
        Assert.DoesNotContain(blocking, r => r == "social_candidate_stand_npc_evidence_missing");
    }

    [Fact]
    public void SelectReachableStandTileNavigatesObstaclesAndShortestRouteExceedsManhattan()
    {
        var blockedTiles = new List<string>();
        for (var y = 1; y <= 4; y++)
        {
            for (var x = 3; x <= 8; x++)
            {
                blockedTiles.Add("{\"tile_x\":" + x + ",\"tile_y\":" + y + ",\"collision_blocked\":true}");
            }
        }
        var collisionValue = "{\"width\":16,\"height\":10,\"notable_tiles\":[" + string.Join(",", blockedTiles) + "]}";
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":3,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]",
            collisionValue: collisionValue,
            playerTileX: 2,
            playerTileY: 3);
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        Assert.True(socialCandidate.Available);

        var standX = socialCandidate.Parameters.First(p => p.Name == "stand_tile_x").Value;
        var standY = socialCandidate.Parameters.First(p => p.Name == "stand_tile_y").Value;
        var npcX = socialCandidate.Parameters.First(p => p.Name == "npc_tile_x").Value;
        var npcY = socialCandidate.Parameters.First(p => p.Name == "npc_tile_y").Value;
        var distanceTiles = int.Parse(socialCandidate.Parameters.First(p => p.Name == "route_distance_tiles").Value);

        Assert.Equal("10", npcX);
        Assert.Equal("3", npcY);
        var manhattanToNpc = Math.Abs(2 - 10) + Math.Abs(3 - 3);
        Assert.True(distanceTiles > manhattanToNpc,
            "Route distance (" + distanceTiles + ") should exceed Manhattan distance (" + manhattanToNpc + ") due to obstacle wall");

        Assert.True(int.Parse(standX) >= 9, "Stand tile should be on right side of obstacle wall");
    }

    [Fact]
    public void PrimitiveSocialInteractWithoutCandidateEvidenceIsBlocked()
    {
        var snapshot = CompleteSocialSnapshot();
        var request = Request(snapshot.StateHash, "executor.social_interact");
        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("blocked", queue.Status);
        var blocking = queue.Items[0].BlockingReasons;
        Assert.DoesNotContain("social_runtime_executor_not_implemented", blocking);
        Assert.Contains("social_candidate_stand_npc_evidence_missing", blocking);
    }

    [Fact]
    public void SocialInteractPlanStepNpcTileDiffersFromStandTile()
    {
        var snapshot = CompleteSocialSnapshot();
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var socialCandidate = Assert.Single(candidates.Options[0].SocialCandidates);
        var prediction = ToPrediction(socialCandidate, "social.talk_npc");
        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { prediction }, snapshot.StateHash);

        var moveStep = plan.Steps[0];
        var socialStep = plan.Steps[1];
        Assert.Equal("move_to_tile", moveStep.Kind);
        Assert.Equal("social_interact", socialStep.Kind);
        Assert.True(moveStep.TargetTileX.HasValue);
        Assert.True(moveStep.TargetTileY.HasValue);
        Assert.True(socialStep.TargetTileX.HasValue);
        Assert.True(socialStep.TargetTileY.HasValue);
        Assert.NotEqual(moveStep.TargetTileX, socialStep.TargetTileX);
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_x" && p.Value == "10");
        Assert.Contains(socialStep.Parameters, p => p.Name == "npc_tile_y" && p.Value == "10");
        Assert.Contains(socialStep.Parameters, p => p.Name == "stand_tile_x");
        Assert.Contains(socialStep.Parameters, p => p.Name == "stand_tile_y");
    }

    private static PolicyEventCandidatePrediction ToPrediction(EventCandidate candidate, string optionId)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = candidate.CandidateId,
            OptionId = optionId,
            Kind = candidate.Kind,
            Available = candidate.Available,
            Score = 1.0,
            LocationId = candidate.LocationId,
            TileX = candidate.TileX,
            TileY = candidate.TileY,
            ExpectedEffect = candidate.ExpectedEffect,
            EstimatedTicks = candidate.EstimatedTicks,
            EnergyCost = candidate.EnergyCost,
            TimelineStatus = candidate.Available ? "ready_now" : "blocked",
            Parameters = candidate.Parameters,
            BlockReasons = candidate.BlockReasons
        };
    }

    [Fact]
    public void RemoteNpcWithoutTransparentRouteIsBlockedForTalk()
    {
        var remoteSocial = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"SeedShop\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(socialInteractionValue: remoteSocial);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.talk_npc" });
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_cross_map_route_unavailable", candidate.BlockReasons);
        Assert.DoesNotContain("social_npc_not_in_player_location", candidate.BlockReasons);
        Assert.DoesNotContain(candidate.Parameters, p => p.Name == "stand_tile_x" && !string.IsNullOrEmpty(p.Value));
        Assert.DoesNotContain(candidate.Parameters, p => p.Name == "stand_tile_y" && !string.IsNullOrEmpty(p.Value));
    }

    [Fact]
    public void RemoteNpcWithoutTransparentRouteIsBlockedForGift()
    {
        var remoteSocial = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"SeedShop\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(socialInteractionValue: remoteSocial);

        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { "social.gift_npc" });
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("social_cross_map_route_unavailable", candidate.BlockReasons);
        Assert.DoesNotContain("social_npc_not_in_player_location", candidate.BlockReasons);
        Assert.DoesNotContain(candidate.Parameters, p => p.Name == "stand_tile_x" && !string.IsNullOrEmpty(p.Value));
        Assert.DoesNotContain(candidate.Parameters, p => p.Name == "stand_tile_y" && !string.IsNullOrEmpty(p.Value));
    }

    [Fact]
    public void RemoteTalkCandidateCompilesOneTransparentConnectorThenReplans()
    {
        var remoteSocial = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"SeedShop\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: remoteSocial,
            collisionValue: "{\"location_id\":\"Town\",\"width\":20,\"height\":20,\"notable_tiles\":[]}",
            routeConnectorsValue: "{\"location_id\":\"Town\",\"connectors\":[{\"kind\":\"warp\",\"tile_x\":12,\"tile_y\":10,\"target_location\":\"Farm\",\"target_x\":4,\"target_y\":12,\"resolved\":true}]}",
            routeGraphValue: "{\"edges\":[{\"kind\":\"warp\",\"from_location\":\"Town\",\"from_x\":12,\"from_y\":10,\"target_location\":\"Farm\",\"target_x\":4,\"target_y\":12,\"resolved\":true},{\"kind\":\"building_door\",\"from_location\":\"Farm\",\"from_x\":6,\"from_y\":5,\"target_location\":\"SeedShop\",\"target_x\":3,\"target_y\":9,\"resolved\":true}]}");

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "social.talk_npc" })
            .Options);
        var candidate = Assert.Single(option.SocialCandidates);

        Assert.True(candidate.Available);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal(12, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.option_id" && parameter.Value == "social.talk_npc");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.target_location" && parameter.Value == "SeedShop");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "social_route.remaining_connector_count" && parameter.Value == "2");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "social_route.future_schedule_projection" && parameter.Value == "not_used");
        Assert.DoesNotContain(candidate.Parameters, parameter => parameter.Name == "stand_tile_x" && !string.IsNullOrWhiteSpace(parameter.Value));

        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { ToPrediction(candidate, "social.talk_npc") },
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("traverse_connector", step.Kind);
        Assert.Contains(step.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "Farm");
        Assert.Contains(step.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");
        Assert.Contains(step.Parameters, parameter => parameter.Name == "continuation.target_location" && parameter.Value == "SeedShop");
        Assert.Contains(step.ExpectedEffects, effect => effect == "fresh_snapshot_replan_required=true");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.traverse_connector", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "continuation.target_location" && parameter.Value == "SeedShop");
    }

    [Fact]
    public void RemoteTalkPrefersExecutableFirstConnectorOverShorterUnavailableRoute()
    {
        var remoteSocial = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"SeedShop\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: remoteSocial,
            collisionValue: "{\"location_id\":\"Town\",\"width\":20,\"height\":20,\"notable_tiles\":[]}",
            routeConnectorsValue: "{\"location_id\":\"Town\",\"connectors\":[{\"kind\":\"warp\",\"tile_x\":13,\"tile_y\":10,\"target_location\":\"B\",\"target_x\":2,\"target_y\":2,\"resolved\":true}]}",
            routeGraphValue: "{\"edges\":[{\"kind\":\"warp\",\"from_location\":\"Town\",\"from_x\":12,\"from_y\":10,\"target_location\":\"A\",\"target_x\":1,\"target_y\":1,\"resolved\":true},{\"kind\":\"warp\",\"from_location\":\"A\",\"from_x\":2,\"from_y\":2,\"target_location\":\"SeedShop\",\"target_x\":3,\"target_y\":3,\"resolved\":true},{\"kind\":\"warp\",\"from_location\":\"Town\",\"from_x\":13,\"from_y\":10,\"target_location\":\"B\",\"target_x\":2,\"target_y\":2,\"resolved\":true},{\"kind\":\"warp\",\"from_location\":\"B\",\"from_x\":3,\"from_y\":3,\"target_location\":\"C\",\"target_x\":4,\"target_y\":4,\"resolved\":true},{\"kind\":\"warp\",\"from_location\":\"C\",\"from_x\":5,\"from_y\":5,\"target_location\":\"SeedShop\",\"target_x\":6,\"target_y\":6,\"resolved\":true}]}");

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "social.talk_npc" })
            .Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.Equal(13, candidate.TileX);
        Assert.Equal(10, candidate.TileY);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "B");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "social_route.remaining_connector_count" && parameter.Value == "3");
    }

    [Fact]
    public void RemoteGiftRoutePreservesExactGiftContinuation()
    {
        var remoteSocial = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"SeedShop\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: remoteSocial,
            collisionValue: "{\"location_id\":\"Town\",\"width\":20,\"height\":20,\"notable_tiles\":[]}",
            routeConnectorsValue: "{\"location_id\":\"Town\",\"connectors\":[{\"kind\":\"warp\",\"tile_x\":12,\"tile_y\":10,\"target_location\":\"SeedShop\",\"target_x\":4,\"target_y\":12,\"resolved\":true}]}",
            routeGraphValue: "{\"edges\":[{\"kind\":\"warp\",\"from_location\":\"Town\",\"from_x\":12,\"from_y\":10,\"target_location\":\"SeedShop\",\"target_x\":4,\"target_y\":12,\"resolved\":true}]}");

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "social.gift_npc" })
            .Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.option_id" && parameter.Value == "social.gift_npc");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.slot_index" && parameter.Value == "0");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.qualified_item_id" && parameter.Value == "(O)66");
    }

    [Fact]
    public void GiftCandidateCapKeepsAvailableCurrentLocationCandidateAheadOfRemoteDiagnostics()
    {
        var remoteNpcs = Enumerable.Range(0, 65).Select(index => new
        {
            name = $"Remote{index:D3}",
            display_name = $"Remote{index:D3}",
            master_data_present = true,
            gift_taste_master_data_present = true,
            current_instance_loaded = true,
            location_id = "AnimalShop",
            tile_x = 10,
            tile_y = 10,
            facing_direction = 2,
            is_villager = true,
            simple_non_villager_npc = false,
            is_invisible = false,
            is_sleeping = false,
            has_controller = false,
            is_busy = false,
            schedule_loaded = true,
            can_socialize = true,
            can_socialize_complete = true,
            can_receive_gifts = true,
            can_receive_gifts_complete = true,
            is_birthday = false,
            current_route_window_complete = true
        });
        var currentNpc = new
        {
            name = "Abigail",
            display_name = "Abigail",
            master_data_present = true,
            gift_taste_master_data_present = true,
            current_instance_loaded = true,
            location_id = "Town",
            tile_x = 10,
            tile_y = 10,
            facing_direction = 2,
            is_villager = true,
            simple_non_villager_npc = false,
            is_invisible = false,
            is_sleeping = false,
            has_controller = false,
            is_busy = false,
            schedule_loaded = true,
            can_socialize = true,
            can_socialize_complete = true,
            can_receive_gifts = true,
            can_receive_gifts_complete = true,
            is_birthday = false,
            current_route_window_complete = true
        };
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: JsonSerializer.Serialize(remoteNpcs.Append(currentNpc), JsonOptions));

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "social.gift_npc" })
            .Options);

        Assert.Equal(64, option.SocialCandidates.Length);
        var available = Assert.Single(option.SocialCandidates.Where(candidate => candidate.Available));
        Assert.Contains(available.Parameters, parameter => parameter.Name == "npc_name" && parameter.Value == "Abigail");
        Assert.DoesNotContain("no_available_social_current_state_candidates", option.BlockingReasons);
    }

    private static SnapshotEnvelope CompleteSocialSnapshot(
        string? socialInteractionValue = null,
        string? friendshipValue = null,
        string? inventoryValue = null,
        string? giftTasteField = null,
        string? scheduleField = null,
        string? collisionValue = null,
        string? routeActionCoverageValue = null,
        string? routeConnectorsValue = null,
        string? routeGraphValue = null,
        int playerTileX = 8,
        int playerTileY = 10)
    {
        socialInteractionValue ??= "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        friendshipValue ??= "[{\"npc_name\":\"Abigail\",\"points\":250,\"heart_level\":1,\"gifts_this_week\":0,\"gifts_today\":0,\"talked_to_today\":false,\"is_divorced\":false}]";
        inventoryValue ??= "[{\"slot_index\":0,\"item_id\":\"66\",\"qualified_item_id\":\"(O)66\",\"display_name\":\"Amethyst\",\"stack\":1,\"quality\":0,\"maximum_stack_size\":999,\"is_object\":true,\"object_quest_item\":false,\"object_big_craftable\":false,\"is_furniture\":false,\"is_wallpaper\":false,\"protected_from_auto_sell\":false,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"context_tags\":[],\"is_empty\":false}]";
        giftTasteField ??= "{\"value\":[{\"npc_name\":\"Abigail\",\"slot_index\":0,\"qualified_item_id\":\"(O)66\",\"quality\":0,\"taste\":\"love\",\"expected_friendship_delta\":\"80\",\"expected_friendship_delta_complete\":true,\"complete\":true}],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}";
        scheduleField ??= "{\"value\":[{\"name\":\"Abigail\",\"schedule_key\":\"spring\",\"follow_schedule\":true,\"ignore_schedule_today\":false,\"schedule_loaded\":true,\"entries\":[]}],\"status\":\"available\",\"source\":{\"kind\":\"test\",\"path\":\"test\"},\"adapter\":\"test\",\"read_at_tick\":1,\"confidence\":1}";
        collisionValue ??= "{\"width\":20,\"height\":20,\"notable_tiles\":[]}";
        routeActionCoverageValue ??= "{\"rows\":[]}";
        routeConnectorsValue ??= "{\"location_id\":\"Town\",\"connectors\":[]}";
        routeGraphValue ??= "{\"edges\":[]}";

        return Snapshot("""
        {
            "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":PLAYER_TILE_X,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":PLAYER_TILE_Y,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":INVENTORY_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"spouse": {"value":"","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"active_dialogue_events": {"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "year": {"value":2,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "is_green_rain": {"value":false,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "social_interaction": {"value":SOCIAL_INTERACTION_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "friendships": {"value":FRIENDSHIP_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "schedules": SCHEDULE_FIELD,
            "gift_tastes": GIFT_TASTE_FIELD
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":COLLISION_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":ROUTE_ACTION_COVERAGE_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":ROUTE_CONNECTORS_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":ROUTE_GRAPH_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("SOCIAL_INTERACTION_VALUE", socialInteractionValue)
        .Replace("FRIENDSHIP_VALUE", friendshipValue)
        .Replace("INVENTORY_VALUE", inventoryValue)
        .Replace("GIFT_TASTE_FIELD", giftTasteField)
        .Replace("SCHEDULE_FIELD", scheduleField)
        .Replace("COLLISION_VALUE", collisionValue)
        .Replace("ROUTE_ACTION_COVERAGE_VALUE", routeActionCoverageValue)
        .Replace("ROUTE_CONNECTORS_VALUE", routeConnectorsValue)
        .Replace("ROUTE_GRAPH_VALUE", routeGraphValue)
        .Replace("PLAYER_TILE_X", playerTileX.ToString())
        .Replace("PLAYER_TILE_Y", playerTileY.ToString()));
    }

    private static SmallModelActionEnvelope Request(string stateHash, string optionId)
    {
        return new SmallModelActionEnvelope
        {
            ModelOutputId = "model.social.test",
            SourceModel = "test",
            StateHash = stateHash,
            GoalId = "goal.social.test",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef
            {
                ActorId = "training_farmer.test",
                ActorType = "training_farmer",
                ControlSurface = "training_sandbox"
            },
            Actions = new[]
            {
                new SmallModelAction
                {
                    ActionId = "action.social.test",
                    OptionId = optionId,
                    Rationale = "test"
                }
            }
        };
    }

    private static SnapshotEnvelope Snapshot(string stateJson)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(stateJson, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-07-05T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
