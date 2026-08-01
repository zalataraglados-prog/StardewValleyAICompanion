using System.Text;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class PolicyTrajectoryDatasetBuilderTests
{
    [Fact]
    public void BuildCleansDeduplicatesSplitsHashesAndBackfillsClosedHorizons()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        var observations = Path.Combine(root, "observations.jsonl");
        var rows = new[]
        {
            Row("t1", "h1", 1, "spring", 1, 610, 0.1),
            Row("t2", "h2", 1, "spring", 1, 700, 0.2),
            Row("t3", "h3", 1, "spring", 2, 610, 0.3),
            Row("t4", "h4", 1, "summer", 1, 610, 0.4),
            Row("t5", "h5", 2, "spring", 1, 610, 0.5),
            Row("t6", "h6", 2, "winter", 28, 2500, 0.6)
        };
        var duplicate = Clone(rows[0]);
        duplicate.TrajectoryId = "t1.duplicate";
        WriteLines(input, rows.Select(Serialize).Concat(new[] { Serialize(duplicate), "{invalid" }));
        WriteLines(observations, new[]
        {
            Serialize(Observation("day.final", PolicyHorizonKinds.Day, null)),
            Serialize(Observation("season.final", PolicyHorizonKinds.Season, null)),
            Serialize(Observation("year.final", PolicyHorizonKinds.Year, null)),
            Serialize(Observation("grandpa.final", PolicyHorizonKinds.Grandpa21, 21))
        });

        var first = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "build-one"), observations);
        var second = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "build-two"), observations);

        Assert.Equal(8, first.Manifest.Counts.InputLines);
        Assert.Equal(6, first.Manifest.Counts.AcceptedRows);
        Assert.Equal(2, first.Manifest.Counts.RejectedRows);
        Assert.Equal(1, first.Manifest.Counts.DuplicateRows);
        Assert.Equal(0, first.Manifest.Counts.ConflictingDuplicateRows);
        Assert.Equal(6, first.Manifest.Returns.DayComplete);
        Assert.Equal(6, first.Manifest.Returns.SeasonComplete);
        Assert.Equal(6, first.Manifest.Returns.YearComplete);
        Assert.Equal(6, first.Manifest.Returns.Grandpa21Complete);
        Assert.Equal(6, first.Manifest.Returns.FullyComplete);
        Assert.Equal(first.Manifest.Cleaned.Sha256, second.Manifest.Cleaned.Sha256);
        Assert.Equal(
            first.Manifest.Partitions.Select(row => row.Sha256),
            second.Manifest.Partitions.Select(row => row.Sha256));
        Assert.Equal(6, first.Manifest.Partitions.Sum(row => row.Rows));

        var cleaned = ReadRows(first.Manifest.Cleaned.Path);
        Assert.Equal(0.3, cleaned.Single(row => row.TrajectoryId == "t1").Returns.Day!.Value, 8);
        Assert.Equal(0.2, cleaned.Single(row => row.TrajectoryId == "t2").Returns.Day!.Value, 8);
        Assert.All(cleaned, row =>
        {
            Assert.Equal("complete", row.Returns.LongHorizonStatus);
            Assert.Equal(1, row.Returns.Grandpa21);
        });
        var firstDay = cleaned.Where(row => row.Context.Day == 1 && row.Context.Year == 1 && row.Context.Season == "spring").ToArray();
        Assert.Equal(2, firstDay.Length);
        Assert.Single(firstDay.Select(row => row.Context.DatasetPartition).Distinct(StringComparer.Ordinal));
        Assert.Equal(
            PolicyTrajectoryDatasetBuilder.PartitionFor(firstDay[0].Context.SplitKey),
            firstDay[0].Context.DatasetPartition);
    }

    [Fact]
    public void ConflictingDuplicateDecisionRejectsEveryConflictingLabel()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        var first = Row("conflict.one", "same-hash", 1, "spring", 1, 610, 0.1);
        var conflict = Clone(first);
        conflict.TrajectoryId = "conflict.two";
        conflict.Outcome.ActualTicks++;
        var independent = Row("independent", "other-hash", 1, "spring", 2, 610, 0.2);
        WriteLines(input, new[] { Serialize(first), Serialize(conflict), Serialize(independent) });

        var result = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "output"));

        Assert.Equal(1, result.Manifest.Counts.AcceptedRows);
        Assert.Equal(2, result.Manifest.Counts.ConflictingDuplicateRows);
        Assert.Equal(2, result.Manifest.Rejections.Single(row => row.Reason == "conflicting_duplicate_decision").Count);
    }

    [Fact]
    public void InvalidGrandpaObservationFailsClosed()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        var observations = Path.Combine(root, "observations.jsonl");
        WriteLines(input, new[] { Serialize(Row("valid", "hash", 1, "spring", 1, 610, 0.1)) });
        WriteLines(observations, new[] { Serialize(Observation("bad", PolicyHorizonKinds.Grandpa21, 22)) });

        Assert.Throws<InvalidOperationException>(() =>
            new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "output"), observations));
    }

    [Fact]
    public void ExpectedKnowledgeDictionaryRejectsAStaleDataset()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        WriteLines(input, new[] { Serialize(Row("valid", "hash", 1, "spring", 1, 610, 0.1)) });

        var error = Assert.Throws<InvalidOperationException>(() =>
            new PolicyTrajectoryDatasetBuilder().Build(
                input,
                Path.Combine(root, "output"),
                expectedKnowledgeDictionary: PolicyTrajectoryVersionPins.KnowledgeDictionary));

        Assert.Contains("does not match", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MalformedEnvelopeAndNonContiguousRanksAreRejectedWithoutStoppingTheBuild()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        var badRank = Row("bad.rank", "rank-hash", 1, "spring", 1, 610, 0.1);
        badRank.Candidates[1].Rank = 3;
        WriteLines(input, new[]
        {
            "{\"schema_version\":\"policy_decision_trajectory.v2\",\"trajectory_id\":\"null-context\",\"run_id\":\"run\",\"source_state_hash\":\"hash\",\"context\":null}",
            Serialize(badRank),
            Serialize(Row("valid", "valid-hash", 1, "spring", 2, 610, 0.2))
        });

        var result = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "output"));

        Assert.Equal(1, result.Manifest.Counts.AcceptedRows);
        Assert.Contains(result.Manifest.Rejections, row => row.Reason == "envelope_section_missing");
        Assert.Contains(result.Manifest.Rejections, row => row.Reason == "candidate_rank_not_contiguous");
    }

    [Fact]
    public void GrandpaReturnDoesNotBackfillADecisionAfterTheTerminalObservation()
    {
        var root = TestRoot();
        var input = Path.Combine(root, "raw.jsonl");
        var observations = Path.Combine(root, "observations.jsonl");
        WriteLines(input, new[]
        {
            Serialize(Row("before", "before-hash", 2, "winter", 28, 2500, 0.1)),
            Serialize(Row("after", "after-hash", 3, "spring", 1, 610, 0.2))
        });
        WriteLines(observations, new[] { Serialize(Observation("terminal", PolicyHorizonKinds.Grandpa21, 21)) });

        var result = new PolicyTrajectoryDatasetBuilder().Build(input, Path.Combine(root, "output"), observations);
        var rows = ReadRows(result.Manifest.Cleaned.Path);

        Assert.Equal(1, rows.Single(row => row.TrajectoryId == "before").Returns.Grandpa21);
        Assert.Null(rows.Single(row => row.TrajectoryId == "after").Returns.Grandpa21);
    }

    [Fact]
    public void ObservationWriterDoesNotDuplicateStableObservationId()
    {
        var path = Path.Combine(TestRoot(), "observations.jsonl");
        var observation = Observation("stable", PolicyHorizonKinds.Day, null);
        var writer = new JsonlPolicyHorizonObservationWriter();

        Assert.True(writer.AppendIfNew(path, observation));
        Assert.False(writer.AppendIfNew(path, observation));
        Assert.Single(File.ReadAllLines(path));
    }

    private static PolicyDecisionTrajectoryEnvelope Row(
        string trajectoryId,
        string stateHash,
        int year,
        string season,
        int day,
        int time,
        double reward)
    {
        return new PolicyDecisionTrajectoryBuilder().Build(
            trajectoryId,
            "run.dataset",
            new PolicyTrajectoryContext
            {
                SaveId = "save.dataset",
                Year = year,
                Season = season,
                Day = day,
                Time = time
            },
            Features(time, season),
            Versions(),
            stateHash,
            new AvailabilityAwarePolicyPredictionEnvelope
            {
                RankedEventCandidates = new[]
                {
                    Candidate("social:gift:Abigail", "social.gift_npc", 1, true),
                    Candidate("economy:buy:seed", "economy.buy_supplies", 2, true)
                }
            },
            "social:gift:Abigail",
            new PlanExecutionEpisodeEnvelope
            {
                EpisodeId = "episode." + trajectoryId,
                QueueId = "queue." + stateHash,
                OptionId = "executor.social_interact",
                SourceStateHash = stateHash,
                Status = "applied",
                Success = true,
                Reward = reward,
                BeforeGameTick = 100,
                AfterGameTick = 130,
                StateHashChanged = true,
                AfterSnapshotFresh = true,
                ChangedFacts = JsonDocument.Parse("[]").RootElement.Clone()
            });
    }

    private static PolicyEventCandidatePrediction Candidate(
        string candidateId,
        string optionId,
        int rank,
        bool available) => new()
    {
        CandidateId = candidateId,
        OptionId = optionId,
        Kind = "test",
        Rank = rank,
        Score = 1d / rank,
        ExpectedReward = 0.1,
        Available = available,
        EstimatedTicks = 30,
        Parameters = new[] { new SmallModelActionParameter { Name = "target", Value = candidateId } }
    };

    private static PolicyTrajectoryVersions Versions() => new()
    {
        FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
        CandidateVocabulary = "capability_registry.v2",
        CapabilityRegistry = "capability_registry.v2",
        KnowledgeDictionary = "game-1.6.15-test",
        Compiler = "action_queue.v1",
        Executor = "runtime_test_harness_executor.v1"
    };

    private static FeatureVector Features(int time, string season) => new()
    {
        Numeric = new[]
        {
            new NumericFeature { Name = "game.time", Value = time },
            new NumericFeature { Name = "player.money", Value = 1000 }
        },
        Categorical = new[]
        {
            new CategoricalFeature { Name = "game.season", Value = season }
        },
        Boolean = new[]
        {
            new BooleanFeature { Name = "planner_inputs.blocked", Value = false }
        }
    };

    private static PolicyHorizonObservationEnvelope Observation(
        string id,
        string horizon,
        int? grandpaScore)
    {
        var grandpa = string.Equals(horizon, PolicyHorizonKinds.Grandpa21, StringComparison.Ordinal);
        return new PolicyHorizonObservationEnvelope
        {
            ObservationId = id,
            SaveId = "save.dataset",
            Year = grandpa ? 3 : 2,
            Season = grandpa ? "spring" : "winter",
            Day = grandpa ? 1 : 28,
            Time = grandpa ? 600 : 2600,
            Horizon = horizon,
            Closed = true,
            GrandpaScore = grandpaScore,
            SourceStateHash = "terminal.hash",
            EvidenceKind = "test_transparent_snapshot",
            EvidencePath = "after.json"
        };
    }

    private static PolicyDecisionTrajectoryEnvelope Clone(PolicyDecisionTrajectoryEnvelope row) =>
        JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(Serialize(row), JsonOptions)!;

    private static PolicyDecisionTrajectoryEnvelope[] ReadRows(string path) =>
        File.ReadAllLines(path)
            .Select(line => JsonSerializer.Deserialize<PolicyDecisionTrajectoryEnvelope>(line, JsonOptions)!)
            .ToArray();

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
