using System.Collections;
using System.Reflection;
using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MarriageTeacherCorpusTests
{
    [Theory]
    [InlineData("authority")]
    [InlineData("method")]
    [InlineData("criteria")]
    [InlineData("executable")]
    public void CorpusCannotPromoteAnUnprovedOrCrossAuthorityMethod(string mutation)
    {
        var frontier = Frontier();
        if (mutation == "authority") frontier.KnowledgeSha256 = "foreign";
        frontier.Methods[0] = mutation switch
        {
            "method" => frontier.Methods[0] with { MethodId = "foreign" },
            "criteria" => frontier.Methods[0] with { CriterionIds = new[] { "friendships_5" } },
            "executable" => frontier.Methods[0] with { Status = "executable_frontier" },
            _ => frontier.Methods[0]
        };
        var evidence = Evidence();
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() => Apply(Corpus(Run("a")), frontier, evidence)).InnerException);
        Assert.All(evidence.Values.Cast<object>(), value => Assert.Empty((IEnumerable)value.GetType().GetProperty("TeacherSourceKinds")!.GetValue(value)!));
    }

    [Fact]
    public void OriginalCoverageReceivesOnlyTheJointCriterionAndRetainsMethodBlockers()
    {
        var frontier = Frontier();
        var evidence = Evidence();
        Apply(Corpus(Run("a")), frontier, evidence);
        var joint = evidence[MarriageTeacherCorpus.CriterionId]!;
        Assert.Equal(new[] { "train" }, ((IEnumerable)joint.GetType().GetProperty("NativeOutcomePartitions")!.GetValue(joint)!).Cast<string>());
        var other = evidence["friendships_5"]!;
        Assert.Empty((IEnumerable)other.GetType().GetProperty("TeacherSourceKinds")!.GetValue(other)!);
        Assert.Equal("pending_dependency_expansion", frontier.Methods[0].Status);
        var method = typeof(GoalMethodCoverageReconciliationBuilder).GetMethod("ImplementedSourceKinds", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.Contains(GoalMethodTeacherCoverageSourceKinds.MarriageJointCorpus, (string[])method.Invoke(null, new object[] { frontier.Methods[0] })!);
        Assert.DoesNotContain(GoalMethodTeacherCoverageSourceKinds.MarriageJointCorpus,
            (string[])method.Invoke(null, new object[] { frontier.Methods[0] with { DirectionId = "raise_friendships" } })!);
    }

    private static GoalMethodFrontierReport Frontier() => new()
    {
        KnowledgeSha256 = "knowledge", Methods = new[] { JsonSerializer.Deserialize<GoalMethodFrontierMethod>(
            "{\"direction_id\":\"marriage_and_house_upgrade\",\"method_id\":\"grandpa.direct.marriage_and_house_upgrade\",\"criterion_ids\":[\"married_or_roommate_house_2\"],\"status\":\"pending_dependency_expansion\",\"requirement_set_readiness\":[]}")! }
    };

    private static IDictionary Evidence()
    {
        var type = typeof(GoalMethodTeacherCoverageBuilder).GetNestedType("CriterionCoverageEvidence", BindingFlags.NonPublic)!;
        var values = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(typeof(string), type))!;
        foreach (var id in new[] { MarriageTeacherCorpus.CriterionId, "friendships_5" }) values.Add(id, Activator.CreateInstance(type, id));
        return values;
    }

    private static void Apply(MarriageTeacherCorpus corpus, GoalMethodFrontierReport frontier, IDictionary evidence) =>
        typeof(GoalMethodTeacherCoverageBuilder).GetMethod("ApplyMarriageCorpus", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { corpus, GoalMethodTeacherCoverageSourceKinds.MarriageJointCorpus, frontier, evidence });
}
