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
    public void RemoteTalkAtClosedDoorDefersSameNpcWithOneRollingWait()
    {
        var remoteSocial = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"gift_taste_master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"SeedShop\",\"tile_x\":10,\"tile_y\":10,\"facing_direction\":2,\"is_villager\":true,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"has_controller\":false,\"is_busy\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"can_receive_gifts\":true,\"can_receive_gifts_complete\":true,\"is_birthday\":false,\"current_route_window_complete\":true}]";
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: remoteSocial,
            routeConnectorsValue: "{\"location_id\":\"Town\",\"connectors\":[{\"kind\":\"locked_door_warp\",\"tile_x\":11,\"tile_y\":10,\"target_location\":\"SeedShop\",\"target_x\":3,\"target_y\":9,\"resolved\":true},{\"kind\":\"warp\",\"tile_x\":19,\"tile_y\":10,\"target_location\":\"Beach\",\"target_x\":1,\"target_y\":1,\"resolved\":true}]}",
            routeGraphValue: "{\"edges\":[{\"kind\":\"locked_door_warp\",\"from_location\":\"Town\",\"from_x\":11,\"from_y\":10,\"target_location\":\"SeedShop\",\"target_x\":3,\"target_y\":9,\"resolved\":true},{\"kind\":\"warp\",\"from_location\":\"Town\",\"from_x\":19,\"from_y\":10,\"target_location\":\"Beach\",\"target_x\":1,\"target_y\":1,\"resolved\":true},{\"kind\":\"warp\",\"from_location\":\"Beach\",\"from_x\":2,\"from_y\":2,\"target_location\":\"SeedShop\",\"target_x\":3,\"target_y\":9,\"resolved\":true}]}",
            routeGateContextValue: "{\"location_id\":\"Town\",\"action_gates\":[{\"kind\":\"locked_door_warp\",\"tile_x\":11,\"tile_y\":10,\"target_location\":\"SeedShop\",\"open_time\":900,\"effective_open_time\":900,\"close_time\":1700,\"festival_closed\":false,\"seed_shop_wednesday_closed\":false,\"friendship_allowed\":true,\"green_rain_override\":false,\"allowed_now\":false,\"unresolved_reason\":null}]}",
            currentTime: 620);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "social.talk_npc" })
            .Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.False(candidate.AllowedNow);
        Assert.True(candidate.AllowedToday);
        Assert.Equal(900, candidate.NextOpenTime);
        Assert.Equal(11, candidate.TileX);
        Assert.Equal(9600, candidate.WaitCost);
        Assert.Contains("route_gate_not_open_yet", candidate.GateReasons);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");

        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { ToPrediction(candidate, "social.talk_npc") },
            snapshot.StateHash);
        var wait = Assert.Single(plan.Steps);
        Assert.Equal("wait_ticks", wait.Kind);
        Assert.Equal(600, wait.WaitTicks);
        Assert.Contains(wait.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.wait_ticks", item.OptionId);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");
    }

    [Fact]
    public void BoundTalkContinuationRoutesToLastObservedLocationWhenNpcInstanceUnloads()
    {
        var snapshot = CompleteSocialSnapshot(
            socialInteractionValue: "[]",
            routeConnectorsValue: "{\"location_id\":\"Town\",\"connectors\":[{\"kind\":\"door\",\"tile_x\":null,\"tile_y\":null,\"target_location\":\"Unknown\",\"resolved\":false},{\"kind\":\"warp\",\"tile_x\":20,\"tile_y\":10,\"target_location\":\"SeedShop\",\"target_x\":3,\"target_y\":9,\"resolved\":true}]}",
            routeGraphValue: "{\"edges\":[{\"kind\":\"warp\",\"from_location\":\"Town\",\"from_x\":20,\"from_y\":10,\"target_location\":\"SeedShop\",\"target_x\":3,\"target_y\":9,\"resolved\":true}]}" );
        var binding = new OptionAvailabilityCandidate
        {
            OptionId = "social.talk_npc",
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "continuation.option_id", Value = "social.talk_npc" },
                new SmallModelActionParameter { Name = "continuation.npc_name", Value = "Abigail" },
                new SmallModelActionParameter { Name = "continuation.target_location", Value = "SeedShop" }
            }
        };

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { binding })
            .Options);
        var candidate = Assert.Single(option.SocialCandidates);

        Assert.True(candidate.Available);
        Assert.Equal("route_connector_tile", candidate.Kind);
        Assert.Equal(20, candidate.TileX);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "social_route.position_source" && parameter.Value == "continuation.last_observed_current_loaded_instance");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "social_route.future_schedule_projection" && parameter.Value == "not_used");
    }

    [Fact]
    public void BoundTalkContinuationWaitsWhenCurrentNpcIsTemporarilyUnreachable()
    {
        var collision = "{\"width\":20,\"height\":20,\"notable_tiles\":[{\"tile_x\":9,\"tile_y\":10,\"collision_blocked\":true},{\"tile_x\":11,\"tile_y\":10,\"collision_blocked\":true},{\"tile_x\":10,\"tile_y\":9,\"collision_blocked\":true},{\"tile_x\":10,\"tile_y\":11,\"collision_blocked\":true}]}";
        var snapshot = CompleteSocialSnapshot(collisionValue: collision);
        var binding = new OptionAvailabilityCandidate
        {
            OptionId = "social.talk_npc",
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "continuation.option_id", Value = "social.talk_npc" },
                new SmallModelActionParameter { Name = "continuation.npc_name", Value = "Abigail" },
                new SmallModelActionParameter { Name = "continuation.target_location", Value = "Town" }
            }
        };

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { binding })
            .Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.Equal("social_continuation_retry_wait", candidate.Kind);
        Assert.Equal("current_loaded_social_target_temporarily_unreachable_retry", candidate.AvailabilityClass);
        Assert.Contains("social_no_reachable_adjacent_stand_tile", candidate.GateReasons);
        Assert.Empty(candidate.BlockReasons);

        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { ToPrediction(candidate, "social.talk_npc") },
            snapshot.StateHash);
        var wait = Assert.Single(plan.Steps);
        Assert.Equal("wait_ticks", wait.Kind);
        Assert.Equal(600, wait.WaitTicks);
        Assert.Contains(wait.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");
    }

    [Fact]
    public void BoundTalkContinuationClosesDialogueMenuBeforeRetryingNpc()
    {
        var snapshot = CompleteSocialSnapshot(
            activeMenuValue: "{\"is_open\":true,\"type\":\"DialogueBox\",\"last_question_key\":null,\"is_sleep_prompt\":false,\"event_up\":false,\"dialogue_is_question\":false,\"dialogue_response_count\":0,\"dialogue_transitioning\":false,\"dialogue_character_present\":false,\"dialogue_speaker_name\":null}");
        var binding = new OptionAvailabilityCandidate
        {
            OptionId = "social.talk_npc",
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "continuation.option_id", Value = "social.talk_npc" },
                new SmallModelActionParameter { Name = "continuation.npc_name", Value = "Abigail" },
                new SmallModelActionParameter { Name = "continuation.target_location", Value = "Town" }
            }
        };

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { binding })
            .Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.Equal("recovery_close_menu", candidate.Kind);
        Assert.Equal("social_continuation_menu_recovery", candidate.AvailabilityClass);
        Assert.Contains("social_menu_must_be_clear", candidate.GateReasons);

        var plan = new StardewAI.Core.Training.DailyPlanCompiler().Compile(
            new[] { ToPrediction(candidate, "social.talk_npc") },
            snapshot.StateHash);
        var close = Assert.Single(plan.Steps);
        Assert.Equal("close_menu", close.Kind);
        Assert.Contains(close.Parameters, parameter => parameter.Name == "continuation.npc_name" && parameter.Value == "Abigail");

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.True(queue.Status == "pending", string.Join(",", queue.CompilerDiagnostics.Concat(queue.Items.SelectMany(item => item.BlockingReasons))));
        Assert.Equal("executor.close_menu", Assert.Single(queue.Items).OptionId);
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
        string? routeGateContextValue = null,
        string? activeMenuValue = null,
        int currentTime = 900,
        int playerTileX = 8,
        int playerTileY = 10,
        bool playerMarriedOrRoommate = false,
        bool playerEngaged = false,
        int farmhouseUpgradeLevel = 0)
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
        routeGateContextValue ??= "{\"location_id\":\"Town\",\"action_gates\":[]}";
        activeMenuValue ??= "{\"is_open\":false,\"type\":\"none\"}";

        return Snapshot("""
        {
            "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":PLAYER_TILE_X,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":PLAYER_TILE_Y,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":INVENTORY_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"spouse": {"value":"","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"married_or_roommate": {"value":PLAYER_MARRIED_OR_ROOMMATE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"engaged": {"value":PLAYER_ENGAGED,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"farmhouse_upgrade_level": {"value":FARMHOUSE_UPGRADE_LEVEL,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
            ,"active_dialogue_events": {"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time": {
            "time": {"value":CURRENT_TIME,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
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
            "active_menu": {"value":ACTIVE_MENU_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":COLLISION_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":ROUTE_ACTION_COVERAGE_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":ROUTE_CONNECTORS_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_gate_context": {"value":ROUTE_GATE_CONTEXT_VALUE,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
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
        .Replace("ROUTE_GATE_CONTEXT_VALUE", routeGateContextValue)
        .Replace("ACTIVE_MENU_VALUE", activeMenuValue)
        .Replace("ROUTE_GRAPH_VALUE", routeGraphValue)
        .Replace("CURRENT_TIME", currentTime.ToString())
        .Replace("PLAYER_TILE_X", playerTileX.ToString())
        .Replace("PLAYER_TILE_Y", playerTileY.ToString())
        .Replace("PLAYER_MARRIED_OR_ROOMMATE", playerMarriedOrRoommate.ToString().ToLowerInvariant())
        .Replace("PLAYER_ENGAGED", playerEngaged.ToString().ToLowerInvariant())
        .Replace("FARMHOUSE_UPGRADE_LEVEL", farmhouseUpgradeLevel.ToString()));
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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);}
