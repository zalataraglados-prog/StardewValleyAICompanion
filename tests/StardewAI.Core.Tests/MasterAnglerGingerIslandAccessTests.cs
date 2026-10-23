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
                "master_angler_ginger_island_no_exact_current_walnut_producer:first_parrot",
                candidate.BlockReasons);
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
