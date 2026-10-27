using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutResortFundingRouteAlternative(
    [property: JsonPropertyName("route_id")] string RouteId,
    [property: JsonPropertyName("source_keys")] string[] SourceKeys,
    [property: JsonPropertyName("acquisition_option_ids")] string[] AcquisitionOptionIds,
    [property: JsonPropertyName("native_walnut_quantity")] int NativeWalnutQuantity,
    [property: JsonPropertyName("deterministic")] bool Deterministic,
    [property: JsonPropertyName("action_bindings_complete")] bool ActionBindingsComplete,
    [property: JsonPropertyName("supply_schedule_complete")] bool SupplyScheduleComplete,
    [property: JsonPropertyName("deadline_composition_complete")] bool DeadlineCompositionComplete,
    [property: JsonPropertyName("training_admission_eligible")] bool TrainingAdmissionEligible,
    [property: JsonPropertyName("bound_evidence_ids")] string[] BoundEvidenceIds,
    [property: JsonPropertyName("missing_evidence_ids")] string[] MissingEvidenceIds);

public sealed record GoldenWalnutResortFundingRouteProof(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("required_walnut_quantity")] int RequiredWalnutQuantity,
    [property: JsonPropertyName("preferred_route_id")] string PreferredRouteId,
    [property: JsonPropertyName("preferred_route_reason")] string PreferredRouteReason,
    [property: JsonPropertyName("route_selection_complete")] bool RouteSelectionComplete,
    [property: JsonPropertyName("training_admission_eligible")] bool TrainingAdmissionEligible,
    [property: JsonPropertyName("alternatives")] GoldenWalnutResortFundingRouteAlternative[] Alternatives);

public static class GoldenWalnutResortFundingRouteProofBuilder
{
    public const string SchemaVersion = "golden_walnut_resort_funding_routes.v1";
    public const string PreferredRouteId = "deterministic_gourmand_requests";
    public const int RequiredWalnutQuantity = 15;

    private static readonly string[] StochasticSourceKeys =
    {
        "IslandFarming",
        "IslandFishing",
        "MusselStone"
    };

    private static readonly string[] GourmandSourceKeys =
    {
        "IslandGourmand1",
        "IslandGourmand2",
        "IslandGourmand3"
    };

    public static GoldenWalnutResortFundingRouteProof Build() =>
        Build(GoldenWalnutNativeSourceCatalog.Sources, null, null);

    public static GoldenWalnutResortFundingRouteProof Build(
        int westernTurtleUnlockTotalDay,
        int islandHouseUnlockTotalDay) =>
        Build(
            GoldenWalnutNativeSourceCatalog.Sources,
            westernTurtleUnlockTotalDay,
            islandHouseUnlockTotalDay);

    public static GoldenWalnutResortFundingRouteProof Build(
        GoldenWalnutFreshSaveUnlockTimelineProof timeline)
    {
        if (timeline is null)
            throw new ArgumentNullException(nameof(timeline));
        Require(
            timeline.SchemaVersion == GoldenWalnutFreshSaveUnlockTimelineProofBuilder.SchemaVersion &&
            timeline.SourceScheduleComplete &&
            timeline.NativePhaseOrderingComplete &&
            !timeline.TrainingAdmissionEligible &&
            timeline.SourceCompletions is { Length: > 0 } &&
            timeline.SourceScheduleSha256 is { Length: 64 } &&
            timeline.SourceScheduleSha256.All(character =>
                character is >= '0' and <= '9' or >= 'a' and <= 'f'),
            "Golden Walnut fresh-save unlock timeline is incomplete.");
        var westernTurtleUnlockTotalDay = timeline.WesternTurtleUnlockTotalDay ??
            throw new InvalidOperationException(
                "Golden Walnut fresh-save unlock timeline has no Western Turtle date.");
        var islandHouseUnlockTotalDay = timeline.IslandHouseUnlockTotalDay ??
            throw new InvalidOperationException(
                "Golden Walnut fresh-save unlock timeline has no Island House date.");
        return Build(
            GoldenWalnutNativeSourceCatalog.Sources,
            westernTurtleUnlockTotalDay,
            islandHouseUnlockTotalDay,
            timeline);
    }

    public static GoldenWalnutResortFundingRouteProof Build(
        IReadOnlyCollection<GoldenWalnutNativeSourceDefinition> sources,
        int? westernTurtleUnlockTotalDay = null,
        int? islandHouseUnlockTotalDay = null) =>
        Build(sources, westernTurtleUnlockTotalDay, islandHouseUnlockTotalDay, null);

    private static GoldenWalnutResortFundingRouteProof Build(
        IReadOnlyCollection<GoldenWalnutNativeSourceDefinition> sources,
        int? westernTurtleUnlockTotalDay,
        int? islandHouseUnlockTotalDay,
        GoldenWalnutFreshSaveUnlockTimelineProof? timeline)
    {
        if (sources is null)
            throw new ArgumentNullException(nameof(sources));

        var stochastic = ExactSources(sources, StochasticSourceKeys);
        Require(
            stochastic.Sum(source => source.NativeQuantity) == RequiredWalnutQuantity &&
            stochastic.All(source => source.ProbabilityStatus.StartsWith(
                "bounded_native_rng_",
                StringComparison.Ordinal)),
            "Golden Walnut stochastic Resort funding route drifted.");

        var gourmand = ExactSources(sources, GourmandSourceKeys);
        Require(
            gourmand.Sum(source => source.NativeQuantity) == RequiredWalnutQuantity &&
            gourmand.All(source =>
                source.AccessPhase == "house_after_upgrade" &&
                source.RequiredMailKey == "Island_UpgradeHouse" &&
                source.AcquisitionOptionId == "island.complete_gourmand_request" &&
                source.ProbabilityStatus == "exact_native_one_time"),
            "Golden Walnut Gourmand Resort funding route drifted.");

        var stochasticActionBindingsComplete = stochastic.All(source =>
            GoldenWalnutUnlockScheduleProofBuilder.BoundAcquisitionOptionIds.Contains(
                source.AcquisitionOptionId));
        const bool gourmandActionBindingsComplete = true;
        const bool stochasticSupplyScheduleComplete = false;
        var gourmandSupplyProof = IslandGourmandSupplyScheduleProofBuilder.Build(
            westernTurtleUnlockTotalDay,
            islandHouseUnlockTotalDay);
        var gourmandSupplyScheduleComplete = gourmandSupplyProof.SupplyScheduleComplete;
        var gourmandDeadlineCompositionComplete =
            gourmandSupplyProof.DeadlineCompositionComplete;

        var alternatives = new[]
        {
            new GoldenWalnutResortFundingRouteAlternative(
                "existing_stochastic_three_family_route",
                stochastic.Select(source => source.SourceKey).ToArray(),
                stochastic.Select(source => source.AcquisitionOptionId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                stochastic.Sum(source => source.NativeQuantity),
                false,
                stochasticActionBindingsComplete,
                stochasticSupplyScheduleComplete,
                false,
                false,
                new[] { GoldenWalnutUnlockScheduleProofBuilder.SchemaVersion },
                new[]
                {
                    "mussel_node_spawn_and_action_capacity_schedule",
                    "island_crop_214_attempt_seed_growth_service_schedule",
                    "three_family_attempt_minutes_and_unlock_days_before_day_224"
                }),
            new GoldenWalnutResortFundingRouteAlternative(
                PreferredRouteId,
                gourmand.Select(source => source.SourceKey).ToArray(),
                new[] { "island.complete_gourmand_request" },
                gourmand.Sum(source => source.NativeQuantity),
                true,
                gourmandActionBindingsComplete,
                gourmandSupplyScheduleComplete,
                gourmandDeadlineCompositionComplete,
                false,
                new[]
                {
                    "island_gourmand_progress.v1",
                    "island.complete_gourmand_request",
                    "executor.advance_gourmand_request",
                    IslandGourmandSupplyScheduleProofBuilder.SchemaVersion,
                    "EVD-341"
                }.Concat(timeline is null
                    ? Array.Empty<string>()
                    : new[]
                    {
                        GoldenWalnutFreshSaveUnlockTimelineProofBuilder.SchemaVersion,
                        "sha256:" + timeline.SourceScheduleSha256
                    }).ToArray(),
                (gourmandDeadlineCompositionComplete
                    ? Array.Empty<string>()
                    : new[] { "gourmand_route_and_unlock_days_before_day_224" })
                    .Append("second_and_third_gourmand_native_continuation_receipts")
                    .ToArray())
        };
        var preferred = alternatives.Single(route => route.RouteId == PreferredRouteId);
        var routeSelectionComplete = preferred.ActionBindingsComplete &&
            preferred.SupplyScheduleComplete &&
            preferred.DeadlineCompositionComplete;

        return new GoldenWalnutResortFundingRouteProof(
            SchemaVersion,
            routeSelectionComplete
                ? "deterministic_gourmand_route_selected_training_runtime_evidence_pending"
                : "deterministic_gourmand_runtime_and_supply_bound_deadline_pending",
            RequiredWalnutQuantity,
            PreferredRouteId,
            "same_exact_15_walnut_capacity_without_random_retry_or_daily_mussel_spawn_dependency",
            routeSelectionComplete,
            routeSelectionComplete && preferred.TrainingAdmissionEligible,
            alternatives);
    }

    private static GoldenWalnutNativeSourceDefinition[] ExactSources(
        IEnumerable<GoldenWalnutNativeSourceDefinition> sources,
        IReadOnlyCollection<string> requiredKeys)
    {
        var selected = sources
            .Where(source => requiredKeys.Contains(source.SourceKey))
            .OrderBy(source => source.SourceKey, StringComparer.Ordinal)
            .ToArray();
        Require(
            selected.Length == requiredKeys.Count &&
            selected.Select(source => source.SourceKey)
                .SequenceEqual(requiredKeys.OrderBy(value => value, StringComparer.Ordinal), StringComparer.Ordinal),
            "Golden Walnut Resort funding source denominator drifted.");
        return selected;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
