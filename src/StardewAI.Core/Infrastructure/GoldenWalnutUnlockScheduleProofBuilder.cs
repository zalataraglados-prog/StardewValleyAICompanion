using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;

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

public sealed record GoldenWalnutDeadlineCapacityRequirement(
    [property: JsonPropertyName("source_key")] string SourceKey,
    [property: JsonPropertyName("attempt_unit")] string AttemptUnit,
    [property: JsonPropertyName("required_attempt_count")] int RequiredAttemptCount,
    [property: JsonPropertyName("conservative_action_minutes")] int? ConservativeActionMinutes,
    [property: JsonPropertyName("attempt_action_budget_bound")] bool AttemptActionBudgetBound,
    [property: JsonPropertyName("source_availability_capacity_bound")] bool SourceAvailabilityCapacityBound,
    [property: JsonPropertyName("earliest_access_day_bound")] bool EarliestAccessDayBound,
    [property: JsonPropertyName("deadline_composition_complete")] bool DeadlineCompositionComplete,
    [property: JsonPropertyName("bound_evidence_ids")] string[] BoundEvidenceIds,
    [property: JsonPropertyName("missing_evidence_ids")] string[] MissingEvidenceIds);

public sealed record GoldenWalnutUnlockScheduleProof(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("bound_source_capacity")] int BoundSourceCapacity,
    [property: JsonPropertyName("required_cumulative_spend")] int RequiredCumulativeSpend,
    [property: JsonPropertyName("target_combined_retry_success_probability")] double TargetCombinedRetrySuccessProbability,
    [property: JsonPropertyName("proven_combined_retry_success_probability_lower_bound")] double ProvenCombinedRetrySuccessProbabilityLowerBound,
    [property: JsonPropertyName("deadline_total_day_exclusive")] int DeadlineTotalDayExclusive,
    [property: JsonPropertyName("known_conservative_action_minutes_lower_bound")] int KnownConservativeActionMinutesLowerBound,
    [property: JsonPropertyName("phase_capacity_complete")] bool PhaseCapacityComplete,
    [property: JsonPropertyName("independent_retry_budget_complete")] bool IndependentRetryBudgetComplete,
    [property: JsonPropertyName("deadline_capacity_requirements_complete")] bool DeadlineCapacityRequirementsComplete,
    [property: JsonPropertyName("year_three_deadline_composition_complete")] bool YearThreeDeadlineCompositionComplete,
    [property: JsonPropertyName("training_admission_eligible")] bool TrainingAdmissionEligible,
    [property: JsonPropertyName("milestones")] GoldenWalnutUnlockMilestoneProof[] Milestones,
    [property: JsonPropertyName("retry_budgets")] GoldenWalnutRetryBudget[] RetryBudgets,
    [property: JsonPropertyName("deadline_capacity_requirements")] GoldenWalnutDeadlineCapacityRequirement[] DeadlineCapacityRequirements,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);

public static class GoldenWalnutUnlockScheduleProofBuilder
{
    public const string SchemaVersion = "golden_walnut_unlock_schedule_proof.v2";
    public const double TargetCombinedRetrySuccessProbability = 0.95d;
    public const int RequiredCumulativeSpend = 51;
    public const int DeadlineTotalDayExclusive = 224;

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

        var retryBySource = retryBudgets.ToDictionary(
            budget => budget.SourceKey,
            StringComparer.Ordinal);
        var deadlineCapacityRequirements = new[]
        {
            new GoldenWalnutDeadlineCapacityRequirement(
                "IslandFarming",
                "successful_island_crop_harvest",
                retryBySource["IslandFarming"].RequiredIndependentAttemptCount,
                CropHarvestBudgetPolicy.ConservativeGameMinutesForHarvests(
                    retryBySource["IslandFarming"].RequiredIndependentAttemptCount),
                true,
                false,
                false,
                false,
                new[]
                {
                    "HoeDirt.performUseAction_or_performToolAction:island_location_rng_0.05",
                    "CropHarvestBudgetPolicy"
                },
                new[]
                {
                    "island_turtle_unlock_total_day",
                    "island_crop_seed_supply_schedule",
                    "island_crop_growth_and_service_schedule",
                    "island_crop_harvest_slot_capacity_before_day_224"
                }),
            new GoldenWalnutDeadlineCapacityRequirement(
                "IslandFishing",
                "island_fishing_getFish_attempt",
                retryBySource["IslandFishing"].RequiredIndependentAttemptCount,
                FishingAttemptBudgetPolicy.ConservativeGameMinutesForAttempts(
                    retryBySource["IslandFishing"].RequiredIndependentAttemptCount,
                    challengeBait: false),
                true,
                true,
                false,
                false,
                new[]
                {
                    "IslandLocation.getFish:seeded_rng_0.15",
                    "FishingAttemptBudgetPolicy"
                },
                new[]
                {
                    "island_south_unlock_total_day",
                    "island_fishing_daily_minute_allocation_before_day_224"
                }),
            new GoldenWalnutDeadlineCapacityRequirement(
                "MusselStone",
                "native_mussel_node_break",
                retryBySource["MusselStone"].RequiredIndependentAttemptCount,
                null,
                false,
                false,
                false,
                false,
                new[]
                {
                    "GameLocation.breakStone:item_25_rng_0.10",
                    "IslandWest.spawnObjects:mussel_node_single_spawn_attempt_below_10"
                },
                new[]
                {
                    "island_turtle_unlock_total_day",
                    "mussel_node_spawn_valid_tile_probability",
                    "mussel_node_available_break_count_before_day_224",
                    "mussel_node_route_tool_and_action_minutes"
                })
        };
        var deadlineCapacityRequirementsComplete =
            deadlineCapacityRequirements.All(requirement =>
                requirement.AttemptActionBudgetBound &&
                requirement.SourceAvailabilityCapacityBound &&
                requirement.EarliestAccessDayBound &&
                requirement.DeadlineCompositionComplete);
        var knownConservativeActionMinutes = deadlineCapacityRequirements
            .Where(requirement => requirement.ConservativeActionMinutes.HasValue)
            .Sum(requirement => requirement.ConservativeActionMinutes!.Value);

        const bool deadlineCompositionComplete = false;
        return new GoldenWalnutUnlockScheduleProof(
            SchemaVersion,
            "phase_capacity_retry_and_partial_deadline_capacity_complete_deadline_composition_pending",
            boundSources.Sum(source => source.NativeQuantity),
            RequiredCumulativeSpend,
            TargetCombinedRetrySuccessProbability,
            provenCombinedProbability,
            DeadlineTotalDayExclusive,
            knownConservativeActionMinutes,
            phaseCapacityComplete,
            retryBudgetComplete,
            deadlineCapacityRequirementsComplete,
            deadlineCompositionComplete,
            phaseCapacityComplete && retryBudgetComplete &&
            deadlineCapacityRequirementsComplete && deadlineCompositionComplete,
            milestones.ToArray(),
            retryBudgets,
            deadlineCapacityRequirements,
            new[]
            {
                "island_unlock_total_days_not_yet_composed_with_year_three_deadline",
                "island_farming_seed_growth_service_and_harvest_slot_capacity_schedule_missing",
                "mussel_stone_spawn_availability_and_action_minute_capacity_schedule_missing"
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
