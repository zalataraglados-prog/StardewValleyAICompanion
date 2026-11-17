using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData("automatic_progress")]
    [InlineData("dialogue_decision")]
    public async Task ManagedStoryControlPreparesOriginalSelectedWorkWithoutCropCredit(string boundary)
    {
        using var f = new OwnedRouteFixture(productExecutor: true,
            prepareSource: source => SetManagedStorySource(source, boundary));
        f.Runtime.LiveOptions.Goal = "goal.grandpa_max_score_year3";
        f.Runtime.LiveOptions.UseJointDailyCalendar = true;
        await using var owner = await f.Files.Open();
        var originalHead = owner.CurrentSnapshotPath;
        var originalLedger = File.ReadAllBytes(owner.CurrentLedgerPath);
        var operation = await BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step,
            inspectBuild: build =>
            {
                Assert.Equal("priority_story_event", build.GetType().GetProperty("InterruptKind")!.GetValue(build));
                Assert.Equal("full", build.GetType().GetProperty("SnapshotProfile")!.GetValue(build));
                var queue = (JsonObject)build.GetType().GetProperty("Queue")!.GetValue(build)!;
                Assert.Equal("executor.advance_story_event", Assert.Single(queue["items"]!.AsArray())!["option_id"]!.GetValue<string>());
            });
        Assert.NotNull(operation);
        Assert.True(operation.IsSharedSourceReturn);
        Assert.False(operation.IsControlRecovery);
        Assert.False(operation.IsSupportingRoute);
        Assert.Same(operation, owner.Pending);
        Assert.Equal(originalHead, owner.CurrentSnapshotPath);
        Assert.Equal(originalLedger, File.ReadAllBytes(owner.CurrentLedgerPath));
        Assert.Equal(0, f.Runtime.Handler.Commits);
        Assert.False(File.Exists(Path.Combine(operation.RuntimeDirectory, "shared-source-native-claim.json")));
        var intent = JsonNode.Parse(File.ReadAllText(Path.Combine(f.Files.OwnerRoot,
            $"operation-{operation.Sequence:D6}-intent.json")))!;
        Assert.Equal("selected_native_work_yield.v1", intent["shared_source_execution_protocol"]!.GetValue<string>());
        var selected = intent["shared_source_selection"]!.Deserialize<CropTeacherSharedSourceSelection>(JsonOptions)!;
        Assert.Null(selected.DecisionReference);
        Assert.Equal(originalHead, selected.DecisionSnapshotPath);
        var plan = LifecycleRead<SmallModelPlanEnvelope>(selected.DecisionPlanPath);
        Assert.NotEmpty(plan.Steps);
        Assert.All(plan.Steps, step => Assert.Equal("advance_story_event", step.Kind));
        var executable = LifecycleRead<SmallModelPlanEnvelope>(selected.ModelPlanPath);
        Assert.Equal("advance_story_event", Assert.Single(executable.Steps).Kind);
        var ranking = JsonNode.Parse(File.ReadAllText(selected.DecisionRankingPath))!;
        var choices = ranking["ranked_event_candidates"]!.AsArray().Where(row =>
            row!["option_id"]!.GetValue<string>() == "story.advance_event").ToArray();
        Assert.Equal(boundary == "dialogue_decision" ? 2 : 1, choices.Length);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            BuildManagedOperation(f.Runtime, f.Inputs, owner, f.Step, 2));
    }

    private static void SetManagedStorySource(Fixture source, string boundary)
    {
        if (!source.Before.State.ContainsKey("menus"))
            source.Before.State["menus"] = JsonSerializer.SerializeToElement(new JsonObject());
        ChangeState(source.Before, "player", player => player["energy"] =
            JsonSerializer.SerializeToNode(new { status = "available", confidence = 1d, value = 270 }, JsonOptions));
        var location = source.Before.State["player"].GetProperty("location_id").GetProperty("value").GetString();
        ChangeState(source.Before, "player", player => player["story_event"] = JsonSerializer.SerializeToNode(new
        {
            status = "available", value = new
            {
                schema_version = "story_event.v1", projection_status = "complete_locked_base_1.6.15",
                projection_fingerprint = new string('a', 64),
                native_contract = "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation",
                active = true, event_up = true, event_id = "declared-managed-story", location_id = location,
                is_festival = false, skipped = false, current_command_index = 3,
                current_command_raw = "message \"declared story\"", boundary_kind = boundary,
                dialogue_question_key = "declared-managed-question",
                dialogue_responses = boundary == "dialogue_decision" ? new object[]
                {
                    new { index = 0, response_key = "yes", response_text = "Yes", hotkey = "Y" },
                    new { index = 1, response_key = "no", response_text = "No", hotkey = "N" }
                } : Array.Empty<object>(),
                active_minigame_type = "", player_control_sequence = false
            }
        }, JsonOptions));
        ChangeState(source.Before, "menus", menus => menus["active_menu"] = JsonSerializer.SerializeToNode(new
        {
            status = "available", value = new
            {
                is_open = boundary == "dialogue_decision", type = boundary == "dialogue_decision" ? "DialogueBox" : "none"
            }
        }, JsonOptions));
    }
}
