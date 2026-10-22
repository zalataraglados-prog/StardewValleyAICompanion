using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CraftRainTotemCompilesThroughExactSupportCraftingContract()
    {
        var snapshot = RainTotemCraftingSnapshot();
        var queue = new ActionQueueCompiler().Compile(
            RainTotemCraftingPlan(snapshot.StateHash),
            snapshot);

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
        var snapshot = RainTotemCraftingSnapshot();
        var plan = RainTotemCraftingPlan(snapshot.StateHash);
        var targetDay = Assert.Single(plan.Steps[0].Parameters.Where(row =>
            row.Name == "weather_supply_target_total_day"));
        targetDay.Value = "102";

        var item = Assert.Single(
            new ActionQueueCompiler().Compile(plan, snapshot).Items);

        Assert.Contains(
            "craft_rain_totem_target_day_not_tomorrow",
            item.BlockingReasons);
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
        string stateHash) => Plan(
        stateHash,
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
            }
        });

    private static StardewAI.Contracts.State.SnapshotEnvelope
        RainTotemCraftingSnapshot()
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
        var itemsJson = JsonSerializer.Serialize(items);
        return Snapshot($$"""
        {
          "time": {
            "total_days": {"value":100,"status":"available"}
          },
          "player": {
            "location_id": {"value":"FarmHouse","status":"available"},
            "inventory": {"value":[],"status":"available"},
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
          "menus": {
            "active_menu": {"value":{"is_open":false},"status":"available"}
          }
        }
        """);
    }

    private static SmallModelActionParameter RainParameter(
        string name,
        string value) => new() { Name = name, Value = value };
}
