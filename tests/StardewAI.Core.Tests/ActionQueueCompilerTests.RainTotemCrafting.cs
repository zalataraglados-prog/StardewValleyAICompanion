using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CraftRainTotemCompilesThroughExactSupportCraftingContract()
    {
        using var fixture = RainTotemCraftingFixture.Create();
        var queue = new ActionQueueCompiler().Compile(
            RainTotemCraftingPlan(fixture),
            fixture.Snapshot);

        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.craft_rain_totem", item.OptionId);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("craft_rain_totem", step.StepType);
        Assert.Equal("recipe:Rain Totem:(O)681", step.Target);
        Assert.Contains(
            "master_angler_weather_supply_replan_required=true",
            step.ExpectedEffect,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CraftRainTotemRejectsNonTomorrowSupplyIntent()
    {
        using var fixture = RainTotemCraftingFixture.Create();
        var plan = RainTotemCraftingPlan(fixture);
        var targetDay = Assert.Single(plan.Steps[0].Parameters.Where(row =>
            row.Name == "weather_supply_target_total_day"));
        targetDay.Value = "102";

        var item = Assert.Single(
            new ActionQueueCompiler().Compile(plan, fixture.Snapshot).Items);

        Assert.Contains(
            "craft_rain_totem_target_day_not_tomorrow",
            item.BlockingReasons);
    }

    [Fact]
    public void AuthoritativeTomorrowRainIntentExpandsThroughHighLevelFishingOption()
    {
        using var fixture = RainTotemCraftingFixture.Create();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = fixture.SupplyParameters
                }
            });

        var option = Assert.Single(availability.Options);
        Assert.True(
            option.EventCandidates.Length > 0,
            JsonSerializer.Serialize(option));
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("craft_rain_totem", candidate.Kind);
        Assert.Equal("(O)681", candidate.QualifiedItemId);

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.fishing.complete_master_angler");
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            fixture.Snapshot.StateHash);
        var item = Assert.Single(
            new ActionQueueCompiler().Compile(plan, fixture.Snapshot).Items);

        Assert.Equal("executor.craft_rain_totem", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void UnlearnedRainTotemRecipeReusesExactWildTreeForagingXpStep()
    {
        using var fixture = RainTotemCraftingFixture.Create(
            recipeLearned: false,
            foragingLevel: 8,
            readyWildTree: true);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = fixture.SupplyParameters
            }]);

        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("clear_obstacle_tile", candidate.Kind);
        Assert.StartsWith("rain-totem-foraging-xp:", candidate.CandidateId);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "rain_totem_foraging_current_unmodified_level" &&
            parameter.Value == "8");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "rain_totem_foraging_required_level" &&
            parameter.Value == "9");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.fishing.complete_master_angler");
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            fixture.Snapshot.StateHash);
        var queue = new ActionQueueCompiler().Compile(
            plan,
            fixture.Snapshot);
        var item = Assert.Single(
            queue.Items.Where(value =>
                value.OptionId == "executor.clear_obstacle"));

        Assert.Equal("executor.clear_obstacle", item.OptionId);
        Assert.Empty(item.BlockingReasons);
    }

    [Fact]
    public void RainTotemCandidateRejectsMaterialsReservedForAnotherGoal()
    {
        using var fixture = RainTotemCraftingFixture.Create(
            exactMaterialTopology: true);
        var ledger = RainTotemMaterialLedger(
            "goal.other",
            "decision:other",
            "consume elsewhere");
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = fixture.SupplyParameters
            }],
            commitmentLedger: ledger);

        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains(
            "machine_recipe_material_reserved_for_other_goal:player:123#0",
            candidate.BlockReasons);
    }

    [Fact]
    public void RainTotemCandidateCanConsumeItsOwnExactMaterialReservation()
    {
        using var fixture = RainTotemCraftingFixture.Create(
            exactMaterialTopology: true);
        var authorization = RainTotemMaterialReservationContract.Authorization(
            101,
            "Default");
        var ledger = RainTotemMaterialLedger(
            authorization.GoalId,
            authorization.SourceDecisionId,
            authorization.Purpose);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = fixture.SupplyParameters
            }],
            commitmentLedger: ledger);

        var candidate = Assert.Single(
            Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "material_reservation_ids_json" &&
            parameter.Value == "[\"rain-material\"]");
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "commitment_ledger_revision" &&
            parameter.Value == "4");
    }

    [Fact]
    public void MissingPineTarUsesReadyNativeMachineOutputBeforeCrafting()
    {
        using var fixture = RainTotemCraftingFixture.Create(
            pineTarAvailable: 4,
            readyPineTarMachine: true);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = fixture.SupplyParameters
                }
            });

        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("collect_machine_output_tile", candidate.Kind);
        Assert.Equal("(O)726", candidate.QualifiedItemId);
        Assert.StartsWith("rain-totem-material:(O)726:", candidate.CandidateId);
        Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "rain_totem_material_producer_stage" &&
            parameter.Value == "collect_ready_native_machine_output");

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.fishing.complete_master_angler");
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            fixture.Snapshot.StateHash);
        var item = Assert.Single(
            new ActionQueueCompiler().Compile(plan, fixture.Snapshot).Items);

        Assert.Equal("executor.collect_machine_output", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("collect_machine_output", Assert.Single(
            item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MissingHardwoodUsesExactFarmResourceClumpProducer()
    {
        using var fixture = RainTotemCraftingFixture.Create(
            pineTarAvailable: 5,
            hardwoodAvailable: 0,
            readyHardwoodClump: true);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            [new OptionAvailabilityCandidate
            {
                OptionId = "fishing.catch_fish",
                Parameters = fixture.SupplyParameters
            }]);

        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("clear_farm_resource_clump", candidate.Kind);
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "rain_totem_material_target_qualified_item_id" &&
            row.Value == "(O)709");
        Assert.Contains(candidate.Parameters, row =>
            row.Name == "rain_totem_material_producer_stage" &&
            row.Value == "clear_exact_farm_resource_clump_for_hardwood");
    }

    [Fact]
    public void NonExactRainTotemIngredientRowsCannotProduceMaterialCandidates()
    {
        using var fixture = RainTotemCraftingFixture.Create(
            pineTarAvailable: 4,
            readyPineTarMachine: true,
            pineTarRequired: 6);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = fixture.SupplyParameters
                }
            });

        var option = Assert.Single(availability.Options);
        Assert.NotEmpty(option.EventCandidates);
        Assert.DoesNotContain(option.EventCandidates, candidate =>
            candidate.CandidateId.StartsWith(
                "rain-totem-material:",
                StringComparison.Ordinal));
        Assert.DoesNotContain(option.EventCandidates, candidate =>
            candidate.Available);
    }

    [Fact]
    public void AuthoritativeWindowProducerBuildsTomorrowRainSupplyIntent()
    {
        using var fixture = RainTotemCraftingFixture.Create();

        var produced = MasterAnglerWindowIntentValidator
            .TryBuildRainTotemSupplyIntent(
                fixture.Snapshot,
                fixture.WindowIndexPath,
                fixture.WindowIndexSha256,
                out var parameters,
                out var validation,
                out var rejectionReason);

        Assert.True(produced, rejectionReason);
        Assert.Equal(101, validation.TargetTotalDay);
        Assert.Equal(new[] { "(O)145" }, validation.TargetQualifiedItemIds);
        Assert.Equal("Default", validation.AffectedLocationContextId);
        Assert.Equal(
            fixture.SupplyParameters.Select(row => (row.Name, row.Value)),
            parameters.Select(row => (row.Name, row.Value)));
    }

    [Fact]
    public void OwnedRainTotemExpandsThroughExistingNativeUsePrimitive()
    {
        using var fixture = RainTotemCraftingFixture.Create(
            ownsRainTotem: true);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            fixture.Snapshot,
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = "fishing.catch_fish",
                    Parameters = fixture.SupplyParameters
                }
            });

        var option = Assert.Single(availability.Options);
        Assert.True(
            option.EventCandidates.Length > 0,
            JsonSerializer.Serialize(option));
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("use_rain_totem", candidate.Kind);

        var ranked = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            "goal.fishing.complete_master_angler");
        var plan = new DailyPlanCompiler().Compile(
            ranked,
            fixture.Snapshot.StateHash);
        var item = Assert.Single(
            new ActionQueueCompiler().Compile(plan, fixture.Snapshot).Items);

        Assert.Equal("executor.use_rain_totem", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("use_rain_totem", Assert.Single(
            item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void CraftRainTotemIsRuntimeVerifiedButRemainsCalibrationOnly()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "executor.craft_rain_totem");

        Assert.Equal(
            OptionRuntimeStatus.RuntimeVerified,
            declaration.RuntimeEvidenceStatus);
        Assert.True(declaration.HarnessDispatchSupported);
        Assert.DoesNotContain(
            declaration.OptionId,
            OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.Equal(
            ImplementationEngineIds.FarmMachine,
            OptionImplementationCatalog.GetRequired(
                declaration.OptionId).PrimaryEngineId);
    }

    private static SmallModelPlanEnvelope RainTotemCraftingPlan(
        RainTotemCraftingFixture fixture)
    {
        var authorization = RainTotemMaterialReservationContract.Authorization(
            101,
            "Default");
        return Plan(
            fixture.Snapshot.StateHash,
            new SmallModelPlanStep
            {
            StepId = "plan.step.craft.rain-totem",
            Kind = "craft_rain_totem",
            TargetLocation = "FarmHouse",
            EstimatedMinutes = 1,
            Parameters = new[]
            {
                RainParameter("recipe_name", "Rain Totem"),
                RainParameter("output_qualified_item_id", "(O)681"),
                RainParameter("output_item_id", "681"),
                RainParameter("output_count", "1"),
                RainParameter("times_crafted_before", "0"),
                RainParameter("ingredient_rows_json", "[]"),
                RainParameter(
                    "crafting_source",
                    "native_personal_crafting_menu"),
                RainParameter("workbench_access_point_id", string.Empty),
                RainParameter(
                    "workbench_container_node_ids_json",
                    "[]"),
                RainParameter(
                    "rain_totem_crafting_reason",
                    "master_angler_future_rain_window"),
                RainParameter(
                    "weather_supply_required_weather",
                    "Rain"),
                RainParameter(
                    "weather_supply_scheduler_status",
                    "authoritative_future_window_bound"),
                RainParameter(
                    "weather_supply_affected_context_id",
                    "Default"),
                RainParameter(
                    "weather_supply_target_total_day",
                    "101"),
                RainParameter(
                    "weather_supply_target_qualified_item_ids_json",
                    "[\"(O)145\"]"),
                RainParameter("commitment_ledger_id", string.Empty),
                RainParameter("commitment_ledger_revision", "0"),
                RainParameter(
                    "material_reservation_guard_status",
                    "ready_no_active_material_reservations"),
                RainParameter(
                    "material_reservation_ledger_id",
                    string.Empty),
                RainParameter(
                    "material_reservation_ledger_revision",
                    "0"),
                RainParameter("material_reservation_ids_json", "[]"),
                RainParameter(
                    "material_reservation_goal_id",
                    authorization.GoalId),
                RainParameter(
                    "material_reservation_source_decision_id",
                    authorization.SourceDecisionId),
                RainParameter(
                    "material_reservation_purpose",
                    authorization.Purpose),
                RainParameter(
                    "material_reservation_portfolio_id",
                    RainTotemMaterialReservationContract.PortfolioId(
                        101,
                        "Default"))
                }.Concat(fixture.SupplyParameters).ToArray()
            });
    }

    private sealed class RainTotemCraftingFixture : IDisposable
    {
        private RainTotemCraftingFixture(
            SnapshotEnvelope snapshot,
            string windowIndexPath,
            string windowIndexSha256)
        {
            Snapshot = snapshot;
            WindowIndexPath = windowIndexPath;
            WindowIndexSha256 = windowIndexSha256;
            SupplyParameters = new[]
            {
                RainParameter(
                    MasterAnglerWindowIntentValidator
                        .WeatherSupplyStatusParameter,
                    "authoritative_tomorrow_rain_window_match"),
                RainParameter(
                    MasterAnglerWindowIntentValidator
                        .WeatherSupplyTargetDayParameter,
                    "101"),
                RainParameter(
                    MasterAnglerWindowIntentValidator
                        .WeatherSupplyTargetIdsParameter,
                    "[\"(O)145\"]"),
                RainParameter(
                    MasterAnglerWindowIntentValidator
                        .WeatherSupplyAffectedContextParameter,
                    "Default"),
                RainParameter(
                    MasterAnglerWindowIntentValidator
                        .WeatherSupplyWindowIndexPathParameter,
                    windowIndexPath),
                RainParameter(
                    MasterAnglerWindowIntentValidator
                        .WeatherSupplyWindowIndexSha256Parameter,
                    windowIndexSha256),
                RainParameter(
                    MasterAnglerWindowIntentValidator
                        .WeatherSupplyReplanRequiredParameter,
                    "true")
            };
        }

        public SnapshotEnvelope Snapshot { get; }
        public string WindowIndexPath { get; }
        public string WindowIndexSha256 { get; }
        public SmallModelActionParameter[] SupplyParameters { get; }

        public static RainTotemCraftingFixture Create(
            bool ownsRainTotem = false,
            int? pineTarAvailable = null,
            bool readyPineTarMachine = false,
            int pineTarRequired = 5,
            int hardwoodAvailable = 1,
            bool readyHardwoodClump = false,
            bool exactMaterialTopology = false,
            bool recipeLearned = true,
            int foragingLevel = 9,
            bool readyWildTree = false)
        {
            var items = Enumerable.Range(0, 72)
                .Select(index => new
                {
                    qualified_item_id = index == 0
                        ? "(O)145"
                        : "(O)" + (1000 + index),
                    caught = index != 0
                })
                .ToArray();
            var species = items.Select((item, index) => new
            {
                qualified_item_id = item.qualified_item_id,
                windows = new[]
                {
                    new
                    {
                        source_kind = "location_rule",
                        source_key = index == 0 ? "Beach:0" : "Town:0",
                        location_id = index == 0 ? "Beach" : "Town",
                        first_total_day = 100,
                        last_total_day = 101,
                        minimum_fishing_level = 0,
                        require_magic_bait = false,
                        training_rod_allowed = true,
                        dynamic_conditions = Array.Empty<string>(),
                        weather_modes = index == 0
                            ? new[] { "rain", "storm" }
                            : new[] { "sun", "rain", "storm" },
                        time_windows = new[]
                        {
                            new { start_time = 600, end_time = 2600 }
                        }
                    }
                }
            }).ToArray();
            var windowIndexPath = Path.Combine(
                Path.GetTempPath(),
                "stardewai-rain-totem-window-index-" +
                Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(
                windowIndexPath,
                JsonSerializer.Serialize(new
                {
                    schema_version =
                        "master_angler_stage_one_window_index.v1",
                    status =
                        "complete_static_windows_dynamic_execution_pending",
                    game_version = string.Empty,
                    deadline_total_day_exclusive = 224,
                    native_denominator_count = 72,
                    static_window_coverage_complete = true,
                    training_label_eligible = false,
                    species
                }),
                new UTF8Encoding(false));
            var windowIndexSha256 = Convert.ToHexString(
                    SHA256.HashData(File.ReadAllBytes(windowIndexPath)))
                .ToLowerInvariant();
            var itemsJson = JsonSerializer.Serialize(items);
            var inventoryJson = ownsRainTotem
                ? "[{\"slot_index\":2,\"item_id\":\"681\",\"qualified_item_id\":\"(O)681\",\"stack\":2}]"
                : "[]";
            var rainTotemRowsJson = ownsRainTotem
                ? "[{\"inventory_slot_index\":2,\"item_id\":\"681\",\"qualified_item_id\":\"(O)681\",\"inventory_runtime_type\":\"StardewValley.Object\",\"stack_before\":2,\"stack_after\":1,\"temporarily_invisible\":false}]"
                : "[]";
            var playerLocation = readyPineTarMachine || readyHardwoodClump ||
                readyWildTree
                ? "Farm"
                : "FarmHouse";
            var ingredientRowsJson = exactMaterialTopology
                ? """
                  [{"requirement_id_or_category":"709","required_count":1,"available_count_before_this_ingredient":1,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":0,"qualified_item_id":"(O)709","amount":1}]},{"requirement_id_or_category":"432","required_count":1,"available_count_before_this_ingredient":1,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":1,"qualified_item_id":"(O)432","amount":1}]},{"requirement_id_or_category":"726","required_count":5,"available_count_before_this_ingredient":5,"satisfied":true,"reverse_slot_consumption_plan":[{"slot_index":2,"qualified_item_id":"(O)726","amount":5}]}]
                  """
                : pineTarAvailable.HasValue
                ? JsonSerializer.Serialize(new[]
                {
                    new
                    {
                        requirement_id_or_category = "709",
                        required_count = 1,
                        available_count_before_this_ingredient = hardwoodAvailable,
                        satisfied = hardwoodAvailable >= 1
                    },
                    new
                    {
                        requirement_id_or_category = "432",
                        required_count = 1,
                        available_count_before_this_ingredient = 1,
                        satisfied = true
                    },
                    new
                    {
                        requirement_id_or_category = "726",
                        required_count = pineTarRequired,
                        available_count_before_this_ingredient =
                            pineTarAvailable.Value,
                        satisfied = pineTarAvailable.Value >= pineTarRequired
                    }
                })
                : "[]";
            var craftCandidateStatus = pineTarAvailable.HasValue &&
                (pineTarAvailable.Value < pineTarRequired ||
                    hardwoodAvailable < 1)
                    ? "blocked_insufficient_player_inventory_materials"
                    : "ready_for_native_personal_crafting_menu";
            var machinesJson = readyPineTarMachine
                ? "[{\"location_id\":\"Farm\",\"location_kind\":\"farm_outdoor\",\"tile_x\":5,\"tile_y\":4,\"qualified_item_id\":\"(BC)105\",\"display_name\":\"Tapper\",\"ready_for_harvest\":true,\"minutes_until_ready\":0,\"harvest_experience_raw\":\"\",\"harvest_experience_entries\":[],\"harvest_experience_deltas\":[],\"harvest_experience_deltas_json\":\"[]\",\"harvest_mastery_experience_delta\":0,\"harvest_experience_projection_status\":\"exact_no_configured_experience\",\"output_authoritative_route_sources\":[\"native_wild_tree_tapper_output\"],\"held_item\":{\"item_id\":\"726\",\"qualified_item_id\":\"(O)726\",\"stack\":1,\"quality\":0,\"sale_price\":300,\"maximum_stack_size\":999}}]"
                : "[]";
            var resourceClumpsJson = readyHardwoodClump
                ? "[{\"tile_x\":7,\"tile_y\":5,\"width\":2,\"height\":2,\"parent_sheet_index\":600,\"clear_kind\":\"resource_stump\",\"minimum_tool_upgrade_level\":1,\"clear_obstacle_executor_status\":\"ready\",\"expected_tool_hits_to_clear\":5,\"tool_slot_index\":1,\"clear_expected_output_items_json\":\"[{\\\"runtimeType\\\":\\\"StardewValley.Object\\\",\\\"qualifiedItemId\\\":\\\"(O)709\\\",\\\"quality\\\":0,\\\"unitStateSha256\\\":\\\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\\\",\\\"quantity\\\":2}]\",\"clear_output_projection_status\":\"exact_vanilla_conservative_minimum\",\"clear_output_native_contract\":\"native_resource_clump_destroy_contract\",\"harvest_experience_projection_status\":\"exact\",\"harvest_experience_skill_id\":\"foraging\",\"harvest_experience_on_success_min\":25,\"harvest_experience_on_success_max\":25,\"harvest_experience_condition\":\"native_player_axe_destroys_resource_stump_or_hollow_log\"}]"
                : "[]";
            var terrainFeaturesJson = readyWildTree
                ? "[{\"tile_x\":12,\"tile_y\":10,\"type\":\"StardewValley.TerrainFeatures.Tree\",\"runtime_type\":\"StardewValley.TerrainFeatures.Tree\",\"tree_type\":\"1\",\"growth_stage\":5,\"health\":10,\"stump\":false,\"tapped\":false,\"falling\":false,\"has_moss\":false,\"has_seed\":false,\"max_shake\":0,\"tree_chop_acquisition_status\":\"ready\",\"tree_chop_data_contract_status\":\"exact_locked_base_1.6.15_chop\",\"tree_chop_protection_status\":\"unprotected\",\"tree_chop_completion_mode\":\"wild_tree_removed\",\"tree_chop_tool_slot_index\":2,\"tree_chop_required_tool_kind\":\"axe\",\"tree_chop_expected_tool_swings\":6,\"tree_chop_energy_cost\":7.2,\"tree_chop_guaranteed_minimum_outputs\":[{\"qualifiedItemId\":\"(O)388\",\"quality\":0,\"quantityMin\":17},{\"qualifiedItemId\":\"(O)92\",\"quality\":0,\"quantityMin\":6}],\"tree_chop_optional_output_domain\":[{\"kind\":\"exact\",\"qualified_item_id\":\"(O)388\",\"quality\":0,\"quantity_max\":null,\"branch\":\"quantity_above_guaranteed\"}],\"tree_chop_authoritative_route_sources\":[{\"route_kind\":\"native_wild_tree_chop_drop\",\"source_id\":\"wild_tree:1:0\",\"qualified_item_id\":\"(O)92\"}],\"tree_chop_output_distribution_status\":\"complete_stochastic_native_branch_domain_no_rng_consumed\",\"tree_chop_projection_status\":\"exact_live_tree_and_locked_wild_tree_chop_domain\",\"tree_chop_foraging_experience_before\":6900,\"tree_chop_foraging_experience_delta\":16,\"tree_chop_foraging_experience_after\":6916,\"tree_chop_trees_chopped_before\":25,\"tree_chop_trees_chopped_delta\":1,\"tree_chop_trees_chopped_after\":26,\"tree_chop_expected_tree_present_after\":false,\"tree_chop_native_contract\":\"Axe native tool lifecycle -> Tree.performToolAction -> Tree.performTreeFall(trunk,stump) -> Tree.tickUpdate falling settlement; locked Data/WildTrees DropWoodOnChop, DropHardwoodOnLumberChop, ChopItems, SeedItemId, and SeedOnChopChance; complete stochastic output domain; no direct tree, RNG, debris, inventory, stats, or skill mutation\"}]"
                : "[]";
            var rainTotemCraftingRowsJson = recipeLearned
                ? $$"""
                  [{
                    "recipe_name":"Rain Totem",
                    "times_crafted":0,
                    "output_item_id":"681",
                    "output_qualified_item_id":"(O)681",
                    "output_count_per_craft":1,
                    "ingredient_rows":{{ingredientRowsJson}},
                    "workbench_crafting_sources":[],
                    "output_inventory_acceptance_after_material_consumption":true,
                    "craft_candidate_status":"{{craftCandidateStatus}}"
                  }]
                  """
                : "[]";
            var materialGraphJson = exactMaterialTopology
                ? """
                  {"schema_version":"material_inventory_graph.v1","status":"available","player_id":123,"inventory_nodes":[{"node_id":"player:123","inventory_kind":"player_inventory","supply_state":"available","owner_player_id":123,"actor_use_authorized":true,"capacity":12,"slots":[{"slot_index":0,"item_id":"709","qualified_item_id":"(O)709","stack":1,"maximum_stack_size":999,"quality":0},{"slot_index":1,"item_id":"432","qualified_item_id":"(O)432","stack":1,"maximum_stack_size":999,"quality":0},{"slot_index":2,"item_id":"726","qualified_item_id":"(O)726","stack":5,"maximum_stack_size":999,"quality":0}]}],"access_points":[],"workbench_links":[],"quantity_rows":[]}
                  """
                : "null";
            var snapshot = ActionQueueCompilerTests.Snapshot($$"""
        {
          "time": {
            "total_days": {"value":100,"status":"available"},
            "time": {"value":900,"status":"available"}
          },
          "player": {
            "location_id": {"value":"{{playerLocation}}","status":"available"},
            "tile_x": {"value":4,"status":"available"},
            "tile_y": {"value":4,"status":"available"},
            "energy": {"value":270,"status":"available"},
            "inventory": {"value":{{inventoryJson}},"status":"available"},
            "inventory_capacity": {"value":{"capacity":12,"occupied_slots":{{(ownsRainTotem ? 1 : 0)}},"free_slots":{{(ownsRainTotem ? 11 : 12)}},"empty_slots":{{(ownsRainTotem ? 11 : 12)}},"has_empty_slot":true},"status":"available"},
            "skills_detail": {"value":{"skills":[{"skill_id":"fishing","effective_level":10,"unmodified_level":10,"experience":15000},{"skill_id":"foraging","effective_level":{{foragingLevel}},"unmodified_level":{{foragingLevel}},"experience":6900}]} ,"status":"available"},
            "rain_totem": {"value":{
              "projection_status":"complete_current_native_rain_totem_context",
              "projection_fingerprint":"rain-totem-fingerprint",
              "native_use_gate_status":"ready",
              "native_contract":"Object.performUseAction((O)681)->Object.rainTotem->AllowRainTotem->RainTotemAffectsContext_or_location_context->Default_festival_guard_or_context_WeatherForTomorrow=Rain->Default_Game1.getWeatherModificationsForDate",
              "context_routing":{"source_location_context_id":"Default","configured_affected_context_id":"Default","affected_location_context_id":"Default","weather_state_owner_context_id":"Default","allow_rain_totem":true},
              "weather_transition":{"tomorrow_is_default_festival":false,"affected_weather_before":"Sun","affected_weather_after":"Rain","tomorrow_total_days":101,"effective_tomorrow_weather":"Rain","rain_will_take_effect_tomorrow":true},
              "animation_contract":{"facing_direction":2,"animation_duration_ms":2000,"cloud_sprite_count":18,"item_sprite_count":1,"cloud_batch_count":6,"cloud_delay_step_ms":200,"initial_sound":"thunder","delayed_sound":"rainsound","delayed_sound_ms":2000},
              "rows":{{rainTotemRowsJson}}
            },"status":"available"},
            "rain_totem_crafting": {"value":{
              "projection_status":"complete_learned_rain_totem_recipe_projection",
              "recipe_learned":{{recipeLearned.ToString().ToLowerInvariant()}},
              "rows":{{rainTotemCraftingRowsJson}}
            },"status":"available"}
          },
          "farm": {
            "machines": {"value":{{machinesJson}},"status":"available"},
            "resource_clumps": {"value":{{resourceClumpsJson}},"status":"available"},
            "material_inventory_graph": {"value":{{materialGraphJson}},"status":"{{(exactMaterialTopology ? "available" : "unavailable")}}"}
          },
          "world_progress": {
            "fish_collection_progress": {"value":{
              "eligible_species_count":72,
              "caught_eligible_species_count":71,
              "missing_species_count":1,
              "items":{{itemsJson}}
            },"status":"available"}
          },
          "fishing": {
            "location_context": {"value":{},"status":"available"},
            "fishable_tiles": {"value":[],"status":"available"},
            "rod_inventory": {"value":[],"status":"available"},
            "rod_contexts": {"value":[{"has_magic_bait":false,"uses_training_rod":false}],"status":"available"},
            "active_cast_state": {"value":{"is_active":false},"status":"available"}
          },
          "locations": {
            "collision_grid": {"value":{"location_id":"{{playerLocation}}","width":100,"height":100,"notable_tiles":[]},"status":"available"},
            "route_action_branch_coverage": {"value":{"rows":[]},"status":"available"},
            "social_route_date_evidence": {"value":{"locations":[
              {"location_id":"Farm","location_context_id":"Default"},
              {"location_id":"FarmHouse","location_context_id":"Default"},
              {"location_id":"Beach","location_context_id":"Default"},
              {"location_id":"Town","location_context_id":"Default"}
            ]},"status":"available"}
          },
          "current_location": {
            "map": {"value":{"location_id":"{{playerLocation}}"},"status":"available"},
            "objects": {"value":[],"status":"available"},
            "debris": {"value":[],"status":"available"},
            "terrain_features": {"value":{{terrainFeaturesJson}},"status":"available"}
          },
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available"}
          }
        }
        """);
            return new RainTotemCraftingFixture(
                snapshot,
                windowIndexPath,
                windowIndexSha256);
        }

        public void Dispose()
        {
            if (File.Exists(WindowIndexPath))
                File.Delete(WindowIndexPath);
        }
    }

    private static SmallModelActionParameter RainParameter(
        string name,
        string value) => new() { Name = name, Value = value };

    private static StrategyCommitmentLedger RainTotemMaterialLedger(
        string goalId,
        string sourceDecisionId,
        string purpose) => new()
        {
            LedgerId = "ledger:rain-totem",
            Revision = 4,
            MaterialReservations =
            [
                new MaterialReservation
                {
                    ReservationId = "rain-material",
                    Status = StrategyCommitmentStatuses.Active,
                    GoalId = goalId,
                    SourceDecisionId = sourceDecisionId,
                    Purpose = purpose,
                    NodeId = "player:123",
                    SlotIndex = 0,
                    QualifiedItemId = "(O)709",
                    Quantity = 1
                }
            ]
        };
}
