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

public sealed class PlayerEmoteMainlineTests
{
    private const string OptionId = "social.emote";
    private const string NativeContract =
        "EmoteMenu.ConfirmSelection->ChatBox.textBoxEnter('/emote '+key)->ChatCommands.Emote->Farmer.CanEmote->Farmer.netDoEmote->doEmoteEvent->Farmer.performPlayerEmote->performedEmotes_and_native_icon_or_animation";

    [Theory]
    [InlineData("happy", false)]
    [InlineData("blush", true)]
    [InlineData("yes", false)]
    [InlineData("jar", true)]
    public void ExplicitIntentCompilesVisibleHiddenIconAndAnimationBranches(string key, bool hidden)
    {
        var snapshot = Snapshot();
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot, key, true).Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("perform_emote", candidate.Kind);
        AssertParameter(candidate.Parameters, "emote_hidden", hidden.ToString().ToLowerInvariant());

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("perform_emote", planStep.Kind);
        Assert.Contains("player_command_only_and_excluded_from_autonomous_candidates_and_strategy_training", planStep.SafetyConstraints);

        var item = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.perform_emote", item.OptionId);
        Assert.Equal("perform_emote", Assert.Single(item.NormalizedCommand.Steps).StepType);
        AssertParameter(item.NormalizedCommand.Parameters, "emote_native_input", "/emote " + key);
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void MissingConfirmationUnknownKeyAndIncompleteCatalogAreExcludedUpstream()
    {
        Assert.Empty(Assert.Single(Evaluate(Snapshot(), "happy", false).Options).EventCandidates);
        Assert.Empty(Assert.Single(Evaluate(Snapshot(), "not_native", true).Options).EventCandidates);

        var incomplete = Snapshot(PlayerEmoteIdentity.LockedBaseEmoteKeys.Take(21).ToArray());
        Assert.Empty(Assert.Single(Evaluate(incomplete, "happy", true).Options).EventCandidates);
    }

    [Fact]
    public void CompilerRebindsForgedRuntimeFieldsFromFreshTransparentProjection()
    {
        var snapshot = Snapshot();
        var action = Action(snapshot, new[]
        {
            P("emote_key", "yes"), P("emote_reason", "explicit test"), P("confirm_emote", "true"),
            P("emote_projection_fingerprint", new string('f', 64)), P("emote_option_fingerprint", new string('e', 64)),
            P("emote_index", "999"), P("emote_icon_index", "999"), P("emote_native_input", "/emote forged"),
            P("native_contract", "forged")
        });
        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "emote_index", "12");
        AssertParameter(item.NormalizedCommand.Parameters, "emote_icon_index", "56");
        AssertParameter(item.NormalizedCommand.Parameters, "emote_native_input", "/emote yes");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void GovernanceTransportAndRuntimeRemainPlayerCommandOnlyAndNative()
    {
        foreach (var optionId in new[] { OptionId, "executor.perform_emote" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-320" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-320" }, capability.RuntimeEvidenceIds);
            Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, capability.InvocationPolicy);
            Assert.True(capability.PlayerConfirmationRequired);
            Assert.False(capability.AutonomousCandidateEnabled);
            Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.perform_emote"));

        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.PlayerEmote.cs"));
        Assert.Contains("chat.activate();", runtime, StringComparison.Ordinal);
        Assert.Contains("chat.chatBox.RecieveTextInput(character);", runtime, StringComparison.Ordinal);
        Assert.Contains("chat.textBoxEnter(chat.chatBox);", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain(".netDoEmote(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain(".performPlayerEmote(", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain(".doEmote(", runtime, StringComparison.Ordinal);

        var adapter = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.Emote.cs"));
        Assert.DoesNotContain("GetEmoteFavorites(", adapter, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRequestAndReceiptFieldsRoundTrip()
    {
        var request = new TrainingExecutionRequest
        {
            EmoteKey = "yes", EmoteReason = "explicit test", ConfirmEmote = true,
            EmoteIndex = 12, EmoteIconIndex = 56, EmoteHasAnimation = true,
            EmoteProjectionFingerprint = new string('a', 64), EmoteOptionFingerprint = new string('b', 64)
        };
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(request, JsonOptions), JsonOptions)!;
        Assert.Equal("yes", roundTrip.EmoteKey);
        Assert.True(roundTrip.EmoteHasAnimation);

        var result = new TrainingExecutionResult { EmoteKey = "yes", EmoteNativeCommandReceiptVerified = true };
        var receipt = JsonSerializer.Deserialize<TrainingExecutionResult>(JsonSerializer.Serialize(result, JsonOptions), JsonOptions)!;
        Assert.True(receipt.EmoteNativeCommandReceiptVerified);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot, string key, bool confirm) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId, InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = confirm,
                Parameters = new[] { P("emote_key", key), P("emote_reason", "explicit test"), P("confirm_emote", confirm.ToString().ToLowerInvariant()) }
            }
        }, true);

    private static SnapshotEnvelope Snapshot(string[]? keys = null)
    {
        keys ??= PlayerEmoteIdentity.LockedBaseEmoteKeys.ToArray();
        var hidden = PlayerEmoteIdentity.LockedBaseHiddenEmoteKeys.ToHashSet(StringComparer.Ordinal);
        var emotes = keys.Select((key, index) =>
        {
            var option = new PlayerEmoteOptionRef
            {
                EmoteIndex = index, EmoteKey = key, DisplayNameKey = "Emote_" + key, DisplayName = key,
                IconIndex = key == "yes" ? 56 : index, Hidden = hidden.Contains(key),
                HasAnimation = key is "yes" or "no" or "hi" or "taunt" or "uh" or "music" or "jar",
                AnimationFacingDirection = 2, AnimationDurationMilliseconds = key == "yes" ? 1400 : 0,
                NativeCommandAccepted = true
            };
            option.OptionFingerprint = PlayerEmoteIdentity.ComputeOptionFingerprint(option);
            return option;
        }).ToArray();
        var projection = new PlayerEmoteProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15", NativeContract = NativeContract,
            ServiceStatus = "ready", PlayerId = 50, LanguageCode = 0, NetworkRole = "server",
            CanEmoteNative = true, ChatBoxPresent = true, ChatInputWidthPixels = 896,
            ChatInputContentWidthPixels = 880, MenuClear = true, ActiveMinigameType = "none", Emotes = emotes
        };
        projection.ProjectionFingerprint = PlayerEmoteIdentity.ComputeProjectionFingerprint(projection);
        var json = JsonSerializer.Serialize(new
        {
            player = new { location_id = Field("Farm"), emote = Field(projection) },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        }, JsonOptions);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-31T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId, OptionId = OptionId, Kind = candidate.Kind,
        Available = candidate.Available, LocationId = candidate.LocationId, ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks, Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "player.emote.action", SourceModel = "test", StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.emote", ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "perform.emote", OptionId = OptionId, Rationale = "explicit request", Parameters = parameters } }
    };

    private static object Field(object value) => new { value, status = "available", source = new { kind = "game_object", path = "test" }, adapter = "test", read_at_tick = 1, confidence = 1 };
    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static void AssertParameter(IEnumerable<SmallModelActionParameter> values, string name, string value) => Assert.Contains(values, row => row.Name == name && row.Value == value);

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Repository root not found.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
