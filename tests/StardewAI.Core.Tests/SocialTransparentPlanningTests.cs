using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
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

}
