using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMasterAnglerChanceModifierParsing()
    {
        using var document = JsonDocument.Parse("""
            {
              "ChanceModifiers": [
                {
                  "Id": "fixture",
                  "Condition": "PLAYER_HAS_MAIL Current fixture",
                  "Modification": 2,
                  "Amount": 1.5,
                  "RandomAmount": [0.25, 0.75]
                }
              ]
            }
            """);
        var modifiers = MasterAnglerOpportunityCatalogBuilder.ParseChanceModifiers(
            document.RootElement);
        Require(
            modifiers.Length == 1 &&
            modifiers[0].Id == "fixture" &&
            modifiers[0].Condition == "PLAYER_HAS_MAIL Current fixture" &&
            modifiers[0].Modification == 2 &&
            Math.Abs(modifiers[0].Amount - 1.5) < 0.000001 &&
            modifiers[0].RandomAmount.SequenceEqual(new[] { 0.25, 0.75 }),
            "Master Angler chance modifier inputs were not preserved losslessly.");
    }

    private static void VerifyMasterAnglerFullRouteIntent(string outputRoot)
    {
        var root = Path.Combine(outputRoot, "master-angler-route-fixture");
        Directory.CreateDirectory(root);
        var windowsPath = Path.Combine(root, "windows.json");
        var snapshotPath = Path.Combine(root, "snapshot.json");
        var calibrationPath = Path.Combine(root, "route-timing.json");

        var species = Enumerable.Range(0, 72)
            .Select(index => new
            {
                qualified_item_id = index == 0
                    ? "(O)145"
                    : "(O)test_" + index,
                windows = index == 0
                    ? new object[]
                    {
                        new
                        {
                            source_kind = "location_rule",
                            source_key = "Beach:0",
                            location_id = "Beach",
                            first_total_day = 0,
                            last_total_day = 27,
                            minimum_fishing_level = 0,
                            require_magic_bait = false,
                            training_rod_allowed = true,
                            dynamic_conditions = Array.Empty<string>(),
                            time_windows = new[]
                            {
                                new { start_time = 600, end_time = 2600 }
                            }
                        }
                    }
                    : Array.Empty<object>()
            })
            .ToArray();
        Write(windowsPath, new
        {
            schema_version = "master_angler_stage_one_window_index.v1",
            status = "complete_static_windows_dynamic_execution_pending",
            game_version = "1.6.15",
            static_window_coverage_complete = true,
            training_label_eligible = false,
            native_denominator_count = 72,
            deadline_total_day_exclusive = 224,
            species
        });
        Write(calibrationPath, new
        {
            schema_version = "stardewai.runtime_movement_timing_calibration.v1",
            status = "passed",
            game_version = "1.6.15",
            capture_total_days = 223,
            sample_count = 3,
            total_manhattan_tiles = 66,
            maximum_observed_game_minutes_per_tile = 0.29,
            native_milliseconds_per_game_minute = 700,
            conservative_game_minute_numerator_per_tile = 1,
            conservative_game_minute_denominator_per_tile = 1,
            connector_sample_count = 2,
            connector_kinds = new[] { "building_door", "warp" },
            maximum_observed_connector_game_minutes = 1.04,
            conservative_connector_transition_game_minutes = 2,
            calibration_evidence_kind = "conservative_upper_bound",
            connector_transition_game_minutes_status =
                "runtime_proven_building_door_and_step_warp",
            movement_context_projection_status =
                "exact_current_player_native_cardinal_movement_context",
            movement_context_scope =
                "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay",
            source_save_fingerprint_before = "ABC",
            source_save_fingerprint_after = "ABC",
            source_save_untouched = true
        });

        var fishRows = Enumerable.Range(0, 72)
            .Select(index => new
            {
                item_id = index == 0 ? "145" : "test_" + index,
                qualified_item_id = index == 0
                    ? "(O)145"
                    : "(O)test_" + index,
                caught = index != 0
            })
            .ToArray();
        var routeEdges = new object[]
        {
            Edge("Farm", 2, 4, "Town", 1, 5),
            Edge("Town", 3, 4, "Beach", 1, 5)
        };
        Write(snapshotPath, new
        {
            schema_version = "snapshot.v1",
            game_version = "1.6.15",
            state_hash = "master-angler-full-route-self-test",
            state = new
            {
                time = new
                {
                    total_days = Field(5),
                    time = Field(800)
                },
                player = new
                {
                    location_id = Field("Farm"),
                    tile_x = Field(1),
                    tile_y = Field(5),
                    skills_detail = Field(new
                    {
                        skills = new[]
                        {
                            new { skill_id = "fishing", effective_level = 0 }
                        }
                    }),
                    movement_timing_context = Field(new
                    {
                        projection_status =
                            "exact_current_player_native_cardinal_movement_context",
                        runtime_calibration_compatible = true,
                        route_timing_ready_now = true,
                        real_milliseconds_per_game_minute = 700,
                        theoretical_upper_bound_game_minutes_per_tile = 0.7,
                        scope =
                            "ordinary_on_foot_cardinal_input_without_collision_dialogue_or_clearance_delay"
                    })
                },
                locations = new
                {
                    route_graph = Field(new { edges = routeEdges }),
                    social_route_date_evidence = Field(new
                    {
                        schema_version = "social_route_date_evidence.v2",
                        capture_total_days = 5,
                        all_location_static_walkability_complete = true,
                        projection_status =
                            "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
                        location_count = 3,
                        locations = new[]
                        {
                            Location("Farm"),
                            Location("Town"),
                            Location("Beach")
                        }
                    })
                },
                fishing = new
                {
                    rod_contexts = Field(new[]
                    {
                        new
                        {
                            has_magic_bait = false,
                            uses_training_rod = false
                        }
                    })
                },
                current_location = new
                {
                    objects = Field(Array.Empty<object>())
                },
                world_progress = new
                {
                    fish_collection_progress = Field(new
                    {
                        eligible_species_count = 72,
                        caught_eligible_species_count = 71,
                        missing_species_count = 1,
                        items = fishRows
                    })
                }
            }
        });

        var result = MasterAnglerTargetDateIntentBuilder.Build(
            windowsPath,
            snapshotPath,
            calibrationPath);
        var candidate = result.Candidates.Single();
        Require(
            result.Status ==
                "validated_current_date_full_route_intents_runtime_terminal_pending" &&
            !result.TrainingLabelEligible &&
            candidate.RouteEdgeCount == 2 &&
            candidate.RouteGuaranteedArrivalTime == 807 &&
            candidate.Parameters.Any(parameter =>
                parameter.Name ==
                    MasterAnglerRouteTimingValidator.CalibrationSha256Parameter &&
                parameter.Value == result.RouteTimingCalibrationSha256),
            "Master Angler teacher intent did not retain its complete route timing proof.");

        var constrainedWindows = JsonNode.Parse(
            File.ReadAllText(windowsPath))!;
        constrainedWindows["species"]![0]!["windows"]![0]!
            ["time_windows"]![0]!["end_time"] = 820;
        Write(windowsPath, constrainedWindows);
        var noRouteSlack = MasterAnglerTargetDateIntentBuilder.Build(
            windowsPath,
            snapshotPath,
            calibrationPath);
        Require(
            noRouteSlack.CandidateCount == 0,
            "Master Angler teacher admitted a window without post-arrival catch reserve.");
    }

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "test", path = "self-test" },
        adapter = "self-test",
        read_at_tick = 1,
        confidence = 1
    };

    private static object Edge(
        string from,
        int fromX,
        int fromY,
        string target,
        int targetX,
        int targetY,
        string kind = "building_door") => new
    {
        kind,
        from_location = from,
        from_x = fromX,
        from_y = fromY,
        target_location = target,
        target_x = targetX,
        target_y = targetY,
        resolved = true
    };

    private static object Location(
        string id,
        bool seedsIgnoreSeasonsHere = false,
        object[]? actionGates = null,
        int openPreparedSoilSlots = 0,
        int occupiedPreparedSoilSlots = 0,
        object[]? occupiedHarvestItems = null) => new
    {
        location_id = id,
        location_context_id = "Default",
        seeds_ignore_seasons_here = seedsIgnoreSeasonsHere,
        map_width = 5,
        map_height = 10,
        projection_status = "exact_current_date_static_native_walkability",
        static_walkable_tile_count = 50,
        static_walkable_tile_ranges = Enumerable.Range(0, 10)
            .Select(y => new { y, start_x = 0, end_x = 4 })
            .ToArray(),
        build_conditions = (string?)null,
        build_conditions_met = (bool?)null,
        unsupported_route_action_record_count = 0,
        unsupported_route_action_tile_count = 0,
        unsupported_route_action_tiles = Array.Empty<object>(),
        cultivation_capacity = new
        {
            schema_version = "prepared_cultivation_capacity.v1",
            projection_status =
                "exact_current_snapshot_prepared_soil_slots",
            total_prepared_soil_slot_count =
                openPreparedSoilSlots + occupiedPreparedSoilSlots,
            open_prepared_soil_slot_count = openPreparedSoilSlots,
            occupied_crop_slot_count = occupiedPreparedSoilSlots,
            unresolved_harvest_item_slot_count = 0,
            garden_pot_slot_count = 0,
            occupied_harvest_items =
                occupiedHarvestItems ?? Array.Empty<object>()
        },
        action_gates = actionGates ?? Array.Empty<object>()
    };
}
