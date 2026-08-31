using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class TailoringMainlineTests
{
    [Fact]
    public void ExactLiveTailoringCandidateFlowsThroughOneNativePrimitive()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate { OptionId = "tailoring.sew_item", ExplicitConfirmationGranted = true }
        }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("tailor_item", candidate.Kind);
        Assert.Contains(candidate.Parameters, value => value.Name == "tailoring_recipe_id" && value.Value == "BasicPullover_FromWood");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("tailor_item", step.Kind);
        Assert.Contains("native_TailoringMenu_clicks_only", step.SafetyConstraints);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.tailor_item", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("tailor_item", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void FreshCompilerRejectsInputOrOutputDomainDrift()
    {
        var snapshot = Snapshot();
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate { OptionId = "tailoring.sew_item", ExplicitConfirmationGranted = true }
        }, true).Options).EventCandidates);
        var action = new SmallModelActionEnvelope
        {
            ModelOutputId = "tailoring-drift",
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
                    ActionId = "tailor",
                    OptionId = "executor.tailor_item",
                    Rationale = "test",
                    Parameters = candidate.Parameters.Select(value => value.Name == "expected_output_state_json"
                        ? P(value.Name, "{}")
                        : value).ToArray()
                }
            }
        };
        Assert.Contains("tailoring_projection_drifted", Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesNativeMenuAndCollectsLeftoversWithoutDirectMutation()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Tailoring.cs"));
        Assert.Contains("active.Location.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("machine.checkForAction(Game1.player)", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.Contains("ReturnTailoringIngredient", source, StringComparison.Ordinal);
        Assert.DoesNotContain("applyStats(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Dye(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkItemAsTailored(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.random", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BridgeUsesLiveRecipeDataAndExcludesDyeBranch()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.Tailoring.cs"));
        Assert.Contains("DataLoader.TailoringRecipes(Game1.temporaryContent)", source, StringComparison.Ordinal);
        Assert.Contains("belongs_to_tailoring.dye_item", source, StringComparison.Ordinal);
        Assert.Contains("native_random_result_domain", source, StringComparison.Ordinal);
        Assert.Contains("(BC)247", source, StringComparison.Ordinal);
        Assert.Contains("eventsSeen.Contains(\"992559\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRuntimeRequestRoundTripsTailoringContract()
    {
        var request = new TrainingExecutionRequest
        {
            TailoringCandidateId = "candidate",
            TailoringOperation = "recipe",
            TailoringPurpose = "first_tailor_discovery",
            TailoringRecipeId = "BasicPullover_FromWood",
            TailoringSourceId = "sewing-machine:Farm:10,10",
            TailoringSourceKind = "placed_sewing_machine",
            LeftSourceId = "inventory:0",
            LeftStateJson = "{}",
            RightSourceId = "inventory:1",
            RightStateJson = "{}",
            TailoringSpendLeftCount = 1,
            TailoringSpendRightCount = 1,
            TailoringOutputContractKind = "exact_item_state",
            TailoringTailoredCountsBeforeJson = "{}",
            TailoringMarksTailoredItem = true,
            TailoringNativeContract = "native"
        };
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var result = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("BasicPullover_FromWood", result.TailoringRecipeId);
        Assert.Equal(1, result.TailoringSpendRightCount);
        Assert.True(result.TailoringMarksTailoredItem);
    }

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot()
    {
        const string left = "{\"qualified_item_id\":\"(O)428\",\"runtime_type\":\"StardewValley.Object\",\"stack\":1}";
        const string right = "{\"qualified_item_id\":\"(O)388\",\"runtime_type\":\"StardewValley.Object\",\"stack\":1}";
        const string output = "{\"qualified_item_id\":\"(S)1176\",\"runtime_type\":\"StardewValley.Objects.Clothing\",\"stack\":1}";
        var native = "live_Tailoring_action_or_BC247_checkAction_then_native_TailoringMenu_inventory_slot_clicks_start_1500ms_update_collect_leftovers_and_verify_without_direct_inventory_tailoredItems_boot_or_clothing_mutation";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},"tile_x":{"value":9,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[],"status":"available"},"inventory_capacity":{"value":{"empty_slots":10},"status":"available"},
            "tailoring":{"value":{"projection_status":"complete_live_native_tailoring_recipe_input_endpoint_and_output_domain_projection","rows":[{
              "tailoring_candidate_id":"sewing-machine:Farm:10,10:recipe:inventory:0:inventory:1","tailoring_operation":"recipe",
              "tailoring_purpose":"first_tailor_discovery","recipe_id":"BasicPullover_FromWood",
              "source_id":"sewing-machine:Farm:10,10","source_kind":"placed_sewing_machine","source_ready":true,
              "location_id":"Farm","interaction_tile_x":10,"interaction_tile_y":10,
              "left_source_id":"inventory:0","left_state_json":{{{JsonSerializer.Serialize(left)}}},
              "right_source_id":"inventory:1","right_state_json":{{{JsonSerializer.Serialize(right)}}},
              "left_display_name":"Cloth","right_display_name":"Wood","left_qualified_item_id":"(O)428",
              "spend_left_count":1,"spend_right_count":1,"output_contract_kind":"exact_item_state",
              "expected_output_state_json":{{{JsonSerializer.Serialize(output)}}},"random_outcome_contract_json":"",
              "tailored_counts_before_json":"{}","marks_tailored_item":true,
              "tailoring_candidate_status":"ready_for_native_tailoring_menu","native_contract":{{{JsonSerializer.Serialize(native)}}}
            }]},"status":"available"}
          },
          "locations":{"route_graph":{"value":{"edges":[]},"status":"available"},"collision_grid":{"value":{"location_id":"Farm","width":40,"height":40,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-31T00:00:00Z",
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
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
