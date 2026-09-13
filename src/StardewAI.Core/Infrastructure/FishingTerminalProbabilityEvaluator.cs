using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Infrastructure;

public enum FishingSpawnRollKind
{
    IndependentRandom,
    DeterministicFishCaughtSeed
}

public sealed record FishingTerminalProbabilityRule
{
    public string RuleKey { get; init; } = string.Empty;
    public int Precedence { get; init; }
    public bool EligibilityResolved { get; init; }
    public bool EligibleBeforeRandomRolls { get; init; }
    public bool ProducesTarget { get; init; }
    public bool OutputResolutionComplete { get; init; }
    public bool RetryContextStable { get; init; }
    public FishingSpawnRollKind SpawnRollKind { get; init; }
    public bool? SeededSpawnRollPassed { get; init; }
    public double? SpawnChance { get; init; }
    public double? TargetOutputSelectionProbability { get; init; }
    public double? TargetGenericAcceptanceProbability { get; init; }
}

public sealed record FishingTerminalProbabilityRequest
{
    public string TargetQualifiedItemId { get; init; } = string.Empty;
    public int FishingPassIndex { get; init; }
    public bool RuleInventoryComplete { get; init; }
    public IReadOnlyList<FishingTerminalProbabilityRule> Rules { get; init; } =
        Array.Empty<FishingTerminalProbabilityRule>();
}

public sealed record FishingTerminalProbabilityCandidate
{
    public string RuleKey { get; init; } = string.Empty;
    public bool Resolved { get; init; }
    public double? ProbabilityLowerBound { get; init; }
    public double? TargetReturnProbabilityLowerBound { get; init; }
    public double? PriorCompetitorNonReturnProbabilityLowerBound { get; init; }
    public bool IndependentRetryLowerBoundProven { get; init; }
    public string[] CompetitorRuleKeys { get; init; } = Array.Empty<string>();
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();
    public string[] RetryBlockingReasons { get; init; } = Array.Empty<string>();
}

public sealed record FishingTerminalProbabilityResult
{
    public string Status { get; init; } = "blocked";
    public bool Resolved { get; init; }
    public string ProbabilityKind { get; init; } =
        "conservative_first_pass_lower_bound";
    public double? SingleAttemptProbabilityLowerBound { get; init; }
    public string? SelectedRuleKey { get; init; }
    public bool IndependentRetryLowerBoundProven { get; init; }
    public FishingTerminalProbabilityCandidate[] Candidates { get; init; } =
        Array.Empty<FishingTerminalProbabilityCandidate>();
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();
    public string[] RetryBlockingReasons { get; init; } = Array.Empty<string>();
}

public static class FishingTerminalProbabilityEvaluator
{
    public static FishingTerminalProbabilityResult Evaluate(
        FishingTerminalProbabilityRequest request)
    {
        var requestBlockers = ValidateRequest(request);
        if (requestBlockers.Count > 0)
        {
            return Blocked(Array.Empty<FishingTerminalProbabilityCandidate>(), requestBlockers);
        }

        var targetRules = request.Rules
            .Where(rule => rule.ProducesTarget)
            .OrderBy(rule => rule.Precedence)
            .ThenBy(rule => rule.RuleKey, StringComparer.Ordinal)
            .ToArray();
        var candidates = targetRules
            .Where(rule => !rule.EligibilityResolved || rule.EligibleBeforeRandomRolls)
            .Select(rule => EvaluateCandidate(rule, request.Rules))
            .ToArray();
        var resolved = candidates
            .Where(candidate => candidate.Resolved)
            .OrderByDescending(candidate => candidate.ProbabilityLowerBound)
            .ThenBy(candidate => candidate.RuleKey, StringComparer.Ordinal)
            .FirstOrDefault();
        if (resolved is not null)
        {
            return new FishingTerminalProbabilityResult
            {
                Status = "resolved_conservative_first_pass_lower_bound",
                Resolved = true,
                SingleAttemptProbabilityLowerBound = resolved.ProbabilityLowerBound,
                SelectedRuleKey = resolved.RuleKey,
                IndependentRetryLowerBoundProven =
                    resolved.IndependentRetryLowerBoundProven,
                RetryBlockingReasons = resolved.RetryBlockingReasons,
                Candidates = candidates
            };
        }

        var blockers = candidates
            .SelectMany(candidate => candidate.BlockingReasons)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToList();
        if (blockers.Count > 0)
        {
            return Blocked(candidates, blockers);
        }

        return new FishingTerminalProbabilityResult
        {
            Status = "resolved_no_eligible_target_rule",
            Resolved = true,
            SingleAttemptProbabilityLowerBound = 0d,
            Candidates = candidates
        };
    }

    private static FishingTerminalProbabilityCandidate EvaluateCandidate(
        FishingTerminalProbabilityRule target,
        IReadOnlyList<FishingTerminalProbabilityRule> rules)
    {
        var blockers = new List<string>();
        if (!target.EligibilityResolved)
        {
            blockers.Add("target_rule_eligibility_unresolved:" + target.RuleKey);
        }
        if (!target.OutputResolutionComplete)
        {
            blockers.Add("target_rule_output_unresolved:" + target.RuleKey);
        }

        var targetSpawnProbability = SpawnProbability(target, false, blockers);
        var selectionProbability = RequiredProbability(
            target.TargetOutputSelectionProbability,
            "target_output_selection_probability_unresolved:" + target.RuleKey,
            blockers);
        var acceptanceProbability = RequiredProbability(
            target.TargetGenericAcceptanceProbability,
            "target_generic_acceptance_probability_unresolved:" + target.RuleKey,
            blockers);

        var competitors = rules
            .Where(rule => !ReferenceEquals(rule, target) &&
                           rule.Precedence <= target.Precedence)
            .OrderBy(rule => rule.Precedence)
            .ThenBy(rule => rule.RuleKey, StringComparer.Ordinal)
            .ToArray();
        var retryBlockers = RetryBlockingReasons(target, competitors);
        var noCompetitorReturnLowerBound = 1d;
        foreach (var competitor in competitors)
        {
            var returnUpperBound = CompetitorReturnProbabilityUpperBound(competitor);
            noCompetitorReturnLowerBound *= 1d - returnUpperBound;
        }

        if (blockers.Count > 0)
        {
            return new FishingTerminalProbabilityCandidate
            {
                RuleKey = target.RuleKey,
                Resolved = false,
                CompetitorRuleKeys = competitors.Select(rule => rule.RuleKey).ToArray(),
                IndependentRetryLowerBoundProven = retryBlockers.Length == 0,
                BlockingReasons = blockers
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(reason => reason, StringComparer.Ordinal)
                    .ToArray(),
                RetryBlockingReasons = retryBlockers
            };
        }

        var targetReturnLowerBound = targetSpawnProbability!.Value *
                                     selectionProbability!.Value *
                                     acceptanceProbability!.Value;
        return new FishingTerminalProbabilityCandidate
        {
            RuleKey = target.RuleKey,
            Resolved = true,
            ProbabilityLowerBound = ClampProbability(
                targetReturnLowerBound * noCompetitorReturnLowerBound),
            TargetReturnProbabilityLowerBound = ClampProbability(targetReturnLowerBound),
            PriorCompetitorNonReturnProbabilityLowerBound =
                ClampProbability(noCompetitorReturnLowerBound),
            IndependentRetryLowerBoundProven = retryBlockers.Length == 0,
            RetryBlockingReasons = retryBlockers,
            CompetitorRuleKeys = competitors.Select(rule => rule.RuleKey).ToArray()
        };
    }

    private static string[] RetryBlockingReasons(
        FishingTerminalProbabilityRule target,
        IReadOnlyList<FishingTerminalProbabilityRule> competitors)
    {
        var blockers = new List<string>();
        AddRuleBlockers(target, "target");
        foreach (var competitor in competitors)
        {
            AddRuleBlockers(competitor, "competitor");
        }
        return blockers.Distinct(StringComparer.Ordinal)
            .OrderBy(reason => reason, StringComparer.Ordinal)
            .ToArray();

        void AddRuleBlockers(
            FishingTerminalProbabilityRule rule,
            string role)
        {
            if (!rule.EligibilityResolved)
            {
                blockers.Add($"{role}_retry_eligibility_unresolved:{rule.RuleKey}");
            }
            if (!rule.RetryContextStable)
            {
                blockers.Add($"{role}_retry_context_not_stable:{rule.RuleKey}");
            }
            if (rule.SpawnRollKind != FishingSpawnRollKind.IndependentRandom)
            {
                blockers.Add($"{role}_spawn_roll_not_independent:{rule.RuleKey}");
            }
        }
    }

    private static double? SpawnProbability(
        FishingTerminalProbabilityRule rule,
        bool competitor,
        ICollection<string> blockers)
    {
        if (rule.SpawnRollKind == FishingSpawnRollKind.DeterministicFishCaughtSeed)
        {
            if (rule.SeededSpawnRollPassed.HasValue)
            {
                return rule.SeededSpawnRollPassed.Value ? 1d : 0d;
            }
            if (!competitor)
            {
                blockers.Add("target_seeded_spawn_roll_unresolved:" + rule.RuleKey);
            }
            return null;
        }

        if (rule.SpawnChance.HasValue)
        {
            return ClampProbability(rule.SpawnChance.Value);
        }
        if (!competitor)
        {
            blockers.Add("target_spawn_chance_unresolved:" + rule.RuleKey);
        }
        return null;
    }

    private static double CompetitorReturnProbabilityUpperBound(
        FishingTerminalProbabilityRule competitor)
    {
        if (!competitor.EligibilityResolved)
        {
            return 1d;
        }
        if (!competitor.EligibleBeforeRandomRolls)
        {
            return 0d;
        }

        var spawnProbability = SpawnProbability(
            competitor,
            true,
            new List<string>());
        return spawnProbability.HasValue
            ? ClampProbability(spawnProbability.Value)
            : 1d;
    }

    private static double? RequiredProbability(
        double? value,
        string blocker,
        ICollection<string> blockers)
    {
        if (!value.HasValue || double.IsNaN(value.Value) ||
            double.IsInfinity(value.Value) || value.Value < 0d || value.Value > 1d)
        {
            blockers.Add(blocker);
            return null;
        }
        return value.Value;
    }

    private static List<string> ValidateRequest(
        FishingTerminalProbabilityRequest request)
    {
        var blockers = new List<string>();
        if (string.IsNullOrWhiteSpace(request.TargetQualifiedItemId))
        {
            blockers.Add("target_qualified_item_id_missing");
        }
        if (request.FishingPassIndex != 0)
        {
            blockers.Add("only_first_fishing_pass_supported");
        }
        if (!request.RuleInventoryComplete)
        {
            blockers.Add("combined_rule_inventory_incomplete");
        }
        if (request.Rules.Any(rule => string.IsNullOrWhiteSpace(rule.RuleKey)) ||
            request.Rules.GroupBy(rule => rule.RuleKey, StringComparer.Ordinal)
                .Any(group => group.Count() != 1))
        {
            blockers.Add("rule_identity_missing_or_duplicate");
        }
        return blockers;
    }

    private static FishingTerminalProbabilityResult Blocked(
        FishingTerminalProbabilityCandidate[] candidates,
        IEnumerable<string> blockers) => new()
        {
            Status = "blocked_probability_evidence",
            Resolved = false,
            Candidates = candidates,
            BlockingReasons = blockers
                .Distinct(StringComparer.Ordinal)
                .OrderBy(reason => reason, StringComparer.Ordinal)
                .ToArray()
        };

    private static double ClampProbability(double value) =>
        value <= 0d ? 0d : value >= 1d ? 1d : value;
}
