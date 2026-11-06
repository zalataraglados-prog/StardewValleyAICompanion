using System.Collections;
using System.Reflection;
using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class FriendshipTeacherCorpusTests
{
    [Fact]
    public void ReconciliationAdvertisesTheNewSourceOnlyForFriendships()
    {
        var method = typeof(GoalMethodCoverageReconciliationBuilder).GetMethod("ImplementedSourceKinds", BindingFlags.NonPublic | BindingFlags.Static)!;
        string[] Kinds(string direction) => (string[])method.Invoke(null, new object[]
        { JsonSerializer.Deserialize<GoalMethodFrontierMethod>("{\"direction_id\":\"" + direction + "\",\"requirement_set_readiness\":[]}")! })!;
        Assert.Contains(GoalMethodTeacherCoverageSourceKinds.FriendshipThresholdCorpus, Kinds("raise_friendships"));
        Assert.DoesNotContain(GoalMethodTeacherCoverageSourceKinds.FriendshipThresholdCorpus, Kinds("raise_skill_levels"));
        Assert.DoesNotContain(GoalMethodTeacherCoverageSourceKinds.FriendshipThresholdCorpus, Kinds("earn_money"));
    }

    [Theory]
    [InlineData("authority")]
    [InlineData("method")]
    [InlineData("criteria")]
    [InlineData("executable")]
    public void CoverageRejectsCrossAuthorityOrUnsupportedMethodPromotionBeforeAddingEvidence(string mutation)
    {
        var frontier = Frontier();
        if (mutation == "authority") frontier.KnowledgeSha256 = "other";
        var method = frontier.Methods[0];
        frontier.Methods[0] = mutation switch
        {
            "method" => method with { MethodId = "other" },
            "criteria" => method with { CriterionIds = new[] { "friendships_5" } },
            "executable" => method with { Status = "executable_frontier" },
            _ => method
        };
        var evidence = Evidence();
        var error = Assert.Throws<TargetInvocationException>(() => Apply(Corpus(Row("friendships_5", "train", "a")), frontier, evidence));
        Assert.IsType<InvalidDataException>(error.InnerException);
        Assert.All(evidence.Values.Cast<object>(), value => Assert.Empty((IEnumerable)value.GetType().GetProperty("TeacherSourceKinds")!.GetValue(value)!));
    }

    [Fact]
    public void CoverageAppliesOnlyTheProvenCriterionWithoutClearingMethodBlockers()
    {
        var evidence = Evidence();
        var frontier = Frontier();
        Apply(Corpus(Row("friendships_5", "train", "a")), frontier, evidence);
        var five = evidence["friendships_5"]!;
        var ten = evidence["friendships_10"]!;
        var type = five.GetType();
        Assert.Contains("train", ((IEnumerable)type.GetProperty("TeacherComparisonPartitions")!.GetValue(five)!).Cast<string>());
        Assert.Empty((IEnumerable)type.GetProperty("TeacherComparisonPartitions")!.GetValue(ten)!);
        Assert.Equal("pending_dependency_expansion", frontier.Methods[0].Status);
    }

    private static GoalMethodFrontierReport Frontier() => new()
    {
        KnowledgeSha256 = "knowledge",
        Methods = new[] { JsonSerializer.Deserialize<GoalMethodFrontierMethod>(
            "{\"direction_id\":\"raise_friendships\",\"method_id\":\"grandpa.direct.raise_friendships\",\"criterion_ids\":[\"friendships_5\",\"friendships_10\"],\"status\":\"pending_dependency_expansion\"}")! }
    };

    private static IDictionary Evidence()
    {
        var type = typeof(GoalMethodTeacherCoverageBuilder).GetNestedType("CriterionCoverageEvidence", BindingFlags.NonPublic)!;
        var values = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), type))!;
        foreach (var id in new[] { "friendships_5", "friendships_10" }) values.Add(id, Activator.CreateInstance(type, id));
        return values;
    }

    private static void Apply(FriendshipTeacherCorpus corpus, GoalMethodFrontierReport frontier, IDictionary evidence) =>
        typeof(GoalMethodTeacherCoverageBuilder).GetMethod("ApplyFriendshipCorpus", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object?[] { corpus, GoalMethodTeacherCoverageSourceKinds.FriendshipThresholdCorpus, frontier, evidence, null, null });
}
