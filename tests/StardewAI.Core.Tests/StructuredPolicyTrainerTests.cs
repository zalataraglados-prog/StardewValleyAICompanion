using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class StructuredPolicyTrainerTests
{
    [Fact]
    public void FormalDatasetTrainsDeterministicCheckpointAndReranksByState()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        var rows = Enumerable.Range(1, 28)
            .Select(day => Row(day, day % 2 == 0 ? 10000 : 100))
            .ToArray();
        WriteLines(input, rows.Select(Serialize));
        var dataset = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "dataset"));
        var firstPath = Path.Combine(root, "checkpoints", "first.json");
        var secondPath = Path.Combine(root, "checkpoints", "second.json");
        var trainer = new StructuredPolicyTrainer();

        var first = trainer.Train(dataset.ManifestPath, firstPath);
        var second = trainer.Train(dataset.ManifestPath, secondPath);
        var loaded = new StructuredPolicyCheckpointStore().Load(firstPath);

        Assert.Equal(first.CheckpointSha256, second.CheckpointSha256);
        Assert.Equal(PolicyTrajectoryVersionPins.FeatureSchema, loaded.Versions.FeatureSchema);
        Assert.True(loaded.Training.TrainPairs > 0);
        Assert.True(loaded.Training.TrainPairAccuracy >= 0.9);
        Assert.Equal("social.gift_npc", Best(loaded, Features(10000)));
        Assert.Equal("social.talk_npc", Best(loaded, Features(100)));
    }

    [Fact]
    public void RankerCannotPromoteNonAdmittedCandidate()
    {
        var checkpoint = CheckpointForRanking();
        var candidates = Candidates();
        candidates = candidates.Append(new PolicyEventCandidatePrediction
        {
            CandidateId = "raw.executor",
            OptionId = "executor.move_to_tile",
            Kind = "move",
            Available = true,
            Score = 999
        }).ToArray();

        var ranked = new StructuredPolicyRanker().Rank(checkpoint, Features(100), candidates);
        var rejected = ranked.Single(candidate => candidate.CandidateId == "raw.executor");

        Assert.False(rejected.Available);
        Assert.Null(rejected.ModelScore);
        Assert.Contains(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason, rejected.BlockReasons);
    }

    [Fact]
    public void TrainerRejectsTamperedPartitionAndStoreRejectsStaleCheckpoint()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        WriteLines(input, Enumerable.Range(1, 28).Select(day => Serialize(Row(day, day % 2 == 0 ? 10000 : 100))));
        var dataset = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "dataset"));
        var trainer = new StructuredPolicyTrainer();
        var checkpointPath = Path.Combine(root, "checkpoint.json");
        var trained = trainer.Train(dataset.ManifestPath, checkpointPath);
        var trainPath = dataset.Manifest.Partitions.Single(item => item.Partition == PolicyDatasetPartitions.Train).Path;
        File.AppendAllText(trainPath, "\n", Encoding.UTF8);

        Assert.Throws<InvalidOperationException>(() =>
            trainer.Train(dataset.ManifestPath, Path.Combine(root, "tampered.json")));

        trained.Checkpoint.Versions.FeatureSchema = "policy_features.stale";
        Assert.Throws<InvalidOperationException>(() =>
            new StructuredPolicyCheckpointStore().Validate(trained.Checkpoint));
    }

    [Fact]
    public void LiveOptionsRequireCheckpointWhenStructuredPolicyIsMandatory()
    {
        Assert.Throws<ArgumentException>(() =>
            LiveTrainingOptions.Parse(new[] { "--require-structured-policy" }));
        var options = LiveTrainingOptions.Parse(new[]
        {
            "--require-structured-policy",
            "--policy-checkpoint-path", @"E:\model.json"
        });
        Assert.True(options.RequireStructuredPolicy);
        Assert.Equal(@"E:\model.json", options.PolicyCheckpointPath);
    }

    private static string Best(StructuredPolicyCheckpointEnvelope checkpoint, FeatureVector features) =>
        new StructuredPolicyRanker().Rank(checkpoint, features, Candidates())
            .First(candidate => candidate.Available).OptionId;

    private static StructuredPolicyCheckpointEnvelope CheckpointForRanking()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        WriteLines(input, Enumerable.Range(1, 28).Select(day => Serialize(Row(day, day % 2 == 0 ? 10000 : 100))));
        var dataset = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "dataset"));
        return new StructuredPolicyTrainer().Train(
            dataset.ManifestPath,
            Path.Combine(root, "checkpoint.json")).Checkpoint;
    }

    private static PolicyDecisionTrajectoryEnvelope Row(int day, double money)
    {
        var selected = money > 1000 ? "gift" : "talk";
        var stateHash = "state." + day;
        return new PolicyDecisionTrajectoryBuilder().Build(
            "trajectory." + day,
            "run.structured",
            new PolicyTrajectoryContext
            {
                SaveId = "save.structured",
                Year = 1,
                Season = "spring",
                Day = day,
                Time = 900
            },
            Features(money),
            Versions(),
            stateHash,
            new AvailabilityAwarePolicyPredictionEnvelope { RankedEventCandidates = Candidates() },
            selected,
            new PlanExecutionEpisodeEnvelope
            {
                EpisodeId = "episode." + day,
                QueueId = "queue." + day,
                OptionId = "executor.social_interact",
                SourceStateHash = stateHash,
                Status = "applied",
                Success = true,
                Reward = 1,
                BeforeGameTick = 100,
                AfterGameTick = 130,
                StateHashChanged = true,
                AfterSnapshotFresh = true,
                ChangedFacts = JsonDocument.Parse("[]").RootElement.Clone()
            });
    }

    private static PolicyEventCandidatePrediction[] Candidates() => new[]
    {
        new PolicyEventCandidatePrediction
        {
            CandidateId = "gift",
            OptionId = "social.gift_npc",
            Kind = "gift_npc",
            Rank = 1,
            Score = 0.5,
            ExpectedReward = 0.1,
            Available = true,
            EstimatedTicks = 30,
            Parameters = new[] { new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" } }
        },
        new PolicyEventCandidatePrediction
        {
            CandidateId = "talk",
            OptionId = "social.talk_npc",
            Kind = "talk_npc",
            Rank = 2,
            Score = 0.5,
            ExpectedReward = 0.1,
            Available = true,
            EstimatedTicks = 30,
            Parameters = new[] { new SmallModelActionParameter { Name = "npc_name", Value = "Abigail" } }
        }
    };

    private static FeatureVector Features(double money) => new()
    {
        Numeric = new[] { new NumericFeature { Name = "player.money", Value = money } },
        Categorical = new[] { new CategoricalFeature { Name = "game.season", Value = "spring" } },
        Boolean = new[] { new BooleanFeature { Name = "planner_inputs.blocked", Value = false } }
    };

    private static PolicyTrajectoryVersions Versions() => new()
    {
        FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
        CandidateVocabulary = "capability_registry.v3",
        CapabilityRegistry = "capability_registry.v3",
        KnowledgeDictionary = PolicyTrajectoryVersionPins.KnowledgeDictionary,
        Compiler = "action_queue.v1",
        Executor = "runtime_test_harness_executor.v1"
    };

    private static void WriteLines(string path, IEnumerable<string> lines)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);
    private static string TestRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
