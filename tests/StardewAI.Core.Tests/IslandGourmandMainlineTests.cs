using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class IslandGourmandMainlineTests
{
    [Theory]
    [InlineData(false, "introduce_request", 0, 0)]
    [InlineData(true, "complete_request", 0, 1)]
    public void ReadyCropFlowsThroughOneTypedGourmandPrimitive(
        bool talked,
        string expectedStage,
        int requestBefore,
        int requestAfter)
    {
        var snapshot = Snapshot(talked: talked, requestIndex: requestBefore);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "island.complete_gourmand_request" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("advance_gourmand_request", candidate.Kind);
        AssertParameter(candidate.Parameters, "gourmand_interaction_stage", expectedStage);
        AssertParameter(candidate.Parameters, "request_index_after", requestAfter.ToString());
        AssertParameter(candidate.Parameters, "required_crop_item_id", "254");
        AssertParameter(candidate.Parameters, "walnut_debris_count_before", "0");
        AssertParameter(candidate.Parameters, "golden_walnuts_found_before", "0");
        AssertParameter(candidate.Parameters, "golden_walnuts_found_after", talked ? "5" : "0");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("advance_gourmand_request", step.Kind);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.advance_gourmand_request", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("advance_gourmand_request", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void MissingReadyCropIsExcludedUpstream()
    {
        var snapshot = Snapshot(talked: true, requestIndex: 0, ready: false);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "island.complete_gourmand_request" }, true);

        Assert.Empty(Assert.Single(availability.Options).EventCandidates);
    }

    [Fact]
    public void FreshCompilerRejectsRequestIdentityDrift()
    {
        var original = Snapshot(talked: true, requestIndex: 0);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "island.complete_gourmand_request" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability),
            original.StateHash);
        var drifted = Snapshot(talked: true, requestIndex: 1);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("gourmand_request_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RollingRouteContinuationLocksRequestCropAndStage()
    {
        var routeItem = JsonNode.Parse("""
        {"option_id":"executor.traverse_connector","normalized_command":{"parameters":[
          {"name":"continuation.option_id","value":"island.complete_gourmand_request"},
          {"name":"continuation.gourmand_request_index","value":"1"},
          {"name":"continuation.gourmand_crop_item_id","value":"262"},
          {"name":"continuation.gourmand_interaction_stage","value":"complete_request"}
        ]}}
        """)!.AsObject();
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(routeItem);
        Assert.Equal("island_gourmand", continuation!["kind"]!.GetValue<string>());

        var ranked = JsonNode.Parse("""
        [{"option_id":"island.complete_gourmand_request","parameters":[
          {"name":"request_index_before","value":"1"},
          {"name":"required_crop_item_id","value":"262"},
          {"name":"gourmand_interaction_stage","value":"complete_request"}
        ]},{"option_id":"island.complete_gourmand_request","parameters":[
          {"name":"request_index_before","value":"2"},
          {"name":"required_crop_item_id","value":"248"},
          {"name":"gourmand_interaction_stage","value":"complete_request"}
        ]}]
        """)!.AsArray();
        Assert.Single(QueueReplanFilter.FilterRankedCandidates(ranked, continuation));

        var terminal = JsonNode.Parse("""
        {"option_id":"executor.advance_gourmand_request","normalized_command":{"parameters":[
          {"name":"request_index_before","value":"1"},
          {"name":"required_crop_item_id","value":"262"},
          {"name":"gourmand_interaction_stage","value":"complete_request"}
        ]}}
        """)!.AsObject();
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    [Fact]
    public void RuntimeHarnessUsesNativeFlowWithoutDirectProgressMutation()
    {
        var capability = OptionCapabilityRegistrySource.GetRequired("executor.advance_gourmand_request");
        Assert.True(capability.HarnessDispatchSupported);
        Assert.Equal(CapabilityVerifierStatus.PendingRuntimeEvidence, capability.AfterVerifierStatus);
        Assert.Empty(capability.RuntimeEvidenceIds);

        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.IslandGourmand.cs"));
        var validation = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.IslandGourmand.Validation.cs"));
        var production = runtime + validation;
        Assert.Contains(".checkAction(", production, StringComparison.Ordinal);
        Assert.Contains(".answerDialogue(", production, StringComparison.Ordinal);
        Assert.DoesNotContain("GiveReward(", production, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkCollectedNut", production, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"gourmandRequestsFulfilled\.Value\s*=(?!=)"), production);
        Assert.DoesNotMatch(new Regex(@"GoldenWalnutsFound\s*=(?!=)"), production);
    }

    private static SnapshotEnvelope Snapshot(bool talked, int requestIndex, bool ready = true)
    {
        var cropId = requestIndex switch
        {
            0 => "254",
            1 => "262",
            _ => "248"
        };
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"IslandFarmCave","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress":{
            "island_gourmand":{"value":{
              "schema_version":"island_gourmand_progress.v1","projection_status":"exact_native_active_request_projected",
              "location_id":"IslandFarmCave","is_current_location":true,"host_authority":true,"menu_clear":true,
              "mutex_projection_available":true,"mutex_locked":false,"mutex_held_by_local_player":false,"triggered_gourmand":false,
              "required_mail_key":"Island_UpgradeHouse","required_mail_satisfied":true,
              "talked_to_gourmand_received_or_pending":{{{talked.ToString().ToLowerInvariant()}}},
              "requests_fulfilled":{{{requestIndex}}},"total_requests":3,"complete":false,"active_request_index":{{{requestIndex}}},
              "required_crop_item_id":"{{{cropId}}}","required_crop_qualified_item_id":"(O){{{cropId}}}","required_crop_display_name":"test",
              "request_state":"{{{(ready ? "ready_matching_crop_present" : "matching_crop_growing")}}}",
              "matching_crop_count":1,"ready_matching_crop_count":{{{(ready ? 1 : 0)}}},"walnut_debris_count":0,
              "matching_crops":[{"tile_x":10,"tile_y":10,"harvest_item_id":"{{{cropId}}}","current_phase":5,"final_phase_index":5,"fully_grown":false,"day_of_current_phase":0,"ready_for_request":{{{ready.ToString().ToLowerInvariant()}}}}],
              "action_tiles":[{"tile_x":4,"tile_y":3,"action":"Gourmand"}],
              "native_contract":"IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward"
            },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "golden_walnuts":{"value":{"found":0,"current":0},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "locations":{
            "collision_grid":{"value":{"location_id":"IslandFarmCave","width":16,"height":16,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-10-23T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryFile(params string[] relativeSegments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(relativeSegments).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException(string.Join(Path.DirectorySeparatorChar, relativeSegments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
