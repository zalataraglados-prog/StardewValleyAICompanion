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

public sealed class MasteryClaimMainlineTests
{
    private const string OptionId = "skills.claim_mastery";
    private const string NativeContract =
        "Forest.MasteryRoom(all_five_base_skills_10)->MasteryCave;MasteryCave_skill_action->MasteryTrackerMenu(skill)->mainButton->claimReward(recipes,direct_inventory_else_debris,mastery_stat,masteryLevelsSpent,combat_trinket_slot,all_plaque_finale)";

    [Theory]
    [InlineData(0, "farming", "MasteryCave_Farming")]
    [InlineData(1, "fishing", "MasteryCave_Fishing")]
    [InlineData(2, "foraging", "MasteryCave_Foraging")]
    [InlineData(3, "mining", "MasteryCave_Mining")]
    [InlineData(4, "combat", "MasteryCave_Combat")]
    public void FiveStrategicChoicesFlowThroughOneNativeCompilerAndExecutor(int skillId, string skillKey, string actionRaw)
    {
        var snapshot = Snapshot(skillId);
        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate { OptionId = OptionId, Parameters = new[] { P("mastery_skill_id", skillId.ToString()) } }
        }, includeExecutorCalibrationOptions: true).Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("claim_mastery", candidate.Kind);
        AssertParameter(candidate.Parameters, "mastery_skill_key", skillKey);
        AssertParameter(candidate.Parameters, "mastery_action_raw", actionRaw);
        AssertParameter(candidate.Parameters, "continuation.mastery_skill_id", skillId.ToString());

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("claim_mastery", planStep.Kind);
        Assert.Contains("one_native_claim_per_fresh_snapshot", planStep.SafetyConstraints);
        var queue = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(queue.BlockingReasons);
        Assert.Equal("executor.claim_mastery", queue.OptionId);
        Assert.Equal("claim_mastery", Assert.Single(queue.NormalizedCommand.Steps).StepType);
        AssertParameter(queue.NormalizedCommand.Parameters, "native_contract", NativeContract);
        AssertParameter(queue.NormalizedCommand.Parameters, "mastery_skill_id", skillId.ToString());
    }

    [Fact]
    public void RewardProjectionCoversEveryLockedNativeBranchExactly()
    {
        var projection = Projection(selectedSkillId: 0);
        Assert.Equal(new[] { "(W)66" }, projection.Skills[0].DirectRewards.Select(row => row.QualifiedItemId));
        Assert.Equal(new[] { "Statue Of Blessings" }, RecipeNames(projection.Skills[0]));
        Assert.Equal(new[] { "(T)AdvancedIridiumRod" }, projection.Skills[1].DirectRewards.Select(row => row.QualifiedItemId));
        Assert.Equal(new[] { "Challenge Bait" }, RecipeNames(projection.Skills[1]));
        Assert.Equal(new[] { "Mystic Tree Seed", "Treasure Totem" }, RecipeNames(projection.Skills[2]));
        Assert.Equal(new[] { "Statue Of The Dwarf King", "Heavy Furnace" }, RecipeNames(projection.Skills[3]));
        Assert.Equal(new[] { "Anvil", "Mini-Forge" }, RecipeNames(projection.Skills[4]));
        Assert.True(projection.Skills[4].GrantsTrinketSlot);
        Assert.All(projection.Skills.Take(4), row => Assert.False(row.GrantsTrinketSlot));
    }

    [Fact]
    public void ForgedOrClaimedSkillChoiceIsRejectedBeforeExecution()
    {
        var snapshot = Snapshot(0);
        var forged = Action(snapshot, new[]
        {
            P("mastery_skill_id", "0"),
            P("continuation.mastery_skill_id", "0"),
            P("continuation.mastery_option_fingerprint", new string('f', 64))
        });
        var item = Assert.Single(new ActionQueueCompiler().Compile(forged, snapshot).Items);
        Assert.Contains("mastery_claim_complete_fresh_typed_binding_required", item.BlockingReasons);

        var claimedProjection = Projection(0);
        claimedProjection.Skills[0].MasteryStatValue = 1;
        claimedProjection.Skills[0].Claimed = true;
        claimedProjection.Skills[0].Claimable = false;
        claimedProjection.Skills[0].OptionFingerprint = MasteryClaimIdentity.ComputeOptionFingerprint(claimedProjection.Skills[0]);
        claimedProjection.ClaimableOptions = claimedProjection.Skills.Where(row => row.Claimable).ToArray();
        claimedProjection.ProjectionFingerprint = MasteryClaimIdentity.ComputeProjectionFingerprint(claimedProjection);
        var claimed = SnapshotFromProjection(claimedProjection);
        var evaluated = new CandidateOptionAvailabilityEvaluator().Evaluate(claimed, new[]
        {
            new OptionAvailabilityCandidate { OptionId = OptionId, Parameters = new[] { P("mastery_skill_id", "0") } }
        }, true);
        Assert.Empty(Assert.Single(evaluated.Options).EventCandidates);
    }

    [Fact]
    public void RouteContinuationPreservesChoiceAndCompletesOnlyMatchingClaim()
    {
        var optionFingerprint = Projection(3).Skills[3].OptionFingerprint;
        var route = Queue("executor.traverse_connector", "route_connector_tile");
        route["normalized_command"]!["parameters"]!.AsArray().Add(PNode("continuation.option_id", OptionId));
        route["normalized_command"]!["parameters"]!.AsArray().Add(PNode("continuation.mastery_skill_id", "3"));
        route["normalized_command"]!["parameters"]!.AsArray().Add(PNode("continuation.mastery_option_fingerprint", optionFingerprint));
        var continuation = QueueReplanFilter.ReadObjectiveContinuation(route);
        Assert.Equal("mastery_claim", continuation!["kind"]!.GetValue<string>());

        var claim = Queue("executor.claim_mastery", "claim_mastery");
        claim["normalized_command"]!["parameters"]!.AsArray().Add(PNode("mastery_skill_id", "3"));
        claim["normalized_command"]!["parameters"]!.AsArray().Add(PNode("mastery_option_fingerprint", optionFingerprint));
        Assert.True(QueueReplanFilter.CompletesObjectiveContinuation(claim, continuation, "applied"));
        claim["normalized_command"]!["parameters"]!.AsArray().Single(node => node!["name"]!.GetValue<string>() == "mastery_skill_id")!["value"] = "4";
        Assert.False(QueueReplanFilter.CompletesObjectiveContinuation(claim, continuation, "applied"));
    }

    [Fact]
    public void CapabilityTransportAndRuntimeStayNativeAndTrainingAdmitted()
    {
        var high = OptionCapabilityRegistrySource.GetRequired(OptionId);
        var executor = OptionCapabilityRegistrySource.GetRequired("executor.claim_mastery");
        Assert.Equal(new[] { "EVD-319" }, high.RuntimeEvidenceIds);
        Assert.True(high.AutonomousCandidateEnabled);
        Assert.True(TrainingEligibilityPolicy.IsEligible(high));
        Assert.False(TrainingEligibilityPolicy.IsEligible(executor));
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported(executor.OptionId));
        Assert.False(PendingSemanticActionCatalog.TryGet(OptionId, out _));

        var request = new TrainingExecutionRequest
        {
            MasterySkillId = 4,
            MasterySkillKey = "combat",
            MasteryProjectionFingerprint = new string('a', 64),
            MasteryOptionFingerprint = new string('b', 64),
            MasteryGrantsTrinketSlot = true,
            MasteryRecipeRewardsJson = "[{\"recipe_name\":\"Anvil\",\"known_before\":false}]"
        };
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(request, JsonOptions), JsonOptions)!;
        Assert.Equal(4, roundTrip.MasterySkillId);
        Assert.True(roundTrip.MasteryGrantsTrinketSlot);

        var runtime = File.ReadAllText(FindRepositoryFile("tools", "StardewAI.RuntimeTestHarness", "ModEntry.MasteryClaim.cs"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("MasteryTrackerMenu", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("stats.Set", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("stats.Increment", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("craftingRecipes.TryAdd", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Items[", runtime, StringComparison.Ordinal);

        var adapter = File.ReadAllText(FindRepositoryFile("src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.MasteryClaim.cs"));
        Assert.Contains("location?.map?.GetLayer", adapter, StringComparison.Ordinal);
        Assert.DoesNotContain("location?.Map?.GetLayer", adapter, StringComparison.Ordinal);
    }

    private static SnapshotEnvelope Snapshot(int selectedSkillId) => SnapshotFromProjection(Projection(selectedSkillId));

    private static SnapshotEnvelope SnapshotFromProjection(MasteryClaimProjectionRef projection)
    {
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field("MasteryCave"), tile_x = Field(7), tile_y = Field(11),
                mastery_claim = Field(projection), inventory = Field(Array.Empty<object>())
            },
            locations = new
            {
                route_graph = Field(new { status = "complete", edges = Array.Empty<object>() }),
                route_connectors = Field(new { location_id = "MasteryCave", connectors = Array.Empty<object>() }),
                collision_grid = Field(new { location_id = "MasteryCave", width = 20, height = 20, notable_tiles = Array.Empty<object>() })
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

    private static MasteryClaimProjectionRef Projection(int selectedSkillId)
    {
        var coordinates = new[] { (7, 5), (9, 5), (5, 5), (11, 6), (3, 6) };
        var actionTokens = new[] { "MasteryCave_Farming", "MasteryCave_Fishing", "MasteryCave_Foraging", "MasteryCave_Mining", "MasteryCave_Combat" };
        var keys = new[] { "farming", "fishing", "foraging", "mining", "combat" };
        var recipes = new[]
        {
            new[] { "Statue Of Blessings" }, new[] { "Challenge Bait" },
            new[] { "Mystic Tree Seed", "Treasure Totem" },
            new[] { "Statue Of The Dwarf King", "Heavy Furnace" }, new[] { "Anvil", "Mini-Forge" }
        };
        var direct = new[] { new[] { "(W)66" }, new[] { "(T)AdvancedIridiumRod" }, Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>() };
        var skills = Enumerable.Range(0, 5).Select(skillId =>
        {
            var option = new MasteryClaimOptionRef
            {
                SkillId = skillId, SkillKey = keys[skillId], SkillLevel = 10,
                MasteryStatKey = "mastery_" + skillId, MasteryStatValue = 0, Claimed = false, Claimable = true,
                ActionTile = new MasteryClaimActionTileRef { LocationId = "MasteryCave", TileX = coordinates[skillId].Item1, TileY = coordinates[skillId].Item2, ActionRaw = actionTokens[skillId] },
                RecipeRewards = recipes[skillId].Select(name => new MasteryClaimRecipeRewardRef { RecipeName = name, KnownBefore = false }).ToArray(),
                DirectRewards = direct[skillId].Select(id => new MasteryClaimDirectRewardRef
                {
                    QualifiedItemId = id, ItemId = id[(id.IndexOf(')') + 1)..], DisplayName = id, Stack = 1,
                    RuntimeType = skillId == 0 ? "StardewValley.Tools.MeleeWeapon" : "StardewValley.Tools.FishingRod"
                }).ToArray(),
                GrantsTrinketSlot = skillId == 4
            };
            option.OptionFingerprint = MasteryClaimIdentity.ComputeOptionFingerprint(option);
            return option;
        }).ToArray();
        var projection = new MasteryClaimProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15", NativeContract = NativeContract,
            CurrentLocationMatches = true, MenuClear = true, AllBaseSkillsLevelTen = true,
            MasteryExperience = 10000, CurrentMasteryLevel = 1, MasteryLevelsSpent = 0, UnspentMasteryLevels = 1,
            AllPlaquesCompleted = false, TrinketSlots = 0, Skills = skills,
            ClaimableOptions = skills, GameId = 42, PlayerId = 7, ServiceStatus = "ready"
        };
        projection.ProjectionFingerprint = MasteryClaimIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static string[] RecipeNames(MasteryClaimOptionRef option) => option.RecipeRewards.Select(row => row.RecipeName).ToArray();

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId, OptionId = OptionId, Kind = candidate.Kind, Available = candidate.Available,
        LocationId = candidate.LocationId, TileX = candidate.TileX, TileY = candidate.TileY,
        ExpectedEffect = candidate.ExpectedEffect, EstimatedTicks = candidate.EstimatedTicks, Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "mastery.claim.action", SourceModel = "test", StateHash = snapshot.StateHash,
        GoalId = "goal.claim.mastery", ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "claim.mastery", OptionId = OptionId, Rationale = "select mastery", Parameters = parameters } }
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
