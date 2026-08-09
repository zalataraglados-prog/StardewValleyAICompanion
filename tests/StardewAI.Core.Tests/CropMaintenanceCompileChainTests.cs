using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    [Fact]
    public void MaintainCropsEmitsOnlyNativeAllowedExactFertilizerCandidate()
    {
        var snapshot = FertilizerSnapshot(allowApplication: true);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("apply_fertilizer_tile", candidate.Kind);
        Assert.Equal("fertilize:Greenhouse:4,5:(O)368", candidate.CandidateId);
        Assert.Equal("(O)368", candidate.QualifiedItemId);
        Assert.Equal(2, candidate.SlotIndex);
        Assert.Contains("fertilizer_id=(O)368", candidate.ExpectedEffect);
    }

    [Fact]
    public void MaintainCropsKeepsNativeRejectedFertilizerCandidateBlockedUpstream()
    {
        var snapshot = FertilizerSnapshot(allowApplication: false);

        var option = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "farm.maintain_crops" }, includeExecutorCalibrationOptions: true)
            .Options[0];

        var candidate = Assert.Single(option.EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("fertilizer_not_allowed_by_transparent_context", candidate.BlockReasons);
    }

    private static StardewAI.Contracts.State.SnapshotEnvelope FertilizerSnapshot(bool allowApplication)
    {
        return Snapshot(
            """
            {
              "time":{
                "season":{"value":"spring","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "weather":{"value":"sun","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "player":{
                "location_id":{"value":"Greenhouse","status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_x":{"value":3,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "tile_y":{"value":5,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "energy":{"value":270,"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory":{"value":[{"slot_index":2,"item_id":"368","qualified_item_id":"(O)368","stack":4}],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "current_location":{
                "crops":{"value":[],"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "planting_context":{"value":{"hoe_dirt_tiles":[{
                  "tile_x":4,"tile_y":5,"fertilizer_results":[{
                    "slot_index":2,"item_id":"368","qualified_item_id":"(O)368","stack":4,
                    "apply_status":"STATUS","hard_rule_allows_application":ALLOW_APPLICATION
                  }]
                }]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "locations":{
                "collision_grid":{"value":{"width":20,"height":20,"notable_tiles":[]},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "menus":{
                "active_menu":{"value":{"is_open":false},"status":"available","source":{"kind":"test","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """
            .Replace("ALLOW_APPLICATION", allowApplication ? "true" : "false", StringComparison.Ordinal)
            .Replace("STATUS", allowApplication ? "allowed" : "blocked", StringComparison.Ordinal));
    }
}

public sealed partial class DailyPlanCompilerTests
{
    [Fact]
    public void CompilePreservesExactFertilizerIdentityInPrimitiveStep()
    {
        var candidate = new PolicyEventCandidatePrediction
        {
            CandidateId = "fertilize:Greenhouse:4,5:(O)368",
            Kind = "apply_fertilizer_tile",
            Available = true,
            LocationId = "Greenhouse",
            TileX = 4,
            TileY = 5,
            ItemId = "368",
            QualifiedItemId = "(O)368",
            SlotIndex = 2,
            ExpectedEffect = "current_location.planting_context[4,5].fertilizer_id=(O)368"
        };

        var step = Assert.Single(new DailyPlanCompiler().Compile(new[] { candidate }, "state.fertilizer").Steps);

        Assert.Equal("apply_fertilizer", step.Kind);
        Assert.Equal("Greenhouse", step.TargetLocation);
        Assert.Contains(step.Parameters, row => row.Name == "qualified_item_id" && row.Value == "(O)368");
        Assert.Contains(step.Parameters, row => row.Name == "slot_index" && row.Value == "2");
    }
}

public sealed partial class ActionQueueCompilerTests
{
    [Fact]
    public void CompileApplyFertilizerRequiresMatchingTransparentNativeRule()
    {
        var snapshot = CandidateOptionAvailabilityEvaluatorTests.FertilizerSnapshotForCompiler(allowApplication: true);
        var request = Request(snapshot.StateHash, "executor.apply_fertilizer");
        request.Actions[0].Parameters = new[]
        {
            new SmallModelActionParameter { Name = "target_location", Value = "Greenhouse" },
            new SmallModelActionParameter { Name = "target_tile_x", Value = "4" },
            new SmallModelActionParameter { Name = "target_tile_y", Value = "5" },
            new SmallModelActionParameter { Name = "item_id", Value = "368" },
            new SmallModelActionParameter { Name = "qualified_item_id", Value = "(O)368" },
            new SmallModelActionParameter { Name = "slot_index", Value = "2" }
        };

        var queue = new ActionQueueCompiler().Compile(request, snapshot);

        Assert.Equal("pending", queue.Status);
        var step = Assert.Single(Assert.Single(queue.Items).NormalizedCommand.Steps);
        Assert.Equal("apply_fertilizer", step.StepType);
        Assert.Equal("Greenhouse(4,5):(O)368", step.Target);
    }
}

public sealed partial class CandidateOptionAvailabilityEvaluatorTests
{
    internal static StardewAI.Contracts.State.SnapshotEnvelope FertilizerSnapshotForCompiler(bool allowApplication) =>
        FertilizerSnapshot(allowApplication);
}
