using System.Reflection;
using System.Text.Json;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed partial class MarriageTeacherCorpusTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SharedJointReplayRejectsMissingOrForeignTeacherClaimsBeforeUsingTheSource(bool foreignTeacher)
    {
        var row = PolicySupervisionFixture.Observation();
        if (foreignTeacher) row = PolicySupervisionFixture.Attach(row, "gift", "talk");
        Assert.Null(PolicyTrajectoryDatasetBuilder.ValidateEnvelope(row));
        var path = Path.Combine(Path.GetTempPath(), "stardewai-marriage-claim-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(row, new JsonSerializerOptions(JsonSerializerDefaults.Web)));
        var method = typeof(MarriageTeacherRowBuilder).GetMethod("VerifyFromJointSource", BindingFlags.NonPublic | BindingFlags.Static)!;
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null, new object?[] { path, "absent-native", null }));
        Assert.Contains("original preparation identity", Assert.IsType<InvalidDataException>(error.InnerException).Message);
    }

    [Theory]
    [InlineData("formal")]
    [InlineData("empty_id")]
    [InlineData("empty_sources")]
    [InlineData("duplicate_ids")]
    [InlineData("empty_rows")]
    public void InvalidRequestFailsBeforeOpeningNativeEvidence(string mutation)
    {
        var request = new MarriageTeacherCorpusRequest
        {
            CorpusId = mutation == "empty_id" ? " " : "fixture",
            FormalProductTrainingAuthorized = mutation == "formal",
            Sources = mutation == "empty_sources" ? Array.Empty<MarriageTeacherCorpusSource>() : new[]
            {
                new MarriageTeacherCorpusSource("first", "absent-native", mutation == "empty_rows" ? Array.Empty<string>() : new[] { "absent-row" }),
                new MarriageTeacherCorpusSource(mutation == "duplicate_ids" ? "first" : "second", "another-native", new[] { "another-row" })
            }
        };
        var path = Path.Combine(Path.GetTempPath(), "stardewai-marriage-corpus-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(request));
        var error = Assert.Throws<InvalidDataException>(() => MarriageTeacherCorpusBuilder.Build(path));
        Assert.Contains("invalid or repeats source identities", error.Message);
    }

    [Fact]
    public void RenamedReceiptCopiesRejectBeforeParsingNativeSources()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-marriage-copies-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string Empty(string name) { var path = Path.Combine(root, name); File.WriteAllText(path, "{}"); return path; }
        var request = new MarriageTeacherCorpusRequest
        {
            CorpusId = "fixture", Sources = new[]
            {
                new MarriageTeacherCorpusSource("a", Empty("native-a.json"), new[] { Empty("row-a.json") }),
                new MarriageTeacherCorpusSource("b", Empty("native-b.json"), new[] { Empty("row-b.json") })
            }
        };
        var path = Path.Combine(root, "request.json");
        File.WriteAllText(path, JsonSerializer.Serialize(request));
        Assert.Contains("repeats original joint receipt content", Assert.Throws<InvalidDataException>(() => MarriageTeacherCorpusBuilder.Build(path)).Message);
    }

    [Fact]
    public void TwoDecisionsRemainOneNativeOutcomeInTheOriginPartition()
    {
        var run = Run("a");
        Invoke("ValidateRuns", new[] { run });
        var corpus = Corpus(run);
        Assert.Equal(2, corpus.RowCount);
        Assert.Equal(1, corpus.NativeOutcomeCount);
        Assert.Equal(new[] { "train" }, corpus.CriterionCoverage.Single().TeacherComparisonPartitions);
        Assert.Equal(new[] { "train" }, corpus.CriterionCoverage.Single().NativeOutcomePartitions);
        Assert.False(corpus.SplitComplete);
        Assert.All(run.PreparationDecisions, row => { Assert.False(row.CompleteOrderVerified); Assert.NotEmpty(row.UncomparedCandidateIds); });
        // The original native wedding return can settle after the last queue capture.
        Assert.NotEqual(run.AfterStateHash, run.QueueChain.EnumerateArray().Last().GetProperty("after_state_hash").GetString());
    }

    [Fact]
    public void SplitCompleteStillDoesNotProveEarlierAcquisitionOrAuthorizeTraining()
    {
        var corpus = Corpus(Run("a"), Run("b", "validation"), Run("c", "test"));
        Invoke("ValidateRuns", corpus.Runs);
        Assert.True(corpus.SplitComplete);
        Assert.False(corpus.PriorHouseAcquisitionVerified);
        Assert.False(corpus.PriorRelationshipAcquisitionVerified);
        Assert.False(corpus.MechanicalControlsArePreferenceLabels);
        Assert.False(corpus.FullRootVerified);
        Assert.False(corpus.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("denominator")]
    [InlineData("goal")]
    [InlineData("target")]
    [InlineData("operator")]
    [InlineData("path")]
    [InlineData("points")]
    public void AuthorityMustKeepTheWholeGoalAndNativeJointRule(string mutation)
    {
        var criteria = new[] { new GoalCriterion(MarriageTeacherCorpus.CriterionId, 1,
            new[] { "player.married_or_roommate", "player.farmhouse_upgrade_level" }, "and", "married_or_roommate=true;farmhouse_upgrade_level>=2") }
            .Concat(Enumerable.Range(0, 18).Select(index => new GoalCriterion("other" + index, index < 2 ? 2 : 1, new[] { "fixture" }, "gte", "1"))).ToArray();
        var knowledge = new GoalKnowledge("fixture", "grandpa.maximum_21", 21, 19, "knowledge", criteria);
        Invoke("ValidateAuthority", knowledge);
        if (mutation == "denominator") knowledge = knowledge with { CriterionCount = 1 };
        if (mutation == "goal") knowledge = knowledge with { GoalId = "other" };
        criteria[0] = mutation switch
        {
            "target" => criteria[0] with { Target = "farmhouse_upgrade_level>=1" },
            "operator" => criteria[0] with { Operation = "or" },
            "path" => criteria[0] with { StatePaths = new[] { "player.married_or_roommate" } },
            "points" => criteria[0] with { Points = 2 },
            _ => criteria[0]
        };
        Reject("ValidateAuthority", knowledge);
    }

    private static void Invoke(string name, object value) => typeof(MarriageTeacherCorpusBuilder)
        .GetMethod(name, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new[] { value });
    private static void Reject(string name, object value) => Assert.IsType<InvalidDataException>(
        Assert.Throws<TargetInvocationException>(() => Invoke(name, value)).InnerException);
    private static MarriageTeacherCorpus Corpus(params MarriageTeacherCorpusRun[] runs) => new("request", "hash", "fixture", "knowledge", runs);
}
