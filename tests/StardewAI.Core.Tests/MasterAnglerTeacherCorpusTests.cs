using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class MasterAnglerTeacherCorpusTests
{
    [Fact]
    public void ClassificationOnlyRequiresCompleteDenominatorAndFreshNativeAchievement26()
    {
        Assert.True(Terminal(Snapshot(71), Snapshot(72, 26)));
        Assert.False(Terminal(Snapshot(1), Snapshot(2)));
        Assert.False(Terminal(Snapshot(71), Snapshot(72)));
        Assert.False(Terminal(Snapshot(71, 26), Snapshot(72, 26)));
        Assert.False(Terminal(Snapshot(72), Snapshot(72, 26)));
        Assert.False(Terminal(Snapshot(1), Snapshot(72, 26)));
    }

    [Theory]
    [InlineData("missing_achievements")]
    [InlineData("mistyped_achievement")]
    [InlineData("duplicate_achievement")]
    [InlineData("missing_species")]
    public void ClassificationOnlyRejectsUnknownOrMalformedTerminalState(string mode)
    {
        var after = Snapshot(72, 26);
        var world = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(after.State["world_progress"].GetRawText())!;
        switch (mode)
        {
            case "missing_achievements": world.Remove("achievements"); break;
            case "mistyped_achievement": world["achievements"] = JsonSerializer.SerializeToElement(new { status = "available", value = new[] { "26" } }); break;
            case "duplicate_achievement": world["achievements"] = JsonSerializer.SerializeToElement(new { status = "available", value = new[] { 26, 26 } }); break;
            case "missing_species": world["fish_collection_progress"] = JsonSerializer.SerializeToElement(new { status = "unavailable" }); break;
        }
        after.State["world_progress"] = JsonSerializer.SerializeToElement(world);
        var exception = Assert.Throws<TargetInvocationException>(() => Terminal(Snapshot(71), after));
        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Fact]
    public void ProjectionOnlyNeverPromotesThreeProgressPartitionsToCriterionOutcomes()
    {
        var rows = new[] { Row("train", "1"), Row("validation", "2"), Row("test", "3") };
        var corpus = new MasterAnglerTeacherCorpus("request", "hash", "fixture", rows);
        Assert.Equal(3, corpus.TeacherComparisonPartitions.Length);
        Assert.Equal(3, corpus.CollectionProgressPartitions.Length);
        Assert.Empty(corpus.NativeOutcomePartitions);
        Assert.False(corpus.SplitComplete);
        Assert.Equal(0, corpus.CriterionTerminalRowCount);
        var terminal = corpus with { Rows = rows.Select(row => row with { CriterionTerminalVerified = true }).ToArray() };
        Assert.True(terminal.SplitComplete);
        Assert.False(terminal.FullCriterionVerified);
        Assert.False(terminal.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("nonce")]
    [InlineData("action")]
    [InlineData("decision")]
    [InlineData("trajectory")]
    [InlineData("partition")]
    [InlineData("authority")]
    public void DuplicateValidationOnlyRejectsCopiedOrMisgroupedNativeSources(string mode)
    {
        var first = Row("train", "1");
        var second = Row("train", "2");
        var e = second.Evidence;
        e = mode switch
        {
            "nonce" => e with { RequestNonce = first.Evidence.RequestNonce },
            "action" => e with { RunId = first.Evidence.RunId, QueueId = first.Evidence.QueueId, QueueItemId = first.Evidence.QueueItemId },
            "decision" => e with { RunId = first.Evidence.RunId, BeforeStateHash = first.Evidence.BeforeStateHash, SelectedCandidateId = first.Evidence.SelectedCandidateId },
            "trajectory" => e with { RunId = first.Evidence.RunId, TrajectoryId = first.Evidence.TrajectoryId },
            "partition" => e with { DatasetPartition = "test" },
            "authority" => e with { SourceSha256 = new Dictionary<string, string> { ["native.requirement_inventory"] = "foreign", ["native.acquisition_lowering"] = "lowering" } },
            _ => e
        };
        var exception = Assert.Throws<TargetInvocationException>(() => Validate(new[] { first, second with { Evidence = e } }));
        Assert.IsType<InvalidDataException>(exception.InnerException);
    }

    [Fact]
    public void DuplicateValidationOnlyAllowsDifferentDecisionsInTheSameOriginalSplitGroup()
    {
        var first = Row("train", "1");
        var second = Row("train", "2");
        Validate(new[] { first, second with { Evidence = second.Evidence with { SplitKey = first.Evidence.SplitKey } } });
    }

    private static MasterAnglerTeacherCorpusRow Row(string partition, string id)
    {
        var index = 0;
        string split;
        do { split = "fixture" + index++ + ":1:spring:7"; } while (PolicyTrajectoryDatasetBuilder.PartitionFor(split) != partition);
        var hashes = new Dictionary<string, string> { ["native.requirement_inventory"] = "inventory", ["native.acquisition_lowering"] = "lowering" };
        return new(id, "fixture", "hash", new MasterAnglerTeacherNativeJoin("request", hashes, "run" + id, "fixture", "actor", 6,
            "trajectory" + id, split, partition, "candidate" + id, "denominator", 6, "(O)test", "executor.catch_fish",
            "queue" + id, "item" + id, "nonce" + id, "before" + id, "after" + id, 10, 30), false);
    }

    private static GoalRequirementSet Inventory() => new()
    {
        RequirementSetId = "master_angler", RequiredGroupCount = 72,
        Groups = Enumerable.Range(0, 72).Select(index => new GoalRequirementGroup
        {
            RequirementId = "fish:f" + index, SelectionRule = "all_required", RequiredAlternativeCount = 1,
            Alternatives = new[] { new GoalRequirementAlternative { ItemId = "f" + index, QualifiedItemId = "(O)f" + index, Amount = 1, MatchKind = "item_id" } }
        }).ToArray()
    };

    private static SnapshotEnvelope Snapshot(int caught, params int[] achievements) => new()
    {
        State = new() { ["world_progress"] = JsonSerializer.SerializeToElement(new
        {
            achievements = new { status = "available", value = achievements },
            fish_collection_progress = new { status = "available", value = new
            {
                eligible_species_count = 72, caught_eligible_species_count = caught, missing_species_count = 72 - caught,
                missing_item_ids = Enumerable.Range(caught, 72 - caught).Select(index => "f" + index).ToArray(),
                items = Enumerable.Range(0, 72).Select(index => new { item_id = "f" + index, qualified_item_id = "(O)f" + index, caught = index < caught }).ToArray()
            } }
        }) }
    };

    private static bool Terminal(SnapshotEnvelope before, SnapshotEnvelope after) => (bool)typeof(MasterAnglerTeacherCorpusBuilder)
        .GetMethod("IsCriterionTerminal", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { before, after, Inventory() })!;

    private static void Validate(MasterAnglerTeacherCorpusRow[] rows) => typeof(MasterAnglerTeacherCorpusBuilder)
        .GetMethod("ValidateRows", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, new object[] { rows });
}
