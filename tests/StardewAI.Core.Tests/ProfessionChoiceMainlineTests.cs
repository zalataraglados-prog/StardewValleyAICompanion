using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class ProfessionChoiceMainlineTests
{
    [Fact]
    public void ExactOfferedProfessionsUseOneSharedLevelUpExecutionPath()
    {
        var snapshot = Snapshot(LevelUpState(
            "[{\"profession_id\":0,\"title\":\"Rancher\",\"description_lines\":[\"Animal products worth 20% more.\"]},{\"profession_id\":1,\"title\":\"Tiller\",\"description_lines\":[\"Crops worth 10% more.\"]}]"));
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot,
            new[] { "skills.choose_profession", "recovery.stabilize_day" },
            includeExecutorCalibrationOptions: true);
        var professionCandidates = availability.Options
            .Single(option => option.OptionId == "skills.choose_profession")
            .EventCandidates;

        Assert.Equal(2, professionCandidates.Length);
        Assert.All(professionCandidates, candidate => Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons)));
        Assert.Equal(new[] { 0, 1 }, professionCandidates
            .Select(candidate => int.Parse(candidate.Parameters.Single(parameter => parameter.Name == "profession_choice_id").Value))
            .OrderBy(id => id));
        Assert.All(professionCandidates, candidate => Assert.Contains(candidate.Parameters, parameter =>
            parameter.Name == "execution_option_id" && parameter.Value == "executor.close_menu"));

        var ranked = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        Assert.Equal(2, ranked.Length);
        Assert.All(ranked, candidate => Assert.Equal("skills.choose_profession", candidate.OptionId));
        Assert.Equal("1", ranked[0].Parameters.Single(parameter => parameter.Name == "profession_choice_id").Value);

        var plan = new DailyPlanCompiler().Compile(ranked, snapshot.StateHash);
        var professionStep = Assert.Single(plan.Steps);
        Assert.Equal("close_menu", professionStep.Kind);
        Assert.Contains(professionStep.SafetyConstraints, value => value == "never_create_second_profession_executor");
        Assert.Contains(plan.CandidateAudit, row => row.Decision == "skipped" &&
            row.Reasons.Contains("daily_plan_mutually_exclusive_decision_already_reserved"));

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("executor.close_menu", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Contains(item.NormalizedCommand.Parameters, parameter => parameter.Name == "profession_choice_id" && parameter.Value == "1");
        Assert.Equal("close_menu", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void IncompleteProfessionProjectionFailsClosedUpstream()
    {
        var snapshot = Snapshot(LevelUpState("[]"));
        var candidates = new CandidateOptionAvailabilityEvaluator().Evaluate(
                snapshot,
                new[] { "skills.choose_profession" },
                includeExecutorCalibrationOptions: true)
            .Options.Single().EventCandidates;

        var blocked = Assert.Single(candidates);
        Assert.False(blocked.Available);
        Assert.Contains("exactly_two_profession_choices_required", blocked.BlockReasons);
    }

    [Fact]
    public void ProfessionSemanticDoesNotIntroduceAnotherRuntimeExecutor()
    {
        var root = FindRepositoryRoot();
        var semantic = File.ReadAllText(Path.Combine(root, "src", "StardewAI.Core", "Training", "DailyPlanCompiler.Professions.cs"));
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.Farming.cs"));

        Assert.Contains("reuse_executor.close_menu_level_up_path", semantic, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.player.professions.Add", semantic, StringComparison.Ordinal);
        Assert.Equal(1, Count(runtime, "private TrainingExecutionResult ExecuteLevelUpMenu("));
    }

    [Fact]
    public void TransparentBridgePublishesOfferedAndPersistentProfessionState()
    {
        var root = FindRepositoryRoot();
        var menu = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "MenuReadAdapter.cs"));
        var skills = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.Skills.cs"));

        Assert.Contains("profession_id = id", menu, StringComparison.Ordinal);
        Assert.Contains("description_lines = LevelUpMenu.getProfessionDescription(id).ToArray()", menu, StringComparison.Ordinal);
        Assert.Contains("profession_ids = player.professions", skills, StringComparison.Ordinal);
        Assert.Contains("new_levels = player.newLevels", skills, StringComparison.Ordinal);
        Assert.Contains("LevelUpMenu.getProfessionDescription(id).ToArray()", skills, StringComparison.Ordinal);
    }

    private static string LevelUpState(string choices)
    {
        return $$$"""
        {
          "player": {
            "location_id":{"value":"FarmHouse","status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "skills_detail":{"value":{"skills":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "menus": {
            "active_menu":{"value":{"is_open":true,"type":"LevelUpMenu","is_sleep_prompt":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "sleep_prompt_context":{"value":{"prompt_open":false},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
            "menu_specific_state":{"value":{"kind":"level_up","information_up":true,"is_active":true,"is_profession_chooser":true,"has_updated_professions":true,"can_receive_input":true,"current_skill":0,"current_level":5,"timer_before_start":0,"reflection_fields_complete":true,"profession_choices":{{{choices}}}},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
          },
          "time":{"time":{"value":600,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}}
        }
        """;
    }

    private static SnapshotEnvelope Snapshot(string json)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-11T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static int Count(string value, string token)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += token.Length;
        }
        return count;
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "StardewValleyAICompanion.sln")))
                return current.FullName;
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
