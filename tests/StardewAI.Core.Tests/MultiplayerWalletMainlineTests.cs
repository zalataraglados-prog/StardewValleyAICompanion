using System.Text.Json;
using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class MultiplayerWalletMainlineTests
{
    private const string OptionId = "multiplayer.manage_wallet";
    private const string NativeContract =
        "ManorHouse_LedgerBook_checkAction_then_native_DialogueBox_response_clicks_then_optional_DigitEntryMenu_digit_clicks_then_changeWalletTypeTonight_or_sendMoney_receipt_then_Game1_newDay_player_wallets_barrier_settlement";

    [Theory]
    [InlineData("schedule_separate", false, false)]
    [InlineData("cancel_separate", false, true)]
    [InlineData("schedule_merge", true, false)]
    [InlineData("cancel_merge", true, true)]
    [InlineData("transfer", true, false)]
    public void AllFiveExplicitCommandsReachOneTypedNativeQueue(string operation, bool separate, bool pending)
    {
        var snapshot = Snapshot("ManorHouse", separate, pending);
        var option = Assert.Single(Evaluate(snapshot, operation).Options);
        var candidate = Assert.Single(option.EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("manage_multiplayer_wallet", candidate.Kind);
        AssertParameter(candidate.Parameters, "wallet_operation", operation);
        AssertParameter(candidate.Parameters, "wallet_ledger_action_raw", "LedgerBook");
        if (operation == "transfer")
        {
            AssertParameter(candidate.Parameters, "wallet_recipient_response_key", "Transfer1");
            AssertParameter(candidate.Parameters, "wallet_expected_individual_balances_after_csv", "100:650,200:250,300:101");
        }

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("manage_multiplayer_wallet", planStep.Kind);
        Assert.Contains("player_command_only_and_explicit_confirmation_required", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.manage_multiplayer_wallet", item.OptionId);
        Assert.Equal("manage_multiplayer_wallet", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Fact]
    public void RemotePlayerCommandCarriesExactWalletIntentAcrossRouteConnector()
    {
        var remote = Snapshot("Farm", separate: true, pending: false, includeRoute: true);
        var routeCandidate = Assert.Single(Assert.Single(Evaluate(remote, "transfer").Options).EventCandidates);
        Assert.Equal("route_connector_tile", routeCandidate.Kind);
        AssertParameter(routeCandidate.Parameters, "continuation.wallet_operation", "transfer");
        AssertParameter(routeCandidate.Parameters, "continuation.wallet_recipient_player_id", "200");
        AssertParameter(routeCandidate.Parameters, "continuation.wallet_transfer_amount", "50");

        var resumed = Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            Snapshot("ManorHouse", separate: true, pending: false),
            new[]
            {
                new OptionAvailabilityCandidate
                {
                    OptionId = OptionId,
                    InvocationSource = OptionInvocationSource.PlayerCommand,
                    ExplicitConfirmationGranted = true,
                    Parameters = routeCandidate.Parameters
                }
            }, true).Options);
        var terminal = Assert.Single(resumed.EventCandidates);
        Assert.True(terminal.Available, string.Join(";", terminal.BlockReasons));
        Assert.Equal("manage_multiplayer_wallet", terminal.Kind);
    }

    [Fact]
    public void CompilerOverwritesForgedMechanicalWalletStateFromFreshProjection()
    {
        var snapshot = Snapshot("ManorHouse", separate: true, pending: false);
        var action = Action(snapshot, new[]
        {
            P("wallet_operation", "transfer"), P("wallet_reason", "explicit test transfer"),
            P("confirm_wallet_operation", "true"), P("confirm_wallet_transfer", "true"),
            P("wallet_recipient_player_id", "200"), P("wallet_transfer_amount", "50"),
            P("wallet_sender_money_before", "999999"), P("wallet_recipient_response_key", "Transfer99"),
            P("wallet_expected_individual_balances_after_csv", "forged"), P("target_tile_x", "99"),
            P("target_tile_y", "99"), P("native_contract", "forged")
        });

        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "wallet_sender_money_before", "700");
        AssertParameter(item.NormalizedCommand.Parameters, "wallet_recipient_response_key", "Transfer1");
        AssertParameter(item.NormalizedCommand.Parameters, "wallet_expected_individual_balances_after_csv", "100:650,200:250,300:101");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_x", "2");
        AssertParameter(item.NormalizedCommand.Parameters, "target_tile_y", "5");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void CapabilityAndRuntimeKeepWalletCommandsOutOfAutonomyAndDirectMutation()
    {
        foreach (var optionId in new[] { OptionId, "executor.manage_multiplayer_wallet" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-310" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-310" }, capability.RuntimeEvidenceIds);
            Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, capability.InvocationPolicy);
            Assert.True(capability.PlayerConfirmationRequired);
            Assert.False(capability.HostOnly);
            Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.manage_multiplayer_wallet"));

        var runtime = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools",
            "StardewAI.RuntimeTestHarness", "ModEntry.MultiplayerWallet.cs"));
        Assert.Contains("active.Manor.checkAction(", runtime, StringComparison.Ordinal);
        Assert.Contains("GetField(\"digits\"", runtime, StringComparison.Ordinal);
        Assert.DoesNotMatch(new Regex(@"useSeparateWallets\.Value\s*=(?!=)"), runtime);
        Assert.DoesNotMatch(new Regex(@"changeWalletTypeTonight\.Value\s*=(?!=)"), runtime);
        Assert.DoesNotMatch(new Regex(@"team\.money\.Value\s*=(?!=)"), runtime);
        Assert.DoesNotContain("SetIndividualMoney", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("AddIndividualMoney", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("ManorHouse.SeparateWallets", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("ManorHouse.MergeWallets", runtime, StringComparison.Ordinal);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot, string operation) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true,
                Parameters = Intent(operation)
            }
        }, true);

    private static SmallModelActionParameter[] Intent(string operation) => new[]
    {
        P("wallet_operation", operation),
        P("wallet_reason", "explicit test command"),
        P("confirm_wallet_operation", "true"),
        P("confirm_wallet_transfer", operation == "transfer" ? "true" : "false"),
        P("wallet_recipient_player_id", operation == "transfer" ? "200" : string.Empty),
        P("wallet_transfer_amount", operation == "transfer" ? "50" : string.Empty)
    };

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

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "multiplayer-wallet.action",
        SourceModel = "test",
        StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.multiplayer-wallet",
        ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[]
        {
            new SmallModelAction { ActionId = "manage.wallet", OptionId = OptionId, Rationale = "explicit player request", Parameters = parameters }
        }
    };

    private static SnapshotEnvelope Snapshot(string playerLocation, bool separate, bool pending, bool includeRoute = false)
    {
        var mode = separate ? "separate" : "shared";
        var participants = separate
            ? new[]
            {
                new { player_id = "100", effective_balance = 700 },
                new { player_id = "200", effective_balance = 200 },
                new { player_id = "300", effective_balance = 101 }
            }
            : new[]
            {
                new { player_id = "100", effective_balance = 1001 },
                new { player_id = "200", effective_balance = 1001 },
                new { player_id = "300", effective_balance = 1001 }
            };
        string Gate(string operation) => operation switch
        {
            "schedule_separate" => !separate && !pending ? "ready" : "blocked",
            "cancel_separate" => !separate && pending ? "ready" : "blocked",
            "schedule_merge" => separate && !pending ? "ready" : "blocked",
            "cancel_merge" => separate && pending ? "ready" : "blocked",
            "transfer" => separate ? "ready" : "blocked",
            _ => "blocked"
        };
        var projection = new
        {
            schema_version = "multiplayer_wallet.v1",
            projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = new string('a', 64),
            invocation_policy = "player_command_only",
            location_id = "ManorHouse",
            service_status = playerLocation == "ManorHouse" ? "ready" : "route_to_manor_house_required",
            local_player_id = "100",
            is_host = true,
            wallet_mode = mode,
            use_separate_wallets = separate,
            change_wallet_type_tonight = pending,
            pending_transition = !pending ? "none" : separate ? "merge_tonight" : "separate_tonight",
            claimed_participant_count = 3,
            claimed_farmhand_count = 2,
            shared_money = 1001,
            local_effective_money = separate ? 700 : 1001,
            current_individual_total = separate ? 1001 : 0,
            total_money_gifted = 10,
            participants,
            recipients = new[]
            {
                new { player_id = "200", response_key = "Transfer1", balance = separate ? 200 : 1001 },
                new { player_id = "300", response_key = "Transfer2", balance = separate ? 101 : 1001 }
            },
            commands = new[]
            {
                new { operation = "schedule_separate", gate_status = Gate("schedule_separate") },
                new { operation = "cancel_separate", gate_status = Gate("cancel_separate") },
                new { operation = "schedule_merge", gate_status = Gate("schedule_merge") },
                new { operation = "cancel_merge", gate_status = Gate("cancel_merge") },
                new { operation = "transfer", gate_status = Gate("transfer") }
            },
            separation_settlement = new { each_balance = 333, resulting_total = 999, discarded_integer_remainder = 2 },
            merge_settlement = new { resulting_shared_money = separate ? 1001 : 0 },
            ledger_action_tiles = new[] { new { tile_x = 2, tile_y = 5, action_raw = "LedgerBook" } },
            native_contract = NativeContract
        };
        var edges = includeRoute
            ? new object[] { new { kind = "warp", from_location = "Farm", from_x = 4, from_y = 4, target_location = "ManorHouse", target_x = 3, target_y = 10, resolved = true } }
            : Array.Empty<object>();
        var connectors = includeRoute
            ? new object[] { new { kind = "warp", tile_x = 4, tile_y = 4, target_location = "ManorHouse", target_x = 3, target_y = 10, resolved = true } }
            : Array.Empty<object>();
        var json = JsonSerializer.Serialize(new
        {
            player = new
            {
                location_id = Field(playerLocation), tile_x = Field(includeRoute ? 3 : 2),
                tile_y = Field(includeRoute ? 4 : 6), energy = Field(270), multiplayer_wallet = Field(projection)
            },
            locations = new
            {
                route_graph = Field(new { status = "complete", edges }),
                route_connectors = Field(new { location_id = playerLocation, connectors }),
                route_action_branch_coverage = Field(new { rows = Array.Empty<object>() }),
                collision_grid = Field(new { location_id = playerLocation, width = 40, height = 30, notable_tiles = Array.Empty<object>() })
            },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static object Field(object value) => new
    {
        value, status = "available", source = new { kind = "game_object", path = "test" },
        adapter = "test", read_at_tick = 1, confidence = 1
    };

    private static void AssertParameter(SmallModelActionParameter[] parameters, string name, string value) =>
        Assert.Contains(parameters, parameter => parameter.Name == name && parameter.Value == value);

    private static SmallModelActionParameter P(string name, string value) => new() { Name = name, Value = value };

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
