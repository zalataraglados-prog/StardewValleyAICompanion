using System.Reflection;
using System.Text.Json;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class SkillTeacherCorpusTests
{
    [Fact]
    public void ReconciliationAdvertisesOnlyTheSkillsSourceForItsOwnMethod()
    {
        var method = typeof(GoalMethodCoverageReconciliationBuilder).GetMethod("ImplementedSourceKinds", BindingFlags.NonPublic | BindingFlags.Static)!;
        string[] Kinds(string direction) => (string[])method.Invoke(null, new object[]
        {
            JsonSerializer.Deserialize<GoalMethodFrontierMethod>("{\"direction_id\":\"" + direction + "\",\"requirement_set_readiness\":[]}")!
        })!;
        Assert.Contains(GoalMethodTeacherCoverageSourceKinds.SkillLevelThresholdCorpus, Kinds("raise_skill_levels"));
        Assert.DoesNotContain(GoalMethodTeacherCoverageSourceKinds.SkillLevelThresholdCorpus, Kinds("earn_money"));
        Assert.DoesNotContain(GoalMethodTeacherCoverageSourceKinds.SkillLevelThresholdCorpus, Kinds("complete_master_angler"));
    }

    [Fact]
    public void ThreeFifteenPartitionsNeverCreditTwentyFive()
    {
        var rows = new[] { "train", "validation", "test" }.Select((p, i) => Row("player_level_15", p, i.ToString())).ToArray();
        var corpus = Corpus(rows);
        Assert.True(corpus.CriterionCoverage.Single(row => row.CriterionId == "player_level_15").SplitComplete);
        var twentyFive = corpus.CriterionCoverage.Single(row => row.CriterionId == "player_level_25");
        Assert.Empty(twentyFive.TeacherComparisonPartitions);
        Assert.Empty(twentyFive.NativeOutcomePartitions);
        Assert.False(corpus.SplitComplete);
        var full = corpus with { Rows = rows.Concat(rows.Select(row => row with { CriterionId = "player_level_25" })).ToArray() };
        Assert.True(full.SplitComplete);
        Assert.False(full.FullRootVerified);
        Assert.False(full.PriorSkillAcquisitionVerified);
        Assert.False(full.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void UnrelatedComparisonAndOutcomePartitionsCannotCompleteACriterion()
    {
        var corpus = Corpus(new[]
        {
            Row("player_level_15", "train", "1") with { NativeOutcomeVerified = false },
            Row("player_level_15", "validation", "2") with { StrictPairCount = 0 },
            Row("player_level_25", "test", "3")
        });
        Assert.All(corpus.CriterionCoverage, row => Assert.False(row.SplitComplete));
        Assert.False(corpus.SplitComplete);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("action")]
    [InlineData("decision_across_runs")]
    [InlineData("trajectory")]
    [InlineData("partition")]
    [InlineData("no_native")]
    [InlineData("no_comparison")]
    public void SourceIdentityValidationRejectsCopiesOrFalseScope(string mode)
    {
        var first = Row("player_level_15", "train", "1");
        var second = Row("player_level_25", "train", "2");
        second = mode switch
        {
            "nonce" => second with { RequestNonce = first.RequestNonce },
            "action" => second with { RunId = first.RunId, QueueId = first.QueueId, QueueItemId = first.QueueItemId },
            "decision_across_runs" => second with { SaveId = first.SaveId, BeforeStateHash = first.BeforeStateHash, SelectedCandidateId = first.SelectedCandidateId },
            "trajectory" => second with { RunId = first.RunId, TrajectoryId = first.TrajectoryId },
            "partition" => second with { DatasetPartition = "test" },
            "no_native" => second with { NativeOutcomeVerified = false },
            "no_comparison" => second with { StrictPairCount = 0 },
            _ => second
        };
        var error = Assert.Throws<TargetInvocationException>(() => Invoke("ValidateRows", new[] { first, second }));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Fact]
    public void DifferentOriginalDecisionsCanShareOneCanonicalSplit()
    {
        var first = Row("player_level_15", "train", "1");
        var second = Row("player_level_25", "train", "2");
        Invoke("ValidateRows", new[] { first, second with { SaveId = first.SaveId, SplitKey = first.SplitKey } });
    }

    [Theory]
    [InlineData("denominator")]
    [InlineData("goal")]
    [InlineData("target")]
    [InlineData("operator")]
    [InlineData("path")]
    [InlineData("points")]
    public void AuthorityMustRetainTheCompleteGoalAndBothNativeThresholdRules(string mutation)
    {
        var criteria = new[]
        {
            new GoalCriterion("player_level_15", 1, new[] { "player.level" }, "gte", "15"),
            new GoalCriterion("player_level_25", 1, new[] { "player.level" }, "gte", "25")
        }.Concat(Enumerable.Range(0, 17).Select(index => new GoalCriterion("other" + index,
            index < 2 ? 2 : 1, new[] { "fixture" }, "gte", "1"))).ToArray();
        var knowledge = new GoalKnowledge("fixture", "grandpa.maximum_21", 21, 19, "hash", criteria);
        Invoke("ValidateAuthority", knowledge);
        if (mutation == "denominator") knowledge = knowledge with { CriterionCount = 2 };
        if (mutation == "goal") knowledge = knowledge with { GoalId = "other" };
        criteria[0] = mutation switch
        {
            "target" => criteria[0] with { Target = "14" },
            "operator" => criteria[0] with { Operation = "eq" },
            "path" => criteria[0] with { StatePaths = new[] { "player.effective_level" } },
            "points" => criteria[0] with { Points = 2 },
            _ => criteria[0]
        };
        var error = Assert.Throws<TargetInvocationException>(() => Invoke("ValidateAuthority", knowledge));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static void Invoke(string name, object value) => typeof(SkillTeacherCorpusBuilder)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { value });

    private static SkillTeacherCorpus Corpus(SkillTeacherCorpusRow[] rows) => new("request", "hash", "fixture", "knowledge", rows);

    private static SkillTeacherCorpusRow Row(string criterion, string partition, string id)
    {
        var index = 0;
        string save;
        do { save = "fixture" + index++; } while (PolicyTrajectoryDatasetBuilder.PartitionFor(save + ":1:spring:7") != partition);
        return new(id, "row", "hash", "native", "hash", criterion, "run" + id, "trajectory" + id,
            save, "player", save + ":1:spring:7", partition, "state" + id, "candidate" + id,
            "queue" + id, "item" + id, "nonce" + id, 1, true);
    }
}
