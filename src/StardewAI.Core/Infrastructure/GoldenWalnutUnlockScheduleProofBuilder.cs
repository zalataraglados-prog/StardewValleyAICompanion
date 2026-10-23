using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutRetryBudget(
    [property: JsonPropertyName("source_key")] string SourceKey,
    [property: JsonPropertyName("required_success_count")] int RequiredSuccessCount,
    [property: JsonPropertyName("single_attempt_success_probability")] double SingleAttemptSuccessProbability,
    [property: JsonPropertyName("target_family_success_probability")] double TargetFamilySuccessProbability,
    [property: JsonPropertyName("required_independent_attempt_count")] int RequiredIndependentAttemptCount);

public sealed record GoldenWalnutUnlockMilestoneProof(
    [property: JsonPropertyName("milestone_id")] string MilestoneId,
    [property: JsonPropertyName("unlock_mail_key")] string UnlockMailKey,
    [property: JsonPropertyName("walnut_cost")] int WalnutCost,
    [property: JsonPropertyName("cumulative_spend_before")] int CumulativeSpendBefore,
    [property: JsonPropertyName("cumulative_spend_after")] int CumulativeSpendAfter,
    [property: JsonPropertyName("newly_available_access_phases")] string[] NewlyAvailableAccessPhases,
    [property: JsonPropertyName("cumulative_source_keys")] string[] CumulativeSourceKeys,
    [property: JsonPropertyName("cumulative_capacity")] int CumulativeCapacity,
    [property: JsonPropertyName("deterministic_capacity")] int DeterministicCapacity,
    [property: JsonPropertyName("stochastic_capacity")] int StochasticCapacity,
    [property: JsonPropertyName("capacity_wallet_before_payment")] int CapacityWalletBeforePayment,
    [property: JsonPropertyName("deterministic_wallet_before_payment")] int DeterministicWalletBeforePayment,
    [property: JsonPropertyName("capacity_sufficient")] bool CapacitySufficient,
    [property: JsonPropertyName("deterministic_capacity_sufficient")] bool DeterministicCapacitySufficient);

public sealed record GoldenWalnutUnlockScheduleProof(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("bound_source_capacity")] int BoundSourceCapacity,
    [property: JsonPropertyName("required_cumulative_spend")] int RequiredCumulativeSpend,
    [property: JsonPropertyName("target_combined_retry_success_probability")] double TargetCombinedRetrySuccessProbability,
    [property: JsonPropertyName("proven_combined_retry_success_probability_lower_bound")] double ProvenCombinedRetrySuccessProbabilityLowerBound,
    [property: JsonPropertyName("phase_capacity_complete")] bool PhaseCapacityComplete,
    [property: JsonPropertyName("independent_retry_budget_complete")] bool IndependentRetryBudgetComplete,
    [property: JsonPropertyName("year_three_deadline_composition_complete")] bool YearThreeDeadlineCompositionComplete,
    [property: JsonPropertyName("training_admission_eligible")] bool TrainingAdmissionEligible,
    [property: JsonPropertyName("milestones")] GoldenWalnutUnlockMilestoneProof[] Milestones,
    [property: JsonPropertyName("retry_budgets")] GoldenWalnutRetryBudget[] RetryBudgets,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);

public static class GoldenWalnutUnlockScheduleProofBuilder
{
    public const string SchemaVersion = "golden_walnut_unlock_schedule_proof.v1";
    public const double TargetCombinedRetrySuccessProbability = 0.95d;
    public const int RequiredCumulativeSpend = 51;

    public static readonly IReadOnlyCollection<string> BoundAcquisitionOptionIds =
        Array.AsReadOnly(new[]
        {
            "foraging.harvest_bushes",
            "fishing.catch_fish",
            "executor.clear_obstacle",
            "farm.maintain_crops",
            "executor.till_soil"
        });

    private static readonly IReadOnlyDictionary<string, double> RetryProbabilities =
        new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["IslandFishing"] = 0.15d,
            ["MusselStone"] = 0.10d,
            ["IslandFarming"] = 0.05d
        };

    private static readonly MilestoneDefinition[] MilestoneDefinitions =
    {
        new(
            "first_parrot",
            "Island_FirstParrot",
            1,
            new[] { "boat_repaired", "island_south_initial" }),
        new(
            "western_turtle",
            "Island_Turtle",
            10,
            new[]
            {
                "north_after_first_parrot",
                "east_after_first_parrot",
                "volcano_after_first_parrot",
                "caldera_after_volcano"
            }),
        new(
            "island_house",
            "Island_UpgradeHouse",
            20,
            new[] { "west_after_turtle" }),
        new(
            "island_resort",
            "Island_Resort",
            20,
            new[] { "house_after_upgrade" })
    };

    public static GoldenWalnutUnlockScheduleProof Build() =>
        Build(GoldenWalnutNativeSourceCatalog.Sources);

    public static GoldenWalnutUnlockScheduleProof Build(
        IReadOnlyCollection<GoldenWalnutNativeSourceDefinition> sources)
    {
        if (sources is null)
            throw new ArgumentNullException(nameof(sources));
        Require(
            sources.Count == GoldenWalnutNativeSourceCatalog.NativeSourceKeyCount &&
            sources.Sum(source => source.NativeQuantity) == GoldenWalnutNativeSourceCatalog.NativeWalnutTotal &&
            sources.Select(source => source.SourceKey).Distinct(StringComparer.Ordinal).Count() == sources.Count,
            "Golden Walnut native denominator drifted before schedule proof.");

        var boundSources = sources
            .Where(source =>
                !source.ResortOnly &&
                source.OptionalUnlockWalnutCost == 0 &&
                BoundAcquisitionOptionIds.Contains(source.AcquisitionOptionId))
            .OrderBy(source => source.SourceKey, StringComparer.Ordinal)
            .ToArray();
        Require(
            boundSources.Sum(source => source.NativeQuantity) == RequiredCumulativeSpend,
            "Bound pre-resort Golden Walnut capacity must equal the 51-walnut unlock spend.");
        Require(
            RetryProbabilities.Keys.All(key => boundSources.Any(source => source.SourceKey == key)) &&
            boundSources
                .Where(source => source.ProbabilityStatus.StartsWith("bounded_native_rng_", StringComparison.Ordinal))
                .All(source => RetryProbabilities.ContainsKey(source.SourceKey)),
            "Golden Walnut stochastic source inventory drifted.");

        var knownPhases = MilestoneDefinitions
            .SelectMany(definition => definition.NewlyAvailableAccessPhases)
            .ToHashSet(StringComparer.Ordinal);
        Require(
            boundSources.All(source => knownPhases.Contains(source.AccessPhase)),
            "Bound Golden Walnut source has no unlock-stage mapping.");

        var cumulativePhases = new HashSet<string>(StringComparer.Ordinal);
        var milestones = new List<GoldenWalnutUnlockMilestoneProof>();
        var cumulativeSpend = 0;
        foreach (var definition in MilestoneDefinitions)
        {
            cumulativePhases.UnionWith(definition.NewlyAvailableAccessPhases);
            var available = boundSources
                .Where(source => cumulativePhases.Contains(source.AccessPhase))
                .ToArray();
            var deterministicCapacity = available
                .Where(source => !RetryProbabilities.ContainsKey(source.SourceKey))
                .Sum(source => source.NativeQuantity);
            var stochasticCapacity = available
                .Where(source => RetryProbabilities.ContainsKey(source.SourceKey))
                .Sum(source => source.NativeQuantity);
            var cumulativeCapacity = deterministicCapacity + stochasticCapacity;
            milestones.Add(new GoldenWalnutUnlockMilestoneProof(
                definition.MilestoneId,
                definition.UnlockMailKey,
                definition.WalnutCost,
                cumulativeSpend,
                cumulativeSpend + definition.WalnutCost,
                definition.NewlyAvailableAccessPhases,
                available.Select(source => source.SourceKey).ToArray(),
                cumulativeCapacity,
                deterministicCapacity,
                stochasticCapacity,
                cumulativeCapacity - cumulativeSpend,
                deterministicCapacity - cumulativeSpend,
                cumulativeCapacity - cumulativeSpend >= definition.WalnutCost,
                deterministicCapacity - cumulativeSpend >= definition.WalnutCost));
            cumulativeSpend += definition.WalnutCost;
        }

        Require(cumulativeSpend == RequiredCumulativeSpend,
            "Golden Walnut milestone spend denominator drifted.");
        var phaseCapacityComplete = milestones.All(milestone => milestone.CapacitySufficient);

        var perFamilyTarget = Math.Pow(
            TargetCombinedRetrySuccessProbability,
            1d / RetryProbabilities.Count);
        var retryBudgets = boundSources
            .Where(source => RetryProbabilities.ContainsKey(source.SourceKey))
            .Select(source =>
            {
                var attempts = StochasticRetryPolicy.RequiredIndependentAttemptCount(
                    source.NativeQuantity,
                    RetryProbabilities[source.SourceKey],
                    perFamilyTarget);
                if (!attempts.HasValue)
                {
                    throw new InvalidOperationException(
                        "Golden Walnut stochastic source has no finite independent retry budget: " + source.SourceKey);
                }
                return new GoldenWalnutRetryBudget(
                    source.SourceKey,
                    source.NativeQuantity,
                    RetryProbabilities[source.SourceKey],
                    perFamilyTarget,
                    attempts.Value);
            })
            .OrderBy(budget => budget.SourceKey, StringComparer.Ordinal)
            .ToArray();
        var provenCombinedProbability = retryBudgets
            .Aggregate(1d, (value, budget) => value * budget.TargetFamilySuccessProbability);
        var retryBudgetComplete =
            retryBudgets.Length == RetryProbabilities.Count &&
            provenCombinedProbability + 1e-12 >= TargetCombinedRetrySuccessProbability;

        const bool deadlineCompositionComplete = false;
        return new GoldenWalnutUnlockScheduleProof(
            SchemaVersion,
            "phase_capacity_and_independent_retry_budget_complete_deadline_composition_pending",
            boundSources.Sum(source => source.NativeQuantity),
            RequiredCumulativeSpend,
            TargetCombinedRetrySuccessProbability,
            provenCombinedProbability,
            phaseCapacityComplete,
            retryBudgetComplete,
            deadlineCompositionComplete,
            phaseCapacityComplete && retryBudgetComplete && deadlineCompositionComplete,
            milestones.ToArray(),
            retryBudgets,
            new[]
            {
                "stochastic_attempt_day_and_game_minute_capacity_not_yet_composed_with_year_three_deadline",
                "island_farming_attempts_require_crop_growth_seed_supply_and_harvest_capacity_schedule",
                "mussel_stone_attempts_require_daily_node_regeneration_capacity_schedule"
            });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed record MilestoneDefinition(
        string MilestoneId,
        string UnlockMailKey,
        int WalnutCost,
        string[] NewlyAvailableAccessPhases);
}
