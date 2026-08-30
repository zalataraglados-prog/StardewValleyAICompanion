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

public sealed class AdventureGuildRewardMainlineTests
{
    private const string OptionId = "rewards.claim_adventure_guild_reward";
    private const string NativeContract =
        "AdventureGuild.checkAction_gil_tile->gil_all_complete_unclaimed_goals->DialogueBox_optional->ItemGrabMenu->receiveLeftClick_each_reward->OnRewardCollected_Gil_goalId";

    [Fact]
    public void CompleteBatchFlowsThroughCandidatePlanAndOneTypedNativeQueue()
    {
        var snapshot = Snapshot();
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { OptionId }, includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates);

        var candidateProbe = Assert.Single(new ActionQueueCompiler().Compile(
            Action(snapshot, candidate.Parameters), snapshot).Items);
        Assert.Empty(candidateProbe.BlockingReasons);
        Assert.True(option.Available, string.Join(";", option.BlockingReasons));
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("claim_adventure_guild_reward", candidate.Kind);
        Assert.Equal("AdventureGuild", candidate.LocationId);
        AssertParameter(candidate.Parameters, "adventure_guild_reward_pending_goal_count", "1");
        AssertParameter(candidate.Parameters, "adventure_guild_reward_batch_fingerprint", Fingerprint());

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("claim_adventure_guild_reward", planStep.Kind);
        Assert.Contains("claim_complete_native_batch_without_partial_selection", planStep.SafetyConstraints);

        var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(queueItem.BlockingReasons);
        Assert.Equal("executor.claim_adventure_guild_reward", queueItem.OptionId);
        Assert.Equal("claim_adventure_guild_reward", Assert.Single(queueItem.NormalizedCommand.Steps).StepType);
        AssertParameter(queueItem.NormalizedCommand.Parameters, "native_contract", NativeContract);
        AssertParameter(queueItem.NormalizedCommand.Parameters, "adventure_guild_reward_goals_json",
            JsonSerializer.Serialize(Goals(), JsonOptions));
    }

    [Fact]
    public void FreshCompilerOverwritesForgedBatchFieldsAndRejectsStaleIdentity()
    {
        var snapshot = Snapshot();
        var forged = new[]
        {
            P("adventure_guild_reward_batch_fingerprint", Fingerprint()),
            P("adventure_guild_reward_goals_json", "[]"),
            P("adventure_guild_reward_pending_goal_count", "999"),
            P("adventure_guild_reward_inventory_capacity_sufficient", "false"),
            P("target_location", "Farm"),
            P("target_tile_x", "999"),
            P("native_contract", "forged")
        };
        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, forged), snapshot).Items);

        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "adventure_guild_reward_pending_goal_count", "1");
        AssertParameter(item.NormalizedCommand.Parameters, "adventure_guild_reward_inventory_capacity_sufficient", "true");
        AssertParameter(item.NormalizedCommand.Parameters, "target_location", "AdventureGuild");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_x", "7");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);

        var stale = Assert.Single(new ActionQueueCompiler().Compile(
            Action(snapshot, new[] { P("adventure_guild_reward_batch_fingerprint", "stale") }), snapshot).Items);
        Assert.Contains("adventure_guild_reward_complete_fresh_typed_binding_required", stale.BlockingReasons);
    }

    [Fact]
    public void EntireBatchCapacityFailureIsExcludedBeforeNativeInteraction()
    {
        var snapshot = Snapshot(capacitySufficient: false, status: "blocked_capacity");
        var candidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { OptionId }, includeExecutorCalibrationOptions: true).Options.Single().EventCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("adventure_guild_reward_batch_capacity_not_proven", candidate.BlockReasons);
        var queueItem = Assert.Single(new ActionQueueCompiler().Compile(
            Action(snapshot, new[] { P("adventure_guild_reward_batch_fingerprint", Fingerprint()) }), snapshot).Items);
        Assert.Contains("adventure_guild_reward_complete_ready_projection_required", queueItem.BlockingReasons);
        Assert.Contains("adventure_guild_reward_complete_fresh_typed_binding_required", queueItem.BlockingReasons);
    }

    [Fact]
    public void CapabilityAndTypedTransportRemainTrainingAdmittedAndNative()
    {
        var high = OptionCapabilityRegistrySource.GetRequired(OptionId);
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.claim_adventure_guild_reward");
        Assert.Equal(new[] { "EVD-317" }, high.ReadEvidenceIds);
        Assert.Equal(new[] { "EVD-317" }, high.RuntimeEvidenceIds);
        Assert.True(high.AutonomousCandidateEnabled);
        Assert.False(high.PlayerConfirmationRequired);
        Assert.True(TrainingEligibilityPolicy.IsEligible(high));
        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.Contains(OptionId, OptionCapabilityRegistrySource.TrainingAllowlist);
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(executor.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));

        var request = new TrainingExecutionRequest
        {
            AdventureGuildRewardBatchFingerprint = Fingerprint(),
            AdventureGuildRewardGoalsJson = JsonSerializer.Serialize(Goals(), JsonOptions),
            AdventureGuildRewardPendingGoalCount = 1,
            AdventureGuildRewardItemCount = 1,
            AdventureGuildRewardDialogueCount = 1,
            AdventureGuildRewardInventoryMaxItems = 36,
            AdventureGuildRewardInventoryOccupiedSlots = 5,
            AdventureGuildRewardInventoryCapacitySufficient = true,
            AdventureGuildRewardActionTileIndex = 1291
        };
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(
            JsonSerializer.Serialize(request, JsonOptions), JsonOptions)!;
        Assert.Equal(Fingerprint(), roundTrip.AdventureGuildRewardBatchFingerprint);
        Assert.True(roundTrip.AdventureGuildRewardInventoryCapacitySufficient);
        Assert.Equal(1291, roundTrip.AdventureGuildRewardActionTileIndex);
    }

    [Fact]
    public void RuntimeUsesGilAndOwnedMenusWithoutDirectProductionMutation()
    {
        var production = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.AdventureGuildReward.cs"));
        Assert.Contains("AdventureGuildRewardEndpointMatches", production, StringComparison.Ordinal);
        Assert.Contains("AdventureGuildRewardBatchFits", production, StringComparison.Ordinal);
        Assert.Contains("active.Location.checkAction", production, StringComparison.Ordinal);
        Assert.Contains("DialogueBox", production, StringComparison.Ordinal);
        Assert.Contains("ItemGrabMenu", production, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", production, StringComparison.Ordinal);
        Assert.Contains("Game1.player.mailReceived.Contains(goal.GilMailFlag)", production, StringComparison.Ordinal);
        Assert.DoesNotContain("mailReceived.Add", production, StringComparison.Ordinal);
        Assert.DoesNotContain("mailForTomorrow.Add", production, StringComparison.Ordinal);
        Assert.DoesNotContain("specificMonstersKilled[", production, StringComparison.Ordinal);

        var fixture = File.ReadAllText(FindRepositoryFile(
            "tools", "StardewAI.RuntimeTestHarness", "ModEntry.AdventureGuildRewardFixture.cs"));
        Assert.Contains("single_item", fixture, StringComparison.Ordinal);
        Assert.Contains("specificMonstersKilled", fixture, StringComparison.Ordinal);
    }

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId,
        OptionId = OptionId,
        Kind = candidate.Kind,
        Available = candidate.Available,
        LocationId = candidate.LocationId,
        TileX = candidate.TileX,
        TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(
        SnapshotEnvelope snapshot,
        SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "adventure.guild.reward.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.claim.earned.monster.rewards",
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
                ActionId = "claim.adventure.guild.reward.batch",
                OptionId = OptionId,
                Rationale = "claim earned positive reward",
                Parameters = parameters
            }
        }
    };

    private static SnapshotEnvelope Snapshot(bool capacitySufficient = true, string status = "ready")
    {
        var goals = Goals();
        var projection = new AdventureGuildRewardProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15",
            InvocationPolicy = "autonomous_positive_reward",
            NativeContract = NativeContract,
            LocationId = "AdventureGuild",
            CurrentLocationMatches = true,
            ActionTileX = 7,
            ActionTileY = 9,
            ActionTileIndex = 1291,
            StandTileX = 7,
            StandTileY = 10,
            MenuClear = true,
            BatchFingerprint = AdventureGuildRewardIdentity.Compute(goals),
            PendingGoalCount = 1,
            RewardItemCount = 1,
            RewardDialogueCount = 1,
            InventoryMaxItems = 36,
            InventoryOccupiedSlots = 5,
            InventoryCapacitySufficient = capacitySufficient,
            Goals = goals,
            Status = status,
            BlockedDiagnostics = Array.Empty<string>()
        };
        var json = JsonSerializer.Serialize(new
        {
            quests = new
            {
                adventure_guild_reward = Field(projection),
                mail_received = Field(Array.Empty<string>()),
                mail_for_tomorrow = Field(Array.Empty<string>())
            },
            player = new
            {
                inventory = Field(Array.Empty<object>()),
                inventory_capacity = Field(new { max_items = 36, occupied_slots = 5 })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
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

    private static AdventureGuildRewardGoalRef[] Goals() => new[]
    {
        new AdventureGuildRewardGoalRef
        {
            GoalId = "slimes",
            DisplayName = "Slime Charmer",
            Targets = new[] { "Green Slime" },
            RequiredKills = 1000,
            CurrentKills = 1000,
            Complete = true,
            Collected = false,
            GilMailFlag = "Gil_slimes",
            RewardItemId = "(H)56",
            RewardItemRuntimeType = "StardewValley.Objects.Hat",
            RewardItemStack = 1,
            RewardItemQuality = 0,
            RewardItemSpecialVariable = 0,
            RewardItemSpecialItem = false,
            RewardDialogue = "reward dialogue",
            RewardDialogueShouldShow = true
        }
    };

    private static string Fingerprint() => AdventureGuildRewardIdentity.Compute(Goals());

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
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root."),
            Path.Combine(segments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
