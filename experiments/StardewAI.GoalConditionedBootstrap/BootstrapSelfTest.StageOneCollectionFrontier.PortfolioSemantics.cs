using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed partial class StageOneCollectionFrontierFixture
    {
        private static void VerifyPortfolioSemantics(
            AcquisitionRouteTargetDateUnlock portfolioRequirement)
        {
            AcquisitionOpportunityCostVector CostVector(
                int minutes,
                double energy) => new(
                minutes,
                energy,
                0,
                Array.Empty<AcquisitionOpportunityMaterialCost>(),
                Array.Empty<AcquisitionOpportunityCurrencyCost>(),
                Array.Empty<string>());
            var lowerTime = CostVector(10, 2d);
            var higherTime = CostVector(20, 2d);
            var lowerEnergyTradeoff = CostVector(20, 1d);
            var oneWood = new AcquisitionOpportunityCostVector(
                10,
                2d,
                2,
                new[]
                {
                    new AcquisitionOpportunityMaterialCost("(O)388", 0, 2, 1, 2)
                },
                Array.Empty<AcquisitionOpportunityCurrencyCost>(),
                Array.Empty<string>());
            var oneStone = new AcquisitionOpportunityCostVector(
                10,
                2d,
                2,
                new[]
                {
                    new AcquisitionOpportunityMaterialCost("(O)390", 0, 2, 1, 2)
                },
                Array.Empty<AcquisitionOpportunityCurrencyCost>(),
                Array.Empty<string>());
            var money100 = new AcquisitionOpportunityCostVector(
                10,
                2d,
                0,
                Array.Empty<AcquisitionOpportunityMaterialCost>(),
                new[]
                {
                    new AcquisitionOpportunityCurrencyCost(
                        NativeShopCurrencies.Money,
                        "money",
                        100)
                },
                Array.Empty<string>());
            var money200 = money100 with
            {
                CurrencyCosts = new[]
                {
                    new AcquisitionOpportunityCurrencyCost(
                        NativeShopCurrencies.Money,
                        "money",
                        200)
                }
            };
            var qiGems100 = money100 with
            {
                CurrencyCosts = new[]
                {
                    new AcquisitionOpportunityCurrencyCost(
                        NativeShopCurrencies.QiGems,
                        "qi_gems",
                        100)
                }
            };
            Require(
                AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(lowerTime, higherTime) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(higherTime, lowerTime) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(lowerTime, lowerEnergyTradeoff) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(lowerEnergyTradeoff, lowerTime) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(lowerTime, lowerTime) &&
                AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(lowerTime, oneWood) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(oneWood, oneStone) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(oneStone, oneWood) &&
                AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(money100, money200) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(money100, qiGems100) &&
                !AcquisitionRouteTargetDateOpportunityCostBuilder
                    .OpportunityCostDominates(qiGems100, money100),
                "Opportunity-cost strict Pareto boundary semantics drifted.");
            var strictPortfolioPareto =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder.EvaluatePareto(
                    new[]
                    {
                        new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                            .PortfolioCostCandidate("lower", lowerTime),
                        new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                            .PortfolioCostCandidate("higher", higherTime)
                    });
            var tradeoffPortfolioPareto =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder.EvaluatePareto(
                    new[]
                    {
                        new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                            .PortfolioCostCandidate("time", lowerTime),
                        new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                            .PortfolioCostCandidate(
                                "energy",
                                lowerEnergyTradeoff)
                    });
            var equalPortfolioPareto =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder.EvaluatePareto(
                    new[]
                    {
                        new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                            .PortfolioCostCandidate("equal-a", lowerTime),
                        new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                            .PortfolioCostCandidate("equal-b", lowerTime)
                    });
            Require(strictPortfolioPareto.FrontierProposalIds.SequenceEqual(
                        new[] { "lower" },
                        StringComparer.Ordinal) &&
                    strictPortfolioPareto.DominatedByProposalIds["higher"]
                        .SequenceEqual(
                            new[] { "lower" },
                            StringComparer.Ordinal) &&
                    tradeoffPortfolioPareto.FrontierProposalIds.Length == 2 &&
                    equalPortfolioPareto.FrontierProposalIds.Length == 2,
                "Portfolio Teacher Pareto preference semantics drifted.");
            var syntheticScope =
                new AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .ResolvedScope(
                        new AcquisitionRoutePortfolioRequirementScope(
                            "synthetic-set",
                            "synthetic-requirement"),
                        "choose_at_least_required_slots",
                        1,
                        3,
                        new Dictionary<int, string[]>
                        {
                            [0] = new[] { "route-a1", "route-a2" },
                            [1] = new[] { "route-b" },
                            [2] = new[] { "route-c" }
                        });
            var syntheticReasons = new List<string>();
            var syntheticCount =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .CountGroupCandidates(syntheticScope, syntheticReasons);
            var syntheticSelections =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .EnumerateGroupRouteSelections(syntheticScope);
            var allRequiredScope = syntheticScope with
            {
                SelectionRule = "all_required",
                RequiredAlternativeCount = 3
            };
            var allRequiredReasons = new List<string>();
            var largeDenominatorScope = syntheticScope with
            {
                AlternativeCount = 17,
                RoutesByAlternative = Enumerable.Range(0, 17).ToDictionary(
                    index => index,
                    index => new[]
                    {
                        $"route-{index}-a",
                        $"route-{index}-b"
                    })
            };
            var largeDenominatorReasons = new List<string>();
            var continuationScope = syntheticScope with
            {
                CompletedAlternativeIndices = new[] { 0 },
                RemainingRequiredAlternativeCount = 1
            };
            var continuationReasons = new List<string>();
            var continuationSelections =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .EnumerateGroupRouteSelections(continuationScope);
            var allRequiredContinuationScope = allRequiredScope with
            {
                CompletedAlternativeIndices = new[] { 0 },
                RemainingRequiredAlternativeCount = 2
            };
            var allRequiredContinuationReasons = new List<string>();
            var allRequiredContinuationSelections =
                AcquisitionRoutePortfolioTeacherPreferenceBuilder
                    .EnumerateGroupRouteSelections(
                        allRequiredContinuationScope);
            Require(syntheticReasons.Count == 0 &&
                    syntheticCount == 11 &&
                    syntheticSelections.Length == syntheticCount &&
                    syntheticSelections.Select(selection =>
                            string.Join("|", selection))
                        .Distinct(StringComparer.Ordinal).Count() ==
                    syntheticSelections.Length &&
                    allRequiredReasons.Count == 0 &&
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .CountGroupCandidates(
                            allRequiredScope,
                            allRequiredReasons) == 2 &&
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .EnumerateGroupRouteSelections(allRequiredScope).Length ==
                    2 &&
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .CountGroupCandidates(
                            largeDenominatorScope,
                            largeDenominatorReasons) >
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .MaxCandidateCount &&
                    largeDenominatorReasons.Count == 0 &&
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .CountGroupCandidates(
                            continuationScope,
                            continuationReasons) == 3 &&
                    continuationReasons.Count == 0 &&
                    continuationSelections.Length == 3 &&
                    continuationSelections.All(selection =>
                        !selection.Contains("route-a1") &&
                        !selection.Contains("route-a2")) &&
                    AcquisitionRoutePortfolioTeacherPreferenceBuilder
                        .CountGroupCandidates(
                            allRequiredContinuationScope,
                            allRequiredContinuationReasons) == 1 &&
                    allRequiredContinuationReasons.Count == 0 &&
                    allRequiredContinuationSelections.Length == 1 &&
                    allRequiredContinuationSelections[0].SequenceEqual(
                        new[] { "route-b", "route-c" },
                        StringComparer.Ordinal),
                "Portfolio Teacher weighted subset enumeration drifted.");
            var incompleteAllRequiredProgress =
                AcquisitionRoutePortfolioRolloutCheckpointBuilder.BuildProgress(
                    new AuthoritativeRequirementInventoryReport
                    {
                        RequirementSets = new[]
                        {
                            new GoalRequirementSet
                            {
                                RequirementSetId =
                                    portfolioRequirement.RequirementSetId,
                                Groups = new[]
                                {
                                    new GoalRequirementGroup
                                    {
                                        RequirementId =
                                            portfolioRequirement.RequirementId,
                                        SelectionRule = "all_required",
                                        RequiredAlternativeCount = 2,
                                        Alternatives = new[]
                                        {
                                            new GoalRequirementAlternative(),
                                            new GoalRequirementAlternative()
                                        }
                                    }
                                }
                            }
                        }
                    },
                    new AcquisitionRoutePortfolioTeacherPreferenceRequest
                    {
                        ScopedRequirements = new[]
                        {
                            new AcquisitionRoutePortfolioRequirementScope(
                                portfolioRequirement.RequirementSetId,
                                portfolioRequirement.RequirementId)
                        }
                    },
                    portfolioRequirement);
            Require(incompleteAllRequiredProgress.Length == 1 &&
                    !incompleteAllRequiredProgress[0].ScopeComplete &&
                    incompleteAllRequiredProgress[0].RemainingRequiredSlots == 1 &&
                    incompleteAllRequiredProgress[0]
                        .CompletedAlternativeIndices.SequenceEqual(
                            new[] { portfolioRequirement.AlternativeIndex }),
                "Portfolio rollout all-required continuation drifted.");

        }
    }
}
