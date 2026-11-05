using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class GrandpaDirectionDailyCandidateBindingTests
{
    [Fact]
    public void RustyKeyCorpusIsRegisteredInOriginalReconciliationWithoutInventingAnAdapterForOtherRoots()
    {
        var method = typeof(GoalMethodCoverageReconciliationBuilder).GetMethod("ImplementedSourceKinds",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var frontier = RustyCoverageTestFrontier("pending_dependency_expansion");
        foreach (var root in frontier.Methods)
        {
            var kinds = (string[])method.Invoke(null, new object[] { root })!;
            Assert.Equal(root.DirectionId == "obtain_rusty_key",
                kinds.Contains(GoalMethodTeacherCoverageSourceKinds.RustyKeyThresholdDecisionCorpus));
        }
    }

    [Fact]
    public void RustyKeyCorpusKeepsOneRealDecisionGroupPartialAndReplaysItsSources()
    {
        var f = CreateRustyCorpusFixture(splitComplete: false);
        var result = RustyKeyTeacherCorpusBuilder.Build(f.RequestPath);
        Assert.False(result.SplitComplete);
        Assert.False(result.FullCriterionVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
        Assert.Equal(1, result.RowCount);
        Assert.Equal(1, result.PartitionCount);
        var path = Path.Combine(f.Root, "corpus.json");
        WriteNativeKeyJson(path, result);
        Assert.Equal(JsonSerializer.Serialize(result, JsonOptions),
            JsonSerializer.Serialize(RustyKeyTeacherCorpusBuilder.Verify(path), JsonOptions));
    }

    [Fact]
    public void RustyKeyCorpusRequiresIndependentDecisionGroupsForAllThreePartitions()
    {
        var f = CreateRustyCorpusFixture(splitComplete: true);
        var result = RustyKeyTeacherCorpusBuilder.Build(f.RequestPath);
        Assert.True(result.SplitComplete);
        Assert.Equal(3, result.RowCount);
        Assert.Equal(3, result.PartitionCount);
        Assert.Equal(3, result.Rows.Select(row => row.Evidence.RunId).Distinct().Count());
        Assert.Equal(3, result.Rows.Select(row => row.Evidence.SplitKey).Distinct().Count());
        Assert.False(result.FullCriterionVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("empty")]
    [InlineData("null_sources")]
    [InlineData("null_source")]
    [InlineData("missing_id")]
    [InlineData("training")]
    [InlineData("duplicate_source_id")]
    [InlineData("duplicate_join")]
    [InlineData("same_group_new_run")]
    [InlineData("same_run_new_group")]
    public void RustyKeyCorpusRejectsInvalidOrReusedSources(string mode)
    {
        var f = CreateRustyCorpusFixture(splitComplete: false);
        var node = JsonNode.Parse(File.ReadAllText(f.RequestPath))!;
        switch (mode)
        {
            case "schema": node["schema_version"] = "old-schema"; break;
            case "empty": node["sources"] = new JsonArray(); break;
            case "null_sources": node["sources"] = null; break;
            case "null_source": node["sources"]![0] = null; break;
            case "missing_id": node["sources"]![0]!["source_id"] = ""; break;
            case "training": node["formal_product_training_authorized"] = true; break;
            case "duplicate_source_id": node["sources"]!.AsArray().Add(node["sources"]![0]!.DeepClone()); break;
            case "duplicate_join":
                var duplicate = node["sources"]![0]!.DeepClone();
                duplicate["source_id"] = "renamed-copy";
                node["sources"]!.AsArray().Add(duplicate); break;
            case "same_group_new_run":
            case "same_run_new_group":
                var initial = RustyKeyTeacherNativeJoinBuilder.Verify(f.Sources[0].JoinReceiptPath);
                var other = CreateNativeJoinFixture(decisionDay: mode == "same_group_new_run" ? initial.DecisionTotalDay : initial.DecisionTotalDay + 1,
                    runId: mode == "same_run_new_group" ? initial.RunId : "separate-run");
                var joinPath = Path.Combine(other.Root, "join.json");
                WriteNativeKeyJson(joinPath, RustyKeyTeacherNativeJoinBuilder.Build(other.RequestPath));
                node["sources"]!.AsArray().Add(JsonSerializer.SerializeToNode(new RustyKeyTeacherCorpusSource("other", joinPath), JsonOptions));
                break;
        }
        WriteNativeKeyJson(f.RequestPath, node);
        Assert.Throws<InvalidDataException>(() => RustyKeyTeacherCorpusBuilder.Build(f.RequestPath));
    }

    [Theory]
    [InlineData("split_complete")]
    [InlineData("formal_product_training_authorized")]
    [InlineData("full_criterion_verified")]
    [InlineData("row_count")]
    [InlineData("partition")]
    [InlineData("removed_source")]
    public void RustyKeyCorpusReplayRejectsForgedClaimsAndChangedDenominator(string field)
    {
        var f = CreateRustyCorpusFixture(splitComplete: false);
        var node = JsonSerializer.SerializeToNode(RustyKeyTeacherCorpusBuilder.Build(f.RequestPath), JsonOptions)!;
        if (field == "row_count") node[field] = 3;
        else if (field == "partition") node["rows"]![0]!["evidence"]!["dataset_partition"] = "made-up-partition";
        else if (field == "removed_source") node["rows"] = new JsonArray();
        else node[field] = true;
        var path = Path.Combine(f.Root, "forged-corpus.json");
        WriteNativeKeyJson(path, node);
        Assert.Throws<InvalidDataException>(() => RustyKeyTeacherCorpusBuilder.Verify(path));
    }

    [Theory]
    [InlineData(false, "executable_frontier", false)]
    [InlineData(true, "pending_dependency_expansion", false)]
    [InlineData(true, "executable_frontier", true)]
    public void RustyKeyCorpusUsesOriginalCoverageIntersectionRatherThanPromotingNativeAlone(bool splitComplete, string methodStatus, bool ready)
    {
        var f = CreateRustyCorpusFixture(splitComplete);
        var corpusPath = Path.Combine(f.Root, "coverage-corpus.json");
        WriteNativeKeyJson(corpusPath, RustyKeyTeacherCorpusBuilder.Build(f.RequestPath));
        var requestPath = Path.Combine(f.Root, "coverage-request.json");
        WriteNativeKeyJson(requestPath, new GoalMethodTeacherCoverageRequest
        {
            CoverageId = "hermetic-rusty-key-coverage",
            Sources = new[] { new GoalMethodTeacherCoverageSource("rusty", GoalMethodTeacherCoverageSourceKinds.RustyKeyThresholdDecisionCorpus, corpusPath) }
        });
        var frontier = RustyCoverageTestFrontier(methodStatus);
        var method = typeof(GoalMethodTeacherCoverageBuilder).GetMethod("Build", BindingFlags.Static | BindingFlags.NonPublic,
            null, new[] { typeof(GoalMethodFrontierBuildInputs), typeof(string), typeof(GoalMethodFrontierReport) }, null)!;
        var report = (GoalMethodTeacherCoverageReport)method.Invoke(null, new object[] { new GoalMethodFrontierBuildInputs(), requestPath, frontier })!;
        var row = Assert.Single(report.Criteria.Where(value => value.DirectionId == "obtain_rusty_key"));
        Assert.Equal(ready, row.CoverageGateReady);
        Assert.Equal(splitComplete, row.SplitCoverageComplete);
        Assert.Equal(ready ? 1 : 0, report.CoverageGateReadyCriterionCount);
        Assert.Equal(19, report.CriterionDenominatorCount);
        Assert.False(report.CoverageGateSatisfied);
        Assert.False(report.FormalProductTrainingAuthorized);
    }

    private sealed record RustyCorpusFixture(string Root, string RequestPath, RustyKeyTeacherCorpusSource[] Sources);

    private static RustyCorpusFixture CreateRustyCorpusFixture(bool splitComplete)
    {
        var sources = new List<RustyKeyTeacherCorpusSource>();
        string? root = null;
        foreach (var partition in splitComplete ? new[] { "train", "validation", "test" } : new[] { "train" })
        {
            var day = Enumerable.Range(6, 217).First(value => PolicyTrajectoryDatasetBuilder.PartitionFor("MuseumFarm:123:" + value) == partition);
            var f = CreateNativeJoinFixture(decisionDay: day, runId: "hermetic-corpus-" + partition);
            root ??= f.Root;
            var path = Path.Combine(f.Root, "joined.json");
            WriteNativeKeyJson(path, RustyKeyTeacherNativeJoinBuilder.Build(f.RequestPath));
            sources.Add(new(partition, path));
        }
        var requestPath = Path.Combine(root!, "corpus-request.json");
        WriteNativeKeyJson(requestPath, new RustyKeyTeacherCorpusRequest { CorpusId = "hermetic-rusty-key", Sources = sources.ToArray() });
        return new(root!, requestPath, sources.ToArray());
    }

    private static GoalMethodFrontierReport RustyCoverageTestFrontier(string status)
    {
        var methods = GrandpaDirectionCatalog.Entries.Select(entry => new GoalMethodFrontierMethod(entry.BindingRuleId, entry.DirectionId,
            entry.Domain, entry.EffectiveGoalId, entry.DemandFamily, entry.DirectBindingEnabled, entry.CriterionIds, entry.PermittedOptionIds,
            Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>(),
            Array.Empty<GoalMethodRequirementSetReadiness>(), Array.Empty<string>(), Array.Empty<string>(), Array.Empty<GoalMethodOptionBlocker>(),
            entry.DirectionId == "obtain_rusty_key" ? status : "pending_dependency_expansion")).ToArray();
        // Point weights match the pinned 1.6.15 goal-dependency index; this is not native coverage evidence.
        var criteria = methods.SelectMany(method => method.CriterionIds.Select(id => new GoalCriterionFrontier(id,
            id is "money_1000000" or "community_center_accessible_bonus" ? 2 : 1, new[] { method.MethodId }, method.Status))).ToArray();
        return new GoalMethodFrontierReport { GoalId = GoalMethodTeacherCoverageGoalIds.Authoritative, TargetScore = 21,
            CriterionCount = criteria.Length, RootMethodCount = methods.Length, Methods = methods, Criteria = criteria };
    }
}
