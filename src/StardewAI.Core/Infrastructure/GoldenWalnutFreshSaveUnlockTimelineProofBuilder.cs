using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using StardewAI.Contracts.State;

namespace StardewAI.Core.Infrastructure;

public sealed record GoldenWalnutFreshSaveSourceCompletion(
    [property: JsonPropertyName("source_key")] string SourceKey,
    [property: JsonPropertyName("planned_quantity")] int PlannedQuantity,
    [property: JsonPropertyName("planned_completion_total_day")] int PlannedCompletionTotalDay);

public sealed record GoldenWalnutFreshSaveSourceSchedule(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("schedule_id")] string ScheduleId,
    [property: JsonPropertyName("goal_id")] string GoalId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("training_label_eligible")] bool TrainingLabelEligible,
    [property: JsonPropertyName("first_island_arrival_total_day")] int FirstIslandArrivalTotalDay,
    [property: JsonPropertyName("planned_first_parrot_unlock_total_day")] int PlannedFirstParrotUnlockTotalDay,
    [property: JsonPropertyName("planned_western_turtle_unlock_total_day")] int PlannedWesternTurtleUnlockTotalDay,
    [property: JsonPropertyName("planned_island_house_unlock_total_day")] int PlannedIslandHouseUnlockTotalDay,
    [property: JsonPropertyName("source_completions")] GoldenWalnutFreshSaveSourceCompletion[] SourceCompletions);

public sealed record GoldenWalnutFreshSaveUnlockTimelineProof(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("source_schedule_id")] string SourceScheduleId,
    [property: JsonPropertyName("source_schedule_sha256")] string SourceScheduleSha256,
    [property: JsonPropertyName("first_island_arrival_total_day")] int FirstIslandArrivalTotalDay,
    [property: JsonPropertyName("first_parrot_walnut_ready_total_day")] int? FirstParrotWalnutReadyTotalDay,
    [property: JsonPropertyName("western_turtle_walnut_ready_total_day")] int? WesternTurtleWalnutReadyTotalDay,
    [property: JsonPropertyName("island_house_walnut_ready_total_day")] int? IslandHouseWalnutReadyTotalDay,
    [property: JsonPropertyName("first_parrot_unlock_total_day")] int? FirstParrotUnlockTotalDay,
    [property: JsonPropertyName("western_turtle_unlock_total_day")] int? WesternTurtleUnlockTotalDay,
    [property: JsonPropertyName("island_house_unlock_total_day")] int? IslandHouseUnlockTotalDay,
    [property: JsonPropertyName("planned_deterministic_walnut_quantity")] int PlannedDeterministicWalnutQuantity,
    [property: JsonPropertyName("source_schedule_complete")] bool SourceScheduleComplete,
    [property: JsonPropertyName("native_phase_ordering_complete")] bool NativePhaseOrderingComplete,
    [property: JsonPropertyName("gourmand_deadline_composition_complete")] bool GourmandDeadlineCompositionComplete,
    [property: JsonPropertyName("training_admission_eligible")] bool TrainingAdmissionEligible,
    [property: JsonPropertyName("source_completions")] GoldenWalnutFreshSaveSourceCompletion[] SourceCompletions,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);

public static class GoldenWalnutFreshSaveUnlockTimelineProofBuilder
{
    public const string SourceSchemaVersion = "golden_walnut_fresh_save_source_schedule.v1";
    public const string SchemaVersion = "golden_walnut_fresh_save_unlock_timeline_proof.v1";
    public const string GoalId = "grandpa.maximum_21";
    public const string GameVersion = "1.6.15";

    private static readonly HashSet<string> InitialAccessPhases = new(StringComparer.Ordinal)
    {
        "boat_repaired",
        "island_south_initial"
    };

    private static readonly HashSet<string> FirstParrotAccessPhases = new(StringComparer.Ordinal)
    {
        "north_after_first_parrot",
        "east_after_first_parrot",
        "volcano_after_first_parrot",
        "caldera_after_volcano"
    };

    private static readonly HashSet<string> WesternTurtleAccessPhases = new(StringComparer.Ordinal)
    {
        "west_after_turtle"
    };

    public static GoldenWalnutFreshSaveUnlockTimelineProof Build(
        GoldenWalnutFreshSaveSourceSchedule schedule,
        string sourceScheduleSha256)
    {
        if (schedule is null)
            throw new ArgumentNullException(nameof(schedule));
        if (schedule.SourceCompletions is null)
            throw new InvalidOperationException("Golden Walnut source schedule completions are missing.");
        Require(
            schedule.SchemaVersion == SourceSchemaVersion &&
            schedule.GoalId == GoalId &&
            schedule.GameVersion == GameVersion &&
            !schedule.TrainingLabelEligible &&
            !string.IsNullOrWhiteSpace(schedule.ScheduleId) &&
            schedule.FirstIslandArrivalTotalDay >= 0 &&
            schedule.PlannedFirstParrotUnlockTotalDay >= schedule.FirstIslandArrivalTotalDay &&
            schedule.PlannedWesternTurtleUnlockTotalDay >= schedule.PlannedFirstParrotUnlockTotalDay &&
            schedule.PlannedIslandHouseUnlockTotalDay >= schedule.PlannedWesternTurtleUnlockTotalDay,
            "Golden Walnut fresh-save source schedule metadata is invalid.");
        Require(IsSha256(sourceScheduleSha256),
            "Golden Walnut fresh-save source schedule SHA-256 is invalid.");

        var catalog = GoldenWalnutNativeSourceCatalog.Sources
            .ToDictionary(source => source.SourceKey, StringComparer.Ordinal);
        var duplicate = schedule.SourceCompletions
            .GroupBy(row => row.SourceKey, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        Require(duplicate is null,
            "Golden Walnut source schedule contains a duplicate source key: " + duplicate?.Key);

        var completions = schedule.SourceCompletions
            .OrderBy(row => row.PlannedCompletionTotalDay)
            .ThenBy(row => row.SourceKey, StringComparer.Ordinal)
            .ToArray();
        foreach (var row in completions)
        {
            Require(catalog.TryGetValue(row.SourceKey, out var source),
                "Golden Walnut source schedule contains an unknown source: " + row.SourceKey);
            Require(
                GoldenWalnutUnlockScheduleProofBuilder.BoundAcquisitionOptionIds.Contains(
                    source.AcquisitionOptionId) &&
                source.OptionalUnlockWalnutCost == 0 &&
                !source.ResortOnly &&
                source.ProbabilityStatus.StartsWith("exact_native_", StringComparison.Ordinal),
                "Golden Walnut source schedule contains a non-deterministic or unbound source: " + row.SourceKey);
            Require(
                row.PlannedQuantity > 0 &&
                row.PlannedQuantity <= source.NativeQuantity &&
                row.PlannedCompletionTotalDay >= schedule.FirstIslandArrivalTotalDay,
                "Golden Walnut source schedule quantity or completion day is invalid: " + row.SourceKey);
            Require(
                InitialAccessPhases.Contains(source.AccessPhase) ||
                FirstParrotAccessPhases.Contains(source.AccessPhase) ||
                WesternTurtleAccessPhases.Contains(source.AccessPhase),
                "Golden Walnut source schedule contains an unsupported pre-House phase: " + row.SourceKey);
        }

        var firstParrotReadyDay = ThresholdDay(
            completions,
            catalog,
            InitialAccessPhases,
            schedule.FirstIslandArrivalTotalDay,
            requiredCumulativeQuantity: 1);
        if (firstParrotReadyDay.HasValue)
        {
            Require(schedule.PlannedFirstParrotUnlockTotalDay >= firstParrotReadyDay.Value,
                "First Parrot unlock is scheduled before its one-walnut threshold is ready.");
        }
        Require(completions
                .Where(row => FirstParrotAccessPhases.Contains(catalog[row.SourceKey].AccessPhase))
                .All(row => row.PlannedCompletionTotalDay >= schedule.PlannedFirstParrotUnlockTotalDay),
            "Golden Walnut source schedule acquires a post-parrot source before the planned first parrot unlock.");

        var turtlePhases = InitialAccessPhases
            .Concat(FirstParrotAccessPhases)
            .ToHashSet(StringComparer.Ordinal);
        var turtleReadyDay = firstParrotReadyDay.HasValue
            ? ThresholdDay(
                completions,
                catalog,
                turtlePhases,
                schedule.PlannedFirstParrotUnlockTotalDay,
                requiredCumulativeQuantity: 11)
            : null;
        if (turtleReadyDay.HasValue)
        {
            Require(schedule.PlannedWesternTurtleUnlockTotalDay >= turtleReadyDay.Value,
                "Western Turtle unlock is scheduled before its eleven-walnut threshold is ready.");
        }
        Require(completions
                .Where(row => WesternTurtleAccessPhases.Contains(catalog[row.SourceKey].AccessPhase))
                .All(row => row.PlannedCompletionTotalDay >= schedule.PlannedWesternTurtleUnlockTotalDay),
            "Golden Walnut source schedule acquires an IslandWest source before the planned Western Turtle unlock.");

        var housePhases = turtlePhases
            .Concat(WesternTurtleAccessPhases)
            .ToHashSet(StringComparer.Ordinal);
        var houseReadyDay = turtleReadyDay.HasValue
            ? ThresholdDay(
                completions,
                catalog,
                housePhases,
                schedule.PlannedWesternTurtleUnlockTotalDay,
                requiredCumulativeQuantity: 31)
            : null;
        if (houseReadyDay.HasValue)
        {
            Require(schedule.PlannedIslandHouseUnlockTotalDay >= houseReadyDay.Value,
                "Island House unlock is scheduled before its thirty-one-walnut threshold is ready.");
        }

        var scheduleComplete = firstParrotReadyDay.HasValue &&
            turtleReadyDay.HasValue && houseReadyDay.HasValue;
        var deadline = scheduleComplete
            ? IslandGourmandSupplyScheduleProofBuilder.Build(
                schedule.PlannedWesternTurtleUnlockTotalDay,
                schedule.PlannedIslandHouseUnlockTotalDay)
            : IslandGourmandSupplyScheduleProofBuilder.Build();
        var blocking = new List<string>();
        if (!firstParrotReadyDay.HasValue)
            blocking.Add("first_parrot_one_walnut_threshold_not_scheduled");
        if (!turtleReadyDay.HasValue)
            blocking.Add("western_turtle_eleven_walnut_threshold_not_scheduled");
        if (!houseReadyDay.HasValue)
            blocking.Add("island_house_thirty_one_walnut_threshold_not_scheduled");
        if (scheduleComplete && !deadline.DeadlineCompositionComplete)
            blocking.Add("gourmand_unlock_timeline_misses_day_224_deadline");

        return new GoldenWalnutFreshSaveUnlockTimelineProof(
            SchemaVersion,
            scheduleComplete && deadline.DeadlineCompositionComplete
                ? "fresh_save_unlock_timeline_and_gourmand_deadline_complete"
                : "fresh_save_unlock_timeline_or_deadline_incomplete",
            schedule.ScheduleId,
            sourceScheduleSha256.ToLowerInvariant(),
            schedule.FirstIslandArrivalTotalDay,
            firstParrotReadyDay,
            turtleReadyDay,
            houseReadyDay,
            scheduleComplete ? schedule.PlannedFirstParrotUnlockTotalDay : null,
            scheduleComplete ? schedule.PlannedWesternTurtleUnlockTotalDay : null,
            scheduleComplete ? schedule.PlannedIslandHouseUnlockTotalDay : null,
            completions.Sum(row => row.PlannedQuantity),
            scheduleComplete,
            scheduleComplete,
            deadline.DeadlineCompositionComplete,
            false,
            completions,
            blocking.ToArray());
    }

    private static int? ThresholdDay(
        IReadOnlyCollection<GoldenWalnutFreshSaveSourceCompletion> completions,
        IReadOnlyDictionary<string, GoldenWalnutNativeSourceDefinition> catalog,
        IReadOnlyCollection<string> allowedPhases,
        int earliestTotalDay,
        int requiredCumulativeQuantity) => completions
        .Where(row => row.PlannedCompletionTotalDay >= earliestTotalDay)
        .Select(row => row.PlannedCompletionTotalDay)
        .Distinct()
        .OrderBy(day => day)
        .Cast<int?>()
        .FirstOrDefault(day => completions
            .Where(row => row.PlannedCompletionTotalDay <= day!.Value)
            .Where(row => allowedPhases.Contains(catalog[row.SourceKey].AccessPhase))
            .Sum(row => row.PlannedQuantity) >= requiredCumulativeQuantity);

    private static bool IsSha256(string value) =>
        value is { Length: 64 } && value.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
