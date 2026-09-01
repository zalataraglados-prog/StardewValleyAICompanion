using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class LiveTrainingPolicyTrajectoryIntegrationTests
{
    [Fact]
    public void AggregateExecutionWritesOneTrajectoryForAllMechanicalContinuationSteps()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
        var artifactDirectory = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(artifactDirectory);
        var beforeSnapshotPath = Path.Combine(artifactDirectory, "before.json");
        File.WriteAllText(beforeSnapshotPath, BeforeSnapshot().ToJsonString());
        var rankingPath = Path.Combine(artifactDirectory, "ranking.json");
        File.WriteAllText(rankingPath, JsonSerializer.Serialize(Decision(), JsonOptions));
        var first = Execution(beforeSnapshotPath, rankingPath, "execution.1.json");
        var duplicatePrimitive = Execution(beforeSnapshotPath, rankingPath, "execution.2.json");
        first["selected_queue_candidate_completed"] = false;
        duplicatePrimitive["effective_before_state_hash"] = "hash.after.first";
        duplicatePrimitive["before_game_tick"] = 130L;
        duplicatePrimitive["after_game_tick"] = 160L;
        var aggregate = new JsonObject
        {
            ["step_results"] = new JsonArray(first, duplicatePrimitive)
        };
        var options = new LiveTrainingOptions
        {
            Root = root,
            RunId = "run.integration"
        };
        var append = new TrainingDatasetAppendResult
        {
            DatasetPath = Path.Combine(root, "datasets", "features.jsonl"),
            RowId = "row.integration",
            EpisodeId = "episode.integration"
        };

        var result = InvokeAppender(options, aggregate, append);

        Assert.Equal(1, result.AppendedCount);
        Assert.Equal(0, result.SkippedCount);
        Assert.Equal(string.Empty, result.FirstSkipReason);
        var line = Assert.Single(File.ReadAllLines(options.PolicyTrajectoryDatasetPath));
        using var document = JsonDocument.Parse(line);
        Assert.Equal("social:gift:Abigail", document.RootElement.GetProperty("selection").GetProperty("candidate_id").GetString());
        Assert.Equal(2, document.RootElement.GetProperty("candidates").GetArrayLength());
        Assert.Equal("hash.before", document.RootElement.GetProperty("source_state_hash").GetString());
        Assert.Equal(60L, document.RootElement.GetProperty("outcome").GetProperty("actual_ticks").GetInt64());
    }

    [Fact]
    public void DeferredCompilerWaitIsNotMisreportedAsRejectedPolicySelection()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
        var artifactDirectory = Path.Combine(root, "artifacts");
        Directory.CreateDirectory(artifactDirectory);
        var beforeSnapshotPath = Path.Combine(artifactDirectory, "before.json");
        File.WriteAllText(beforeSnapshotPath, BeforeSnapshot().ToJsonString());
        var rankingPath = Path.Combine(artifactDirectory, "ranking.json");
        File.WriteAllText(rankingPath, JsonSerializer.Serialize(Decision(selectedAvailable: false), JsonOptions));
        var options = new LiveTrainingOptions
        {
            Root = root,
            RunId = "run.deferred"
        };

        var result = InvokeAppender(
            options,
            Execution(beforeSnapshotPath, rankingPath, "execution.wait.json"),
            new TrainingDatasetAppendResult
            {
                DatasetPath = Path.Combine(root, "datasets", "features.jsonl"),
                RowId = "row.deferred",
                EpisodeId = "episode.deferred"
            });

        Assert.Equal(0, result.AppendedCount);
        Assert.Equal(1, result.SkippedCount);
        Assert.Equal("effective_candidate_unavailable", result.FirstSkipReason);
        Assert.False(File.Exists(options.PolicyTrajectoryDatasetPath));
    }

    private static PolicyTrajectoryAppendBatchResult InvokeAppender(
        LiveTrainingOptions options,
        JsonObject execution,
        TrainingDatasetAppendResult append)
    {
        var programType = typeof(LiveTrainingOptions).Assembly.GetType("Program", throwOnError: true)!;
        var method = programType.GetMethod(
            "AppendPolicyDecisionTrajectories",
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("LiveTrainingLoop policy appender was not found.");
        return Assert.IsType<PolicyTrajectoryAppendBatchResult>(method.Invoke(null, new object[]
        {
            options,
            1,
            execution,
            append
        }));
    }

    private static JsonObject Execution(
        string beforeSnapshotPath,
        string rankingPath,
        string executionPath) => new()
    {
        ["status"] = "applied",
        ["primitive_verification_status"] = "verified",
        ["after_snapshot_fresh"] = true,
        ["selected_queue_candidate_completed"] = true,
        ["effective_ranking_path"] = rankingPath,
        ["effective_decision_source_state_hash"] = "hash.before",
        ["effective_before_state_hash"] = "hash.before",
        ["effective_before_snapshot_path"] = beforeSnapshotPath,
        ["effective_model_plan_path"] = "model-plan.json",
        ["effective_compiled_queue_path"] = "compiled-queue.json",
        ["effective_queue_id"] = "queue.integration",
        ["option_id"] = "executor.social_interact",
        ["execution_path"] = executionPath,
        ["after_state_hash"] = "hash.after",
        ["state_hash_changed"] = true,
        ["before_game_tick"] = 100L,
        ["after_game_tick"] = 130L,
        ["changed_facts"] = new JsonArray(),
        ["effective_queue_item"] = new JsonObject
        {
            ["normalized_command"] = new JsonObject
            {
                ["parameters"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["name"] = "precondition",
                        ["value"] = "candidate_id:social:gift:Abigail"
                    }
                }
            }
        }
    };

    private static JsonObject BeforeSnapshot() => new()
    {
        ["save_id"] = Field("save.integration"),
        ["state"] = new JsonObject
        {
            ["time"] = new JsonObject
            {
                ["year"] = Field(2),
                ["season"] = Field("fall"),
                ["day"] = Field(9),
                ["time"] = Field(1140)
            }
        }
    };

    private static AvailabilityAwarePolicyPredictionEnvelope Decision(bool selectedAvailable = true) => new()
    {
        RankedEventCandidates = new[]
        {
            new PolicyEventCandidatePrediction
            {
                CandidateId = "social:gift:Abigail",
                OptionId = "social.gift_npc",
                Kind = "social_gift",
                Rank = 1,
                Score = 1,
                ExpectedReward = 0.2,
                Available = selectedAvailable,
                EstimatedTicks = 30
            },
            new PolicyEventCandidatePrediction
            {
                CandidateId = "economy:buy:seed",
                OptionId = "economy.buy_supplies",
                Kind = "shop_purchase",
                Rank = 2,
                Score = 0.5,
                ExpectedReward = 0.1,
                Available = true,
                EstimatedTicks = 60
            }
        }
    };

    private static JsonObject Field<T>(T value) => new()
    {
        ["status"] = "readable",
        ["value"] = JsonValue.Create(value)
    };

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
