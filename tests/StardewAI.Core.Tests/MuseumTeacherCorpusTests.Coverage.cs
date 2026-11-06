using System.Collections;
using System.Reflection;
using System.Text.Json;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumTeacherCorpusTests
{
    [Fact]
    public void OriginalReconciliationAdvertisesMuseumOnlyForItsOwnDirection()
    {
        var method = typeof(GoalMethodCoverageReconciliationBuilder).GetMethod("ImplementedSourceKinds", BindingFlags.NonPublic | BindingFlags.Static)!;
        foreach (var direction in GrandpaDirectionCatalog.Entries)
        {
            var root = JsonSerializer.Deserialize<GoalMethodFrontierMethod>("{\"direction_id\":\"" + direction.DirectionId + "\",\"requirement_set_readiness\":[]}")!;
            var kinds = (string[])method.Invoke(null, new object[] { root })!;
            Assert.Equal(direction.DirectionId == MuseumTeacherCorpus.Direction, kinds.Contains(GoalMethodTeacherCoverageSourceKinds.MuseumTerminalCorpus));
        }
    }

    [Theory]
    [InlineData("knowledge")]
    [InlineData("inventory")]
    [InlineData("method")]
    [InlineData("criteria")]
    [InlineData("executable")]
    public void NewAdapterRejectsForeignAuthorityOrMethodPromotionBeforeAddingCoverage(string mutation)
    {
        var frontier = Frontier();
        if (mutation == "knowledge") frontier.KnowledgeSha256 = "foreign";
        if (mutation == "inventory") frontier.RequirementInventorySha256 = "foreign";
        frontier.Methods[0] = mutation switch
        {
            "method" => frontier.Methods[0] with { MethodId = "foreign" },
            "criteria" => frontier.Methods[0] with { CriterionIds = new[] { "sewer_accessible" } },
            "executable" => frontier.Methods[0] with { Status = "executable_frontier" },
            _ => frontier.Methods[0]
        };
        var evidence = Evidence();
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Apply(frontier, evidence)).InnerException);
        Assert.All(evidence.Values.Cast<object>(), value => Assert.Empty((IEnumerable)value.GetType().GetProperty("TeacherSourceKinds")!.GetValue(value)!));
    }

    [Fact]
    public void NativeCorpusAddsOnlyItsOriginalTrainPartitionAndDoesNotChangeTheMethod()
    {
        var frontier = Frontier(); var evidence = Evidence();
        Apply(frontier, evidence);
        var own = evidence[MuseumTeacherCorpus.CriterionId]!;
        Assert.Equal(new[] { "train" }, ((IEnumerable)own.GetType().GetProperty("TeacherComparisonPartitions")!.GetValue(own)!).Cast<string>());
        Assert.Equal(new[] { "train" }, ((IEnumerable)own.GetType().GetProperty("NativeOutcomePartitions")!.GetValue(own)!).Cast<string>());
        var other = evidence["sewer_accessible"]!;
        Assert.Empty((IEnumerable)other.GetType().GetProperty("TeacherSourceKinds")!.GetValue(other)!);
        Assert.Equal("pending_dependency_expansion", frontier.Methods[0].Status);
    }

    private static GoalMethodFrontierReport Frontier() => new()
    {
        KnowledgeSha256 = "knowledge", RequirementInventorySha256 = "inventory",
        Methods = new[] { JsonSerializer.Deserialize<GoalMethodFrontierMethod>(
            "{\"direction_id\":\"complete_museum_collection\",\"method_id\":\"grandpa.direct.complete_museum_collection\",\"criterion_ids\":[\"achievement_complete_collection\"],\"status\":\"pending_dependency_expansion\"}")! }
    };

    private static IDictionary Evidence()
    {
        var type = typeof(GoalMethodTeacherCoverageBuilder).GetNestedType("CriterionCoverageEvidence", BindingFlags.NonPublic)!;
        var values = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), type))!;
        foreach (var id in new[] { MuseumTeacherCorpus.CriterionId, "sewer_accessible" }) values.Add(id, Activator.CreateInstance(type, id));
        return values;
    }

    private static void Apply(GoalMethodFrontierReport frontier, IDictionary evidence) =>
        typeof(GoalMethodTeacherCoverageBuilder).GetMethod("ApplyMuseumCorpus", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null,
            new object[] { new MuseumTeacherCorpus("request", "hash", "fixture", new[] { Row("train", "a") }),
                GoalMethodTeacherCoverageSourceKinds.MuseumTerminalCorpus, frontier, evidence });
}
