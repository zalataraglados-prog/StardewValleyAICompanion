using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumTeacherCorpusTests
{
    [Fact]
    public void ProjectionCountsOneNativeOutcomeRatherThanMultiplyingItByPairs()
    {
        var row = Row("train", "a");
        row = row with { Evidence = row.Evidence with { PairwisePreferences = row.Evidence.PairwisePreferences.Concat(new[]
            { new CurrentCollectionTeacherPairwisePreference("donor", "other-reference", "achievement5", "preferred_over_current_eligible_counterfactual") }).ToArray() } };
        var corpus = new MuseumTeacherCorpus("request", "hash", "fixture", new[] { row });
        Validate(corpus.Rows);
        Assert.Equal(95, corpus.Rows[0].Evidence.Items.Length);
        Assert.Equal(1, corpus.RowCount);
        Assert.Equal(1, corpus.NativeOutcomeCount);
        Assert.Equal(2, corpus.StrictPairCount);
        Assert.Equal(new[] { "train" }, corpus.TeacherComparisonPartitions);
        Assert.Equal(new[] { "train" }, corpus.NativeOutcomePartitions);
        Assert.False(corpus.SplitComplete);
        Assert.False(corpus.FullRootVerified);
        Assert.False(corpus.FormalProductTrainingAuthorized);
    }

    [Fact]
    public void ThreeIndependentOriginalGroupsCanCompleteOnlyTheLocalSplitCorpus()
    {
        var corpus = new MuseumTeacherCorpus("request", "hash", "fixture", new[] { Row("train", "a"), Row("validation", "b"), Row("test", "c") });
        Validate(corpus.Rows);
        Assert.True(corpus.SplitComplete);
        Assert.False(corpus.FullRootVerified);
        Assert.False(corpus.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("group")]
    [InlineData("run")]
    [InlineData("nonce")]
    [InlineData("partition")]
    [InlineData("late")]
    [InlineData("authority")]
    [InlineData("short_inventory")]
    [InlineData("unpaired")]
    public void CorpusGroupingRejectsCopiedMisdatedOrForeignTerminalProofs(string mutation)
    {
        var first = Row("train", "a");
        var second = Row("validation", "b");
        var e = second.Evidence;
        e = mutation switch
        {
            "group" => e with { SplitKey = first.Evidence.SplitKey, DecisionTotalDay = first.Evidence.DecisionTotalDay, DatasetPartition = first.Evidence.DatasetPartition },
            "run" => e with { RunId = first.Evidence.RunId },
            "nonce" => e with { Action = e.Action with { RequestNonce = first.Evidence.Action.RequestNonce } },
            "partition" => e with { DatasetPartition = "test" },
            "late" => e with { DecisionTotalDay = 224 },
            "authority" => e with { SourceSha256 = new Dictionary<string, string>(e.SourceSha256) { ["comparison.requirement_inventory"] = "foreign" } },
            "short_inventory" => e with { Items = e.Items.Take(94).ToArray() },
            "unpaired" => e with { PairwisePreferences = Array.Empty<CurrentCollectionTeacherPairwisePreference>() },
            _ => e
        };
        var error = Assert.Throws<TargetInvocationException>(() => Validate(new[] { first, second with { Evidence = e } }));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("empty")]
    [InlineData("null_sources")]
    [InlineData("null_source")]
    [InlineData("duplicate_id")]
    [InlineData("training")]
    public void InvalidRequestsRejectBeforeAttemptingToOpenNativeSources(string mutation)
    {
        var request = JsonSerializer.SerializeToNode(new MuseumTeacherCorpusRequest
        {
            CorpusId = "fixture", Sources = new[] { new MuseumTeacherCorpusSource("a", "nonexistent-source.json") }
        })!;
        if (mutation == "schema") request["schema_version"] = "other";
        if (mutation == "empty") request["sources"] = new JsonArray();
        if (mutation == "null_sources") request["sources"] = null;
        if (mutation == "null_source") request["sources"]![0] = null;
        if (mutation == "duplicate_id") request["sources"]!.AsArray().Add(request["sources"]![0]!.DeepClone());
        if (mutation == "training") request["formal_product_training_authorized"] = true;
        var path = Path.Combine(Path.GetTempPath(), "museum-corpus-request-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, request.ToJsonString());
        Assert.Throws<InvalidDataException>(() => MuseumTeacherCorpusBuilder.Build(path));
    }

    private static MuseumTeacherCorpusRow Row(string partition, string id)
    {
        var day = Enumerable.Range(0, 224).First(value => PolicyTrajectoryDatasetBuilder.PartitionFor("MuseumFarm:123:" + value) == partition);
        return new(id, "join" + id, "hash" + id, new MuseumTerminalNativeJoin("request",
            new Dictionary<string, string> { ["comparison.knowledge"] = "knowledge", ["comparison.requirement_inventory"] = "inventory", ["comparison.dependencies"] = "dependencies" },
            "run" + id, "MuseumFarm", "123", day, "MuseumFarm:123:" + day, partition, "donor", "(O)96", 11, 2, 1, 1,
            new[] { new CurrentCollectionTeacherPairwisePreference("donor", "defer", "achievement5", "preferred_over_current_eligible_counterfactual") },
            Enumerable.Range(1, 95).Select(value => new MuseumTerminalJoinedItem("item:" + (value < 95 ? value : 96), "(O)" + (value < 95 ? value : 96), value < 95, true)).ToArray(),
            new("queue" + id, "item" + id, "nonce" + id, "before" + id, "after" + id, 10, 30, 20)));
    }

    private static void Validate(MuseumTeacherCorpusRow[] rows) => typeof(MuseumTeacherCorpusBuilder)
        .GetMethod("ValidateRows", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { rows });
}
