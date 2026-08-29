using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class FieldOfficeDonationMainlineTests
{
    [Fact]
    public void ExactFossilFlowsFromTransparentCandidateToNativeExecutor()
    {
        var snapshot = Snapshot(pieceDonated: false, donatedCount: 5);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "island.field_office_donate" }, true);
        var option = Assert.Single(availability.Options);
        Assert.True(option.EventCandidates.Length == 1,
            "missing=" + string.Join(",", option.MissingStateFactors) +
            "; blocking=" + string.Join(",", option.BlockingReasons));
        var candidate = option.EventCandidates[0];

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("donate_field_office_piece", candidate.Kind);
        Assert.Equal("(O)823", candidate.QualifiedItemId);
        AssertParameter(candidate.Parameters, "target_piece_index", "0");
        AssertParameter(candidate.Parameters, "expected_completes_set", "true");
        AssertParameter(candidate.Parameters, "expected_collected_nut_key", "IslandCenterSkeletonRestored");
        AssertParameter(candidate.Parameters, "uncollected_rewards_after_json",
            "[{\"qualified_item_id\":\"(O)73\",\"stack\":6,\"quality\":0},{\"qualified_item_id\":\"(O)69\",\"stack\":1,\"quality\":0}]");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("donate_field_office_piece", step.Kind);
        Assert.Contains("native_FieldOfficeMenu_inventory_and_exact_holder_click_only", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.donate_field_office_piece", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("donate_field_office_piece", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void FreshCompilerRejectsPieceStateDrift()
    {
        var original = Snapshot(pieceDonated: false, donatedCount: 5);
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "island.field_office_donate" }, true);
        var option = Assert.Single(availability.Options);
        Assert.True(option.EventCandidates.Length == 1,
            "missing=" + string.Join(",", option.MissingStateFactors) +
            "; blocking=" + string.Join(",", option.BlockingReasons));
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot(pieceDonated: false, donatedCount: 6);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("field_office_donation_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RollingRouteContinuationLocksInventoryItemAndPieceIdentity()
    {
        var routeItem = JsonNode.Parse("""
        {"option_id":"executor.traverse_connector","normalized_command":{"parameters":[
          {"name":"continuation.option_id","value":"island.field_office_donate"},
          {"name":"continuation.inventory_slot_index","value":"11"},
          {"name":"continuation.qualified_item_id","value":"(O)826"},
          {"name":"continuation.target_piece_index","value":"7"},
          {"name":"continuation.confirm_donation","value":"true"}
        ]}}
        """)!.AsObject();
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(routeItem);
        Assert.Equal("field_office_donation", continuation!["kind"]!.GetValue<string>());

        var ranked = JsonNode.Parse("""
        [{"option_id":"island.field_office_donate","parameters":[
          {"name":"inventory_slot_index","value":"11"},
          {"name":"qualified_item_id","value":"(O)826"},
          {"name":"target_piece_index","value":"7"},
          {"name":"confirm_donation","value":"true"}
        ]},{"option_id":"island.field_office_donate","parameters":[
          {"name":"inventory_slot_index","value":"11"},
          {"name":"qualified_item_id","value":"(O)826"},
          {"name":"target_piece_index","value":"6"},
          {"name":"confirm_donation","value":"true"}
        ]}]
        """)!.AsArray();
        var filtered = QueueReplanFilter.FilterRankedCandidates(ranked, continuation);
        Assert.Single(filtered);

        var terminal = JsonNode.Parse("""
        {"option_id":"executor.donate_field_office_piece","normalized_command":{"parameters":[
          {"name":"inventory_slot_index","value":"11"},
          {"name":"qualified_item_id","value":"(O)826"},
          {"name":"target_piece_index","value":"7"}
        ]}}
        """)!.AsObject();
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    [Fact]
    public void CapabilityAndRuntimeSourcesOwnOneNativeMutationPath()
    {
        foreach (var optionId in new[] { "island.field_office_donate", "executor.donate_field_office_piece" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-302" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-302" }, capability.RuntimeEvidenceIds);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(PendingSemanticActionCatalog.TryGet("island.field_office_survey", out var survey));
        Assert.Equal("engine.interaction_menu", survey.PrimaryEngineId);

        var runtime = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "ModEntry.FieldOffice.cs"));
        Assert.Contains("FieldOfficeDesk", runtime, StringComparison.Ordinal);
        Assert.Contains("answerDialogue(response)", runtime, StringComparison.Ordinal);
        Assert.Contains("FieldOfficeMenu", runtime, StringComparison.Ordinal);
        Assert.Contains("pieceHolders", runtime, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("QuestionWaitTicks", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("active.ElapsedTicks < 240", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("piecesDonated[piece] =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("uncollectedRewards.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkCollectedNut", runtime, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(bool pieceDonated, int donatedCount)
    {
        var candidateRows = pieceDonated ? "[]" : """
        [{"slot_index":0,"item_id":"823","qualified_item_id":"(O)823","runtime_type":"StardewValley.Object",
          "stack_before":2,"stack_after":1,"target_piece_index":0,"target_piece_kind":"skeleton_back_leg",
          "target_set_kind":"center_skeleton","donated_piece_count_before":5,"donated_piece_count_after":6,
          "completes_set":true,
          "new_reward_items":[{"qualified_item_id":"(O)73","stack":6,"quality":0},{"qualified_item_id":"(O)69","stack":1,"quality":0}],
          "uncollected_rewards_before":[],
          "uncollected_rewards_after":[{"qualified_item_id":"(O)73","stack":6,"quality":0},{"qualified_item_id":"(O)69","stack":1,"quality":0}],
          "expected_collected_nut_key":"IslandCenterSkeletonRestored","collected_nut_before":false,
          "expected_finale_ready_after":false,"action_status":"ready"}]
        """;
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"IslandFieldOffice","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":5,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":8,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "inventory":{"value":[{"slot_index":0,"item_id":"823","qualified_item_id":"(O)823","stack":2}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress":{"island_field_office":{"value":{
            "location_id":"IslandFieldOffice","is_current_location":true,"north_cave_opened":true,
            "professor_available":true,"intro_received_or_pending":true,"mutex_locked":false,"menu_clear":true,
            "desk_action_tiles":[{"tile_x":7,"tile_y":7,"action_raw":"FieldOfficeDesk"}],
            "survey_action_tiles":[{"tile_x":4,"tile_y":3,"action_raw":"FieldOfficeSurvey"}],
            "pieces":[],"donated_piece_count":{{{donatedCount}}},"center_skeleton_restored":false,
            "snake_restored":false,"bat_restored":false,"frog_restored":false,
            "plants_restored_left":false,"plants_restored_right":false,"has_failed_survey_today":false,
            "next_survey_kind":"purple_flower","next_survey_answer":22,"finale_ready":false,
            "finale_received_or_pending":false,"golden_walnuts_found":0,"uncollected_rewards":[],
            "donation_candidates":{{{candidateRows}}},"projection_status":"exact_locked_base_1.6.15"
          },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
          "golden_walnuts":{"value":{"found":0,"current":0},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "locations":{
            "collision_grid":{"value":{"location_id":"IslandFieldOffice","width":16,"height":16,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryFile(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(new[] { directory.FullName }.Concat(parts).ToArray());
            if (File.Exists(candidate))
                return candidate;
            directory = directory.Parent;
        }
        throw new FileNotFoundException("Repository file not found.", Path.Combine(parts));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
