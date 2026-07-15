using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Tests;

public sealed class TrainingExecutionContractTests
{
    [Fact]
    public void TrainingExecutionResultSerializesPrimitiveVerificationFields()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.test",
            QueueId = "queue.test",
            QueueItemId = "queue-item.test",
            BeforeStateHash = "hash.before",
            OptionId = "executor.move_to_tile",
            Status = "applied",
            FeedbackAvailable = true,
            PrimitiveKind = "move_to_tile",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "target_tile_reached" },
            RequestedEffect = "player.tile=41,23",
            ObservedEffect = "player.tile=41,23",
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.tile", Before = "42,23", After = "41,23" }
            }
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.Contains("\"primitive_kind\":\"move_to_tile\"", json);
        Assert.Contains("\"primitive_verification_status\":\"verified\"", json);
        Assert.Equal("move_to_tile", roundTrip.PrimitiveKind);
        Assert.Equal("verified", roundTrip.PrimitiveVerificationStatus);
        Assert.Contains("target_tile_reached", roundTrip.PrimitiveVerificationReasons);
        Assert.Equal("player.tile=41,23", roundTrip.RequestedEffect);
        Assert.Equal("player.tile=41,23", roundTrip.ObservedEffect);
    }

    [Fact]
    public void TrainingExecutionResultDeserializesOldJsonWithVerificationDefaults()
    {
        var result = JsonSerializer.Deserialize<TrainingExecutionResult>("""
        {
          "schema_version":"training_execution_result.v1",
          "run_id":"run.old",
          "queue_id":"queue.old",
          "queue_item_id":"item.old",
          "before_state_hash":"hash.old",
          "option_id":"farm.maintain_crops",
          "status":"applied",
          "feedback_available":true,
          "changed_facts":[],
          "block_reasons":[]
        }
        """, JsonOptions)!;

        Assert.Equal(string.Empty, result.PrimitiveKind);
        Assert.Equal("not_applicable", result.PrimitiveVerificationStatus);
        Assert.Empty(result.PrimitiveVerificationReasons);
        Assert.Equal(string.Empty, result.RequestedEffect);
        Assert.Equal(string.Empty, result.ObservedEffect);
    }

    [Fact]
    public void CombatExecutionFieldsRoundTripWithoutTextParsing()
    {
        var request = new TrainingExecutionRequest
        {
            OptionId = "executor.combat_monster",
            TargetTileX = 4,
            TargetTileY = 6,
            TargetRuntimeType = "StardewValley.Monsters.GreenSlime",
            TargetRuntimeIdentity = "0000BEEF",
            TargetName = "Green Slime",
            MaxAttacks = 12
        };
        var requestJson = JsonSerializer.Serialize(request, JsonOptions);
        var requestRoundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(requestJson, JsonOptions)!;
        Assert.Equal(12, requestRoundTrip.MaxAttacks);
        Assert.Equal("Green Slime", requestRoundTrip.TargetName);
        Assert.Equal("0000BEEF", requestRoundTrip.TargetRuntimeIdentity);

        var result = new TrainingExecutionResult
        {
            CombatTargetRuntimeType = request.TargetRuntimeType,
            CombatTargetRuntimeIdentity = request.TargetRuntimeIdentity,
            CombatTargetName = request.TargetName,
            CombatAttackCount = 3,
            CombatHitCount = 2,
            CombatTargetHealthSequence = new[] { 24, 12, 0 },
            CombatPlayerHealthSequence = new[] { 100, 100 },
            CombatDamageTaken = 0,
            CombatTargetDefeated = true
        };
        var resultJson = JsonSerializer.Serialize(result, JsonOptions);
        var resultRoundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(resultJson, JsonOptions)!;
        Assert.Equal(new[] { 24, 12, 0 }, resultRoundTrip.CombatTargetHealthSequence);
        Assert.True(resultRoundTrip.CombatTargetDefeated);
        Assert.Equal(0, resultRoundTrip.CombatDamageTaken);
    }

    [Fact]
    public void RecoveryExecutionFieldsRoundTripWithoutTextParsing()
    {
        var result = new TrainingExecutionResult
        {
            OptionId = "executor.consume_food",
            RecoveryFoodSlotIndex = 5,
            RecoveryFoodQualifiedItemId = "(O)194",
            RecoveryFoodStackBefore = 3,
            RecoveryFoodStackAfter = 2,
            RecoveryHealthBefore = 18,
            RecoveryHealthAfter = 51,
            RecoveryRestoreSlotIndex = 1,
            RecoverySafetyStatus = "native_eating_lifecycle_verified",
            EnergyBefore = 42,
            EnergyAfter = 77
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.Equal(5, roundTrip.RecoveryFoodSlotIndex);
        Assert.Equal("(O)194", roundTrip.RecoveryFoodQualifiedItemId);
        Assert.Equal(3, roundTrip.RecoveryFoodStackBefore);
        Assert.Equal(2, roundTrip.RecoveryFoodStackAfter);
        Assert.Equal(18, roundTrip.RecoveryHealthBefore);
        Assert.Equal(51, roundTrip.RecoveryHealthAfter);
        Assert.Equal(1, roundTrip.RecoveryRestoreSlotIndex);
        Assert.Equal("native_eating_lifecycle_verified", roundTrip.RecoverySafetyStatus);
    }

    [Fact]
    public void TrainingExecutionRequestSerializesInteractSafetyFields()
    {
        var request = new TrainingExecutionRequest
        {
            RunId = "run.interact",
            QueueId = "queue.interact",
            QueueItemId = "item.interact",
            BeforeStateHash = "hash.before",
            OptionId = "executor.interact",
            TargetTileX = 20,
            TargetTileY = 10,
            InteractionKind = "map_action",
            ExpectedActionType = "OpenShop"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Contains("\"interaction_kind\":\"map_action\"", json);
        Assert.Contains("\"expected_action_type\":\"OpenShop\"", json);
        Assert.Equal("map_action", roundTrip.InteractionKind);
        Assert.Equal("OpenShop", roundTrip.ExpectedActionType);
    }

    [Fact]
    public void TrainingExecutionRequestDeserializesOldJsonWithInteractDefaults()
    {
        var request = JsonSerializer.Deserialize<TrainingExecutionRequest>("""
        {
          "schema_version":"training_execution_request.v1",
          "run_id":"run.old",
          "queue_id":"queue.old",
          "queue_item_id":"item.old",
          "before_state_hash":"hash.old",
          "option_id":"executor.move_to_tile",
          "target_tile_x":42,
          "target_tile_y":23
        }
        """, JsonOptions)!;

        Assert.Equal(string.Empty, request.InteractionKind);
        Assert.Equal(string.Empty, request.ExpectedActionType);
    }

    [Fact]
    public void TrainingExecutionRequestSerializesConnectorSafetyFields()
    {
        var request = new TrainingExecutionRequest
        {
            RunId = "run.connector",
            QueueId = "queue.connector",
            QueueItemId = "item.connector",
            BeforeStateHash = "hash.before",
            OptionId = "executor.traverse_connector",
            TargetTileX = 27,
            TargetTileY = 31,
            ConnectorKind = "warp",
            ExpectedTargetLocation = "Farm",
            ExpectedArrivalTileX = 64,
            ExpectedArrivalTileY = 15
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Contains("\"connector_kind\":\"warp\"", json);
        Assert.Contains("\"expected_target_location\":\"Farm\"", json);
        Assert.Equal("warp", roundTrip.ConnectorKind);
        Assert.Equal("Farm", roundTrip.ExpectedTargetLocation);
        Assert.Equal(64, roundTrip.ExpectedArrivalTileX);
        Assert.Equal(15, roundTrip.ExpectedArrivalTileY);
    }

    [Fact]
    public void TrainingExecutionRequestSerializesHarvestMethod()
    {
        var request = new TrainingExecutionRequest
        {
            RunId = "run.harvest",
            QueueId = "queue.harvest",
            QueueItemId = "item.harvest",
            BeforeStateHash = "hash.before",
            OptionId = "executor.harvest_crop",
            TargetTileX = 7,
            TargetTileY = 8,
            HarvestMethod = "Grab"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Contains("\"harvest_method\":\"Grab\"", json);
        Assert.Equal("Grab", roundTrip.HarvestMethod);
    }

    [Fact]
    public void TrainingExecutionRequestSerializesDebugFillInventory()
    {
        var request = new TrainingExecutionRequest
        {
            RunId = "run.harvest",
            QueueId = "queue.harvest",
            QueueItemId = "item.harvest",
            BeforeStateHash = "hash.before",
            OptionId = "debug.setup_harvest_crop_target",
            DebugFillInventory = true
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Contains("\"debug_fill_inventory\":true", json);
        Assert.True(roundTrip.DebugFillInventory);
    }

    [Fact]
    public void TrainingExecutionRequestSerializesGiantCropId()
    {
        var request = new TrainingExecutionRequest
        {
            RunId = "run.giant",
            QueueId = "queue.giant",
            QueueItemId = "item.giant",
            BeforeStateHash = "hash.before",
            OptionId = "executor.harvest_giant_crop",
            TargetTileX = 64,
            TargetTileY = 15,
            GiantCropId = "Pumpkin"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Contains("\"giant_crop_id\":\"Pumpkin\"", json);
        Assert.Equal("Pumpkin", roundTrip.GiantCropId);
    }

    [Fact]
    public void TrainingExecutionRequestSerializesSeedId()
    {
        var request = new TrainingExecutionRequest
        {
            RunId = "run.plant",
            QueueId = "queue.plant",
            QueueItemId = "item.plant",
            BeforeStateHash = "hash.before",
            OptionId = "executor.plant_seed",
            TargetTileX = 64,
            TargetTileY = 15,
            SeedId = "472"
        };

        var json = JsonSerializer.Serialize(request, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionRequest>(json, JsonOptions)!;

        Assert.Contains("\"seed_id\":\"472\"", json);
        Assert.Equal("472", roundTrip.SeedId);
    }

    [Fact]
    public void PlanExecutionEpisodeSerializesPrimitiveVerificationFields()
    {
        var episode = new PlanExecutionEpisodeEnvelope
        {
            EpisodeId = "episode.test",
            RunId = "run.test",
            SourceStateHash = "hash.before",
            AfterStateHash = "hash.after",
            QueueId = "queue.test",
            OptionId = "executor.face_direction",
            Status = "applied",
            Success = true,
            EffectiveQueueItem = JsonDocument.Parse("""
            {"option_id":"executor.load_machine_input","normalized_command":{"parameters":[{"name":"qualified_item_id","value":"(O)262"}],"steps":[{"step_type":"load_machine_input","target":"Farm(66,15):slot0:(O)262","expected_effect":"predicted_output_qualified_item_id=(O)346;predicted_minutes_until_ready=1750"}]}}
            """).RootElement.Clone(),
            PrimitiveKind = "face_direction",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "facing_direction_matches_request" },
            RequestedEffect = "player.facing_direction=2",
            ObservedEffect = "player.facing_direction=2",
            ChangedFacts = JsonDocument.Parse("[]").RootElement.Clone()
        };

        var json = JsonSerializer.Serialize(episode, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(json, JsonOptions)!;

        Assert.Equal("face_direction", roundTrip.PrimitiveKind);
        Assert.Equal("verified", roundTrip.PrimitiveVerificationStatus);
        Assert.Contains("facing_direction_matches_request", roundTrip.PrimitiveVerificationReasons);
        Assert.Equal("player.facing_direction=2", roundTrip.RequestedEffect);
        Assert.Equal("player.facing_direction=2", roundTrip.ObservedEffect);
        Assert.True(roundTrip.EffectiveQueueItem.HasValue);
        var effectiveQueueItem = roundTrip.EffectiveQueueItem.Value;
        Assert.Equal("executor.load_machine_input", effectiveQueueItem.GetProperty("option_id").GetString());
        var step = effectiveQueueItem.GetProperty("normalized_command").GetProperty("steps")[0];
        Assert.Equal("load_machine_input", step.GetProperty("step_type").GetString());
        Assert.Contains("predicted_output_qualified_item_id=(O)346", step.GetProperty("expected_effect").GetString());
    }

    [Fact]
    public void LegacyPlanExecutionEpisodeWithoutEffectiveQueueItemRemainsSerializable()
    {
        var legacy = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>("{}", JsonOptions)!;

        Assert.Null(legacy.EffectiveQueueItem);
        legacy.ChangedFacts = JsonDocument.Parse("[]").RootElement.Clone();
        var serialized = JsonSerializer.Serialize(legacy, JsonOptions);
        Assert.Contains("\"effective_queue_item\":null", serialized);
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void QuestPlanEnvelopeSerializesExecutorBlockAndUnknownCost()
    {
        var envelope = new QuestPlanEnvelope
        {
            SchemaVersion = "quest_compiler.v1",
            SelectedCandidateId = "quest:10:ItemDeliveryQuest",
            SelectedRuntimeType = "ItemDeliveryQuest",
            Family = "ordinary_quest",
            NextActionCategory = "deliver_to_npc",
            RequiredTargetNpc = "Lewis",
            RequiredItemId = "(O)70",
            RequiredTargetCount = 1,
            TimeEstimate = "unknown",
            EnergyCost = "unknown",
            ExecutorBlockReason = "quest_native_executor_not_implemented"
        };

        var json = JsonSerializer.Serialize(envelope, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"quest_compiler.v1\"", json);
        Assert.Contains("\"quest_native_executor_not_implemented\"", json);
        Assert.Contains("\"time_estimate\":\"unknown\"", json);
        Assert.Contains("\"energy_cost\":\"unknown\"", json);
        Assert.Contains("\"ItemDeliveryQuest\"", json);
    }

    [Fact]
    public void NormalizedCommandSerializesQuestPlanEnvelope()
    {
        var command = new NormalizedCommand
        {
            OptionId = "quest.advance",
            QuestPlan = new QuestPlanEnvelope
            {
                SelectedCandidateId = "quest:10:ItemDeliveryQuest",
                ExecutorBlockReason = "quest_native_executor_not_implemented",
                TimeEstimate = "unknown",
                EnergyCost = "unknown"
            }
        };

        var json = JsonSerializer.Serialize(command, new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Contains("\"quest_plan\"", json);
        Assert.Contains("\"quest_native_executor_not_implemented\"", json);
    }

    [Fact]
    public void DialogueFieldsRoundTripSuccessfully()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.test",
            QueueId = "queue.test",
            QueueItemId = "queue-item.test",
            BeforeStateHash = "hash.before",
            OptionId = "executor.close_menu",
            Status = "applied",
            FeedbackAvailable = true,
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "dialogue_advanced_and_closed_natively" },
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = "menus.active_menu.is_open=false;menus.active_menu.type=none",
            DialogueNativeHandled = true,
            DialoguePressAttempts = 3,
            DialogueAdvanceTicks = 15,
            DialogueMenuTypeBefore = "DialogueBox",
            DialogueMenuTypeAfter = "none",
            DialogueIsQuestionBefore = false,
            DialogueIsQuestionAfter = null,
            DialogueResponseCountBefore = 0,
            DialogueResponseCountAfter = null,
            DialogueSpeakerNameBefore = "Lewis",
            DialogueSpeakerNameAfter = "",
            DialogueEventUpBefore = false,
            DialogueEventUpAfter = null
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.Contains("\"dialogue_native_handled\":true", json);
        Assert.Contains("\"dialogue_press_attempts\":3", json);
        Assert.Contains("\"dialogue_advance_ticks\":15", json);
        Assert.Contains("\"dialogue_menu_type_before\":\"DialogueBox\"", json);
        Assert.Contains("\"dialogue_menu_type_after\":\"none\"", json);
        Assert.Contains("\"dialogue_is_question_before\":false", json);
        Assert.Contains("\"dialogue_response_count_before\":0", json);
        Assert.Contains("\"dialogue_speaker_name_before\":\"Lewis\"", json);
        Assert.Contains("\"dialogue_event_up_before\":false", json);

        Assert.True(roundTrip.DialogueNativeHandled);
        Assert.Equal(3, roundTrip.DialoguePressAttempts);
        Assert.Equal(15, roundTrip.DialogueAdvanceTicks);
        Assert.Equal("DialogueBox", roundTrip.DialogueMenuTypeBefore);
        Assert.Equal("none", roundTrip.DialogueMenuTypeAfter);
        Assert.False(roundTrip.DialogueIsQuestionBefore);
        Assert.Null(roundTrip.DialogueIsQuestionAfter);
        Assert.Equal(0, roundTrip.DialogueResponseCountBefore);
        Assert.Null(roundTrip.DialogueResponseCountAfter);
        Assert.Equal("Lewis", roundTrip.DialogueSpeakerNameBefore);
        Assert.Equal("", roundTrip.DialogueSpeakerNameAfter);
        Assert.False(roundTrip.DialogueEventUpBefore);
        Assert.Null(roundTrip.DialogueEventUpAfter);
    }

    [Fact]
    public void DialogueFieldsSerializeNullsWhenDefault()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.test",
            QueueId = "queue.test",
            OptionId = "executor.move_to_tile",
            Status = "applied"
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.Null(roundTrip.DialogueNativeHandled);
        Assert.Null(roundTrip.DialoguePressAttempts);
        Assert.Null(roundTrip.DialogueAdvanceTicks);
        Assert.Equal("", roundTrip.DialogueMenuTypeBefore);
        Assert.Equal("", roundTrip.DialogueMenuTypeAfter);
        Assert.Null(roundTrip.DialogueIsQuestionBefore);
        Assert.Null(roundTrip.DialogueResponseCountBefore);
        Assert.Equal("", roundTrip.DialogueSpeakerNameBefore);
        Assert.Null(roundTrip.DialogueEventUpBefore);
    }

    [Fact]
    public void DialogueFieldsRoundTripBlockedResult()
    {
        var result = new TrainingExecutionResult
        {
            RunId = "run.test",
            QueueId = "queue.test",
            QueueItemId = "queue-item.test",
            BeforeStateHash = "hash.before",
            OptionId = "executor.close_menu",
            Status = "blocked",
            FeedbackAvailable = true,
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = "blocked",
            PrimitiveVerificationReasons = new[] { "dialogue_became_unsafe_during_advance" },
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = "menus.active_menu.is_open=true;menus.active_menu.type=DialogueBox",
            DialogueNativeHandled = true,
            DialoguePressAttempts = 5,
            DialogueAdvanceTicks = 40,
            DialogueMenuTypeBefore = "DialogueBox",
            DialogueMenuTypeAfter = "DialogueBox",
            DialogueIsQuestionBefore = false,
            DialogueIsQuestionAfter = true,
            DialogueResponseCountBefore = 0,
            DialogueResponseCountAfter = 2,
            DialogueSpeakerNameBefore = "Abigail",
            DialogueSpeakerNameAfter = "Abigail",
            DialogueEventUpBefore = false,
            DialogueEventUpAfter = true,
            BlockReasons = new[] { "dialogue_became_unsafe_during_advance" }
        };

        var json = JsonSerializer.Serialize(result, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<TrainingExecutionResult>(json, JsonOptions)!;

        Assert.True(roundTrip.DialogueNativeHandled);
        Assert.Equal(5, roundTrip.DialoguePressAttempts);
        Assert.Equal(40, roundTrip.DialogueAdvanceTicks);
        Assert.Equal("DialogueBox", roundTrip.DialogueMenuTypeBefore);
        Assert.Equal("DialogueBox", roundTrip.DialogueMenuTypeAfter);
        Assert.False(roundTrip.DialogueIsQuestionBefore);
        Assert.True(roundTrip.DialogueIsQuestionAfter);
        Assert.Equal(0, roundTrip.DialogueResponseCountBefore);
        Assert.Equal(2, roundTrip.DialogueResponseCountAfter);
        Assert.Equal("Abigail", roundTrip.DialogueSpeakerNameBefore);
        Assert.Equal("Abigail", roundTrip.DialogueSpeakerNameAfter);
        Assert.False(roundTrip.DialogueEventUpBefore);
        Assert.True(roundTrip.DialogueEventUpAfter);
        Assert.Contains("dialogue_became_unsafe_during_advance", roundTrip.BlockReasons);
    }

    [Fact]
    public void DialogueFieldsPropagateThroughPlanExecutionEpisodeEnvelope()
    {
        var episode = new PlanExecutionEpisodeEnvelope
        {
            EpisodeId = "episode.dialogue.propagation",
            RunId = "run.dialogue",
            SourceStateHash = "hash.before",
            AfterStateHash = "hash.after",
            QueueId = "queue.dialogue",
            OptionId = "executor.close_menu",
            Status = "applied",
            Success = true,
            PrimitiveKind = "close_menu",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "dialogue_advanced_and_closed_natively" },
            RequestedEffect = "menus.active_menu.is_open=false",
            ObservedEffect = "menus.active_menu.is_open=false;menus.active_menu.type=none",
            DialogueNativeHandled = true,
            DialoguePressAttempts = 3,
            DialogueAdvanceTicks = 15,
            DialogueMenuTypeBefore = "DialogueBox",
            DialogueMenuTypeAfter = "none",
            DialogueIsQuestionBefore = false,
            DialogueIsQuestionAfter = null,
            DialogueResponseCountBefore = 0,
            DialogueResponseCountAfter = null,
            DialogueSpeakerNameBefore = "Lewis",
            DialogueSpeakerNameAfter = "",
            DialogueEventUpBefore = false,
            DialogueEventUpAfter = null,
            EffectiveQueueItem = JsonDocument.Parse("{}").RootElement.Clone(),
            ChangedFacts = JsonDocument.Parse("[]").RootElement.Clone()
        };

        var json = JsonSerializer.Serialize(episode, JsonOptions);
        var roundTrip = JsonSerializer.Deserialize<PlanExecutionEpisodeEnvelope>(json, JsonOptions)!;

        Assert.Contains("\"dialogue_native_handled\":true", json);
        Assert.Contains("\"dialogue_press_attempts\":3", json);
        Assert.Contains("\"dialogue_advance_ticks\":15", json);
        Assert.Contains("\"dialogue_menu_type_before\":\"DialogueBox\"", json);
        Assert.Contains("\"dialogue_menu_type_after\":\"none\"", json);
        Assert.Contains("\"dialogue_is_question_before\":false", json);
        Assert.Contains("\"dialogue_speaker_name_before\":\"Lewis\"", json);
        Assert.Contains("\"dialogue_event_up_before\":false", json);

        Assert.True(roundTrip.DialogueNativeHandled);
        Assert.Equal(3, roundTrip.DialoguePressAttempts);
        Assert.Equal(15, roundTrip.DialogueAdvanceTicks);
        Assert.Equal("DialogueBox", roundTrip.DialogueMenuTypeBefore);
        Assert.Equal("none", roundTrip.DialogueMenuTypeAfter);
        Assert.False(roundTrip.DialogueIsQuestionBefore);
        Assert.Null(roundTrip.DialogueIsQuestionAfter);
        Assert.Equal(0, roundTrip.DialogueResponseCountBefore);
        Assert.Equal("Lewis", roundTrip.DialogueSpeakerNameBefore);
        Assert.Equal("", roundTrip.DialogueSpeakerNameAfter);
        Assert.False(roundTrip.DialogueEventUpBefore);
        Assert.Null(roundTrip.DialogueEventUpAfter);
    }
}
