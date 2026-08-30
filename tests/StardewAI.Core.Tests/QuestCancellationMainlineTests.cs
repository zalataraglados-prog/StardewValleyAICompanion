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

public sealed class QuestCancellationMainlineTests
{
    private const string OptionId = "quest.cancel";
    private const string NativeContract =
        "QuestLog_row_receiveLeftClick->cancelQuestButton_receiveLeftClick->accepted_false->questLog_remove->same_day_daily_acceptedDailyQuest_false";

    [Fact]
    public void CancellationProducesNoAutonomousCandidateWithoutExactConfirmedIntent()
    {
        var snapshot = Snapshot();
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { OptionId }, includeExecutorCalibrationOptions: true).Options);

        Assert.False(option.Available);
        Assert.Empty(option.EventCandidates);
        Assert.Contains("no_explicit_quest_cancellation_candidate", option.BlockingReasons);

        var unconfirmed = Intent().Where(parameter => parameter.Name != "confirm_quest_cancel").ToArray();
        Assert.Empty(Assert.Single(Evaluate(snapshot, unconfirmed).Options).EventCandidates);
    }

    [Fact]
    public void ExactConfirmedCancellationReachesOneTypedNativeQueue()
    {
        var snapshot = Snapshot();
        var option = Assert.Single(Evaluate(snapshot, Intent()).Options);
        var candidate = Assert.Single(option.EventCandidates);

        Assert.True(option.Available, string.Join(";", option.BlockingReasons));
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("cancel_quest", candidate.Kind);
        AssertParameter(candidate.Parameters, "quest_cancellation_fingerprint", Fingerprint());
        AssertParameter(candidate.Parameters, "quest_accepted_daily_after", "false");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var step = Assert.Single(plan.Steps);
        Assert.Equal("cancel_quest", step.Kind);
        Assert.Contains("player_command_only", step.SafetyConstraints);
        Assert.Contains("do_not_write_quest_or_acceptedDailyQuest_state_directly", step.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Equal("executor.cancel_quest", item.OptionId);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("cancel_quest", Assert.Single(item.NormalizedCommand.Steps).StepType);
        AssertParameter(item.NormalizedCommand.Parameters, "quest_log_count_before", "2");
        AssertParameter(item.NormalizedCommand.Parameters, "quest_log_count_after", "1");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void FreshCompilerOverwritesForgedStateBindingAndRejectsMissingIdentity()
    {
        var snapshot = Snapshot();
        var forged = Intent().Concat(new[]
        {
            P("quest_id", "forged"),
            P("quest_runtime_type", "SpecialOrder"),
            P("quest_log_count_before", "999"),
            P("quest_log_count_after", "998"),
            P("quest_accepted_daily_after", "true"),
            P("native_contract", "forged")
        }).ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, forged), snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "quest_id", "cancel-fixture");
        AssertParameter(item.NormalizedCommand.Parameters, "quest_runtime_type", "ItemDeliveryQuest");
        AssertParameter(item.NormalizedCommand.Parameters, "quest_log_count_before", "2");
        AssertParameter(item.NormalizedCommand.Parameters, "quest_log_count_after", "1");
        AssertParameter(item.NormalizedCommand.Parameters, "quest_accepted_daily_after", "false");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);

        var missingIdentity = new[] { P("quest_cancel_reason", "explicit cleanup"), P("confirm_quest_cancel", "true") };
        var blocked = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, missingIdentity), snapshot).Items);
        Assert.Contains("quest_cancellation_exact_identity_missing_or_ambiguous", blocked.BlockingReasons);
    }

    [Fact]
    public void CapabilityRemainsConfirmedPlayerCommandOnlyAndOutsideTraining()
    {
        foreach (var optionId in new[] { OptionId, "executor.cancel_quest" })
        {
            var declaration = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-316" }, declaration.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-316" }, declaration.RuntimeEvidenceIds);
            Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, declaration.InvocationPolicy);
            Assert.True(declaration.PlayerConfirmationRequired);
            Assert.False(declaration.AutonomousCandidateEnabled);
            Assert.False(TrainingEligibilityPolicy.IsEligible(declaration));
            Assert.DoesNotContain(optionId, OptionCapabilityRegistrySource.TrainingAllowlist);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.cancel_quest"));
    }

    [Fact]
    public void RuntimeUsesOnlyNativeQuestLogClicksForCancellationMutation()
    {
        var production = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.QuestCancellation.cs"));
        Assert.Contains("new QuestLog()", production, StringComparison.Ordinal);
        Assert.Contains("FindQuestMenuPosition", production, StringComparison.Ordinal);
        Assert.Contains("cancelQuestButton", production, StringComparison.Ordinal);
        Assert.Contains("receiveLeftClick", production, StringComparison.Ordinal);
        Assert.DoesNotContain(".accepted.Value =", production, StringComparison.Ordinal);
        Assert.DoesNotContain(".questLog.Remove", production, StringComparison.Ordinal);
        Assert.DoesNotContain(".acceptedDailyQuest.Set", production, StringComparison.Ordinal);
        Assert.DoesNotContain(".completed.Value =", production, StringComparison.Ordinal);
        Assert.DoesNotContain(".destroy.Value =", production, StringComparison.Ordinal);

        var fixture = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.QuestCancellationFixture.cs"));
        Assert.Contains("same_day_daily", fixture, StringComparison.Ordinal);
        Assert.Contains("ordinary_preserve_daily_flag", fixture, StringComparison.Ordinal);
        Assert.Contains("non_cancellable", fixture, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true,
                Parameters = parameters
            }
        }, includeExecutorCalibrationOptions: true);

    private static SmallModelActionParameter[] Intent() => new[]
    {
        P("quest_cancellation_fingerprint", Fingerprint()),
        P("quest_cancel_reason", "explicit cleanup"),
        P("confirm_quest_cancel", "true")
    };

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = OptionId,
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "quest.cancel.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.quest_cancel",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef
        {
            ActorId = "training_farmer.main",
            ActorType = "training_farmer",
            ControlSurface = "training_sandbox"
        },
        Actions = new[]
        {
            new SmallModelAction
            {
                ActionId = "quest.cancel.exact",
                OptionId = OptionId,
                Rationale = "explicit player request",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot()
    {
        var quest = Quest();
        var projection = new
        {
            schema_version = "quest_cancellation.v1",
            projection_status = "complete_locked_base_1.6.15",
            invocation_policy = "player_command_only",
            native_contract = NativeContract,
            current_total_days = 42,
            quest_log_count_before = 2,
            accepted_daily_quest_before = true,
            candidates = new[]
            {
                new
                {
                    cancellation_fingerprint = Fingerprint(),
                    quest,
                    eligible = true,
                    native_button_visible = true,
                    resets_accepted_daily_quest = true,
                    expected_accepted_daily_quest_after = false,
                    expected_quest_log_count_after = 1,
                    status = "ready",
                    blocked_diagnostics = Array.Empty<string>()
                }
            }
        };
        var json = JsonSerializer.Serialize(new
        {
            quests = new
            {
                cancellation_candidates = Field(projection),
                active_quests = Field(Array.Empty<object>())
            },
            menus = new
            {
                active_menu = Field(new { is_open = false, type = "none" })
            }
        }, JsonOptions);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1,
            RealTimestamp = "2026-08-31T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static QuestProgressRef Quest() => new()
    {
        Id = "cancel-fixture",
        Title = "Cancel fixture",
        CurrentObjective = "Deliver one item",
        QuestType = 3,
        Accepted = true,
        Completed = false,
        Hidden = false,
        DailyQuest = true,
        CanBeCancelled = true,
        DayQuestAccepted = 42,
        DaysLeft = 2,
        MoneyReward = 0,
        Destroy = false,
        RuntimeType = "ItemDeliveryQuest"
    };

    private static string Fingerprint() => QuestCancellationIdentity.Compute(Quest());

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

    private static void AssertParameter(IEnumerable<SmallModelActionParameter> parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null &&
            !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
        {
            directory = directory.Parent;
        }
        return Path.Combine(
            directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
