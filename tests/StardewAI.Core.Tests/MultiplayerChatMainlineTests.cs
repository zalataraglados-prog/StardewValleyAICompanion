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

public sealed class MultiplayerChatMainlineTests
{
    private const string OptionId = "multiplayer.send_chat";
    private const string NativeContract =
        "ChatBox_activate_then_ChatTextBox_character_input_under_880px_then_textBoxEnter_TextBox_then_global_AllPlayers_or_compiler_owned_message_private_then_Multiplayer_type10_network_dispatch_and_sender_local_ChatMessage_receipt";

    [Theory]
    [InlineData("global", "", "Hello farm")]
    [InlineData("private", "200", "Meet at the mine")]
    public void ExplicitChatIntentReachesOneTypedNativeQueue(string scope, string recipientId, string message)
    {
        var snapshot = Snapshot();
        var candidate = Assert.Single(Assert.Single(Evaluate(snapshot, scope, recipientId, message).Options).EventCandidates);
        Assert.True(candidate.Available, string.Join(";", candidate.BlockReasons));
        Assert.Equal("send_multiplayer_chat", candidate.Kind);
        AssertParameter(candidate.Parameters, "chat_scope", scope);
        AssertParameter(candidate.Parameters, "chat_message_text", message);
        AssertParameter(candidate.Parameters, "chat_recipient_player_id", recipientId);

        var plan = new DailyPlanCompiler().Compile(new[] { Prediction(candidate) }, snapshot.StateHash);
        var planStep = Assert.Single(plan.Steps);
        Assert.Equal("send_multiplayer_chat", planStep.Kind);
        Assert.Contains("player_command_only_and_explicit_confirmation_required", planStep.SafetyConstraints);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var item = Assert.Single(queue.Items);
        Assert.Empty(item.BlockingReasons);
        Assert.Equal("executor.send_multiplayer_chat", item.OptionId);
        Assert.Equal("send_multiplayer_chat", Assert.Single(item.NormalizedCommand.Steps).StepType);
    }

    [Theory]
    [InlineData("global", "", "/money 999")]
    [InlineData("private", "200", "two  spaces")]
    [InlineData("private", "999", "unknown target")]
    public void UnsafeOrUnstableIntentIsExcludedUpstream(string scope, string recipientId, string message)
    {
        var option = Assert.Single(Evaluate(Snapshot(), scope, recipientId, message).Options);
        Assert.Empty(option.EventCandidates);
    }

    [Fact]
    public void PrivateTargetThatNativePrefixMatchingWouldMisrouteIsExcludedUpstream()
    {
        var snapshot = Snapshot(new[]
        {
            Recipient(0, "100", "Chat"),
            Recipient(1, "200", "Chat Test Farmhand")
        });
        var option = Assert.Single(Evaluate(snapshot, "private", "200", "meet here").Options);
        Assert.Empty(option.EventCandidates);
    }

    [Fact]
    public void CompilerOverwritesForgedSenderRecipientAndTransportStateFromFreshProjection()
    {
        var snapshot = Snapshot();
        var action = Action(snapshot, new[]
        {
            P("chat_scope", "private"), P("chat_reason", "explicit test"), P("confirm_chat", "true"),
            P("chat_message_text", "Meet at the mine"), P("chat_recipient_player_id", "200"),
            P("chat_sender_player_id", "forged"), P("chat_recipient_display_name", "forged"),
            P("chat_network_role", "client"), P("chat_language_code", "999"),
            P("chat_message_sha256", "forged"), P("chat_network_message_type", "99"),
            P("chat_expected_wire_recipient_id", "forged"), P("chat_expected_kind", "99"),
            P("chat_message_count_before", "999"), P("chat_message_limit", "999"),
            P("chat_input_width_pixels", "999"), P("chat_input_content_width_pixels", "999"),
            P("chat_native_route", "forged"),
            P("native_contract", "forged")
        });

        var item = Assert.Single(new ActionQueueCompiler().Compile(action, snapshot).Items);
        Assert.Empty(item.BlockingReasons);
        AssertParameter(item.NormalizedCommand.Parameters, "chat_sender_player_id", "50");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_recipient_display_name", "Chat Test Farmhand");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_recipient_command_name", "Chat Test Farmhand");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_network_role", "server");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_language_code", "0");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_expected_wire_recipient_id", "200");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_expected_kind", "3");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_network_message_type", "10");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_message_count_before", "2");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_message_limit", "10");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_input_width_pixels", "896");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_input_content_width_pixels", "880");
        AssertParameter(item.NormalizedCommand.Parameters, "chat_native_route", "compiler_owned_message_private");
        AssertParameter(item.NormalizedCommand.Parameters, "native_contract", NativeContract);
    }

    [Fact]
    public void CapabilityAndRuntimeKeepChatOutOfPolicyTrainingAndDirectTransportCalls()
    {
        foreach (var optionId in new[] { OptionId, "executor.send_multiplayer_chat" })
        {
            var capability = OptionCapabilityRegistrySource.GetRequired(optionId);
            Assert.Equal(new[] { "EVD-311" }, capability.ReadEvidenceIds);
            Assert.Equal(new[] { "EVD-311" }, capability.RuntimeEvidenceIds);
            Assert.Equal(OptionInvocationPolicy.PlayerCommandOnly, capability.InvocationPolicy);
            Assert.True(capability.PlayerConfirmationRequired);
            Assert.False(TrainingEligibilityPolicy.IsEligible(capability));
            Assert.False(PendingSemanticActionCatalog.TryGet(optionId, out _));
        }
        Assert.True(RuntimeTestHarnessDispatchCatalog.IsSupported("executor.send_multiplayer_chat"));

        var runtime = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "tools",
            "StardewAI.RuntimeTestHarness", "ModEntry.MultiplayerChat.cs"));
        Assert.Contains("chat.activate();", runtime, StringComparison.Ordinal);
        Assert.Contains("chat.chatBox.RecieveTextInput(character);", runtime, StringComparison.Ordinal);
        Assert.Contains("chat.textBoxEnter(chat.chatBox);", runtime, StringComparison.Ordinal);
        Assert.Contains("findMatchingFarmer", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("Game1.multiplayer.sendChatMessage", runtime, StringComparison.Ordinal);
        Assert.DoesNotContain("chat.receiveChatMessage", runtime, StringComparison.Ordinal);
    }

    [Fact]
    public void TypedRequestAndReceiptFieldsRoundTrip()
    {
        var request = new TrainingExecutionRequest
        {
            ChatScope = "private", ChatMessageText = "Meet at the mine", ChatRecipientPlayerId = "200",
            ChatMessageSha256 = new string('a', 64), ChatExpectedKind = 3, ChatNetworkMessageType = 10
        };
        var requestRoundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(JsonSerializer.Serialize(request, JsonOptions), JsonOptions)!;
        Assert.Equal("private", requestRoundTrip.ChatScope);
        Assert.Equal("Meet at the mine", requestRoundTrip.ChatMessageText);
        Assert.Equal(10, requestRoundTrip.ChatNetworkMessageType);

        var result = new TrainingExecutionResult
        {
            ChatScope = "private", ChatRequestedMessageSha256 = new string('a', 64),
            ChatFilteredMessageSha256 = new string('b', 64), ChatLocalReceiptVerified = true
        };
        var resultRoundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(JsonSerializer.Serialize(result, JsonOptions), JsonOptions)!;
        Assert.True(resultRoundTrip.ChatLocalReceiptVerified);
        Assert.Equal(new string('b', 64), resultRoundTrip.ChatFilteredMessageSha256);
    }

    private static OptionAvailabilityEnvelope Evaluate(SnapshotEnvelope snapshot, string scope, string recipientId, string message) =>
        new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, new[]
        {
            new OptionAvailabilityCandidate
            {
                OptionId = OptionId,
                InvocationSource = OptionInvocationSource.PlayerCommand,
                ExplicitConfirmationGranted = true,
                Parameters = new[]
                {
                    P("chat_scope", scope), P("chat_reason", "explicit test command"), P("confirm_chat", "true"),
                    P("chat_message_text", message), P("chat_recipient_player_id", recipientId)
                }
            }
        }, true);

    private static PolicyEventCandidatePrediction Prediction(EventCandidate candidate) => new()
    {
        CandidateId = candidate.CandidateId, OptionId = OptionId, Kind = candidate.Kind,
        Available = candidate.Available, LocationId = candidate.LocationId, ExpectedEffect = candidate.ExpectedEffect,
        EstimatedTicks = candidate.EstimatedTicks, Parameters = candidate.Parameters
    };

    private static SmallModelActionEnvelope Action(SnapshotEnvelope snapshot, SmallModelActionParameter[] parameters) => new()
    {
        ModelOutputId = "multiplayer-chat.action", SourceModel = "test", StateHash = snapshot.StateHash,
        GoalId = "goal.explicit.multiplayer-chat", ExecutionMode = "training_singleplayer",
        Actor = new ActionActorRef { ActorId = "training_farmer.main", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
        Actions = new[] { new SmallModelAction { ActionId = "send.chat", OptionId = OptionId, Rationale = "explicit player request", Parameters = parameters } }
    };

    private static SnapshotEnvelope Snapshot(object[]? recipients = null)
    {
        recipients ??= new[] { Recipient(0, "200", "Chat Test Farmhand") };
        var projection = new
        {
            schema_version = "multiplayer_chat.v1", projection_status = "complete_locked_base_1.6.15",
            projection_fingerprint = new string('f', 64), invocation_policy = "player_command_only", service_status = "ready",
            network_role = "server", is_multiplayer = true, is_server = true, is_client = false,
            sender_player_id = "50", sender_display_name = "AI Host", sender_default_chat_color = "white",
            language_code = 0, all_players_recipient_id = "0", global_chat_kind = 0, private_chat_kind = 3,
            network_message_type = 10, chat_box_present = true, chat_box_active = false, chat_message_count = 2,
            chat_message_limit = 10, chat_message_display_ticks = 600, input_width_pixels = 896,
            input_content_width_pixels = 880, online_recipients = recipients, native_contract = NativeContract
        };
        var json = JsonSerializer.Serialize(new
        {
            player = new { location_id = Field("Farm"), multiplayer_chat = Field(projection) },
            menus = new { active_menu = Field(new { is_open = false, type = "none" }) }
        });
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json, JsonOptions)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "transparent_state.v1", StateHash = SnapshotHash.ComputeStateHash(state), GameTick = 1,
            RealTimestamp = "2026-08-30T00:00:00Z", Completeness = "complete", State = state
        };
    }

    private static object Recipient(int index, string id, string displayName) => new
    {
        native_enumeration_index = index, player_id = id, name = displayName, display_name = displayName,
        display_name_tokens = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries),
        native_command_name = string.Join(" ", displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)),
        native_match_token_count = displayName.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length,
        is_active = true, private_base_gate_status = "payload_dependent_first_match_validation_required"
    };

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
