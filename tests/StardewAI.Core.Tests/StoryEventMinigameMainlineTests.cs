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

public sealed class StoryEventMinigameMainlineTests
{
    private const string HighOption = "story.advance_event_minigame";
    private const string LowOption = "executor.advance_story_event_minigame";
    private const string NativeContract =
        "live_Game1_currentMinigame_native_tick_and_event_Update_with_exact_DialogueBox_input_until_minigame_end_or_fresh_decision_without_forceQuit_manual_tick_or_direct_event_minigame_state_mutation";

    [Fact]
    public void PassiveNativeMinigameCompilesOneObserverStep()
    {
        var snapshot = Snapshot("StardewValley.Minigames.HaleyCowPictures", false);
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
        Assert.Equal("advance_story_event_minigame_passive", candidate.Kind);
        AssertParameter(candidate.Parameters, "story_event_minigame_type", "StardewValley.Minigames.HaleyCowPictures");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        Assert.Equal("advance_story_event_minigame", Assert.Single(plan.Steps).Kind);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal(LowOption, item.OptionId);
        Assert.Equal("advance_story_event_minigame", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void FantasyBoardDialogueResponsesRemainDistinctModelChoices()
    {
        var snapshot = Snapshot("StardewValley.Minigames.FantasyBoardGame", true);
        var candidates = Assert.Single(Evaluate(snapshot).Options).EventCandidates;
        Assert.Equal(2, candidates.Length);
        Assert.All(candidates, candidate => Assert.Equal("advance_story_event_minigame_choice", candidate.Kind));
        var chosen = candidates.Single(candidate =>
            ReadParameter(candidate.Parameters, "story_event_response_key") == "second");
        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(chosen) }, snapshot.StateHash);
        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "story_event_question_key", "EVD323Question");
        AssertParameter(item.NormalizedCommand.Parameters, "story_event_response_key", "second");
    }

    [Theory]
    [InlineData("StardewValley.Minigames.GrandpaStory", "new_game_player_setup")]
    [InlineData("StardewValley.Minigames.Intro", "new_game_player_setup")]
    [InlineData("StardewValley.Minigames.TelescopeScene", "deprecated_base_placeholder")]
    [InlineData("StardewValley.Minigames.AbigailGame", "other_minigame_owner")]
    public void PlayerSetupDeprecatedAndSeparatelyOwnedTypesDoNotBecomeCandidates(string type, string owner)
    {
        var snapshot = Snapshot(type, false, supported: false, owner: owner);
        Assert.Empty(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
    }

    [Fact]
    public void RuntimeEvidenceClosesHighActionButKeepsExecutorOutOfPolicyTraining()
    {
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(LowOption));
        Assert.False(PendingSemanticActionCatalog.TryGet(HighOption, out _));
        var high = OptionCapabilityRegistrySource.GetRequired(HighOption);
        var low = OptionCapabilityRegistrySource.GetRequired(LowOption);
        Assert.Equal(new[] { "EVD-323" }, high.RuntimeEvidenceIds);
        Assert.Equal(new[] { "EVD-323" }, low.RuntimeEvidenceIds);
        Assert.True(TrainingEligibilityPolicy.IsEligible(high));
        Assert.False(TrainingEligibilityPolicy.IsEligible(low));
        Assert.Contains(HighOption, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.DoesNotContain(LowOption, OptionCapabilityRegistrySource.TrainingAllowlist);
    }

    [Fact]
    public void RuntimePreservesNativeMinigameOwnershipAndBridgeKeepsAllPlaceholdersExplicit()
    {
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.StoryEventMinigame.cs"));
        var bridge = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.StoryEvent.cs"));
        Assert.Contains("active.NativeMinigame.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("NativeMinigame.tick(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("forceQuit(", runtime, StringComparison.Ordinal);
        Assert.Contains("active.NativeMinigame is not FantasyBoardGame", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("CurrentCommand =", runtime, StringComparison.Ordinal);
        Assert.All(new[]
        {
            "BoatJourney", "FantasyBoardGame", "GrandpaStory", "HaleyCowPictures", "Intro",
            "MaruComet", "PlaneFlyBy", "RobotBlastoff", "TelescopeScene"
        }, type => Assert.Contains(type, bridge, StringComparison.Ordinal));
        Assert.Contains("deprecated_telescope_scene_placeholder", bridge, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate { OptionId = HighOption, InvocationSource = OptionInvocationSource.Policy }
        }, includeExecutorCalibrationOptions: true);

    private static SnapshotEnvelope Snapshot(
        string type,
        bool question,
        bool supported = true,
        string owner = "event_script")
    {
        var responses = question
            ? new[]
            {
                new { index = 0, response_key = "first", response_text = "First", hotkey = "A" },
                new { index = 1, response_key = "second", response_text = "Second", hotkey = "B" }
            }
            : Array.Empty<object>();
        var projection = new
        {
            schema_version = "story_event.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = new string('a', 64),
            active = true,
            event_up = true,
            event_id = "EVD323Fixture",
            location_id = "Farm",
            is_festival = false,
            current_command_index = 4,
            current_command_raw = "pause 50",
            boundary_kind = "event_minigame",
            active_minigame_type = type,
            active_minigame_id = type.EndsWith("FantasyBoardGame", StringComparison.Ordinal) ? "FantasyBoardGame" : string.Empty,
            active_minigame_native_contract = NativeContract,
            active_minigame_owner_kind = owner,
            active_minigame_execution_mode = question ? "native_event_with_dialogue" : "native_passive",
            active_minigame_support_status = supported ? "supported" : "excluded",
            active_minigame_supported = supported,
            active_minigame_requires_model_response = question,
            dialogue_question_key = question ? "EVD323Question" : string.Empty,
            dialogue_responses = responses
        };
        var json = JsonSerializer.Serialize(new
        {
            player = new { location_id = Field("Farm"), story_event = Field(projection) },
            menus = new { active_menu = Field(new { is_open = question, type = question ? "DialogueBox" : "none" }) }
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
