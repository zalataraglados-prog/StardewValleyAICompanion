using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class PolicyDecisionTrajectoryBuilderTests
{
    [Fact]
    public void BuildPreservesCompleteCandidateSetSelectionBudgetsAndOutcome()
    {
        var decision = new AvailabilityAwarePolicyPredictionEnvelope
        {
            RankedEventCandidates = new[]
            {
                Candidate("gift", "social.gift_npc", 1, available: true, ticks: 120, energy: 0),
                Candidate("talk", "social.talk_npc", 2, available: false, ticks: 60, energy: 0,
                    blockReasons: new[] { "already_talked_today" }),
                Candidate("buy", "economy.buy_supplies", 3, available: true, ticks: 180, energy: 0)
            }
        };
        var trajectory = new PolicyDecisionTrajectoryBuilder().Build(
            "trajectory.one",
            "run.one",
            Context(),
            Features(),
            Versions(),
            "hash.before",
            decision,
            "gift",
            Execution());

        Assert.Equal(PolicyTrajectoryVersionPins.TrajectorySchema, trajectory.SchemaVersion);
        Assert.Contains(trajectory.StateFeatures.Numeric, feature => feature.Name == "player.money" && feature.Value == 1000);
        Assert.Equal("save.one:2:winter:17", trajectory.Context.SplitKey);
        Assert.Equal(3, trajectory.Candidates.Length);
        Assert.Equal("gift", trajectory.Selection.CandidateId);
        Assert.Equal("social.gift_npc", trajectory.Selection.OptionId);
        var selected = Assert.Single(trajectory.Candidates.Where(row => row.Selected));
        Assert.True(selected.AdmittedForPolicy);
        Assert.Equal(120, selected.EstimatedTicks);
        Assert.Equal("SeedShop", selected.SourceCandidate.LocationId);
        Assert.Equal("Pierre", selected.SourceCandidate.ShopId);
        Assert.Equal(900, selected.SourceCandidate.EffectiveOpenTime);
        Assert.Equal(1700, selected.SourceCandidate.ClosesAt);
        Assert.Equal("goal_support=friendship;utility=0.2", selected.SourceCandidate.ExpectedEffect);
        var unavailable = Assert.Single(trajectory.Candidates.Where(row => row.CandidateId == "talk"));
        Assert.Contains("already_talked_today", unavailable.ExclusionReasons);
        var excluded = Assert.Single(trajectory.Candidates.Where(row => row.CandidateId == "buy"));
        Assert.False(excluded.AdmittedForPolicy);
        Assert.Contains(PolicyTrainingAdmissionFilter.OptionNotAdmittedReason, excluded.ExclusionReasons);
        Assert.Equal("executor.social_interact", trajectory.Outcome.PrimitiveOptionId);
        Assert.Equal(90, trajectory.Outcome.ActualTicks);
        Assert.True(trajectory.Outcome.AfterSnapshotFresh);
        Assert.Equal(0.4, trajectory.Returns.Immediate);
        Assert.Equal("pending", trajectory.Returns.LongHorizonStatus);
    }

    [Fact]
    public void BuildRejectsNonAdmittedSelectionAndSourceHashDrift()
    {
        var builder = new PolicyDecisionTrajectoryBuilder();
        var decision = new AvailabilityAwarePolicyPredictionEnvelope
        {
            RankedEventCandidates = new[]
            {
                Candidate("buy", "economy.buy_supplies", 1, available: true, ticks: 1, energy: 0)
            }
        };

        Assert.Throws<InvalidOperationException>(() => builder.Build(
            "trajectory.bad",
            "run.one",
            Context(),
            Features(),
            Versions(),
            "hash.before",
            decision,
            "buy",
            Execution()));

        decision.RankedEventCandidates = new[]
        {
            Candidate("gift", "social.gift_npc", 1, available: true, ticks: 1, energy: 0)
        };
        var execution = Execution();
        execution.SourceStateHash = "hash.other";
        Assert.Throws<InvalidOperationException>(() => builder.Build(
            "trajectory.drift",
            "run.one",
            Context(),
            Features(),
            Versions(),
            "hash.before",
            decision,
            "gift",
            execution));
    }

    [Fact]
    public void WriterAppendsTypedJsonlWithoutDroppingCandidateNegatives()
    {
        var trajectory = new PolicyDecisionTrajectoryBuilder().Build(
            "trajectory.write",
            "run.one",
            Context(),
            Features(),
            Versions(),
            "hash.before",
            new AvailabilityAwarePolicyPredictionEnvelope
            {
                RankedEventCandidates = new[]
                {
                    Candidate("gift", "social.gift_npc", 1, available: true, ticks: 120, energy: 0),
                    Candidate("buy", "economy.buy_supplies", 2, available: true, ticks: 180, energy: 0)
                }
            },
            "gift",
            Execution());
        var path = Path.Combine(Path.GetTempPath(), "stardewai-tests", Guid.NewGuid().ToString("N"), "policy.jsonl");

        var result = new JsonlPolicyTrajectoryWriter().Append(path, trajectory);

        Assert.Equal(1, result.RowCount);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("trajectory.write", json.RootElement.GetProperty("trajectory_id").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("candidates").GetArrayLength());
    }

    private static PolicyEventCandidatePrediction Candidate(
        string candidateId,
        string optionId,
        int rank,
        bool available,
        int ticks,
        int energy,
        string[]? blockReasons = null)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = candidateId,
            OptionId = optionId,
            Kind = "test",
            Rank = rank,
            Score = 1.0 / rank,
            ExpectedReward = 0.2,
            Available = available,
            ShopId = "Pierre",
            LocationId = "SeedShop",
            TileX = 4,
            TileY = 17,
            AllowedNow = true,
            AllowedToday = true,
            EffectiveOpenTime = 900,
            ClosesAt = 1700,
            AvailabilityClass = "open_now",
            TimelineStatus = "scheduled_now",
            ExpectedEffect = "goal_support=friendship;utility=0.2",
            EstimatedTicks = ticks,
            EnergyCost = energy,
            BlockReasons = blockReasons ?? Array.Empty<string>(),
            Parameters = new[]
            {
                new SmallModelActionParameter { Name = "target", Value = candidateId }
            }
        };
    }

    private static PolicyTrajectoryContext Context() => new()
    {
        SaveId = "save.one",
        Year = 2,
        Season = "winter",
        Day = 17,
        Time = 1200
    };

    private static PolicyTrajectoryVersions Versions() => new()
    {
        FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
        CandidateVocabulary = "capability_registry.v2",
        CapabilityRegistry = "capability_registry.v2",
        KnowledgeDictionary = "game-1.6.15-20260723T093543Z-linux-v24",
        Compiler = "action_queue.v1",
        Executor = "runtime_test_harness.v1"
    };

    private static FeatureVector Features() => new()
    {
        Numeric = new[]
        {
            new NumericFeature { Name = "game.time", Value = 1200 },
            new NumericFeature { Name = "player.money", Value = 1000 }
        },
        Categorical = new[]
        {
            new CategoricalFeature { Name = "game.season", Value = "winter" }
        },
        Boolean = new[]
        {
            new BooleanFeature { Name = "planner_inputs.blocked", Value = false }
        }
    };

    private static PlanExecutionEpisodeEnvelope Execution() => new()
    {
        EpisodeId = "episode.one",
        QueueId = "queue.one",
        OptionId = "executor.social_interact",
        SourceStateHash = "hash.before",
        Status = "applied",
        Success = true,
        Reward = 0.4,
        BeforeGameTick = 100,
        AfterGameTick = 190,
        StateHashChanged = true,
        AfterSnapshotFresh = true,
        ChangedFacts = JsonDocument.Parse("[{\"path\":\"player.inventory[11]\"}]").RootElement.Clone()
    };
}
