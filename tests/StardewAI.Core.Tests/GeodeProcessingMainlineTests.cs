using System.Text.Json;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class GeodeProcessingMainlineTests
{
    private const string OptionId = "processing.crack_geode";
    private const string NativeContract =
        "shared_route->Blacksmith_checkAction->answerDialogue(Process)->GeodeMenu_inventory_click->GeodeMenu_geodeSpot_click->2700ms_native_animation->inventory_receipt";

    [Fact]
    public void TransparentInputFlowsThroughCandidatePlanAndFreshNativeQueue()
    {
        var snapshot = Snapshot("Blacksmith");
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("crack_geode", candidate.Kind);
        AssertParameter(candidate.Parameters, "geode_qualified_item_id", "(O)535");
        AssertParameter(candidate.Parameters, "geode_input_quality", "0");
        AssertParameter(candidate.Parameters, "geodes_cracked_before", "42");

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        Assert.Contains("no_direct_money_inventory_stats_mail_or_team_state_mutation",
            Assert.Single(plan.Steps).SafetyConstraints);
        var queueItem = Assert.Single(new ActionQueueCompiler().Compile(plan, snapshot).Items);
        Assert.Empty(queueItem.BlockingReasons);
        Assert.Equal("executor.crack_geode", queueItem.OptionId);
        Assert.Equal("crack_geode", Assert.Single(queueItem.NormalizedCommand.Steps).StepType);
        AssertParameter(queueItem.NormalizedCommand.Parameters, "geode_expected_output_qid", "(O)378");
        AssertParameter(queueItem.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void FreshCompilerOverwritesForgedMechanicalPredictionAndEndpoint()
    {
        var snapshot = Snapshot("Blacksmith");
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot).Options).EventCandidates);
        var forged = candidate.Parameters.Concat(new[]
        {
            P("geode_input_quality", "4"), P("geodes_cracked_before", "999"),
            P("geode_expected_output_qid", "(O)74"), P("geode_accepted_outputs_json", "[]"),
            P("geode_projection_fingerprint", "forged"), P("target_tile_x", "99"),
            P("stand_tile_y", "99"), P("geode_action_raw", "forged"), P("native_contract", "forged")
        }).ToArray();

        var item = Assert.Single(new ActionQueueCompiler().Compile(Action(snapshot, forged), snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "geode_input_quality", "0");
        AssertParameter(item.NormalizedCommand.Parameters, "geodes_cracked_before", "42");
        AssertParameter(item.NormalizedCommand.Parameters, "golden_walnuts_before", "17");
        AssertParameter(item.NormalizedCommand.Parameters, "geode_expected_output_qid", "(O)378");
        AssertParameter(item.NormalizedCommand.Parameters, "geode_accepted_outputs_json",
            "[{\"qualified_item_id\":\"(O)378\",\"stack\":3,\"quality\":0,\"set_flag_on_pickup\":\"\",\"inventory_persists\":true,\"pickup_effect_kind\":\"copper_found_counter\",\"expected_mail_additions\":[]}]");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_x", "3");
        AssertParameter(item.NormalizedCommand.Parameters, "stand_tile_y", "4");
        AssertParameter(item.NormalizedCommand.Parameters, "geode_action_raw", "Blacksmith");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void RemoteInputProducesOneLockedRollingContinuation()
    {
        var candidate = Assert.Single(Assert.Single(Evaluate(Snapshot("Town", includeRoute: true)).Options).EventCandidates);
        Assert.Equal("route_connector_tile", candidate.Kind);
        AssertParameter(candidate.Parameters, "continuation.option_id", OptionId);
        AssertParameter(candidate.Parameters, "continuation.geode_qualified_item_id", "(O)535");
        AssertParameter(candidate.Parameters, "continuation.geode_purpose", "open_for_projected_value");
    }

    [Fact]
    public void CapacityOrServiceDriftIsExcludedBeforeRanking()
    {
        var unavailable = Assert.Single(Assert.Single(Evaluate(Snapshot("Blacksmith", capacity: false)).Options).EventCandidates);
        Assert.False(unavailable.Available);
        Assert.Contains("geode_processing_output_capacity_unavailable", unavailable.BlockReasons);

        var closed = Assert.Single(Assert.Single(Evaluate(Snapshot("Blacksmith", serviceStatus: "clint_not_present")).Options).EventCandidates);
        Assert.False(closed.Available);
        Assert.Contains("geode_processing_service_not_ready:clint_not_present", closed.BlockReasons);
    }

    [Fact]
    public void CapabilityAndRuntimeSourcesOwnOneNativeMutationPath()
    {
        foreach (var optionId in new[] { OptionId, "executor.crack_geode" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-315" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-315" }, capability.CandidateEvidenceIds);
            Assert.Equal(new[] { "EVD-315" }, capability.CompilerEvidenceIds);
            Assert.Equal(new[] { "EVD-315" }, capability.RuntimeEvidenceIds);
            Assert.Equal(new[] { "EVD-315" }, capability.OutputEvidenceIds);
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }

        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.crack_geode"));
        var root = FindRepositoryRoot();
        var runtime = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "ModEntry.GeodeProcessing.cs"));
        var bridge = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters", "PlayerReadAdapter.GeodeProcessing.cs"));
        Assert.Contains("active.Location.checkAction", runtime, StringComparison.Ordinal);
        Assert.Contains("active.Location.answerDialogue", runtime, StringComparison.Ordinal);
        Assert.Contains("menu.receiveLeftClick", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"Game1\.player\.Money\s*[+\-]?=(?!=)"), runtime);
        Assert.DoesNotMatch(new Regex(@"Game1\.stats\.(GeodesCracked|Set)\s*[+\-]?=(?!=)"), runtime);
        Assert.DoesNotContain("Game1.random", bridge, StringComparison.Ordinal);
        Assert.Contains("complete_current-season_family_is_published_without_consuming_it", bridge, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[] { OptionId }, true);

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId, OptionId = OptionId, Kind = candidate.Kind,
        Available = candidate.Available, LocationId = candidate.LocationId,
        ExpectedEffect = candidate.ExpectedEffect, EstimatedTicks = candidate.EstimatedTicks,
        Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "geode.action", SourceModel = "test", StateHash = snapshot.StateHash,
        GoalId = "goal.process.geode", ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "crack.geode", OptionId = OptionId,
            Rationale = "process projected geode", Parameters = parameters } }
    };

    private static SnapshotEnvelope Snapshot(string playerLocation, bool capacity = true,
        string? serviceStatus = null, bool includeRoute = false)
    {
        serviceStatus ??= playerLocation == "Blacksmith" ? "ready" : "route_to_blacksmith_required";
        var projection = new
        {
            schema_version = "geode_processing.v1", projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = new string('g', 64), location_id = "Blacksmith", base_service_status = serviceStatus,
            price_gold = 25, money_before = 1000, free_inventory_slots = capacity ? 5 : 0,
            geodes_cracked_before = 42, mystery_boxes_opened_before = 11, golden_coconut_cracked_before = false,
            golden_walnuts_before = 17, golden_walnuts_found_before = 29, archaeology_found_count = 0,
            predictor_context = new { save_id_half = 12345L, player_id_half = 678L, season = "fall",
                deepest_mine_level = 90, skill_1_unmodified_level = 8, farming_mastery_unlocked = false,
                qi_beans_rule_active = false, got_mystery_book_mail = true, artifact_found_mail = false },
            inventory_inputs = new[] { new { slot_index = 0, qualified_item_id = "(O)535", item_id = "535",
                display_name = "Geode", stack_before = capacity ? 2 : 2, quality = 0, locked_base_1_6_15 = true,
                output_capacity_allowed = capacity, kind = "exact", status = "available",
                expected_output = new { qualified_item_id = "(O)378", stack = 3, quality = 0, set_flag_on_pickup = "",
                    inventory_persists = true, pickup_effect_kind = "copper_found_counter", expected_mail_additions = Array.Empty<string>() },
                accepted_outputs = new[] { new { qualified_item_id = "(O)378", stack = 3, quality = 0, set_flag_on_pickup = "",
                    inventory_persists = true, pickup_effect_kind = "copper_found_counter", expected_mail_additions = Array.Empty<string>() } },
                expected_mail_additions = Array.Empty<string>(), reason = "seeded_exact" } },
            counter_action_tiles = new[] { new { tile_x = 3, tile_y = 3, action_raw = "Blacksmith", action_token = "Blacksmith" } },
            native_contract = NativeContract
        };
        var edges = includeRoute ? new object[] { new { kind = "warp", from_location = "Town", from_x = 5,
            from_y = 5, target_location = "Blacksmith", target_x = 3, target_y = 4, resolved = true } } : Array.Empty<object>();
        var connectors = includeRoute ? new object[] { new { kind = "warp", tile_x = 5, tile_y = 5,
            target_location = "Blacksmith", target_x = 3, target_y = 4, resolved = true } } : Array.Empty<object>();
        var json = JsonSerializer.Serialize(new
        {
            player = new { location_id = Field(playerLocation), tile_x = Field(playerLocation == "Blacksmith" ? 3 : 4),
                tile_y = Field(playerLocation == "Blacksmith" ? 4 : 5), energy = Field(270),
                geode_processing = Field(projection), inventory = Field(new { free_slots = capacity ? 5 : 0 }) },
            locations = new { route_graph = Field(new { status = "complete", edges }),
                route_connectors = Field(new { location_id = playerLocation, connectors }),
                route_action_branch_coverage = Field(new { rows = Array.Empty<object>() }),
                collision_grid = Field(new { location_id = playerLocation, width = 80, height = 80, notable_tiles = Array.Empty<object>() }) },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope { SchemaVersion = "transparent_state.v1", StateHash = SnapshotHash.ComputeStateHash(state),
            GameTick = 1, RealTimestamp = "2026-08-30T00:00:00Z", Completeness = "complete", State = state };
    }

    private static object Field(object value) => new { value, status = "available", source = new { kind = "game_object", path = "test" }, adapter = "test", read_at_tick = 1, confidence = 1 };
    private static void AssertParameter(SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);
    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

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
