using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
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
    public void CraftRainTotemRemainsCalibrationOnlyUntilNativeReceiptExists()
    {
        var declaration = OptionCapabilityRegistrySource.GetRequired(
            "executor.craft_rain_totem");

        Assert.Equal(
            OptionRuntimeStatus.RegisteredOnly,
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
        RainTotemCraftingFixture fixture) => Plan(
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
                RainParameter("material_reservation_ids_json", "[]")
            }.Concat(fixture.SupplyParameters).ToArray()
        });

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
            bool ownsRainTotem = false)
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
            var snapshot = ActionQueueCompilerTests.Snapshot($$"""
        {
          "time": {
            "total_days": {"value":100,"status":"available"}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available"},
            "tile_x": {"value":4,"status":"available"},
            "tile_y": {"value":4,"status":"available"},
            "energy": {"value":270,"status":"available"},
            "inventory": {"value":{{inventoryJson}},"status":"available"},
            "inventory_capacity": {"value":{"capacity":12,"occupied_slots":{{(ownsRainTotem ? 1 : 0)}},"free_slots":{{(ownsRainTotem ? 11 : 12)}}},"status":"available"},
            "skills_detail": {"value":{"skills":[{"skill_id":"fishing","effective_level":10}]},"status":"available"},
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
              "rows":[{
                "recipe_name":"Rain Totem",
                "times_crafted":0,
                "output_item_id":"681",
                "output_qualified_item_id":"(O)681",
                "output_count_per_craft":1,
                "ingredient_rows":[],
                "workbench_crafting_sources":[],
                "output_inventory_acceptance_after_material_consumption":true,
                "craft_candidate_status":"ready_for_native_personal_crafting_menu"
              }]
            },"status":"available"}
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
            "collision_grid": {"value":{"locations":[]},"status":"available"},
            "social_route_date_evidence": {"value":{"locations":[
              {"location_id":"FarmHouse","location_context_id":"Default"},
              {"location_id":"Beach","location_context_id":"Default"},
              {"location_id":"Town","location_context_id":"Default"}
            ]},"status":"available"}
          },
          "current_location": {
            "map": {"value":{"location_id":"FarmHouse"},"status":"available"}
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
}
