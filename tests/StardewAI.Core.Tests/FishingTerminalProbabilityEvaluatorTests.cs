using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class FishingTerminalProbabilityEvaluatorTests
{
    [Fact]
    public void ComputesConservativeFirstPassLowerBoundWithWorstCaseCompetitorOrder()
    {
        var result = FishingTerminalProbabilityEvaluator.Evaluate(Request(
            Rule("earlier", 0, spawnChance: 0.2d),
            Rule(
                "target",
                1,
                producesTarget: true,
                spawnChance: 0.5d,
                selectionProbability: 0.25d,
                acceptanceProbability: 0.4d),
            Rule("later", 2, spawnChance: 1d)));

        Assert.True(result.Resolved);
        Assert.Equal("conservative_first_pass_lower_bound", result.ProbabilityKind);
        Assert.Equal("target", result.SelectedRuleKey);
        Assert.Equal(0.04d, result.SingleAttemptProbabilityLowerBound!.Value, 10);
        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(new[] { "earlier" }, candidate.CompetitorRuleKeys);
        Assert.Equal(0.8d, candidate.PriorCompetitorNonReturnProbabilityLowerBound);
        Assert.Equal(0.05d, candidate.TargetReturnProbabilityLowerBound);
    }

    [Fact]
    public void TreatsEqualPrecedenceCompetitorAsIfItRunsFirst()
    {
        var result = FishingTerminalProbabilityEvaluator.Evaluate(Request(
            Rule("target", 5, true, 0.5d, 1d, 1d),
            Rule("same-precedence", 5, spawnChance: 0.75d)));

        Assert.Equal(0.125d, result.SingleAttemptProbabilityLowerBound);
        Assert.Equal(
            new[] { "same-precedence" },
            Assert.Single(result.Candidates).CompetitorRuleKeys);
    }

    [Fact]
    public void SeededTargetRollIsDeterministicRatherThanAnIndependentRetry()
    {
        var failed = FishingTerminalProbabilityEvaluator.Evaluate(Request(
            Rule(
                "seeded-target",
                0,
                true,
                spawnChance: 0.9d,
                selectionProbability: 1d,
                acceptanceProbability: 1d,
                rollKind: FishingSpawnRollKind.DeterministicFishCaughtSeed,
                seededRollPassed: false)));
        var passed = FishingTerminalProbabilityEvaluator.Evaluate(Request(
            Rule(
                "seeded-target",
                0,
                true,
                spawnChance: 0.1d,
                selectionProbability: 0.5d,
                acceptanceProbability: 0.4d,
                rollKind: FishingSpawnRollKind.DeterministicFishCaughtSeed,
                seededRollPassed: true)));

        Assert.Equal(0d, failed.SingleAttemptProbabilityLowerBound);
        Assert.Equal(0.2d, passed.SingleAttemptProbabilityLowerBound!.Value, 10);
    }

    [Fact]
    public void UnresolvedCompetitorConservativelyReducesLowerBoundToZero()
    {
        var unresolvedCompetitor = Rule("random-condition", 0, spawnChance: 0.1d) with
        {
            EligibilityResolved = false
        };
        var result = FishingTerminalProbabilityEvaluator.Evaluate(Request(
            unresolvedCompetitor,
            Rule("target", 1, true, 0.5d, 1d, 1d)));

        Assert.True(result.Resolved);
        Assert.Equal(0d, result.SingleAttemptProbabilityLowerBound);
    }

    [Fact]
    public void UnresolvedTargetBlocksWhenNoOtherTargetRuleIsProven()
    {
        var result = FishingTerminalProbabilityEvaluator.Evaluate(Request(
            Rule("target", 0, true, 0.5d, null, 1d)));

        Assert.False(result.Resolved);
        Assert.Equal("blocked_probability_evidence", result.Status);
        Assert.Equal(
            new[] { "target_output_selection_probability_unresolved:target" },
            result.BlockingReasons);
    }

    [Fact]
    public void CompleteInventoryWithoutEligibleTargetRuleResolvesToZero()
    {
        var target = Rule("target", 0, true, 0.5d, 1d, 1d) with
        {
            EligibleBeforeRandomRolls = false
        };
        var result = FishingTerminalProbabilityEvaluator.Evaluate(Request(target));

        Assert.True(result.Resolved);
        Assert.Equal("resolved_no_eligible_target_rule", result.Status);
        Assert.Equal(0d, result.SingleAttemptProbabilityLowerBound);
        Assert.Empty(result.Candidates);
    }

    [Fact]
    public void RejectsIncompleteInventoryAndNonFirstPass()
    {
        var result = FishingTerminalProbabilityEvaluator.Evaluate(new FishingTerminalProbabilityRequest
        {
            TargetQualifiedItemId = "(O)145",
            FishingPassIndex = 1,
            RuleInventoryComplete = false,
            Rules = new[] { Rule("target", 0, true, 0.5d, 1d, 1d) }
        });

        Assert.False(result.Resolved);
        Assert.Equal(
            new[]
            {
                "combined_rule_inventory_incomplete",
                "only_first_fishing_pass_supported"
            },
            result.BlockingReasons);
    }

    private static FishingTerminalProbabilityRequest Request(
        params FishingTerminalProbabilityRule[] rules) => new()
        {
            TargetQualifiedItemId = "(O)145",
            FishingPassIndex = 0,
            RuleInventoryComplete = true,
            Rules = rules
        };

    private static FishingTerminalProbabilityRule Rule(
        string ruleKey,
        int precedence,
        bool producesTarget = false,
        double? spawnChance = null,
        double? selectionProbability = null,
        double? acceptanceProbability = null,
        FishingSpawnRollKind rollKind = FishingSpawnRollKind.IndependentRandom,
        bool? seededRollPassed = null) => new()
        {
            RuleKey = ruleKey,
            Precedence = precedence,
            EligibilityResolved = true,
            EligibleBeforeRandomRolls = true,
            ProducesTarget = producesTarget,
            OutputResolutionComplete = true,
            SpawnRollKind = rollKind,
            SeededSpawnRollPassed = seededRollPassed,
            SpawnChance = spawnChance,
            TargetOutputSelectionProbability = selectionProbability,
            TargetGenericAcceptanceProbability = acceptanceProbability
        };
}
