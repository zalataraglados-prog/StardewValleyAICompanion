using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class ForgeMainlineTests
{
    [Fact]
    public void ExactForgeIntentFlowsThroughOneNativeForgePrimitive()
    {
        var snapshot = Snapshot();
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { Intent() }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("forge_item", candidate.Kind);
        Assert.Contains(candidate.Parameters, value => value.Name == "forge_source_id" && value.Value == "mini-forge:Farm:10,10");

        var ranked = new EventCandidateRanker().Rank(new(), availability);
        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("forge_item", step.Kind);
        Assert.Contains("native_ForgeMenu_clicks_only", step.SafetyConstraints);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Equal("executor.forge_item", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("forge_item", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MissingPurposeOrExactInputSelectionIsExcludedUpstream()
    {
        var evaluator = new CandidateOptionAvailabilityEvaluator();
        var noPurpose = Intent();
        noPurpose.Parameters = noPurpose.Parameters.Where(value => value.Name != "forge_reason").ToArray();
        var noRight = Intent();
        noRight.Parameters = noRight.Parameters.Where(value => value.Name != "right_source_id").ToArray();
        Assert.Empty(Assert.Single(evaluator.Evaluate(Snapshot(), new[] { noPurpose }, true).Options).EventCandidates);
        Assert.Empty(Assert.Single(evaluator.Evaluate(Snapshot(), new[] { noRight }, true).Options).EventCandidates);
    }

    [Fact]
    public void InputStateDriftBlocksCompiler()
    {
        var snapshot = Snapshot();
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { Intent() }, true).Options).EventCandidates);
        var action = new SmallModelActionEnvelope
        {
            ModelOutputId = "forge-drift", SourceModel = "test", StateHash = snapshot.StateHash, GoalId = "test",
            ExecutionMode = "training_singleplayer",
            Actor = new ActionActorRef { ActorId = "training_farmer.test", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
            Actions = new[] { new SmallModelAction { ActionId = "forge", OptionId = "executor.forge_item", Rationale = "test", Parameters = candidate.Parameters.Select(value => value.Name == "left_state_json" ? P(value.Name, "{}") : value).ToArray() } }
        };
        Assert.Contains("forge_item_projection_drifted", Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items).BlockingReasons);
    }

    [Fact]
    public void RuntimeUsesNativeForgeMenuClicksWithoutDirectForgeMutation()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Forge.cs"));
        Assert.Contains("active.Location.checkAction", source, StringComparison.Ordinal);
        Assert.Contains("value.checkForAction(Game1.player)", source, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Forge(", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".Combine(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReduceId(", source, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRuntimeRequestRoundTripsForgeContract()
    {
        var request = new TrainingExecutionRequest
        {
            ForgeCandidateId = "candidate", ForgeOperation = "gem_forge", ForgeReason = "combat_upgrade",
            ForgeSourceId = "mini-forge:Farm:10,10", ForgeSourceKind = "mini_forge",
            LeftSourceId = "inventory:0", LeftStateJson = "{}", RightSourceId = "inventory:1", RightStateJson = "{}",
            ForgeShardCost = 10, ForgeShardRefund = 0, ForgeShardCountBefore = 20,
            TimesEnchantedBefore = 0, TimesEnchantedAfter = 0, ForgeOutputContractKind = "exact_item_state",
            ExpectedOutputStateJson = "{}", RandomOutcomeContractJson = ""
        };
        var json = JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var result = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("gem_forge", result.ForgeOperation);
        Assert.Equal(10, result.ForgeShardCost);
        Assert.Equal("exact_item_state", result.ForgeOutputContractKind);
    }

    private static OptionAvailabilityCandidate Intent() => new()
    {
        OptionId = "crafting.forge_item", ExplicitConfirmationGranted = true,
        Parameters = new[] { P("forge_operation", "gem_forge"), P("forge_reason", "combat_upgrade"), P("left_source_id", "inventory:0"), P("right_source_id", "inventory:1") }
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot()
    {
        const string left = "{\"qualified_item_id\":\"(W)4\",\"runtime_type\":\"StardewValley.Tools.MeleeWeapon\"}";
        const string right = "{\"qualified_item_id\":\"(O)64\",\"runtime_type\":\"StardewValley.Object\"}";
        const string output = "{\"qualified_item_id\":\"(W)4\",\"enchantments\":[{\"runtime_type\":\"RubyEnchantment\",\"level\":1}]}";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},"tile_x":{"value":9,"status":"available"},"tile_y":{"value":10,"status":"available"},
            "inventory":{"value":[],"status":"available"},"inventory_capacity":{"value":{"empty_slots":10},"status":"available"},
            "forge":{"value":{"projection_status":"complete_loaded_native_forge_source_and_live_input_projection","rows":[{
              "forge_candidate_id":"mini-forge:Farm:10,10:gem_forge:inventory:0:inventory:1","forge_operation":"gem_forge",
              "forge_source_id":"mini-forge:Farm:10,10","forge_source_kind":"mini_forge","location_id":"Farm","interaction_tile_x":10,"interaction_tile_y":10,
              "left_source_id":"inventory:0","left_state_json":{{{JsonSerializer.Serialize(left)}}},"right_source_id":"inventory:1","right_state_json":{{{JsonSerializer.Serialize(right)}}},
              "shard_cost":10,"shard_refund":0,"shard_count_before":20,"times_enchanted_before":0,"times_enchanted_after":0,
              "output_contract_kind":"exact_item_state","expected_output_state_json":{{{JsonSerializer.Serialize(output)}}},"random_outcome_contract_json":"",
              "left_display_name":"Galaxy Sword","left_qualified_item_id":"(W)4","output_inventory_acceptance_after_input_removal":true,
              "forge_candidate_status":"ready_for_native_forge_menu"
            }]} ,"status":"available"}
          },
          "locations":{"route_graph":{"value":{"edges":[]},"status":"available"},"collision_grid":{"value":{"location_id":"Farm","width":40,"height":40,"notable_tiles":[]},"status":"available"}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope { StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1, RealTimestamp = "2026-08-18T00:00:00Z", Completeness = "complete", State = state };
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }
}
