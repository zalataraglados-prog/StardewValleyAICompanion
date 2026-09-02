using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void RecoveryEmitsNoCandidateWhenNoStabilizationIsRequired()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 1800, menuOpen: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.Empty(option.EventCandidates);
    }

    [Fact]
    public void AutonomousPlanningEvaluatesOnlyRecoveryAtTheDepartureBoundary()
    {
        var availability = new CandidateOptionAvailabilityEvaluator()
            .EvaluateForAutonomousRuntimePlanning(
                RecoverySnapshot(
                    time: GameClockBudgetPolicy.AutonomousRecoveryStartTime,
                    menuOpen: false,
                    currentLocationIsHome: true),
                new[] { "farm.maintain_crops", "social.talk_to_npc" });

        var option = Assert.Single(availability.Options);
        Assert.Equal("recovery.stabilize_day", option.OptionId);
        Assert.Contains(
            option.EventCandidates,
            candidate => candidate.Kind == "recovery_return_home" && candidate.Available);
    }

    [Fact]
    public void RecoveryKeepsLateNightHomeAndSleepCandidatesBlockedUntilExecutorsExist()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: true, currentLocationIsHome: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.Equal(3, option.EventCandidates.Length);
        Assert.Contains(option.EventCandidates, candidate => candidate.Kind == "recovery_close_menu" && candidate.BlockReasons.Contains("close_menu_type_unknown"));
        Assert.Contains(option.EventCandidates, candidate => candidate.Kind == "recovery_return_home" && candidate.BlockReasons.Contains("recovery_route_graph_unavailable"));
        Assert.Contains(option.EventCandidates, candidate => candidate.Kind == "recovery_sleep_before_collapse" && candidate.BlockReasons.Contains("recovery_terminal_sleep_already_covered_by_return_home"));
    }

    [Fact]
    public void RecoveryReturnHomeCandidateUsesTransparentHomeBedTargetWhenAlreadyHome()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, bedBlocked: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.True(candidate.Available);
        Assert.Equal("FarmHouse", candidate.LocationId);
        Assert.Equal(3, candidate.TileX);
        Assert.Equal(9, candidate.TileY);
        Assert.Equal("move_to_bed_adjacent=3,9;step_onto_sleep_touch_tile=3,8;touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateAllowsBlockedBedTileWhenAdjacentStandTileIsReachable()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, bedBlocked: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.True(candidate.Available);
        Assert.Equal("FarmHouse", candidate.LocationId);
        Assert.Equal(3, candidate.TileX);
        Assert.Equal(9, candidate.TileY);
        Assert.Equal("move_to_bed_adjacent=3,9;step_onto_sleep_touch_tile=3,8;touch_action=Sleep;sleep_prompt_expected;Sleep_Yes_not_executed", candidate.ExpectedEffect);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateBlocksWhenNoAdjacentBedStandTileIsReachable()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, bedBlocked: true, adjacentBedTilesBlocked: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.False(candidate.Available);
        Assert.Contains("recovery_bed_adjacent_stand_tile_unavailable", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateDoesNotUseActiveObjectAsSleepGate()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: false, currentLocationIsHome: true, activeObjectQualifiedId: "(O)472"), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.True(candidate.Available);
        Assert.DoesNotContain("sleep_interact_active_object_must_be_clear", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateBlocksBedInteractionWhenMenuIsOpen()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: true, currentLocationIsHome: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.False(candidate.Available);
        Assert.Contains("sleep_prompt_menu_must_be_clear", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryReturnHomeCandidateBlocksConfirmWhenSleepPromptIsAlreadyOpen()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2300, menuOpen: true, currentLocationIsHome: true, sleepPromptOpen: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.False(candidate.Available);
        Assert.Contains("sleep_prompt_menu_must_be_clear", candidate.BlockReasons);
        Assert.Contains("recovery_sleep_prompt_already_open", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryResumesExactSleepPromptAtAnyTime()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                RecoverySnapshot(
                    time: 600,
                    menuOpen: true,
                    currentLocationIsHome: true,
                    sleepPromptOpen: true),
                new[] { "recovery.stabilize_day" },
                includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(
            option.EventCandidates,
            item => item.Kind == "recovery_resume_sleep_prompt");
        Assert.True(candidate.Available);
        Assert.Empty(candidate.BlockReasons);
        Assert.Contains(
            candidate.Parameters,
            parameter =>
                parameter.Name == "sleep_resume_mode" &&
                parameter.Value == "existing_exact_prompt");
    }

    [Fact]
    public void RecoverySleepImmediatelyAvailableAtOrPast2400WhenHomeWithBed()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2400, menuOpen: false, currentLocationIsHome: true, bedBlocked: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.ExecutorEnabled);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:sleep_immediately", candidate.CandidateId);
        Assert.True(candidate.Available);
        Assert.Equal("FarmHouse", candidate.LocationId);
        Assert.Equal(3, candidate.TileX);
        Assert.Equal(9, candidate.TileY);
        Assert.Empty(candidate.BlockReasons);
    }

    [Fact]
    public void RecoverySleepImmediatelyCanLeaveUnsupportedSleepActionStartTile()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(
                RecoverySnapshot(
                    time: 2400,
                    menuOpen: false,
                    currentLocationIsHome: true,
                    playerTileX: 3,
                    playerTileY: 8,
                    routeActionRows: "{\"tile_x\":3,\"tile_y\":8,\"branch\":\"Sleep\",\"route_training_blocked\":true}"),
                new[] { "recovery.stabilize_day" },
                includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:sleep_immediately", candidate.CandidateId);
        Assert.True(candidate.Available);
        Assert.DoesNotContain("unsupported_route_action_branch_on_path", candidate.BlockReasons);
    }

    [Fact]
    public void RecoverySleepImmediatelyBlocksWhenOutsideHomeAtOrPast2400()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2400, menuOpen: false, currentLocationIsHome: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:sleep_immediately", candidate.CandidateId);
        Assert.False(candidate.Available);
        Assert.Contains("recovery_route_graph_unavailable", candidate.BlockReasons);
    }

    [Fact]
    public void RecoverySleepImmediatelyBlocksWhenBedMissingAtOrPast2400()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 2400, menuOpen: false, currentLocationIsHome: true, bedBlocked: true, adjacentBedTilesBlocked: true), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.Equal("recovery:sleep_immediately", candidate.CandidateId);
        Assert.False(candidate.Available);
        Assert.Contains("recovery_bed_adjacent_stand_tile_unavailable", candidate.BlockReasons);
    }

    [Fact]
    public void RecoveryOutsideHomeCarriesExactNextConnectorParameters()
    {
        var snapshot = Snapshot("""
        {
          "time": {
            "time": {"value":2300,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":3,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":9,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"Town","current_location_is_home":false,"bed_tile_x":3,"bed_tile_y":8,"bed_tile_has_bed":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":12,"height":12,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Town","connectors":[{"kind":"warp","tile_x":5,"tile_y":9,"target_location":"Farm","target_x":10,"target_y":11,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_gate_context": {"value":{"location_id":"Town","action_gates":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[
              {"kind":"warp","from_location":"Town","from_x":5,"from_y":9,"target_location":"Farm","target_x":10,"target_y":11,"resolved":true},
              {"kind":"building_door","from_location":"Farm","from_x":6,"from_y":5,"target_location":"FarmHouse","target_x":3,"target_y":9,"resolved":true}
            ]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates, item => item.Kind == "recovery_return_home");
        Assert.True(candidate.Available);
        Assert.Equal("Town", candidate.LocationId);
        Assert.Equal(5, candidate.TileX);
        Assert.Equal(9, candidate.TileY);
        Assert.True(candidate.EstimatedTicks > 0);
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "execution_option_id" && parameter.Value == "executor.traverse_connector");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "connector_kind" && parameter.Value == "warp");
        Assert.Contains(candidate.Parameters, parameter => parameter.Name == "expected_target_location" && parameter.Value == "Farm");
    }

    [Fact]
    public void RecoveryHighLevelEnabledAfterCandidateChainComplete()
    {
        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(RecoverySnapshot(time: 1800, menuOpen: false), new[] { "recovery.stabilize_day" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        Assert.True(option.ExecutorEnabled);
        Assert.DoesNotContain("executor_disabled", option.BlockingReasons);
    }

    private static SnapshotEnvelope RecoverySnapshot(
        int time,
        bool menuOpen,
        bool currentLocationIsHome = true,
        bool bedBlocked = false,
        bool adjacentBedTilesBlocked = false,
        string activeObjectQualifiedId = "",
        bool sleepPromptOpen = false,
        int playerTileX = 3,
        int playerTileY = 9,
        string routeActionRows = "")
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":TIME,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"CURRENT_LOCATION","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":PLAYER_TILE_X,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":PLAYER_TILE_Y,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "current_item_qualified_id": {"value":"","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "active_object_qualified_id": {"value":"ACTIVE_OBJECT","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "home_context": {"value":{"home_available":true,"home_location_id":"FarmHouse","current_location_id":"CURRENT_LOCATION","current_location_is_home":CURRENT_HOME,"entry_tile_x":3,"entry_tile_y":9,"bed_tile_x":3,"bed_tile_y":8,"bed_tile_has_bed":true,"sleep_executor_enabled":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN,"type":"MENU_TYPE","last_question_key":LAST_QUESTION_KEY,"is_sleep_prompt":SLEEP_PROMPT_OPEN,"event_up":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context": {"value":{"prompt_open":SLEEP_PROMPT_OPEN,"active_menu_open":MENU_OPEN,"active_menu_type":"MENU_TYPE","last_question_key":LAST_QUESTION_KEY,"can_confirm_sleep":CAN_CONFIRM_SLEEP,"confirm_executor_enabled":CAN_CONFIRM_SLEEP,"confirm_action_key":"Sleep_Yes"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"CURRENT_LOCATION","width":12,"height":12,"notable_tiles":[BLOCKED_TILES]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[ROUTE_ACTION_ROWS]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("TIME", time.ToString())
        .Replace("CURRENT_LOCATION", currentLocationIsHome ? "FarmHouse" : "Town")
        .Replace("CURRENT_HOME", currentLocationIsHome ? "true" : "false")
        .Replace("PLAYER_TILE_X", playerTileX.ToString())
        .Replace("PLAYER_TILE_Y", playerTileY.ToString())
        .Replace("BLOCKED_TILES", RecoveryBlockedTiles(bedBlocked, adjacentBedTilesBlocked))
        .Replace("ROUTE_ACTION_ROWS", routeActionRows)
        .Replace("ACTIVE_OBJECT", activeObjectQualifiedId)
        .Replace("SLEEP_PROMPT_OPEN", sleepPromptOpen ? "true" : "false")
        .Replace("CAN_CONFIRM_SLEEP", sleepPromptOpen ? "true" : "false")
        .Replace("LAST_QUESTION_KEY", sleepPromptOpen ? "\"Sleep\"" : "null")
        .Replace("MENU_TYPE", sleepPromptOpen ? "DialogueBox" : "")
        .Replace("MENU_OPEN", menuOpen ? "true" : "false"));
    }

    private static string RecoveryBlockedTiles(bool bedBlocked, bool adjacentBedTilesBlocked)
    {
        var tiles = new List<string>();
        if (bedBlocked)
        {
            tiles.Add("{\"tile_x\":3,\"tile_y\":8,\"collision_blocked\":true}");
        }

        if (adjacentBedTilesBlocked)
        {
            tiles.Add("{\"tile_x\":4,\"tile_y\":8,\"collision_blocked\":true}");
            tiles.Add("{\"tile_x\":2,\"tile_y\":8,\"collision_blocked\":true}");
            tiles.Add("{\"tile_x\":3,\"tile_y\":9,\"collision_blocked\":true}");
            tiles.Add("{\"tile_x\":3,\"tile_y\":7,\"collision_blocked\":true}");
        }

        return string.Join(",", tiles);
    }

    private static SnapshotEnvelope RouteConnectorSnapshot(
        bool routeTrainingBlocked,
        bool resolved = true,
        string targetLocation = "Town")
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"Farm","connector_count":1,"connectors":[{"kind":"warp","tile_x":12,"tile_y":10,"target_location":"TARGET_LOCATION","target_x":1,"target_y":2,"resolved":RESOLVED}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":12,"tile_y":10,"branch":"Warp","route_training_blocked":ROUTE_BLOCKED}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace(
            "ROUTE_BLOCKED",
            routeTrainingBlocked ? "true" : "false")
        .Replace("RESOLVED", resolved ? "true" : "false")
        .Replace("TARGET_LOCATION", targetLocation));
    }

    private static SnapshotEnvelope InteractEndpointSnapshot(
        bool menuOpen,
        string branch,
        bool routeTrainingBlocked,
        string action = "OpenShop SeedShop Down 900 1700",
        string parsed = "\"parsed\":{\"kind\":\"open_shop\",\"shop_id\":\"SeedShop\",\"required_direction\":\"Down\",\"open_time\":900,\"close_time\":1700}",
        string ownerServiceStatus = "\"owner_service_status\":{\"owner_required\":false,\"owner_npc\":null,\"owner_found\":null,\"in_service_area\":null,\"block_reason\":null}",
        string serviceTimeStatus = "\"service_time_status\":{\"current_time\":900,\"time_gate_known\":false,\"allowed_now\":true,\"block_reasons\":[]}",
        string npcPositions = "[]")
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_action_tiles": {"value":[{"tile_x":11,"tile_y":10,"action":"ACTION",PARSED,OWNER_SERVICE_STATUS,SERVICE_TIME_STATUS}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Town","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"BRANCH","route_training_blocked":ROUTE_BLOCKED}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "npcs": {
            "positions": {"value":NPC_POSITIONS,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("MENU_OPEN", menuOpen ? "true" : "false")
        .Replace("ACTION", action)
        .Replace("PARSED", parsed)
        .Replace("OWNER_SERVICE_STATUS", ownerServiceStatus)
        .Replace("SERVICE_TIME_STATUS", serviceTimeStatus)
        .Replace("BRANCH", branch)
        .Replace("NPC_POSITIONS", npcPositions)
        .Replace("ROUTE_BLOCKED", routeTrainingBlocked ? "true" : "false"));
    }

    private static SnapshotEnvelope InteractSnapshot(bool menuOpen, string branch, bool blocked)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Town","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":10,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "facing_direction": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "route_context": {"value":{"probes":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":MENU_OPEN},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "route_action_branch_coverage": {"value":{"rows":[{"tile_x":11,"tile_y":10,"branch":"BRANCH","route_training_blocked":BLOCKED}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("MENU_OPEN", menuOpen ? "true" : "false")
        .Replace("BRANCH", branch)
        .Replace("BLOCKED", blocked ? "true" : "false"));
    }

    private static SnapshotEnvelope BuySnapshot(
        string entryOverride,
        int safetyTimer = 0)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "seed_inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crop_catalog": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_stock": {"value":{"kind":"shop_stock","shop_id":"SeedShop","read_only":false,"safety_timer":SAFETY_TIMER,"entry_count":1,"entries":[ENTRY]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
        .Replace("ENTRY", entryOverride)
        .Replace("SAFETY_TIMER", safetyTimer.ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }

    private static SnapshotEnvelope BuyPreviewSnapshot(
        string entryOverride,
        bool endpointAllowed = true,
        string endpointExtra = "")
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "season": {"value":"spring","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "seed_inventory": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "crop_catalog": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":{"stores_closed_for_festival":false,"shops":[{"shop_id":"Blacksmith","stock_preview":{"kind":"shop_stock_preview","shop_id":"Blacksmith","entries":[ENTRY]}}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "collision_grid": {"value":{"location_id":"FarmHouse","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"FarmHouse","connectors":[{"kind":"warp","tile_x":2,"tile_y":3,"target_location":"Blacksmith","target_x":3,"target_y":4,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[
              {"kind":"warp","from_location":"FarmHouse","from_x":2,"from_y":3,"target_location":"Blacksmith","target_x":3,"target_y":4,"resolved":true},
              {"kind":"shop_endpoint","from_location":"Blacksmith","from_x":3,"from_y":5,"shop_id":"Blacksmith","action_type":"Blacksmith","allowed_now":ENDPOINT_ALLOWED ENDPOINT_EXTRA,"resolved":false}
            ]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"id":"FarmHouse"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_action_tiles": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
            .Replace("ENTRY", entryOverride)
            .Replace(
                "ENDPOINT_ALLOWED",
                endpointAllowed ? "true" : "false")
            .Replace("ENDPOINT_EXTRA", endpointExtra));
    }

    private static SnapshotEnvelope SellSnapshot(string inventoryItemOverride, string? sellContextOverride = null)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"SeedShop","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[ITEM],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":{"stores_closed_for_festival":false,"shops":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sell_context": {"value":SELL_CONTEXT,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "shipping_bins": {"value":[{"days_of_construction_left":0,"player_within_shipping_range":true}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
            .Replace("ITEM", inventoryItemOverride)
            .Replace(
                "SELL_CONTEXT",
                sellContextOverride ??
                """{"kind":"shop_sell_context","shop_id":"SeedShop","currency":0,"read_only":false,"safety_timer":0,"held_item_present":false,"storage_shop":false,"sell_percentage":1.0,"custom_on_sell_present":false,"categories_to_sell":[-75],"tag_groups_to_sell":[]}"""));
    }

    private static SnapshotEnvelope SellPreviewSnapshot(
        string inventoryItemOverride,
        bool endpointAllowed = true)
    {
        return Snapshot("""
        {
          "time": {
            "time": {"value":900,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":1,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "energy": {"value":270,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[ITEM],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "shops": {"value":{"stores_closed_for_festival":false,"shops":[{"shop_id":"SeedShop","sale_preview":{"kind":"shop_sale_preview","shop_id":"SeedShop","currency":0,"default_sell_percentage":1.0,"tag_groups_to_sell":[["category_vegetable"]],"executor_sale_preview_enabled":true,"executor_block_reasons":[]}}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "collision_grid": {"value":{"location_id":"FarmHouse","width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors": {"value":{"location_id":"FarmHouse","connectors":[{"kind":"warp","tile_x":2,"tile_y":3,"target_location":"SeedShop","target_x":3,"target_y":4,"resolved":true}]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_graph": {"value":{"edges":[
              {"kind":"warp","from_location":"FarmHouse","from_x":2,"from_y":3,"target_location":"SeedShop","target_x":3,"target_y":4,"resolved":true},
              {"kind":"shop_endpoint","from_location":"SeedShop","from_x":3,"from_y":5,"shop_id":"SeedShop","action_type":"OpenShop","allowed_now":ENDPOINT_ALLOWED,"resolved":false}
            ]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "current_location": {
            "map": {"value":{"id":"FarmHouse"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "shop_action_tiles": {"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """
            .Replace("ITEM", inventoryItemOverride)
            .Replace(
                "ENDPOINT_ALLOWED",
                endpointAllowed ? "true" : "false"));
    }

    private static OptionAvailabilityCandidate Candidate(string optionId, params SmallModelActionParameter[] parameters)
    {
        return new OptionAvailabilityCandidate
        {
            OptionId = optionId,
            Parameters = parameters
        };
    }

    private static SnapshotEnvelope MachinePredictionSnapshot(string outputItemFields)
    {
        return Snapshot("""
        {
          "player": {
            "location_id": {"value":"Farm","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x": {"value":63,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y": {"value":15,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory_capacity": {"value":{"occupied_stacks":1,"empty_slots":1,"has_empty_slot":true},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory": {"value":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "farm": {
            "machines": {"value":[{"tile_x":64,"tile_y":15,"qualified_item_id":"(BC)12","display_name":"Keg","ready_for_harvest":false,"minutes_until_ready":-1,"machine_data":{"status":"available","has_output":true,"output_rule_count":1,"output_rules":[{"id":"derived_price","required_item_id":"(O)262","output_item":{"item_id":"346","qualified_item_id":"(O)346","stack":1,"sale_price":200OUTPUT_ITEM_FIELDS}}]},"held_item":null,"loadable_inputs":[{"slot_index":0,"item_id":"262","qualified_item_id":"(O)262","stack":1,"quality":0,"sale_price":15,"probe_source":"Object.performObjectDropInAction(probe:true)"}]}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          }
        }
        """.Replace("OUTPUT_ITEM_FIELDS", outputItemFields));
    }

    private static SmallModelActionParameter Parameter(string name, string value)
    {
        return new SmallModelActionParameter { Name = name, Value = value };
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
