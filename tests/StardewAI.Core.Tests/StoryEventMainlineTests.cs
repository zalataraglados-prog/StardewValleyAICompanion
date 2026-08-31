using System.Text.Json;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StoryEventMainlineTests
{
    private const string HighOption = "story.advance_event";
    private const string LowOption = "executor.advance_story_event";
    private const string NativeContract =
        "live_Event_Update_tryEventCommand_and_DialogueBox_native_input_until_event_end_or_fresh_decision_minigame_or_player_control_boundary_without_skipEvent_or_direct_event_state_mutation";

    [Fact]
    public void AutomaticBoundaryCompilesExactlyOneNativeEventStep()
    {
        var snapshot = Snapshot("automatic_progress");
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
        Assert.Equal("advance_story_event_automatic", candidate.Kind);
        AssertParameter(candidate.Parameters, "story_event_id", "EVD322Auto");
        AssertParameter(candidate.Parameters, "story_event_command_raw", "message \"hello\"");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("advance_story_event", planStep.Kind);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(LowOption, item.OptionId);
        Assert.Equal("advance_story_event", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void LiveDialogueResponsesBecomeDistinctModelChoices()
    {
        var snapshot = Snapshot("dialogue_decision");
        var candidates = Assert.Single(Evaluate(snapshot).Options).EventCandidates;
        Assert.Equal(2, candidates.Length);
        Assert.Collection(
            candidates.OrderBy(row => ReadParameter(row.Parameters, "story_event_response_index")),
            first =>
            {
                Assert.Equal("advance_story_event_choice", first.Kind);
                AssertParameter(first.Parameters, "story_event_response_index", "0");
                AssertParameter(first.Parameters, "story_event_response_key", "yes");
            },
            second =>
            {
                AssertParameter(second.Parameters, "story_event_response_index", "1");
                AssertParameter(second.Parameters, "story_event_response_key", "no");
            });

        var chosen = candidates.Single(row =>
            ReadParameter(row.Parameters, "story_event_response_key") == "no");
        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(chosen) }, snapshot.StateHash);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "story_event_question_key", "EVD322Question");
        AssertParameter(item.NormalizedCommand.Parameters, "story_event_response_key", "no");
    }

    [Fact]
    public void ProjectionOrDialogueDriftBlocksBeforeRuntime()
    {
        var original = Snapshot("dialogue_decision");
        var candidate = Assert.Single(Assert.Single(Evaluate(original).Options).EventCandidates
            .Where(row => ReadParameter(row.Parameters, "story_event_response_key") == "yes"));
        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, original.StateHash);
        var drifted = Snapshot("dialogue_decision", fingerprint: new string('b', 64), questionKey: "changed");
        plan.StateHash = drifted.StateHash;

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, drifted).Items);
        Assert.Contains("story_event_projection_drifted", item.BlockingReasons);
        Assert.Contains("story_event_dialogue_response_drifted", item.BlockingReasons);
    }

    [Theory]
    [InlineData("event_minigame", false, true, false)]
    [InlineData("player_control", false, false, true)]
    [InlineData("automatic_progress", true, false, false)]
    public void SeparateOwnersNeverLeakIntoOrdinaryStoryCandidates(
        string boundary,
        bool festival,
        bool minigame,
        bool playerControl)
    {
        var snapshot = Snapshot(boundary, festival: festival, minigame: minigame, playerControl: playerControl);
        Assert.Empty(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
    }

    [Fact]
    public void RuntimeEvidenceAdmitsOnlyThePolicyFacingStoryActionToTraining()
    {
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(LowOption));
        Assert.False(PendingSemanticActionCatalog.TryGet(HighOption, out _));
        Assert.False(PendingSemanticActionCatalog.TryGet(LowOption, out _));
        var high = OptionCapabilityRegistrySource.GetRequired(HighOption);
        var low = OptionCapabilityRegistrySource.GetRequired(LowOption);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, high.CompilerStatus);
        Assert.Equal(CapabilityCompilerStatus.StepCompilerDeclared, low.CompilerStatus);
        Assert.Equal(new[] { "EVD-322" }, high.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-322" }, low.RuntimeEvidenceIds);
        Assert.True(TrainingEligibilityPolicy.IsEligible(high));
        Assert.False(TrainingEligibilityPolicy.IsEligible(low));
        Assert.Contains(HighOption, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain(LowOption, OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void RuntimeUsesNativeUiInputWithoutDirectEventMutation()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(
            root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.StoryEvent.cs"));
        Assert.Contains("dialogue.performHoverAction", runtime, StringComparison.Ordinal);
        Assert.Contains("dialogue.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("namingMenu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("skipEvent(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentCommand =", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("eventsSeen.Add", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("selectedResponse =", runtime, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = HighOption,
                InvocationSource = OptionInvocationSource.Policy
            }
        }, includeExecutorCalibrationOptions: true);

    private static SnapshotEnvelope Snapshot(
        string boundary,
        string? fingerprint = null,
        string questionKey = "EVD322Question",
        bool festival = false,
        bool minigame = false,
        bool playerControl = false)
    {
        fingerprint ??= new string('a', 64);
        var responses = boundary == "dialogue_decision"
            ? new[]
            {
                new { index = 0, response_key = "yes", response_text = "Yes", hotkey = "Y" },
                new { index = 1, response_key = "no", response_text = "No", hotkey = "N" }
            }
            : Array.Empty<object>();
        var projection = new
        {
            schema_version = "story_event.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = fingerprint,
            native_contract = NativeContract,
            active = true,
            event_up = true,
            event_id = "EVD322Auto",
            location_id = "Farm",
            is_festival = festival,
            skipped = false,
            current_command_index = 3,
            current_command_raw = "message \"hello\"",
            boundary_kind = boundary,
            dialogue_question_key = questionKey,
            dialogue_responses = responses,
            active_minigame_type = minigame ? "StardewValley.Minigames.Intro" : string.Empty,
            player_control_sequence = playerControl
        };
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("Farm"),
                story_event = Field(projection)
            },
            menus = new
            {
                active_menu = Field(new
                {
                    is_open = boundary == "dialogue_decision",
                    type = boundary == "dialogue_decision" ? "DialogueBox" : "none"
                })
            }
        }, JsonOptions);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-31T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = HighOption,
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static string ReadParameter(SmallModelActionParameter[] parameters, string name) =>
        parameters.First(row => row.Name == name).Value;

    private static void AssertParameter(SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, row => row.Name == name && row.Value == value);

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

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
