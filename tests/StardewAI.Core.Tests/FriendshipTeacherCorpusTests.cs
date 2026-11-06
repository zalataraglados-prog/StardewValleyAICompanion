using System.Reflection;
using System.Text.Json;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class FriendshipTeacherCorpusTests
{
    [Theory]
    [InlineData("formal")]
    [InlineData("empty_id")]
    [InlineData("empty_sources")]
    [InlineData("duplicate_ids")]
    public void InvalidRequestCannotReachNativeSourcesOrAuthorizeTraining(string mutation)
    {
        var request = new FriendshipTeacherCorpusRequest
        {
            CorpusId = mutation == "empty_id" ? " " : "fixture",
            FormalProductTrainingAuthorized = mutation == "formal",
            Sources = mutation == "empty_sources" ? Array.Empty<FriendshipTeacherCorpusSource>() : new[]
            { new FriendshipTeacherCorpusSource("first", "missing-a"),
                new FriendshipTeacherCorpusSource(mutation == "duplicate_ids" ? "first" : "second", "missing-b") }
        };
        var path = Path.Combine(Path.GetTempPath(), "stardewai-friendship-corpus-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(request));
        var error = Assert.Throws<InvalidDataException>(() => FriendshipTeacherCorpusBuilder.Build(path));
        Assert.Contains("invalid or repeats source identities", error.Message);
    }

    [Fact]
    public void FiveVillagerEvidenceNeverCreditsTenAndAllSplitsStillDoNotProvePriorAcquisition()
    {
        var rows = new[] { "train", "validation", "test" }.Select((partition, i) => Row("friendships_5", partition, i.ToString())).ToArray();
        var corpus = Corpus(rows);
        Assert.True(corpus.CriterionCoverage.Single(row => row.CriterionId == "friendships_5").SplitComplete);
        Assert.Empty(corpus.CriterionCoverage.Single(row => row.CriterionId == "friendships_10").NativeOutcomePartitions);
        Assert.False(corpus.SplitComplete);
        var full = corpus with { Rows = rows.Concat(rows.Select(row => row with
            { CriterionId = "friendships_10", QualifyingCountBefore = 9, QualifyingCountAfter = 10 })).ToArray() };
        Assert.True(full.SplitComplete);
        Assert.False(full.FullRootVerified);
        Assert.False(full.PriorFriendshipAcquisitionVerified);
        Assert.False(full.PriorStoryProgressVerified);
        Assert.False(full.FormalProductTrainingAuthorized);
        Assert.All(rows, row => { Assert.False(row.CompleteOrderVerified); Assert.NotEmpty(row.UncomparedCandidateIds); });
    }

    [Fact]
    public void DisjointComparisonAndNativePartitionsCannotCompleteEitherCriterion()
    {
        var corpus = Corpus(new[]
        {
            Row("friendships_5", "train", "1") with { NativeOutcomeVerified = false },
            Row("friendships_5", "validation", "2") with { StrictPairCount = 0 },
            Row("friendships_10", "test", "3")
        });
        Assert.All(corpus.CriterionCoverage, row => Assert.False(row.SplitComplete));
    }

    [Theory]
    [InlineData("movement_nonce")]
    [InlineData("social_nonce")]
    [InlineData("movement_action")]
    [InlineData("social_action")]
    [InlineData("repeated_inner_nonce")]
    [InlineData("repeated_inner_item")]
    [InlineData("missing_nonce")]
    [InlineData("primitive_count")]
    [InlineData("decision")]
    [InlineData("trajectory")]
    [InlineData("partition")]
    [InlineData("count")]
    [InlineData("borrowed_criterion")]
    [InlineData("no_native")]
    [InlineData("no_comparison")]
    public void EveryQueueItemAndNonceIsRetainedAndCopiesCannotBecomeNewEvidence(string mutation)
    {
        var first = Row("friendships_5", "train", "first");
        var second = Row("friendships_10", "train", "second");
        second = mutation switch
        {
            "movement_nonce" => second with { RequestNonces = new[] { first.RequestNonces[0], second.RequestNonces[1] } },
            "social_nonce" => second with { RequestNonces = new[] { second.RequestNonces[0], first.RequestNonces[1] } },
            "movement_action" => second with { RunId = first.RunId, QueueId = first.QueueId,
                QueueItemIds = new[] { first.QueueItemIds[0], second.QueueItemIds[1] } },
            "social_action" => second with { RunId = first.RunId, QueueId = first.QueueId,
                QueueItemIds = new[] { second.QueueItemIds[0], first.QueueItemIds[1] } },
            "repeated_inner_nonce" => second with { RequestNonces = new[] { "repeat", "repeat" } },
            "repeated_inner_item" => second with { QueueItemIds = new[] { "repeat", "repeat" } },
            "missing_nonce" => second with { RequestNonces = new[] { second.RequestNonces[1] } },
            "primitive_count" => second with { PrimitiveOptionIds = new[] { "executor.social_interact" } },
            "decision" => second with { SaveId = first.SaveId, PlayerId = first.PlayerId,
                BeforeStateHash = first.BeforeStateHash, SelectedCandidateId = first.SelectedCandidateId },
            "trajectory" => second with { RunId = first.RunId, TrajectoryId = first.TrajectoryId },
            "partition" => second with { DatasetPartition = "test" },
            "count" => second with { QualifyingCountBefore = 4 },
            "borrowed_criterion" => second with { CriterionId = "friendships_5" },
            "no_native" => second with { NativeOutcomeVerified = false },
            "no_comparison" => second with { StrictPairCount = 0 },
            _ => second
        };
        var error = Assert.Throws<TargetInvocationException>(() => Invoke("ValidateRows", new[] { first, second }));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void DistinctOriginalDecisionsCanShareTheSameSaveDateAndPartition()
    {
        var first = Row("friendships_5", "train", "first");
        var second = Row("friendships_10", "train", "second") with { SaveId = first.SaveId, SplitKey = first.SplitKey };
        Invoke("ValidateRows", new[] { first, second });
    }

    [Theory]
    [InlineData("denominator")]
    [InlineData("goal")]
    [InlineData("target")]
    [InlineData("operator")]
    [InlineData("path")]
    [InlineData("points")]
    [InlineData("duplicate")]
    public void FullAuthorityAndExactNative1975CountRulesCannotBeReplaced(string mutation)
    {
        var criteria = new[]
        {
            new GoalCriterion("friendships_5", 1, new[] { "npcs.friendships" }, "count_points_gte_1975", "5"),
            new GoalCriterion("friendships_10", 1, new[] { "npcs.friendships" }, "count_points_gte_1975", "10")
        }.Concat(Enumerable.Range(0, 17).Select(index => new GoalCriterion("other" + index,
            index < 2 ? 2 : 1, new[] { "fixture" }, "gte", "1"))).ToArray();
        var knowledge = new GoalKnowledge("fixture", "grandpa.maximum_21", 21, 19, "knowledge", criteria);
        Invoke("ValidateAuthority", knowledge);
        if (mutation == "denominator") knowledge = knowledge with { CriterionCount = 2 };
        if (mutation == "goal") knowledge = knowledge with { GoalId = "other" };
        criteria[0] = mutation switch
        {
            "target" => criteria[0] with { Target = "4" },
            "operator" => criteria[0] with { Operation = "count_points_gte_2000" },
            "path" => criteria[0] with { StatePaths = new[] { "npcs.positions" } },
            "points" => criteria[0] with { Points = 2 },
            "duplicate" => criteria[1],
            _ => criteria[0]
        };
        var error = Assert.Throws<TargetInvocationException>(() => Invoke("ValidateAuthority", knowledge));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static void Invoke(string name, object value) => typeof(FriendshipTeacherCorpusBuilder)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { value });

    private static FriendshipTeacherCorpus Corpus(params FriendshipTeacherCorpusRow[] rows) => new("request", "hash", "fixture", "knowledge", rows);

    // Protocol-port fixtures only; not claims of native game evidence or product readiness.
    private static FriendshipTeacherCorpusRow Row(string criterion, string partition, string id)
    {
        var index = 0;
        string save;
        do { save = "fixture" + index++; } while (PolicyTrajectoryDatasetBuilder.PartitionFor(save + ":1:spring:7") != partition);
        var target = criterion == "friendships_5" ? 5 : 10;
        return new(id, "row", "hash", "native", "hash", criterion, "run" + id, "trajectory" + id,
            save, "player", save + ":1:spring:7", partition, "before" + id, "after" + id, "candidate" + id, "denominator", 29,
            "queue" + id, new[] { "move" + id, "social" + id }, new[] { "executor.move_to_tile", "executor.social_interact" },
            new[] { "move-nonce" + id, "social-nonce" + id }, target - 1, target, 5, new[] { "uncompared" }, false, true);
    }
}
