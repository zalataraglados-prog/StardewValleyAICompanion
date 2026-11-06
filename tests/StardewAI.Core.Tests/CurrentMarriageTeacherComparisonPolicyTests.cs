using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class CurrentMarriageTeacherComparisonPolicyTests
{
    [Fact]
    public void SameNativePreparationWithLowerRegisteredReserveSafeLiquidationCostProducesOnlyAStrictPair()
    {
        var pair = CurrentMarriageTeacherComparisonPolicy.Compare(Row("a", 100), Row("b", 352));
        Assert.NotNull(pair);
        Assert.Equal("same_native_effect_lower_unreserved_gift_liquidation_cost", pair!.FirstDifferingAuthoritativeCriterion);
        Assert.Null(CurrentMarriageTeacherComparisonPolicy.Compare(Row("a", 352), Row("b", 100)));
        Assert.Null(CurrentMarriageTeacherComparisonPolicy.Compare(Row("a", 100), Row("b", 100)));
    }

    [Theory]
    [InlineData("source")]
    [InlineData("ledger")]
    [InlineData("target")]
    [InlineData("effect")]
    [InlineData("identity_alias")]
    [InlineData("timing")]
    [InlineData("arrival")]
    [InlineData("minutes")]
    [InlineData("energy")]
    [InlineData("outcome")]
    [InlineData("deadline")]
    [InlineData("pending")]
    [InlineData("opportunity")]
    public void CostDoesNotOverrideForeignEvidenceAliasesEffectsBudgetsOrUnknownAdmissions(string mutation)
    {
        var preferred = Row("a", 100);
        var other = Row("b", 352);
        switch (mutation)
        {
            case "source": preferred.SourceStateHash = "other"; break;
            case "ledger": preferred.RegisteredLedgerSha256 = "other"; break;
            case "target": preferred.NpcName = "other"; break;
            case "effect": preferred.NativeEffectSignature = "other"; break;
            case "identity_alias": preferred.NativeActionIdentity = other.NativeActionIdentity; break;
            case "timing": preferred.TimingEvidenceId = "other"; break;
            case "arrival": preferred.ContactArrivalTime = 920; break;
            case "minutes": preferred.CompiledMinutes++; break;
            case "energy": preferred.EnergyCost++; break;
            case "outcome": preferred.FriendshipPointsAfter = 2501; break;
            case "deadline": preferred.ConditionalJointSettlementTotalDay = 224; break;
            case "pending": preferred.Status = "uncompared"; break;
            case "opportunity": preferred.OpportunityId = ""; break;
        }
        Assert.Null(CurrentMarriageTeacherComparisonPolicy.Compare(preferred, other));
    }

    private static CurrentMarriageTeacherCandidate Row(string id, int cost) => new()
    {
        CandidateId = id, SourceStateHash = "same-snapshot", RegisteredLedgerSha256 = "same-ledger", NpcName = "Abigail",
        Status = "admitted_current_partner_preparation", NativeActionIdentity = id, NativeEffectSignature = "same-native-ordinary-gift-effect",
        OpportunityId = id, ProposalSourceCandidateId = "native-proposal", TimingEvidenceId = "same-calibration", ContactArrivalTime = 910,
        CompiledMinutes = 5, GiftSaleValue = cost, GiftSlotIndex = id == "a" ? 0 : 1, GiftQualifiedItemId = id == "a" ? "(O)66" : "(O)276",
        FriendshipPointsBefore = 2420, FriendshipPointsAfter = 2500, ConditionalJointSettlementTotalDay = 215
    };
}
