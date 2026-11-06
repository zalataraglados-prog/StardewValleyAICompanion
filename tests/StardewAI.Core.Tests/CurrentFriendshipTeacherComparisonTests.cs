using StardewAI.GoalConditionedBootstrap;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CurrentFriendshipTeacherComparisonTests
{
    [Fact]
    public void LowerCalibratedBudgetWinsWithoutRankOrIdTiebreaks()
    {
        var first = Row("z", 901);
        var second = Row("a", 908);
        var pair = CurrentFriendshipTeacherComparisonPolicy.Compare(first, second);
        Assert.NotNull(pair);
        Assert.Equal("z", pair.PreferredCandidateId);
        Assert.Equal("lower_calibrated_contact_budget_not_observed_duration", pair.FirstDifferingAuthoritativeCriterion);
        Assert.Null(CurrentFriendshipTeacherComparisonPolicy.Compare(second, first));
        Assert.Null(CurrentFriendshipTeacherComparisonPolicy.Compare(first, Row("a", 901)));
    }

    [Theory]
    [InlineData("progress")]
    [InlineData("count")]
    [InlineData("minutes")]
    [InlineData("energy")]
    [InlineData("kind")]
    [InlineData("option")]
    [InlineData("item")]
    [InlineData("quality")]
    [InlineData("slot")]
    [InlineData("limits")]
    [InlineData("evidence")]
    [InlineData("unavailable")]
    [InlineData("alias")]
    [InlineData("unknown_timing")]
    public void TradeoffsMissingEvidenceAndAliasesAreNotNegativeLabels(string change)
    {
        var first = Row("first", 901);
        var second = Row("second", 908);
        switch (change)
        {
            case "progress": first.UsefulFriendshipDelta = 10; break;
            case "count": first.ExpectedNewQualifiers = 0; break;
            case "minutes": first.CompiledMinutes = 2; break;
            case "energy": first.EnergyCost = 1; break;
            case "kind": second.Kind = "route_connector_tile"; break;
            case "option": second.OptionId = "social.gift_npc"; break;
            case "item": second.GiftQualifiedItemId = "(O)66"; break;
            case "quality": second.GiftQuality = 4; break;
            case "slot": second.GiftSlotIndex = 1; break;
            case "limits": second.GiftNormalLimitEffect = "false"; break;
            case "evidence": second.TimingEvidenceId = "other-calibration"; break;
            case "unavailable": second.AvailableCurrentSource = false; break;
            case "alias": second.NativeActionIdentity = first.NativeActionIdentity; break;
            case "unknown_timing": first.TimingEvidenceKind = "Unknown"; break;
        }
        Assert.Null(CurrentFriendshipTeacherComparisonPolicy.Compare(first, second));
    }

    private static CurrentFriendshipTeacherCandidate Row(string id, int time) => new()
    {
        CandidateId = id, NpcName = id, NativeActionIdentity = "native:" + id, OptionId = "social.talk_npc",
        Kind = "social_talk_current", Status = "admitted_current_social_comparison", AvailableCurrentSource = true, ComparisonRequired = true,
        FriendshipPointsBefore = 1960, ExpectedFriendshipPointsAfter = 1980,
        UsefulFriendshipDelta = 15, ExpectedNewQualifiers = 1, CompiledMinutes = 1,
        ContactBudgetArrivalTime = time, TimingEvidenceId = "calibrated-shared-context",
        TimingEvidenceKind = "ConservativeUpperBound"
    };

    [Fact]
    public void FirstConnectorTimingWithoutFullContactProofCannotBecomeAPreference()
    {
        var first = Row("first", 901);
        var second = Row("second", 908);
        first.Kind = second.Kind = "route_connector_tile";
        Assert.Null(CurrentFriendshipTeacherComparisonPolicy.Compare(first, second));
    }

    [Fact]
    public void ImmediateTalkCanDominateAProvenFullRoutedTalkWithoutChangingTheCostOrder()
    {
        var first = Row("local", 901);
        var second = Routed("remote", 908);
        Assert.NotNull(CurrentFriendshipTeacherComparisonPolicy.Compare(first, second));
        first.ContactBudgetArrivalTime = 910;
        Assert.Null(CurrentFriendshipTeacherComparisonPolicy.Compare(first, second));
        Assert.Null(CurrentFriendshipTeacherComparisonPolicy.Compare(second, first));
    }

    [Theory]
    [InlineData("missing_proof")]
    [InlineData("candidate")]
    [InlineData("opportunity")]
    [InlineData("timing")]
    [InlineData("window")]
    [InlineData("gift")]
    [InlineData("unknown_kind")]
    [InlineData("progress_tradeoff")]
    [InlineData("cost_tradeoff")]
    [InlineData("npc_alias")]
    [InlineData("missing_npc")]
    public void CrossGeometryTalkStillRejectsUnprovenBindingsAndRealTradeoffs(string change)
    {
        var first = Row("local", 901);
        var second = Routed("remote", 908);
        switch (change)
        {
            case "missing_proof": second.RouteContactBudget!.Status = "blocked"; break;
            case "candidate": second.RouteContactBudget!.CandidateId = "foreign"; break;
            case "opportunity": second.RouteContactBudget!.OpportunityId = "foreign"; break;
            case "timing": second.RouteContactBudget!.TimingEvidenceId = "foreign"; break;
            case "window": second.RouteContactBudget!.ContactWindow!.EarliestInteractionTime = 909; break;
            case "gift": first.OptionId = second.OptionId = "social.gift_npc"; break;
            case "unknown_kind": first.Kind = "unknown"; break;
            case "progress_tradeoff": first.UsefulFriendshipDelta = 10; break;
            case "cost_tradeoff": first.CompiledMinutes = 2; break;
            case "npc_alias": second.NpcName = first.NpcName; break;
            case "missing_npc": second.NpcName = string.Empty; break;
        }
        Assert.Null(CurrentFriendshipTeacherComparisonPolicy.Compare(first, second));
    }

    private static CurrentFriendshipTeacherCandidate Routed(string id, int time)
    {
        var row = Row(id, time);
        row.Kind = "route_connector_tile";
        row.OpportunityId = "opportunity:" + id;
        row.RouteContactBudget = new()
        {
            Status = "verified_for_actual_first_connector", CandidateId = id, OpportunityId = row.OpportunityId,
            TimingEvidenceId = row.TimingEvidenceId,
            RequiredFirstEdge = new TransparentRouteEdge("warp", "Farm", "Town", 1, 1, 2, 2),
            RouteScenario = new(), ContactWindow = new() { EarliestInteractionTime = time }
        };
        return row;
    }
}
