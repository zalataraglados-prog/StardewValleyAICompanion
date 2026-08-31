using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class PrizeTicketRewardMainlineTests
{
    private const string OptionId = "rewards.claim_prize_ticket";
    private const string NativeContract =
        "Town.SpecialOrdersPrizeTickets->inventory_PrizeTicket_and_pending_stat_minus_one;ManorHouse.PrizeMachine->PrizeTicketMenu.currentPrizeTrack[0]->inventory_else_debris->PrizeTicket_minus_one->ticketPrizesClaimed_plus_one";

    [Theory]
    [InlineData("collect_pending_ticket", "Town", false)]
    [InlineData("redeem_prize", "ManorHouse", true)]
    public void BothNativeStagesFlowThroughCandidatePlanAndFreshTypedQueue(string stage, string location, bool fullInventory)
    {
        var snapshot = Snapshot(stage, location, fullInventory);
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { OptionId }, includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("claim_prize_ticket", candidate.Kind);
        Assert.Equal(location, candidate.LocationId);
        AssertParameter(candidate.Parameters, "prize_ticket_stage", stage);
        AssertParameter(candidate.Parameters, "continuation.expected_prize_level", "3");
        AssertParameter(candidate.Parameters, "continuation.expected_reward_fingerprint", RewardFingerprint());

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("claim_prize_ticket", planStep.Kind);
        Assert.Contains("one_native_stage_per_fresh_snapshot", planStep.SafetyConstraints);
        var queue = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(queue.BlockingReasons);
        Assert.Equal("executor.claim_prize_ticket", queue.OptionId);
        Assert.Equal("claim_prize_ticket", Assert.Single(queue.NormalizedCommand.Steps).StepType);
        AssertParameter(queue.NormalizedCommand.Parameters, "native_contract", NativeContract);
        if (fullInventory)
            AssertParameter(queue.NormalizedCommand.Parameters, "prize_ticket_pending_capacity_sufficient", "false");
    }

    [Fact]
    public void PendingTicketCapacityBlocksUpstreamButFullInventoryDoesNotBlockMachineRedemption()
    {
        var pending = Snapshot("collect_pending_ticket", "Town", fullInventory: true);
        var pendingCandidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            pending, new[] { OptionId }, true).Options.Single().EventCandidates);
        Assert.False(pendingCandidate.Available);
        Assert.Contains("prize_ticket_pending_ticket_capacity_not_proven", pendingCandidate.BlockReasons);

        var redeem = Snapshot("redeem_prize", "ManorHouse", fullInventory: true);
        var redeemCandidate = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            redeem, new[] { OptionId }, true).Options.Single().EventCandidates);
        Assert.True(redeemCandidate.Available, string.Join(";", redeemCandidate.BlockReasons));
    }

    [Fact]
    public void FreshCompilerRejectsForgedFrozenRewardIdentity()
    {
        var snapshot = Snapshot("redeem_prize", "ManorHouse", false);
        var stale = Action(snapshot, new[]
        {
            P("continuation.option_id", OptionId),
            P("continuation.expected_prize_level", "4"),
            P("continuation.expected_reward_fingerprint", new string('f', 64))
        });
        var item = Assert.Single(new ActionQueueCompiler().Compile(stale, snapshot).Items);
        Assert.Contains("prize_ticket_reward_complete_fresh_typed_binding_required", item.BlockingReasons);
    }

    [Fact]
    public void RollingContinuationSurvivesCollectionAndCompletesOnlyOnMatchingRedemption()
    {
        var route = Queue("executor.traverse_connector", "route_connector_tile");
        route["normalized_command"]!["parameters"]!.AsArray().Add(PNode("continuation.option_id", OptionId));
        route["normalized_command"]!["parameters"]!.AsArray().Add(PNode("continuation.expected_prize_level", "3"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(PNode("continuation.expected_reward_fingerprint", RewardFingerprint()));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);
        Assert.Equal("prize_ticket_reward", continuation!["kind"]!.GetValue<string>());

        var collect = Queue("executor.claim_prize_ticket", "claim_prize_ticket");
        collect["normalized_command"]!["parameters"]!.AsArray().Add(PNode("prize_ticket_stage", "collect_pending_ticket"));
        collect["normalized_command"]!["parameters"]!.AsArray().Add(PNode("prize_ticket_prize_level", "3"));
        collect["normalized_command"]!["parameters"]!.AsArray().Add(PNode("prize_ticket_current_reward_fingerprint", RewardFingerprint()));
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(collect, continuation, "applied"));

        var redeem = Queue("executor.claim_prize_ticket", "claim_prize_ticket");
        redeem["normalized_command"]!["parameters"]!.AsArray().Add(PNode("prize_ticket_stage", "redeem_prize"));
        redeem["normalized_command"]!["parameters"]!.AsArray().Add(PNode("prize_ticket_prize_level", "3"));
        redeem["normalized_command"]!["parameters"]!.AsArray().Add(PNode("prize_ticket_current_reward_fingerprint", RewardFingerprint()));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(redeem, continuation, "applied"));
        redeem["normalized_command"]!["parameters"]!.AsArray().Single(node => node!["name"]!.GetValue<string>() == "prize_ticket_prize_level")!["value"] = "4";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(redeem, continuation, "applied"));
    }

    [Fact]
    public void CapabilityTransportAndRuntimeRemainNativeAndTrainingAdmitted()
    {
        var high = OptionCapabilityRegistrySource.GetRequired(OptionId);
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.claim_prize_ticket");
        Assert.Equal(new[] { "EVD-318" }, high.RuntimeEvidenceIds);
        Assert.True(high.AutonomousCandidateEnabled);
        Assert.True(TrainingEligibilityPolicy.IsEligible(high));
        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(executor.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));

        var request = new TrainingExecutionRequest
        {
            PrizeTicketStage = "redeem_prize",
            PrizeTicketProjectionFingerprint = new string('a', 64),
            PrizeTicketCurrentRewardFingerprint = RewardFingerprint(),
            PrizeTicketPrizeLevel = 3,
            PrizeTicketRewardQualifiedItemId = "(O)MysteryBox",
            PrizeTicketRewardStack = 3,
            PrizeTicketPendingCapacitySufficient = false
        };
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(request, JsonOptions), JsonOptions)!;
        Assert.Equal("redeem_prize", roundTrip.PrizeTicketStage);
        Assert.Equal(3, roundTrip.PrizeTicketPrizeLevel);
        Assert.False(roundTrip.PrizeTicketPendingCapacitySufficient);

        var runtime = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "ModEntry.PrizeTicketReward.cs"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("PrizeTicketMenu", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.Contains("CountPrizeTicketRewardTotal", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("stats.Increment", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("stats.Decrement", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("addItemToInventoryBool", runtime, StringComparison.Ordinal);

        var adapter = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.PrizeTicketReward.cs"));
        Assert.Contains("location?.map?.GetLayer", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("location?.Map?.GetLayer", adapter, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(string stage, string playerLocation, bool fullInventory)
    {
        var projection = Projection(stage, playerLocation, fullInventory);
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field(playerLocation), tile_x = Field(stage == "redeem_prize" ? 1 : 60), tile_y = Field(stage == "redeem_prize" ? 6 : 94),
                prize_ticket_reward = Field(projection), inventory = Field(Array.Empty<object>()),
                inventory_capacity = Field(new { max_items = 36, occupied_slots = fullInventory ? 36 : 1 })
            },
            locations = new
            {
                route_graph = Field(new { status = "complete", edges = Array.Empty<object>() }),
                route_connectors = Field(new { location_id = playerLocation, connectors = Array.Empty<object>() }),
                collision_grid = Field(new { location_id = playerLocation, width = 100, height = 100, notable_tiles = Array.Empty<object>() })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        }, JsonOptions);
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-31T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static PrizeTicketRewardProjectionRef Projection(string stage, string playerLocation, bool fullInventory)
    {
        var preview = Preview();
        var projection = new PrizeTicketRewardProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15", NativeContract = NativeContract, Stage = stage,
            TargetLocationId = stage == "redeem_prize" ? "ManorHouse" : "Town",
            CurrentLocationMatches = true, MenuClear = true,
            InventoryTicketCount = stage == "redeem_prize" ? 1 : 0,
            PendingSpecialOrderTicketCount = stage == "collect_pending_ticket" ? 1 : 0,
            AvailableTicketCount = 1, TicketPrizesClaimed = 3, CurrentPrizeLevel = 3,
            CurrentReward = preview[0], CurrentRewardFingerprint = RewardFingerprint(), PreviewTrack = preview,
            PrizeMachineActionTiles = new[] { new PrizeTicketActionTileRef { LocationId = "ManorHouse", TileX = 1, TileY = 5, ActionRaw = "PrizeMachine" } },
            SpecialOrderTicketActionTiles = new[] { new PrizeTicketActionTileRef { LocationId = "Town", TileX = 60, TileY = 93, ActionRaw = "SpecialOrdersPrizeTickets" } },
            InventoryMaxItems = 36, InventoryOccupiedSlots = fullInventory ? 36 : stage == "redeem_prize" ? 1 : 0,
            PendingTicketCapacitySufficient = !fullInventory, GameId = 42, PlayerId = 7, HouseUpgradeLevel = 1,
            Season = "fall", DayOfMonth = 12, ServiceStatus = playerLocation == (stage == "redeem_prize" ? "ManorHouse" : "Town") ? "ready" : "route_required"
        };
        projection.CurrentLocationMatches = projection.ServiceStatus == "ready";
        projection.ProjectionFingerprint = PrizeTicketRewardIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static PrizeTicketRewardItemRef[] Preview() => new[]
    {
        new PrizeTicketRewardItemRef { PrizeLevel = 3, QualifiedItemId = "(O)MysteryBox", ItemId = "MysteryBox", DisplayName = "Mystery Box", Stack = 3, RuntimeType = "StardewValley.Object" },
        new PrizeTicketRewardItemRef { PrizeLevel = 4, QualifiedItemId = "(O)StardropTea", ItemId = "StardropTea", DisplayName = "Stardrop Tea", Stack = 1, RuntimeType = "StardewValley.Object" },
        new PrizeTicketRewardItemRef { PrizeLevel = 5, QualifiedItemId = "(F)BluePinstripeDoubleBed", ItemId = "BluePinstripeDoubleBed", DisplayName = "Blue Pinstripe Double Bed", Stack = 1, RuntimeType = "StardewValley.Objects.Furniture" },
        new PrizeTicketRewardItemRef { PrizeLevel = 6, QualifiedItemId = "(BC)15", ItemId = "15", DisplayName = "Furnace", Stack = 4, RuntimeType = "StardewValley.Object" }
    };

    private static string RewardFingerprint() => PrizeTicketRewardIdentity.ComputeRewardFingerprint(Preview()[0]);

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId, OptionId = OptionId, Kind = candidate.Kind, Available = candidate.Available,
        LocationId = candidate.LocationId, TileX = candidate.TileX, TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect, EstimatedTicks = candidate.EstimatedTicks, Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "prize.ticket.action", SourceModel = "test", StateHash = snapshot.StateHash,
        GoalId = "goal.claim.prize.ticket", ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "claim.prize.ticket", OptionId = OptionId, Rationale = "claim earned reward", Parameters = parameters } }
    };

    private static JsonObject Queue(string optionId, string stepType) => new()
    {
        ["option_id"] = optionId,
        ["normalized_command"] = new JsonObject
        {
            ["parameters"] = new JsonArray(),
            ["steps"] = new JsonArray(new JsonObject { ["step_type"] = stepType, ["target"] = "test" })
        }
    };

    private static JsonObject PNode(string name, string value) => new() { ["name"] = name, ["value"] = value };
    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };
    private static object Field(object value) => new { value, status = "available", source = new { kind = "game_object", path = "test" }, adapter = "test", read_at_tick = 1, confidence = 1 };
    private static void AssertParameter(IEnumerable<SmallModelActionParameter> values, string name, string value) => Assert.Contains(values, row => row.Name == name && row.Value == value);

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) directory = directory.Parent;
        return Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Cannot find repository root."), Path.Combine(segments));
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
