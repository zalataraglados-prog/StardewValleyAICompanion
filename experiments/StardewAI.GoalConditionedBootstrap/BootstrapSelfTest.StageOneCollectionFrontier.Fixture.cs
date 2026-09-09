namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static object RequirementSet(string id, params object[] groups) => new
    {
        requirement_set_id = id,
        required_group_count = groups.Length,
        route_covered_group_count = groups.Length,
        acquisition_routes_complete = true,
        groups
    };

    private static object LoweringSet(string id, params object[] groups) => new
    {
        requirement_set_id = id,
        required_group_count = groups.Length,
        runtime_admitted_group_count = groups.Length,
        teacher_admitted_group_count = groups.Length,
        groups
    };

    private static object StageOneRouteTimingCalibration() => new
    {
        schema_version = "stardewai.runtime_movement_timing_calibration.v1",
        status = "passed",
        game_version = "1.6.15",
        capture_total_days = 5,
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
    };

    private static void WriteStageOneCollectionSnapshot(
        string path,
        string stateHash,
        StageOneFishFixture[] fish)
    {
        var fishRows = fish.Select((value, index) => new
        {
            item_id = value.ItemId,
            qualified_item_id = value.QualifiedItemId,
            caught = index != 0
        }).ToArray();
        Write(path, new
        {
            schema_version = "snapshot.v1",
            game_version = "1.6.15",
            state_hash = stateHash,
            state = new
            {
                time = new
                {
                    total_days = Field(5),
                    time = Field(800)
                },
                player = new
                {
                    location_id = NativeField("Farm", "vanilla_1_6"),
                    tile_x = NativeField(1, "vanilla_1_6"),
                    tile_y = NativeField(5, "vanilla_1_6"),
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
                    collision_grid = NativeField(new
                    {
                        location_id = "Farm",
                        width = 5,
                        height = 10,
                        notable_tiles = Array.Empty<object>()
                    }, "vanilla_1_6_route"),
                    route_action_branch_coverage = NativeField(new
                    {
                        rows = Array.Empty<object>()
                    }, "vanilla_1_6_route"),
                    route_connectors = NativeField(new
                    {
                        location_id = "Farm",
                        connectors = new[]
                        {
                            new
                            {
                                kind = "building_door",
                                tile_x = 2,
                                tile_y = 4,
                                target_location = "Town",
                                target_x = 1,
                                target_y = 5,
                                resolved = true
                            }
                        }
                    }, "vanilla_1_6_route"),
                    route_graph = Field(new
                    {
                        edges = new object[]
                        {
                            Edge("Farm", 2, 4, "Town", 1, 5),
                            Edge("Town", 3, 4, "Beach", 1, 5)
                        }
                    }),
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
                    full_shipment_progress = new
                    {
                        status = "available",
                        value = new
                        {
                            eligible_item_count = 1,
                            shipped_eligible_item_count = 0,
                            missing_item_count = 1,
                            completion_ratio = 0d,
                            complete = false,
                            items = new[]
                            {
                                ShipmentItem("24", "(O)24", 0, false)
                            },
                            missing_item_ids = new[] { "24" }
                        }
                    },
                    fish_collection_progress = Field(new
                    {
                        eligible_species_count = 72,
                        caught_eligible_species_count = 71,
                        missing_species_count = 1,
                        items = fishRows,
                        missing_item_ids = new[] { "145" }
                    }),
                    museum = new
                    {
                        status = "available",
                        value = new
                        {
                            donated_count = 0,
                            total_donatable_items = 1,
                            missing_item_count = 1,
                            donatable_items = new[]
                            {
                                new
                                {
                                    item_id = "96",
                                    qualified_item_id = "(O)96",
                                    display_name = "Dwarf Scroll I",
                                    object_type = "Arch",
                                    donated = false
                                }
                            },
                            missing_item_ids = new[] { "96" },
                            collection_complete = false
                        }
                    },
                    community_center = new
                    {
                        status = "available",
                        value = new
                        {
                            route_state = "undecided",
                            bundle_data_row_count = 1,
                            projected_bundle_row_count = 1,
                            unavailable_bundle_row_count = 0,
                            complete_bundle_count = 0,
                            bundle_rows = new[]
                            {
                                CollectionBundleRow(
                                    "Pantry/5",
                                    5,
                                    1,
                                    CollectionIngredient(0, "24", 1, 0, false))
                            }
                        }
                    }
                }
            }
        });
    }

    private static object NativeField(object value, string adapter) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "self-test" },
        adapter,
        read_at_tick = 1,
        confidence = 1
    };

    private sealed record StageOneFishFixture(
        string ItemId,
        string QualifiedItemId,
        string DisplayName);
}
