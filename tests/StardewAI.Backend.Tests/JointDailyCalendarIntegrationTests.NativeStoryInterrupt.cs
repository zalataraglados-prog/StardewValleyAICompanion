using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed partial class JointDailyCalendarIntegrationTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("$q")]
    [InlineData("$y")]
    public async Task MidnightStoryUsesTheProductionInterruptWithoutOpeningTheOrdinaryDay(string? question)
    {
        await using var app = new WebApplicationFactory<global::Program>();
        using var http = app.CreateClient();
        var snapshot = Snapshot(new { time = new { value = 2400, status = "available" } });
        snapshot.State["player"] = JsonSerializer.SerializeToElement(new
        {
            energy = new { status = "available", value = 270 },
            location_id = new { status = "available", value = "Town" },
            story_event = new
            {
                status = "available",
                value = new
                {
                    projection_status = "complete_locked_base_1.6.15",
                    projection_fingerprint = new string('a', 64),
                    native_contract = "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation",
                    active = true, event_up = true, is_festival = false, skipped = false,
                    event_id = "502969", location_id = "Town", current_command_index = 66,
                    current_command_resolved_name = question is null ? "Message" : "Speak",
                    current_command_raw = question is null ? "message \"hello\"" : $"speak Linus \"{question} 'A question'\"",
                    boundary_kind = question is null ? "automatic_progress" : "dialogue_decision",
                    dialogue_is_question = question is not null, dialogue_question_key = "",
                    dialogue_responses = new[] { new { index = 0, response_key = "quickResponse1", response_text = "Answer" } },
                    player_control_sequence = false, active_minigame_type = ""
                }
            }
        });
        snapshot.State["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = new { is_open = question is not null, type = question is null ? "none" : "DialogueBox" } }
        });
        var store = app.Services.GetRequiredService<StateStore>();
        store.Snapshots[snapshot.StateHash] = snapshot;

        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("BuildPriorityStoryEventQueueAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var options = LiveTrainingOptions.Parse(["--no-manifest", "--use-daily-plan", "--joint-daily-calendar"]);
        options.BackendUrl = http.BaseAddress!.GetLeftPart(UriPartial.Authority);
        var pending = Assert.IsAssignableFrom<Task<(JsonObject, JsonObject Plan, JsonObject Queue, JsonObject Ranking)>>(
            method.Invoke(null, [http, options, snapshot.StateHash]));
        var (_, plan, queue, ranking) = await pending;
        Assert.True(queue["status"]!.GetValue<string>() == "pending",
            $"availability: {ranking["availability"]?["options"]}\naudit: {plan["candidate_audit"]}\nqueue: {queue}");
        var step = Assert.Single(plan["steps"]!.AsArray())!;
        Assert.Equal("advance_story_event", step["kind"]!.GetValue<string>());
        Assert.Equal(0, step["estimated_minutes"]!.GetValue<int>());
        var item = Assert.Single(queue["items"]!.AsArray())!;
        Assert.Equal("executor.advance_story_event", item["option_id"]!.GetValue<string>());
        var normalized = item["normalized_command"]!;
        Assert.Equal(14400, Assert.Single(normalized["steps"]!.AsArray())!["estimated_ticks"]!.GetValue<int>());
        if (question is not null)
            Assert.Contains(normalized["parameters"]!.AsArray(), parameter =>
                parameter!["name"]!.GetValue<string>() == "story_event_response_key" &&
                parameter["value"]!.GetValue<string>() == "quickResponse1");
        await VerifyMidnightStoryDispatch(options, snapshot, queue, item.AsObject(), question is not null);

        using var ordinaryRank = await http.PostAsJsonAsync("/api/v1/planner/baseline/rank-options", new BaselinePredictionRequest
        {
            StateHash = snapshot.StateHash, CandidateOptionIds = ["story.advance_event"], TrainingReport = new()
        });
        Assert.Equal(HttpStatusCode.OK, ordinaryRank.StatusCode);
        var ordinary = await ordinaryRank.Content.ReadFromJsonAsync<AvailabilityAwarePolicyPredictionEnvelope>();
        Assert.Equal("recovery.stabilize_day", Assert.Single(ordinary!.Availability.Options).OptionId);

        var candidates = ranking["ranked_event_candidates"]!.Deserialize<PolicyEventCandidatePrediction[]>(Json)!;
        using var ordinaryCompile = await http.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new DailyPlanCompileRequest
        {
            StateHash = snapshot.StateHash, UseJointCalendar = true, RankedEventCandidates = candidates
        });
        Assert.Equal(HttpStatusCode.OK, ordinaryCompile.StatusCode);
        var ordinaryPlan = JsonNode.Parse(await ordinaryCompile.Content.ReadAsStringAsync())!["plan"]!;
        Assert.Empty(ordinaryPlan["steps"]!.AsArray());

        using var mixedCompile = await http.PostAsJsonAsync("/api/v1/planner/daily-plan/compile", new DailyPlanCompileRequest
        {
            StateHash = snapshot.StateHash, UseJointCalendar = true, PriorityStoryEventInterrupt = true,
            CompileActionQueue = true, RankedEventCandidates = [.. candidates, Work("unrelated", 1, 42)]
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, mixedCompile.StatusCode);
        Assert.Single(store.ActionQueues);
    }

    private static async Task VerifyMidnightStoryDispatch(
        LiveTrainingOptions options, SnapshotEnvelope snapshot, JsonObject queue, JsonObject item, bool decision)
    {
        var method = typeof(LiveTrainingOptions).Assembly.GetType("Program", true)!
            .GetMethod("ReadDispatchRecoveryOverrideAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        using var bridge = new MidnightDispatchBridge(JsonSerializer.SerializeToNode(snapshot, Json)!.AsObject());
        using var http = new HttpClient(bridge);
        async Task<(string Json, JsonObject Snapshot)?> Check(JsonObject dispatchItem) =>
            await (Task<(string Json, JsonObject Snapshot)?>)method.Invoke(null,
                [http, options, queue, dispatchItem, null, null])!;

        Assert.Null(await Check(item));
        var unrelated = (JsonObject)item.DeepClone();
        unrelated["option_id"] = "executor.move_to_tile";
        Assert.NotNull(await Check(unrelated));

        var liveEvent = bridge.Fresh["state"]!["player"]!["story_event"]!["value"]!;
        if (decision)
            liveEvent["dialogue_responses"]![0]!["response_key"] = "anotherResponse";
        else
            liveEvent["current_command_index"] = 67;
        Assert.NotNull(await Check(item));
        Assert.Equal(3, bridge.FullSnapshotReads);
    }

    private sealed class MidnightDispatchBridge(JsonObject fresh) : HttpMessageHandler
    {
        internal JsonObject Fresh => fresh;
        internal int FullSnapshotReads { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Get, request.Method);
            var clock = request.RequestUri!.Query.Contains("profile=clock", StringComparison.Ordinal);
            if (!clock) FullSnapshotReads++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(clock ? "{\"in_game_time\":{\"value\":2400}}" : fresh.ToJsonString())
            });
        }
    }
}
