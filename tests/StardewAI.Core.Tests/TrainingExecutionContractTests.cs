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
}
