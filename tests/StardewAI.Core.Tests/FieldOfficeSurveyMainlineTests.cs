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

public sealed class FieldOfficeSurveyMainlineTests
{
    [Fact]
    public void ExactFlowerSurveyFlowsFromTransparentCandidateToNativeExecutor()
    {
        var snapshot = Snapshot("purple_flower");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "island.field_office_survey" }, true);
        var option = Assert.Single(availability.Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("answer_field_office_survey", candidate.Kind);
        AssertParameter(candidate.Parameters, "survey_kind", "purple_flower");
        AssertParameter(candidate.Parameters, "survey_answer", "22");
        AssertParameter(candidate.Parameters, "survey_answer_question_key", "PurpleFlowerSurvey");
        AssertParameter(candidate.Parameters, "expected_collected_nut_key", "IslandLeftPlantRestored");
        AssertParameter(candidate.Parameters, "walnut_debris_spawn_count", "1");
        AssertParameter(candidate.Parameters, "golden_walnuts_found_after", "1");

        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("answer_field_office_survey", step.Kind);
        Assert.Contains("native_FieldOfficeSurvey_action_only", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.answer_field_office_survey", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("answer_field_office_survey", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void FailedSurveyTodayIsExcludedUpstream()
    {
        var snapshot = Snapshot("purple_flower", failedToday: true, actionStatus: "failed_survey_today");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "island.field_office_survey" }, true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);

        Assert.False(candidate.Available);
        Assert.False(candidate.AllowedNow);
        Assert.Contains("failed_survey_today", candidate.BlockReasons);
        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        Assert.DoesNotContain(ranked, value => value.OptionId == "island.field_office_survey");
    }

    [Fact]
    public void FreshCompilerRejectsSurveyStateDrift()
    {
        var original = Snapshot("purple_flower");
        var availability = new CandidateOptionAvailabilityEvaluator()
            .Evaluate(original, new[] { "island.field_office_survey" }, true);
        var plan = new DailyPlanCompiler().Compile(
            new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), original.StateHash);
        var drifted = Snapshot("purple_starfish", plantsRestoredLeft: true);
        plan.StateHash = drifted.StateHash;

        var queue = new ActionQueueCompiler().Compile(plan, drifted);

        Assert.Equal("blocked", queue.Status);
        Assert.Contains("field_office_survey_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void RollingRouteContinuationLocksSurveyIdentityAndAnswer()
    {
        var routeItem = JsonNode.Parse("""
        {"option_id":"executor.traverse_connector","normalized_command":{"parameters":[
          {"name":"continuation.option_id","value":"island.field_office_survey"},
          {"name":"continuation.survey_kind","value":"purple_starfish"},
          {"name":"continuation.answer","value":"18"}
        ]}}
        """)!.AsObject();
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(routeItem);
        Assert.Equal("field_office_survey", continuation!["kind"]!.GetValue<string>());

        var ranked = JsonNode.Parse("""
        [{"option_id":"island.field_office_survey","parameters":[
          {"name":"survey_kind","value":"purple_starfish"},{"name":"survey_answer","value":"18"}
        ]},{"option_id":"island.field_office_survey","parameters":[
          {"name":"survey_kind","value":"purple_flower"},{"name":"survey_answer","value":"22"}
        ]}]
        """)!.AsArray();
        Assert.Single(QueueReplanFilter.FilterRankedCandidates(ranked, continuation));

        var terminal = JsonNode.Parse("""
        {"option_id":"executor.answer_field_office_survey","normalized_command":{"parameters":[
          {"name":"survey_kind","value":"purple_starfish"},{"name":"survey_answer","value":"18"}
        ]}}
        """)!.AsObject();
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(terminal, continuation, "applied"));
    }

    [Fact]
    public void CapabilityAndRuntimeSourcesOwnOneNativeMutationPath()
    {
        foreach (var optionId in new[] { "island.field_office_survey", "executor.answer_field_office_survey" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-303" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-303" }, capability.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-303" }, capability.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-303" }, capability.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-303" }, capability.OutputEvidenceIds);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }

        var runtime = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FieldOfficeSurvey.cs"));
        var validation = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.FieldOfficeSurvey.Validation.cs"));
        var production = runtime + validation;
        var bridge = File.ReadAllText(FindRepositoryFile(
            "src", "StardewAI.TransparentBridge", "Adapters", "ProgressReadAdapter.IslandFieldOffice.cs"));
        Assert.Contains(".checkAction(", production, StringComparison.Ordinal);
        Assert.Contains(".answerDialogue(", production, StringComparison.Ordinal);
        Assert.Contains("FieldOfficeSurvey", production, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"plantsRestoredLeft\.Value\s*=(?!=)"), production);
        Assert.DoesNotMatch(new Regex(@"plantsRestoredRight\.Value\s*=(?!=)"), production);
        Assert.DoesNotMatch(new Regex(@"hasFailedSurveyToday\.Value\s*=(?!=)"), production);
        Assert.DoesNotContain("debris.Add", production, StringComparison.Ordinal);
        Assert.DoesNotContain("MarkCollectedNut", production, StringComparison.Ordinal);
        Assert.DoesNotContain("answerDialogueAction", production, StringComparison.Ordinal);
        Assert.Contains("field_office_existing_walnut_debris_requires_pickup", bridge, StringComparison.Ordinal);
        Assert.Contains("native_debris_spawn_then_magnet_pickup_to_golden_walnuts_found", bridge, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(
        string surveyKind,
        bool failedToday = false,
        string actionStatus = "ready",
        bool plantsRestoredLeft = false)
    {
        var flower = surveyKind == "purple_flower";
        var answer = flower ? 22 : 18;
        var minimum = flower ? 18 : 11;
        var maximum = flower ? 24 : 18;
        var question = flower ? "PurpleFlowerSurvey" : "PurpleStarfishSurvey";
        var nutKey = flower ? "IslandLeftPlantRestored" : "IslandRightPlantRestored";
        var json = $$$"""
        {
          "player":{
            "location_id":{"value":"IslandFieldOffice","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_x":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "tile_y":{"value":4,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "world_progress":{"island_field_office":{"value":{
            "location_id":"IslandFieldOffice","is_current_location":true,"north_cave_opened":true,
            "professor_available":true,"intro_received_or_pending":true,"mutex_locked":false,"menu_clear":true,
            "desk_action_tiles":[{"tile_x":7,"tile_y":7,"action_raw":"FieldOfficeDesk"}],
            "survey_action_tiles":[{"tile_x":4,"tile_y":3,"action_raw":"FieldOfficeSurvey"}],
            "pieces":[],"donated_piece_count":0,"center_skeleton_restored":false,
            "snake_restored":false,"bat_restored":false,"frog_restored":false,
            "plants_restored_left":{{{plantsRestoredLeft.ToString().ToLowerInvariant()}}},"plants_restored_right":false,
            "has_failed_survey_today":{{{failedToday.ToString().ToLowerInvariant()}}},
            "next_survey_kind":"{{{surveyKind}}}","next_survey_answer":{{{answer}}},"finale_ready":false,
            "finale_received_or_pending":false,"golden_walnuts_found":0,"uncollected_rewards":[],"donation_candidates":[],
            "survey_candidates":[{
              "survey_kind":"{{{surveyKind}}}","answer":{{{answer}}},"answer_minimum":{{{minimum}}},"answer_maximum":{{{maximum}}},
              "prompt_question_key":"Survey","prompt_response_key":"Yes","answer_question_key":"{{{question}}}","answer_response_key":"Correct",
              "plant_restored_before":false,"plant_restored_after":true,
              "failed_survey_today_before":{{{failedToday.ToString().ToLowerInvariant()}}},"failed_survey_today_after":false,
              "expected_collected_nut_key":"{{{nutKey}}}","collected_nut_before":false,
              "walnut_debris_count_before":0,"walnut_debris_count_after":0,"walnut_debris_spawn_count":1,
              "golden_walnuts_found_before":0,"golden_walnuts_found_after":1,"golden_walnuts_found_delta":1,
              "output_delivery":"native_debris_spawn_then_magnet_pickup_to_golden_walnuts_found",
              "expected_finale_ready_after":false,"expected_finale_trigger_after":false,"action_status":"{{{actionStatus}}}"
            }],"projection_status":"exact_locked_base_1.6.15"
          },"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
          "golden_walnuts":{"value":{"found":0,"current":0},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "current_location":{"debris":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "locations":{
            "collision_grid":{"value":{"location_id":"IslandFieldOffice","width":16,"height":16,"notable_tiles":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "route_connectors":{"value":[],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}},
          "menus":{"active_menu":{"value":{"is_open":false,"type":"none"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static void AssertParameter(
        StardewAI.Contracts.Execution.SmallModelActionParameter[] parameters,
        string name,
        string value) =>
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
