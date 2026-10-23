using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class MasterAnglerMainlineTests
{
    [Fact]
    public void GingerIslandBatteryDeficitReusesReadyMachineOutputCandidate()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_battery_packs");
            access["repairs"]!["battery"]!["available_count"] = 4;
            var snapshot = GingerIslandSnapshot(fixture.Before, "Farm", 63, 15, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                var farm = state["farm"] as JsonObject ?? new JsonObject();
                state["farm"] = farm;
                farm["machines"] = Field(new JsonArray
                {
                    new JsonObject
                    {
                        ["location_id"] = "Farm",
                        ["location_kind"] = "farm_outdoor",
                        ["tile_x"] = 64,
                        ["tile_y"] = 15,
                        ["qualified_item_id"] = "(BC)231",
                        ["display_name"] = "Solar Panel",
                        ["ready_for_harvest"] = true,
                        ["minutes_until_ready"] = 0,
                        ["machine_has_input"] = false,
                        ["machine_is_incubator"] = false,
                        ["harvest_experience_raw"] = string.Empty,
                        ["harvest_experience_entries"] = new JsonArray(),
                        ["harvest_experience_deltas"] = new JsonArray(),
                        ["harvest_experience_deltas_json"] = "[]",
                        ["harvest_mastery_experience_delta"] = 0,
                        ["harvest_experience_projection_status"] = "exact_no_configured_experience",
                        ["held_item"] = new JsonObject
                        {
                            ["item_id"] = "787",
                            ["qualified_item_id"] = "(O)787",
                            ["stack"] = 1,
                            ["quality"] = 0,
                            ["sale_price"] = 62,
                            ["maximum_stack_size"] = 999
                        },
                        ["loadable_inputs"] = new JsonArray()
                    }
                });
            });

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Equal("collect_machine_output_tile", candidate.Kind);
            Assert.Equal("(O)787", candidate.QualifiedItemId);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_supply_deficit_count" && row.Value == "1");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "continuation.option_id" && row.Value == "fishing.catch_fish");

        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandTicketMoneyUsesNativeSettlementAfterEnoughValueIsShipped()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_ticket_money");
            access["ticket"]!["money_available"] = 100;
            var snapshot = GingerIslandSnapshot(fixture.Before, "FarmHouse", 10, 10, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                var farm = state["farm"] as JsonObject ?? new JsonObject();
                state["farm"] = farm;
                farm["shipping_bins"] = Field(new JsonArray
                {
                    new JsonObject
                    {
                        ["completed"] = true,
                        ["pending_settlement_value"] = 900,
                        ["pending_settlement_value_complete"] = true,
                        ["pending_settlement_timing"] = "next_native_day_transition",
                        ["contents"] = new JsonArray()
                    }
                });
                state["current_location"]!["home_context"] = Field(new JsonObject
                {
                    ["home_location_id"] = "FarmHouse",
                    ["current_location_is_home"] = true,
                    ["bed_tile_x"] = 11,
                    ["bed_tile_y"] = 10,
                    ["bed_tile_has_bed"] = true
                });
                state["menus"]!["sleep_prompt_context"] = Field(new JsonObject
                {
                    ["prompt_open"] = false,
                    ["active_menu_open"] = false,
                    ["active_menu_type"] = "none",
                    ["last_question_key"] = null,
                    ["can_confirm_sleep"] = false,
                    ["confirm_executor_enabled"] = false,
                    ["confirm_action_key"] = "Sleep_Yes"
                });
                state["locations"]!["route_action_branch_coverage"] = Field(new JsonObject
                {
                    ["rows"] = new JsonArray()
                });
            });

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Contains(candidate.Kind, new[]
            {
                "recovery_resume_sleep_prompt",
                "recovery_sleep_immediately",
                "recovery_return_home",
                "recovery_sleep_before_collapse"
            });
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_supply_stage" && row.Value == "ticket-money-settlement");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_pending_shipping_settlement" && row.Value == "900");
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandWalnutDeficitReusesExactNativeBushProducer()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_western_turtle_walnuts");
            access["walnuts"]!["current"] = 0;
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandWest", 10, 10, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                state["current_location"]!["debris"] = Field(new JsonArray());
                state["current_location"]!["large_terrain_features"] = Field(new JsonArray
                {
                    new JsonObject
                    {
                        ["tile_x"] = 12,
                        ["tile_y"] = 10,
                        ["runtime_type"] = "StardewValley.TerrainFeatures.Bush",
                        ["bounding_tile_width"] = 2,
                        ["bounding_tile_height"] = 1,
                        ["is_bush"] = true,
                        ["bush_size"] = 4,
                        ["bush_kind"] = "golden_walnut",
                        ["ready_for_harvest"] = true,
                        ["in_bloom"] = true,
                        ["tile_sheet_offset_before"] = 1,
                        ["tile_sheet_offset_expected_after"] = 0,
                        ["bush_harvest_status"] = "ready",
                        ["bush_projection_status"] = "exact_from_native_bush_shake",
                        ["bush_output_qualified_item_id"] = "(O)73",
                        ["bush_authoritative_route_sources"] = new JsonArray(),
                        ["bush_output_quantity_min"] = 1,
                        ["bush_output_quantity_max"] = 1,
                        ["bush_output_quality"] = 0,
                        ["bush_foraging_experience_on_success_min"] = 0,
                        ["bush_foraging_experience_on_success_max"] = 0,
                        ["bush_nut_key"] = "IslandWestBushTest",
                        ["bush_nut_collected_before"] = false,
                        ["bush_nut_collected_expected_after"] = true
                    }
                });
            });

            var bushCandidate = Assert.Single(Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    snapshot,
                    new[] { "foraging.harvest_bushes" }).Options).EventCandidates);
            Assert.True(bushCandidate.Available, GingerIslandCandidateDiagnostic(bushCandidate));
            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Equal("harvest_bush", candidate.Kind);
            Assert.Equal("(O)73", candidate.QualifiedItemId);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_supply_stage" &&
                row.Value == "walnuts-western_turtle_parrot");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_supply_deficit_count" && row.Value == "10");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_supply_projected_quantity" && row.Value == "1");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "continuation.option_id" && row.Value == "fishing.catch_fish");

            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = fixture.Parameters
                    }
                });
            var plan = new DailyPlanCompiler().Compile(
                new EventCandidateRanker().Rank(
                    new StardewAI.Contracts.Training.BaselineTrainingReport(),
                    availability,
                    "goal.fishing.complete_master_angler"),
                snapshot.StateHash);
            Assert.Equal("harvest_bush", Assert.Single(plan.Steps).Kind);
            var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
            Assert.Equal("executor.harvest_bush", queueItem.OptionId);
            Assert.Empty(queueItem.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandWalnutDeficitReusesNativeMusselStoneAttemptAndFreshReceipt()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_western_turtle_walnuts");
            access["walnuts"]!["current"] = 0;
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandWest", 10, 10, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                state["current_location"]!["terrain_features"] = Field(new JsonArray());
                state["current_location"]!["objects"] = Field(new JsonArray
                {
                    new JsonObject
                    {
                        ["tile_x"] = 11,
                        ["tile_y"] = 10,
                        ["qualified_item_id"] = "(O)25",
                        ["name"] = "Stone",
                        ["clear_kind"] = "mussel_stone",
                        ["clear_obstacle_executor_status"] = "ready",
                        ["required_tool_kind"] = "pickaxe",
                        ["tool_slot_index"] = 0,
                        ["expected_tool_hits_to_clear"] = 8,
                        ["harvest_experience_skill_id"] = "mining",
                        ["harvest_experience_on_success_min"] = 5,
                        ["harvest_experience_on_success_max"] = 5,
                        ["harvest_experience_condition"] = "native_pickaxe_breaks_island_mussel_stone",
                        ["harvest_experience_projection_status"] = "exact",
                        ["clear_output_projection_status"] = "native_stochastic_receipt",
                        ["clear_output_items_json"] = string.Empty,
                        ["clear_output_qualified_item_id"] = string.Empty,
                        ["clear_bonus_output_qualified_item_id"] = string.Empty
                    }
                });
                state["world_progress"]!["golden_walnut_sources"] = Field(new JsonObject
                {
                    ["schema_version"] = "golden_walnut_sources.v1",
                    ["projection_status"] = "complete_locked_base_1.6.15_island_hut_denominator",
                    ["native_source_key_count"] = 69,
                    ["native_walnut_total"] = 130,
                    ["sources"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["source_key"] = "MusselStone",
                            ["tracking_kind"] = "limited_drop",
                            ["location_id"] = "IslandWest",
                            ["native_quantity"] = 5,
                            ["remaining_quantity"] = 5,
                            ["acquisition_option_id"] = "executor.clear_obstacle",
                            ["action_binding_status"] = "existing_action_family_bound",
                            ["required_mail_satisfied"] = true,
                            ["pre_resort_eligible"] = true,
                            ["spend_safe_before_resort"] = true,
                            ["probability_status"] = "bounded_native_rng_0.10_per_mussel_node"
                        }
                    }
                });
            });

            var clearOption = Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    snapshot,
                    new[] { "executor.clear_obstacle" },
                    includeExecutorCalibrationOptions: true).Options);
            var clearCandidate = clearOption.EventCandidates.SingleOrDefault(row =>
                row.Parameters.Any(parameter =>
                    parameter.Name == "clear_source_id" && parameter.Value == "(O)25"));
            Assert.True(clearCandidate is not null,
                "missing=" + string.Join(",", clearOption.MissingStateFactors) +
                ";blocks=" + string.Join(",", clearOption.BlockingReasons) +
                ";objects=" + JsonSerializer.SerializeToNode(snapshot.State, JsonOptions)!["current_location"]!["objects"]!.ToJsonString());
            Assert.NotNull(clearCandidate);
            Assert.True(clearCandidate.Available, GingerIslandCandidateDiagnostic(clearCandidate));

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Equal("clear_obstacle_tile", candidate.Kind);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_key" && row.Value == "MusselStone");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_replan_policy" &&
                row.Value == "execute_one_native_attempt_then_fresh_limited_drop_counter");

            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = fixture.Parameters
                    }
                });
            var plan = new DailyPlanCompiler().Compile(
                new EventCandidateRanker().Rank(
                    new StardewAI.Contracts.Training.BaselineTrainingReport(),
                    availability,
                    "goal.fishing.complete_master_angler"),
                snapshot.StateHash);
            Assert.Equal("clear_obstacle", Assert.Single(plan.Steps).Kind);
            var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
            Assert.Equal("executor.clear_obstacle", queueItem.OptionId);
            Assert.Empty(queueItem.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandWalnutDeficitReusesNativeIslandCropHarvestAttempt()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_western_turtle_walnuts");
            access["walnuts"]!["current"] = 0;
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandWest", 10, 10, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                state["current_location"]!["crops"] = Field(new JsonArray
                {
                    new JsonObject
                    {
                        ["tile_x"] = 11,
                        ["tile_y"] = 10,
                        ["ready_for_harvest"] = true,
                        ["harvest_item_id"] = "24",
                        ["harvest_item_qualified_id"] = "(O)24",
                        ["harvest_source_seed_id"] = "472",
                        ["harvest_item_projection_status"] = "exact_from_native_crop_data",
                        ["harvest_item_category"] = -75,
                        ["harvest_context_tags"] = new JsonArray("category_vegetable"),
                        ["harvest_authoritative_route_sources"] = new JsonArray(),
                        ["harvest_min_stack"] = 1,
                        ["harvest_method"] = "Grab",
                        ["harvest_experience_skill_id"] = "farming",
                        ["harvest_experience_on_success_min"] = 8,
                        ["harvest_experience_on_success_max"] = 8,
                        ["harvest_experience_condition"] = "native_crop_harvest",
                        ["harvest_experience_projection_status"] = "exact"
                    }
                });
                state["world_progress"]!["golden_walnut_sources"] = Field(
                    GoldenWalnutSourceCatalog(
                        GoldenWalnutSource(
                            "IslandFarming",
                            "IslandWest",
                            5,
                            "farm.maintain_crops",
                            "bounded_native_rng_0.05_per_harvest")));
            });

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Equal("harvest_crop_tile", candidate.Kind);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_key" && row.Value == "IslandFarming");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_replan_policy" &&
                row.Value == "execute_one_native_attempt_then_fresh_limited_drop_counter");

            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = fixture.Parameters
                    }
                });
            var plan = new DailyPlanCompiler().Compile(
                new EventCandidateRanker().Rank(
                    new StardewAI.Contracts.Training.BaselineTrainingReport(),
                    availability,
                    "goal.fishing.complete_master_angler"),
                snapshot.StateHash);
            Assert.Equal("harvest_crop", Assert.Single(plan.Steps).Kind);
            var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
            Assert.Equal("executor.harvest_crop", queueItem.OptionId);
            Assert.Empty(queueItem.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandWalnutDeficitReusesNativeIslandFishingAttempt()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_western_turtle_walnuts");
            access["walnuts"]!["current"] = 0;
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandSouth", 1, 5, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                state["fishing"]!["location_context"]!["value"]!["location_id"] = "IslandSouth";
                state["world_progress"]!["golden_walnut_sources"] = Field(
                    GoldenWalnutSourceCatalog(
                        GoldenWalnutSource(
                            "IslandFishing",
                            "IslandSouth",
                            5,
                            "fishing.catch_fish",
                            "bounded_native_rng_0.15_per_catch")));
            });

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Equal("catch_fish", candidate.Kind);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_key" && row.Value == "IslandFishing");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_replan_policy" &&
                row.Value == "execute_one_native_attempt_then_fresh_limited_drop_counter");

            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = fixture.Parameters
                    }
                });
            var plan = new DailyPlanCompiler().Compile(
                new EventCandidateRanker().Rank(
                    new StardewAI.Contracts.Training.BaselineTrainingReport(),
                    availability,
                    "goal.fishing.complete_master_angler"),
                snapshot.StateHash);
            Assert.Equal("catch_fish", Assert.Single(plan.Steps).Kind);
            var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
            Assert.Equal("executor.catch_fish", queueItem.OptionId);
            Assert.Empty(queueItem.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Theory]
    [InlineData(
        "Buried_IslandWest_21_81",
        21,
        81,
        "tracker",
        "exact_native_one_time",
        "exact_current_world_buried_nut_point_present",
        "execute_once_then_fresh_collected_nut_tracker")]
    [InlineData(
        "Island_W_BuriedTreasureNut",
        18,
        42,
        "limited_drop",
        "exact_native_journal_tile",
        "exact_current_world_journal_buried_tile_present_and_note_seen",
        "execute_once_then_fresh_limited_drop_counter")]
    public void GingerIslandWalnutDeficitReusesNativeBuriedNutHoeAttemptAndFailsClosedOnDrift(
        string sourceKey,
        int targetX,
        int targetY,
        string trackingKind,
        string probabilityStatus,
        string runtimeValidationStatus,
        string replanPolicy)
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_western_turtle_walnuts");
            access["walnuts"]!["current"] = 0;
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandWest", targetX - 1, targetY, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                state["player"]!["inventory"]!["value"]!.AsArray().Add(new JsonObject
                {
                    ["slot_index"] = 1,
                    ["qualified_item_id"] = "(T)Hoe"
                });
                state["world_progress"]!["golden_walnut_sources"] = Field(
                    GoldenWalnutSourceCatalog(
                        GoldenWalnutSource(
                            sourceKey,
                            "IslandWest",
                            1,
                            "executor.till_soil",
                            probabilityStatus,
                            trackingKind: trackingKind,
                            tileX: targetX,
                            tileY: targetY,
                            runtimeSourceValidationStatus: runtimeValidationStatus)));
            });

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Equal("till_soil_tile", candidate.Kind);
            Assert.Equal(targetX, candidate.TileX);
            Assert.Equal(targetY, candidate.TileY);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_key" &&
                row.Value == sourceKey);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_replan_policy" &&
                row.Value == replanPolicy);

            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = fixture.Parameters
                    }
                });
            var plan = new DailyPlanCompiler().Compile(
                new EventCandidateRanker().Rank(
                    new StardewAI.Contracts.Training.BaselineTrainingReport(),
                    availability,
                    "goal.fishing.complete_master_angler"),
                snapshot.StateHash);
            Assert.Equal("till_soil", Assert.Single(plan.Steps).Kind);
            var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
            Assert.Equal("executor.till_soil", queueItem.OptionId);
            Assert.Empty(queueItem.BlockingReasons);

            var drifted = RewriteGingerIslandState(snapshot, state =>
                state["world_progress"]!["golden_walnut_sources"]!["value"]!["sources"]![0]!["runtime_source_validation_status"] =
                    "blocked_runtime_buried_nut_point_absent");
            var blocked = Assert.Single(new ActionQueueCompiler().Compile(plan, drifted).Items);
            Assert.Contains(
                "till_soil_buried_nut_source_projection_drifted",
                blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandWalnutDeficitFailsClosedWithoutExactCurrentProducer()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_first_parrot_walnut");
            access["walnuts"]!["current"] = 0;
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandHut", 7, 8, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                state["current_location"]!["large_terrain_features"] = Field(new JsonArray());
                state["current_location"]!["objects"] = Field(new JsonArray());
                state["world_progress"]!["island_field_office"] = Field(new JsonObject());
                state["player"]!["geode_processing"] = Field(new JsonObject());
            });

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.False(candidate.Available);
            Assert.Equal("ginger_island_access_wait", candidate.Kind);
            Assert.Contains(
                "master_angler_ginger_island_walnut_source_catalog_missing",
                candidate.BlockReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandWalnutDeficitRoutesToSpendSafeCataloguedBushThenReplans()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("requires_first_parrot_walnut");
            access["walnuts"]!["current"] = 0;
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandHut", 7, 8, access);
            snapshot = RewriteGingerIslandState(snapshot, state =>
            {
                state["current_location"]!["large_terrain_features"] = Field(new JsonArray());
                state["current_location"]!["objects"] = Field(new JsonArray());
                state["world_progress"]!["golden_walnut_sources"] = Field(new JsonObject
                {
                    ["schema_version"] = "golden_walnut_sources.v1",
                    ["projection_status"] = "complete_locked_base_1.6.15_island_hut_denominator",
                    ["native_source_key_count"] = 69,
                    ["native_walnut_total"] = 130,
                    ["sources"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["source_key"] = "Bush_IslandSouth_31_5",
                            ["tracking_kind"] = "tracker",
                            ["location_id"] = "IslandSouth",
                            ["tile_x"] = 31,
                            ["tile_y"] = 5,
                            ["native_quantity"] = 1,
                            ["remaining_quantity"] = 1,
                            ["acquisition_option_id"] = "foraging.harvest_bushes",
                            ["action_binding_status"] = "existing_action_family_bound",
                            ["required_mail_satisfied"] = true,
                            ["pre_resort_eligible"] = true,
                            ["spend_safe_before_resort"] = true,
                            ["probability_status"] = "exact_native_one_time"
                        },
                        new JsonObject
                        {
                            ["source_key"] = "Darts",
                            ["tracking_kind"] = "limited_drop",
                            ["location_id"] = "IslandSouthEastCave",
                            ["native_quantity"] = 3,
                            ["remaining_quantity"] = 3,
                            ["acquisition_option_id"] = "minigame.play_darts",
                            ["action_binding_status"] = "existing_action_family_bound",
                            ["required_mail_satisfied"] = false,
                            ["pre_resort_eligible"] = false,
                            ["spend_safe_before_resort"] = false,
                            ["probability_status"] = "bounded_native_three_reward_stages"
                        }
                    }
                });
                state["locations"]!["route_graph"] = Field(new JsonObject
                {
                    ["edges"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["kind"] = "building_door",
                            ["from_location"] = "IslandHut",
                            ["from_x"] = 8,
                            ["from_y"] = 8,
                            ["target_location"] = "IslandSouth",
                            ["target_x"] = 15,
                            ["target_y"] = 24,
                            ["resolved"] = true
                        },
                        new JsonObject
                        {
                            ["kind"] = "warp",
                            ["from_location"] = "IslandSouth",
                            ["from_x"] = 30,
                            ["from_y"] = 12,
                            ["target_location"] = "IslandSouthEastCave",
                            ["target_x"] = 1,
                            ["target_y"] = 1,
                            ["resolved"] = true
                        }
                    }
                });
                state["locations"]!["route_connectors"] = Field(new JsonObject
                {
                    ["location_id"] = "IslandHut",
                    ["connectors"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["tile_x"] = 8,
                            ["tile_y"] = 8,
                            ["kind"] = "building_door",
                            ["target_location"] = "IslandSouth",
                            ["target_x"] = 15,
                            ["target_y"] = 24,
                            ["resolved"] = true
                        }
                    }
                });
                state["locations"]!["route_action_branch_coverage"] = Field(new JsonObject
                {
                    ["rows"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["tile_x"] = 8,
                            ["tile_y"] = 8,
                            ["branch"] = "BuildingDoor",
                            ["route_training_blocked"] = false
                        }
                    }
                });
                state["locations"]!["social_route_date_evidence"] = Field(new JsonObject
                {
                    ["schema_version"] = "social_route_date_evidence.v2",
                    ["capture_total_days"] = 5,
                    ["all_location_static_walkability_complete"] = true,
                    ["projection_status"] = "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
                    ["location_count"] = 3,
                    ["locations"] = new JsonArray
                    {
                        RouteLocationEvidence("IslandHut", 120, 120),
                        RouteLocationEvidence("IslandSouth", 120, 120),
                        RouteLocationEvidence("IslandSouthEastCave", 120, 120)
                    }
                });
            });

            var projectedState = JsonSerializer.SerializeToNode(snapshot.State, JsonOptions)!.AsObject();
            Assert.Equal("IslandSouth",
                projectedState["locations"]!["route_connectors"]!["value"]!["connectors"]![0]!["target_location"]!.GetValue<string>());
            var routeOption = Assert.Single(
                new CandidateOptionAvailabilityEvaluator().Evaluate(
                    snapshot,
                    new[] { "exploration.visit_location" }).Options);
            Assert.True(routeOption.MissingStateFactors.Length == 0,
                "missing=" + string.Join(",", routeOption.MissingStateFactors) +
                ";blocking=" + string.Join(",", routeOption.BlockingReasons));
            var routeCandidates = routeOption.EventCandidates;
            Assert.Contains(routeCandidates, route =>
                route.Kind == "route_connector_tile" &&
                route.Available &&
                route.Parameters.Any(parameter =>
                    parameter.Name == "expected_target_location" &&
                    parameter.Value == "IslandSouth"));

            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Available, GingerIslandCandidateDiagnostic(candidate));
            Assert.Equal("route_connector_tile", candidate.Kind);
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_key" &&
                row.Value == "Bush_IslandSouth_31_5");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_replan_policy" &&
                row.Value == "route_one_connector_then_fresh_current_location_candidate");
            Assert.DoesNotContain(candidate.Parameters, row =>
                row.Name == "ginger_island_walnut_source_key" && row.Value == "Darts");

            var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = fixture.Parameters
                    }
                });
            var plan = new DailyPlanCompiler().Compile(
                new EventCandidateRanker().Rank(
                    new StardewAI.Contracts.Training.BaselineTrainingReport(),
                    availability,
                    "goal.fishing.complete_master_angler"),
                snapshot.StateHash);
            var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
            Assert.Equal("executor.traverse_connector", queueItem.OptionId);
            Assert.Empty(queueItem.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandBoatRepairCompilesOnlyFromExactNativeProjection()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var snapshot = GingerIslandSnapshot(
                fixture.Before,
                "BoatTunnel",
                3,
                9,
                GingerIslandAccess("ready_to_donate_batteries"));
            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Kind == "interact_endpoint",
                GingerIslandCandidateDiagnostic(candidate));
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "expected_action_type" && row.Value == "WillyBoatDonateBatteries");
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "required_executor_profile" && row.Value == "ginger_island_access_executor");

            var item = CompileGingerIslandInteraction(snapshot, fixture.Parameters);
            Assert.Empty(item.BlockingReasons);

            var drifted = RewriteGingerIslandAccess(snapshot, access =>
                access["repairs"]!["battery"]!["available_count"] = 4);
            var blocked = CompileGingerIslandInteraction(snapshot, fixture.Parameters, drifted);
            Assert.Contains("ginger_island_boat_repair_projection_drifted", blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void GingerIslandResortRequiresExactFiftyOneWalnutDependencyChain()
    {
        var fixture = CreateFixture("IslandSouthEastCave:5", targetLocation: "IslandSouthEastCave");
        try
        {
            var access = GingerIslandAccess("ready_to_restore_resort");
            var snapshot = GingerIslandSnapshot(fixture.Before, "IslandSouth", 16, 22, access);
            var candidate = EvaluateGingerIslandCandidate(snapshot, fixture.Parameters);
            Assert.True(candidate.Kind == "interact_endpoint",
                GingerIslandCandidateDiagnostic(candidate));
            Assert.Contains(candidate.Parameters, row =>
                row.Name == "expected_action_type" && row.Value == "IslandResort");
            Assert.Equal(1,
                access["first_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal(10,
                access["western_turtle_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal(20,
                access["island_house_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal(20,
                access["resort_parrot"]!["expected_required_nuts"]!.GetValue<int>());
            Assert.Equal("Island_UpgradeHouse",
                access["resort_parrot"]!["expected_required_mail"]!.GetValue<string>());

            var item = CompileGingerIslandInteraction(snapshot, fixture.Parameters);
            Assert.Empty(item.BlockingReasons);

            var drifted = RewriteGingerIslandAccess(snapshot, row =>
                row["resort_parrot"]!["required_mail"] = string.Empty);
            var blocked = CompileGingerIslandInteraction(snapshot, fixture.Parameters, drifted);
            Assert.Contains("ginger_island_parrot_projection_drifted", blocked.BlockingReasons);
        }
        finally
        {
            fixture.Dispose();
        }
    }

    [Fact]
    public void RuntimeGingerIslandInteractionsUseNativeActionsWithoutDirectProgressWrites()
    {
        var source = RuntimeHarnessSources.LoadFile("ModEntry.GingerIsland.cs");
        Assert.Contains("Game1.currentLocation.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("active.Location.answerDialogue(yes)", source, StringComparison.Ordinal);
        Assert.Contains("parrotUpgradePerches", source, StringComparison.Ordinal);
        Assert.DoesNotContain("GoldenWalnuts -=", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", source, StringComparison.Ordinal);
        Assert.DoesNotContain("mailForTomorrow.Add", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"farmhouseRestored\.Value\s*=(?!=)", source);
        Assert.DoesNotMatch(@"resortRestored\.Value\s*=(?!=)", source);
    }

    private static EventCandidate EvaluateGingerIslandCandidate(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) => Assert.Single(Assert.Single(
            new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[]
                {
                    new OptionAvailabilityCandidate
                    {
                        OptionId = "fishing.catch_fish",
                        Parameters = parameters
                    }
                }).Options).EventCandidates);

    private static string GingerIslandCandidateDiagnostic(EventCandidate candidate) =>
        $"kind={candidate.Kind};available={candidate.Available};blocks={string.Join(",", candidate.BlockReasons)};parameters=" +
        string.Join(",", candidate.Parameters.Select(row => row.Name + "=" + row.Value));

    private static StardewAI.Contracts.Execution.ActionQueueItem CompileGingerIslandInteraction(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters,
        SnapshotEnvelope? compileSnapshot = null)
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = parameters
                }
            });
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(
                new StardewAI.Contracts.Training.BaselineTrainingReport(),
                availability,
                "goal.fishing.complete_master_angler"),
            snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(plan, compileSnapshot ?? snapshot);
        return Assert.Single(queue.Items.Where(item => item.OptionId == "executor.interact"));
    }

    private static SnapshotEnvelope GingerIslandSnapshot(
        SnapshotEnvelope source,
        string locationId,
        int playerX,
        int playerY,
        JsonObject access)
    {
        var state = JsonSerializer.SerializeToNode(source.State, JsonOptions)!.AsObject();
        state["player"]!["location_id"]!["value"] = locationId;
        state["player"]!["tile_x"]!["value"] = playerX;
        state["player"]!["tile_y"]!["value"] = playerY;
        state["current_location"]!["map"]!["value"] = new JsonObject
        {
            ["location_id"] = locationId,
            ["width"] = 120,
            ["height"] = 120
        };
        state["locations"]!["collision_grid"]!["value"] = new JsonObject
        {
            ["location_id"] = locationId,
            ["width"] = 120,
            ["height"] = 120,
            ["notable_tiles"] = new JsonArray()
        };
        const string targetLocation = "IslandSouthEastCave";
        state["locations"]!["route_graph"] = Field(new JsonObject
        {
            ["edges"] = new JsonArray
            {
                new JsonObject
                {
                    ["kind"] = "warp",
                    ["from_location"] = locationId,
                    ["from_x"] = playerX + 1,
                    ["from_y"] = playerY,
                    ["target_location"] = targetLocation,
                    ["target_x"] = 1,
                    ["target_y"] = 1,
                    ["resolved"] = true
                }
            }
        });
        state["locations"]!["social_route_date_evidence"] = Field(new JsonObject
        {
            ["schema_version"] = "social_route_date_evidence.v2",
            ["capture_total_days"] = 5,
            ["all_location_static_walkability_complete"] = true,
            ["projection_status"] = "current_date_static_route_and_gate_evidence_complete_movement_timing_pending",
            ["location_count"] = 2,
            ["locations"] = new JsonArray
            {
                RouteLocationEvidence(locationId, 120, 120),
                RouteLocationEvidence(targetLocation, 120, 120)
            }
        });
        state["locations"]!["ginger_island_access"] = Field(access);
        return SnapshotFromState(source, state);
    }

    private static SnapshotEnvelope RewriteGingerIslandAccess(
        SnapshotEnvelope source,
        Action<JsonObject> rewrite)
    {
        var state = JsonSerializer.SerializeToNode(source.State, JsonOptions)!.AsObject();
        var access = state["locations"]!["ginger_island_access"]!["value"]!.AsObject();
        rewrite(access);
        return SnapshotFromState(source, state);
    }

    private static SnapshotEnvelope RewriteGingerIslandState(
        SnapshotEnvelope source,
        Action<JsonObject> rewrite)
    {
        var state = JsonSerializer.SerializeToNode(source.State, JsonOptions)!.AsObject();
        rewrite(state);
        return SnapshotFromState(source, state);
    }

    private static JsonObject GingerIslandAccess(string status)
    {
        var readyResort = status == "ready_to_restore_resort";
        return new JsonObject
        {
            ["schema_version"] = "ginger_island_access.v1",
            ["projection_status"] = "complete_locked_base_1.6.15_native_contract",
            ["status"] = status,
            ["target_access"] = new JsonObject
            {
                ["island_south_accessible"] = true,
                ["island_north_accessible"] = true,
                ["island_west_accessible"] = true,
                ["island_south_east_accessible"] = false,
                ["island_south_east_cave_accessible"] = false
            },
            ["repairs"] = new JsonObject
            {
                ["battery"] = Repair("(O)787", 5, 5, 4, 9, "WillyBoatDonateBatteries"),
                ["hull"] = Repair("(O)709", 200, 200, 6, 8, "WillyBoatDonateHardwood"),
                ["anchor"] = Repair("(O)337", 5, 5, 8, 10, "WillyBoatDonateIridium"),
                ["all_complete_or_pending"] = false,
                ["boat_fixed_received"] = false,
                ["boat_fixed_pending"] = false
            },
            ["ticket"] = new JsonObject
            {
                ["raw_action"] = "BoatTicket",
                ["tile_x"] = 4,
                ["tile_y"] = 9,
                ["exact_action_count"] = 1,
                ["identity_exact"] = true,
                ["price"] = 1000,
                ["money_available"] = 1000
            },
            ["walnuts"] = new JsonObject { ["current"] = 20, ["found"] = 51 },
            ["first_parrot"] = Perch("IslandHut", 7, 6, "Hut", 1, string.Empty, true),
            ["western_turtle_parrot"] = Perch("IslandSouth", 5, 9, "Turtle", 10, "Island_FirstParrot", true),
            ["island_house_parrot"] = Perch("IslandWest", 81, 40, "House", 20, string.Empty, true),
            ["resort_parrot"] = Perch("IslandSouth", 17, 22, "Resort", 20, "Island_UpgradeHouse", !readyResort)
        };
    }

    private static JsonObject GoldenWalnutSourceCatalog(params JsonObject[] sources) => new()
    {
        ["schema_version"] = "golden_walnut_sources.v1",
        ["projection_status"] = "complete_locked_base_1.6.15_island_hut_denominator",
        ["native_source_key_count"] = 69,
        ["native_walnut_total"] = 130,
        ["sources"] = new JsonArray(sources)
    };

    private static JsonObject GoldenWalnutSource(
        string sourceKey,
        string locationId,
        int nativeQuantity,
        string acquisitionOptionId,
        string probabilityStatus,
        string trackingKind = "limited_drop",
        int? tileX = null,
        int? tileY = null,
        string runtimeSourceValidationStatus = "not_required_for_action_family") => new()
    {
        ["source_key"] = sourceKey,
        ["tracking_kind"] = trackingKind,
        ["location_id"] = locationId,
        ["tile_x"] = tileX,
        ["tile_y"] = tileY,
        ["native_quantity"] = nativeQuantity,
        ["remaining_quantity"] = nativeQuantity,
        ["acquisition_option_id"] = acquisitionOptionId,
        ["action_binding_status"] = "existing_action_family_bound",
        ["required_mail_satisfied"] = true,
        ["pre_resort_eligible"] = true,
        ["spend_safe_before_resort"] = true,
        ["probability_status"] = probabilityStatus,
        ["runtime_source_validation_status"] = runtimeSourceValidationStatus,
        ["required_secret_note_satisfied"] = true
    };

    private static JsonObject Repair(
        string qualifiedItemId,
        int required,
        int available,
        int x,
        int y,
        string actionType) => new()
        {
            ["qualified_item_id"] = qualifiedItemId,
            ["required_count"] = required,
            ["available_count"] = available,
            ["tile_x"] = x,
            ["tile_y"] = y,
            ["complete_or_pending"] = false,
            ["expected_action_type"] = actionType
        };

    private static JsonObject Perch(
        string location,
        int x,
        int y,
        string name,
        int nuts,
        string requiredMail,
        bool complete) => new()
        {
            ["source_location"] = location,
            ["tile_x"] = x,
            ["tile_y"] = y,
            ["upgrade_name"] = name,
            ["required_nuts"] = nuts,
            ["expected_required_nuts"] = nuts,
            ["required_mail"] = requiredMail,
            ["expected_required_mail"] = requiredMail,
            ["available"] = !complete,
            ["current_state"] = complete ? "Complete" : "Idle",
            ["complete_or_pending"] = complete,
            ["identity_exact"] = true
        };
}
