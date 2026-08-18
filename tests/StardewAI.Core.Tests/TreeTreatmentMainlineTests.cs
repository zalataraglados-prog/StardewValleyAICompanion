using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.Core.Tests;

public sealed class TreeTreatmentMainlineTests
{
    [Fact]
    public void ExactTreeAndVinegarCompileToOneNativeTreatmentStep()
    {
        var snapshot = Snapshot(stopGrowingMoss: false, includeVinegar: true);
        var queue = new ActionQueueCompiler().Compile(Request(snapshot), snapshot);

        Assert.Equal("pending", queue.Status);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        var step = Assert.Single(item.NormalizedCommand.Steps);
        Assert.Equal("apply_tree_treatment", step.StepType);
        Assert.Equal("Farm(12,10):(O)419", step.Target);
        Assert.Contains("stop_growing_moss=true", step.ExpectedEffect);
    }

    [Theory]
    [InlineData(true, true, "apply_tree_treatment_not_allowed_by_transparent_context")]
    [InlineData(false, false, "apply_tree_treatment_not_allowed_by_transparent_context")]
    public void StaleTreeOrInventoryFailsClosed(
        bool stopGrowingMoss,
        bool includeVinegar,
        string expectedReason)
    {
        var snapshot = Snapshot(stopGrowingMoss, includeVinegar);
        var item = Assert.Single(new ActionQueueCompiler().Compile(Request(snapshot), snapshot).Items);

        Assert.Equal("blocked", item.Status);
        Assert.Contains(expectedReason, item.BlockingReasons);
    }

    [Fact]
    public void TreatmentReasonIsMandatoryAtCompilerBoundary()
    {
        var snapshot = Snapshot(stopGrowingMoss: false, includeVinegar: true);
        var request = Request(snapshot);
        request.Actions[0].Parameters = request.Actions[0].Parameters
            .Where(value => value.Name != "tree_treatment_reason")
            .ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(request, snapshot).Items);

        Assert.Contains("apply_tree_treatment_reason_required", item.BlockingReasons);
    }

    [Fact]
    public void TreatmentIsCalibrationOnlyAndRuntimeUsesNativePlacement()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.apply_tree_treatment");
        Assert.False(capability.AutonomousCandidateEnabled);
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityCandidateStatus.NotApplicable, capability.CandidateStatus);

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.TreeTreatment.cs"));
        var bridge = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.TerrainExperience.cs"));
        Assert.Contains("treatment.placementAction", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("tree.hasMoss.Value =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("tree.stopGrowingMoss.Value =", runtime, StringComparison.Ordinal);
        Assert.Contains("stop_growing_moss = tree.stopGrowingMoss.Value", bridge, StringComparison.Ordinal);
        Assert.Contains("Object.placementAction (O)419", bridge, StringComparison.Ordinal);
    }

    private static SmallModelActionEnvelope Request(SnapshotEnvelope snapshot) => new()
    {
        ModelOutputId = "tree-treatment-test",
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
                ActionId = "treat-tree",
                OptionId = "executor.apply_tree_treatment",
                Rationale = "preserve planned machine access without future moss",
                Parameters = new[]
                {
                    P("target_location", "Farm"), P("target_tile_x", "12"), P("target_tile_y", "10"),
                    P("target_runtime_type", "StardewValley.TerrainFeatures.Tree"),
                    P("qualified_item_id", "(O)419"), P("slot_index", "2"),
                    P("tree_treatment_reason", "prevent_moss_on_retained_tree")
                }
            }
        }
    };

    private static SmallModelActionParameter P(string name, string value) =>
        new() { Name = name, Value = value };

    private static SnapshotEnvelope Snapshot(bool stopGrowingMoss, bool includeVinegar)
    {
        var inventory = includeVinegar
            ? "[{\"slot_index\":2,\"qualified_item_id\":\"(O)419\",\"stack\":2}]"
            : "[]";
        var allowed = !stopGrowingMoss;
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"Farm","status":"available"},
            "tile_x":{"value":10,"status":"available"},
            "tile_y":{"value":10,"status":"available"},
            "inventory":{"value":{{{inventory}}},"status":"available"}
          },
          "current_location":{
            "terrain_features":{"value":[{
              "tile_x":12,"tile_y":10,"type":"StardewValley.TerrainFeatures.Tree",
              "has_moss":true,"stop_growing_moss":{{{stopGrowingMoss.ToString().ToLowerInvariant()}}},
              "tree_treatment_required_qualified_item_id":"(O)419",
              "tree_treatment_native_allowed":{{{allowed.ToString().ToLowerInvariant()}}}
            }],"status":"available"}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"Farm","width":100,"height":100,"notable_tiles":[]},"status":"available"}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available"}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-18T00:00:00Z",
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
