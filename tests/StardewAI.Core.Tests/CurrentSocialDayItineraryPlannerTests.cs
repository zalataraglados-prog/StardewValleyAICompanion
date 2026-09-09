using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CurrentSocialDayItineraryPlannerTests
{
    [Fact]
    public void IncompleteNonportfolioNpcCoverageCanRemainAnExplicitLimitation()
    {
        var frontier = new CurrentSocialContactFrontier
        {
            RankingAdmissionReady = false,
            RankingAdmissionBlockingReasons = new[]
            {
                "current_social_npc_coverage_incomplete"
            },
            Opportunities = new[]
            {
                new CurrentSocialContactOpportunity
                {
                    NpcName = "Marnie",
                    ExecutionReady = true
                }
            }
        };

        Assert.True(
            CurrentSocialDayItineraryPlanner.CanUseConservativePartialFrontier(
                frontier));
    }

    [Fact]
    public void PartialFrontierCannotHideAnUnboundOpportunityOrAnotherBlocker()
    {
        var unbound = new CurrentSocialContactFrontier
        {
            RankingAdmissionBlockingReasons = new[]
            {
                "current_social_npc_coverage_incomplete"
            },
            Opportunities = new[]
            {
                new CurrentSocialContactOpportunity
                {
                    NpcName = "Marnie",
                    ExecutionReady = false
                }
            }
        };
        var otherBlocker = new CurrentSocialContactFrontier
        {
            RankingAdmissionBlockingReasons = new[]
            {
                "current_social_npc_coverage_incomplete",
                "current_social_opportunity_execution_binding_incomplete"
            }
        };

        Assert.False(
            CurrentSocialDayItineraryPlanner.CanUseConservativePartialFrontier(
                unbound));
        Assert.False(
            CurrentSocialDayItineraryPlanner.CanUseConservativePartialFrontier(
                otherBlocker));
    }
}
