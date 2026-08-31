using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CookingMainlineTests
{
    [Fact]
    public void ExplicitRecipeFlowsThroughOneNativeCookingPrimitive()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { Intent() },
            includeExecutorCalibrationOptions: true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("cook_recipe", candidate.Kind);
        Assert.Contains(candidate.Parameters, row => row.Name == "cooking_source_id" && row.Value == "kitchen:FarmHouse:5,5");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("cook_recipe", step.Kind);
        Assert.Contains("native_CraftingPage_click_only", step.SafetyConstraints);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.cook_recipe", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("cook_recipe", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MissingPurposeOrUnsupportedMultiCraftIsExcludedUpstream()
    {
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var noPurpose = Intent();
        noPurpose.Parameters = noPurpose.Parameters.Where(row => row.Name != "cooking_reason").ToArray();
        var multi = Intent();
        multi.Parameters = multi.Parameters.Select(row => row.Name == "craft_count" ? P("craft_count", "2") : row).ToArray();

        Assert.Empty(Assert.Single(evaluator.Evaluate(Snapshot(), new[] { noPurpose }, true).Options).EventCandidates);
        Assert.Empty(Assert.Single(evaluator.Evaluate(Snapshot(), new[] { multi }, true).Options).EventCandidates);
    }

    [Fact]
    public void MaterialOrSeasoningDriftBlocksCompiler()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { Intent() }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        var parameters = candidate.Parameters
            .Select(row => row.Name == "seasoning_rows_json" ? P(row.Name, "[]") : row)
            .ToArray();
        var action = new SmallModelActionEnvelope
        {
            ModelOutputId = "cooking-drift",
            SourceModel = "test",
            StateHash = snapshot.StateHash,
            GoalId = "test",
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
                    ActionId = "cook",
                    OptionId = "executor.cook_recipe",
                    Rationale = "test drift",
                    Parameters = parameters
                }
            }
        };

        Assert.Contains("cook_recipe_projection_drifted",
            Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesNativeKitchenAndCookoutEntryWithoutDirectMutation()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Cooking.cs"));

        Assert.Contains("active.Location.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("checkForAction(Game1.player)", source, StringComparison.Ordinal);
        Assert.Contains("TryClickCraftingRecipe", source, StringComparison.Ordinal);
        Assert.DoesNotContain("recipesCooked[", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.Items[", source, StringComparison.Ordinal);
        Assert.DoesNotContain("achievements.Add", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TransparentCookingScanDoesNotForcePersistentHomeMapReloads()
    {
        var cooking = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.Cooking.cs"));
        var forge = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.Forge.cs"));
        var boards = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.SpecialOrderBoards.cs"));
        var construction = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.BuildingConstruction.cs"));
        var currentLocation = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.cs"));

        foreach (var source in new[] { cooking, forge, boards })
        {
            Assert.Contains("location.map?.GetLayer(\"Buildings\")", source, StringComparison.Ordinal);
            Assert.DoesNotContain("location.Map?.GetLayer(\"Buildings\")", source, StringComparison.Ordinal);
        }
        Assert.Contains("Where(IsLoadedBuildableLocation)", construction, StringComparison.Ordinal);
        Assert.Contains("var map = location.map;", construction, StringComparison.Ordinal);
        Assert.DoesNotContain("Where(location => location.IsBuildableLocation())", construction, StringComparison.Ordinal);
        Assert.Contains("ReadLoadedHomeEntry(home)", currentLocation, StringComparison.Ordinal);
        Assert.DoesNotContain("home.getEntryLocation()", currentLocation, StringComparison.Ordinal);
        Assert.DoesNotContain("home.GetPlayerBedSpot()", currentLocation, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRuntimeRequestRoundTripsCookingContract()
    {
        var request = new TrainingExecutionRequest
        {
            CraftCount = 1,
            CookingReason = "buff_supply",
            CookingSourceId = "kitchen:FarmHouse:5,5",
            CookingSourceKind = "kitchen",
            RecipesCookedBefore = 2,
            SeasoningRowsJson = "[]",
            MaterialContainerIdsJson = "[\"kitchen-fridge:FarmHouse\"]",
            ExpectedOutputOrderData = "QI_COOKING"
        };
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

        Assert.Equal(1, roundTrip.CraftCount);
        Assert.Equal("buff_supply", roundTrip.CookingReason);
        Assert.Equal(2, roundTrip.RecipesCookedBefore);
        Assert.Equal("QI_COOKING", roundTrip.ExpectedOutputOrderData);
    }

    private static OptionAvailabilityCandidate Intent() => new()
    {
        OptionId = "crafting.cook_recipe",
        ExplicitConfirmationGranted = true,
        Parameters = new[]
        {
            P("recipe_name", "Fried Egg"),
            P("craft_count", "1"),
            P("cooking_reason", "explicit_test_meal")
        }
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot()
    {
        var ingredientRows = "[{\"requirement_id_or_category\":\"176\",\"required_count\":1,\"available_count_before_this_ingredient\":1,\"satisfied\":true,\"native_consumption_plan\":[{\"source_id\":\"kitchen-fridge:FarmHouse\",\"slot_index\":0,\"qualified_item_id\":\"(O)176\",\"amount\":1,\"unit_sale_price\":50,\"total_sale_value\":50}]}]";
        var seasoningRows = "[{\"requirement_id_or_category\":\"917\",\"required_count\":1,\"available_count_before_seasoning\":1,\"satisfied\":true,\"native_consumption_plan\":[{\"source_id\":\"player:1\",\"slot_index\":1,\"qualified_item_id\":\"(O)917\",\"amount\":1,\"unit_sale_price\":100,\"total_sale_value\":100}]}]";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"FarmHouse","status":"available"},
            "tile_x":{"value":4,"status":"available"},
            "tile_y":{"value":5,"status":"available"},
            "inventory":{"value":[{"slot_index":1,"item_id":"917","qualified_item_id":"(O)917","stack":1,"quality":0,"maximum_stack_size":999,"is_empty":false}],"status":"available"},
            "inventory_capacity":{"value":{"occupied_stacks":1,"empty_slots":11,"has_empty_slot":true},"status":"available"},
            "cooking":{"value":{
              "projection_status":"complete_learned_cooking_recipe_and_native_source_projection",
              "rows":[{
                "recipe_name":"Fried Egg","known_recipe":true,
                "cooking_source_id":"kitchen:FarmHouse:5,5","cooking_source_kind":"kitchen",
                "location_id":"FarmHouse","interaction_tile_x":5,"interaction_tile_y":5,
                "material_container_ids":["kitchen-fridge:FarmHouse"],
                "material_container_topology_json":"[\"kitchen-fridge:FarmHouse\"]",
                "output_item_id":"194","output_qualified_item_id":"(O)194","output_display_name":"Fried Egg",
                "output_count_per_craft":1,"output_quality":2,"output_order_data":"","recipes_cooked_before":2,
                "ingredient_rows":{{{ingredientRows}}},"ingredient_rows_json":{{{JsonSerializer.Serialize(ingredientRows)}}},
                "seasoning_rows":{{{seasoningRows}}},"seasoning_rows_json":{{{JsonSerializer.Serialize(seasoningRows)}}},
                "output_inventory_acceptance_after_material_consumption":true,
                "craft_candidate_status":"ready_for_native_cooking_page"
              }]
            },"status":"available"}
          },
          "locations":{
            "route_graph":{"value":{"edges":[]},"status":"available"},
            "collision_grid":{"value":{"location_id":"FarmHouse","width":40,"height":40,"notable_tiles":[]},"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-17T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
