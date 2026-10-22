using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public sealed class MasterAnglerRainTotemReserveIntentValidation
{
    public int TargetTotalDay { get; init; }
    public int UseTotalDay { get; init; }
    public string[] TargetQualifiedItemIds { get; init; } = [];
    public string AffectedLocationContextId { get; init; } = string.Empty;
    public string WindowIndexPath { get; init; } = string.Empty;
    public string WindowIndexSha256 { get; init; } = string.Empty;
    public int InventoryPineTarQuantity { get; init; }
    public int ExistingTapperProjectedOutputQuantity { get; init; }
    public int OutputQuantityPerNewTapper { get; init; }
    public int RequiredAdditionalTapperCount { get; init; }
}

public static partial class MasterAnglerWindowIntentValidator
{
    public const string WeatherReserveStatusParameter =
        "master_angler_weather_reserve_status";
    public const string WeatherReserveTargetDayParameter =
        "master_angler_weather_reserve_target_total_day";
    public const string WeatherReserveUseDayParameter =
        "master_angler_weather_reserve_use_total_day";
    public const string WeatherReserveTargetIdsParameter =
        "master_angler_weather_reserve_target_qualified_item_ids_json";
    public const string WeatherReserveAffectedContextParameter =
        "master_angler_weather_reserve_affected_context_id";
    public const string WeatherReserveWindowIndexPathParameter =
        "master_angler_weather_reserve_window_index_path";
    public const string WeatherReserveWindowIndexSha256Parameter =
        "master_angler_weather_reserve_window_index_sha256";
    public const string WeatherReserveMaterialIdParameter =
        "master_angler_weather_reserve_material_qualified_item_id";
    public const string WeatherReserveRequiredQuantityParameter =
        "master_angler_weather_reserve_required_quantity";
    public const string WeatherReserveInventoryQuantityParameter =
        "master_angler_weather_reserve_inventory_quantity";
    public const string WeatherReserveExistingOutputQuantityParameter =
        "master_angler_weather_reserve_existing_tapper_output_quantity";
    public const string WeatherReserveMachineIdParameter =
        "master_angler_weather_reserve_machine_qualified_item_id";
    public const string WeatherReserveSourceIdParameter =
        "master_angler_weather_reserve_source_id";
    public const string WeatherReserveCycleDaysParameter =
        "master_angler_weather_reserve_cycle_days";
    public const string WeatherReserveOutputPerNewMachineParameter =
        "master_angler_weather_reserve_output_per_new_tapper";
    public const string WeatherReserveRequiredMachineCountParameter =
        "master_angler_weather_reserve_required_additional_tapper_count";
    public const string WeatherReserveReplanRequiredParameter =
        "master_angler_weather_reserve_replan_required";

    private const string WeatherReserveValidationStatus =
        "authoritative_future_rain_window_pine_tar_capacity_reserve";
    private const string PineTarQualifiedItemId = "(O)726";
    private const string TapperQualifiedItemId = "(BC)105";
    private const string PineTapperSourceId = "wild_tree:3:0";
    private const int PineTarRequiredQuantity = 5;
    private const int PineTapperCycleDays = 5;

    public static bool HasRainTotemReserveIntent(
        IEnumerable<SmallModelActionParameter> parameters) =>
        parameters.Any(parameter =>
            string.Equals(
                parameter.Name,
                WeatherReserveStatusParameter,
                StringComparison.Ordinal) ||
            string.Equals(
                parameter.Name,
                "continuation." + WeatherReserveStatusParameter,
                StringComparison.Ordinal));

    public static bool TryValidateRainTotemReserveIntent(
        SnapshotEnvelope snapshot,
        IEnumerable<SmallModelActionParameter> parameters,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        validation = new();
        rejectionReason = string.Empty;
        var values = parameters.ToArray();
        if (!TryRead(values, WeatherReserveStatusParameter, out var status) ||
            !TryReadInt(values, WeatherReserveTargetDayParameter, out var targetDay) ||
            !TryReadInt(values, WeatherReserveUseDayParameter, out var useDay) ||
            !TryRead(values, WeatherReserveTargetIdsParameter, out var targetIdsJson) ||
            !TryRead(values, WeatherReserveAffectedContextParameter, out var affectedContext) ||
            !TryRead(values, WeatherReserveWindowIndexPathParameter, out var indexPath) ||
            !TryRead(values, WeatherReserveWindowIndexSha256Parameter, out var expectedHash) ||
            !TryRead(values, WeatherReserveMaterialIdParameter, out var materialId) ||
            !TryReadInt(values, WeatherReserveRequiredQuantityParameter, out var requiredQuantity) ||
            !TryReadInt(values, WeatherReserveInventoryQuantityParameter, out var inventoryQuantity) ||
            !TryReadInt(values, WeatherReserveExistingOutputQuantityParameter, out var existingOutputQuantity) ||
            !TryRead(values, WeatherReserveMachineIdParameter, out var machineId) ||
            !TryRead(values, WeatherReserveSourceIdParameter, out var sourceId) ||
            !TryReadInt(values, WeatherReserveCycleDaysParameter, out var cycleDays) ||
            !TryReadInt(values, WeatherReserveOutputPerNewMachineParameter, out var outputPerNewMachine) ||
            !TryReadInt(values, WeatherReserveRequiredMachineCountParameter, out var requiredMachineCount) ||
            !TryRead(values, WeatherReserveReplanRequiredParameter, out var replanRequired) ||
            !TryReadQualifiedItemIds(targetIdsJson, out var targetIds))
        {
            rejectionReason =
                "master_angler_weather_reserve_contract_incomplete_or_duplicated";
            return false;
        }
        if (!string.Equals(status, WeatherReserveValidationStatus, StringComparison.Ordinal) ||
            !string.Equals(materialId, PineTarQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(machineId, TapperQualifiedItemId, StringComparison.Ordinal) ||
            !string.Equals(sourceId, PineTapperSourceId, StringComparison.Ordinal) ||
            requiredQuantity != PineTarRequiredQuantity ||
            cycleDays != PineTapperCycleDays ||
            requiredMachineCount <= 0 ||
            outputPerNewMachine <= 0 ||
            inventoryQuantity < 0 ||
            existingOutputQuantity < 0 ||
            !string.Equals(replanRequired, "true", StringComparison.Ordinal) ||
            !IsSha256(expectedHash))
        {
            rejectionReason = "master_angler_weather_reserve_contract_invalid";
            return false;
        }
        if (!TryLoadArtifact(indexPath, expectedHash, out var artifact, out rejectionReason) ||
            !TryProjectRainTotemPineTarReserve(
                snapshot,
                artifact,
                Path.GetFullPath(indexPath),
                expectedHash,
                out var projected,
                out rejectionReason))
        {
            return false;
        }
        if (targetDay != projected.TargetTotalDay ||
            useDay != projected.UseTotalDay ||
            !targetIds.SequenceEqual(projected.TargetQualifiedItemIds, StringComparer.Ordinal) ||
            !string.Equals(affectedContext, projected.AffectedLocationContextId, StringComparison.Ordinal) ||
            inventoryQuantity != projected.InventoryPineTarQuantity ||
            existingOutputQuantity != projected.ExistingTapperProjectedOutputQuantity ||
            outputPerNewMachine != projected.OutputQuantityPerNewTapper ||
            requiredMachineCount != projected.RequiredAdditionalTapperCount)
        {
            rejectionReason = "master_angler_weather_reserve_projection_drifted";
            return false;
        }

        validation = projected;
        return true;
    }

    private static bool TryProjectRainTotemPineTarReserve(
        SnapshotEnvelope snapshot,
        ParsedArtifact artifact,
        string indexPath,
        string indexHash,
        out MasterAnglerRainTotemReserveIntentValidation validation,
        out string rejectionReason)
    {
        validation = new();
        rejectionReason = string.Empty;
        var currentDay = ReadStateFieldInt(snapshot, "time", "total_days");
        var currentTime = ReadStateFieldInt(snapshot, "time", "time");
        if (currentDay < 0 ||
            artifact.DeadlineTotalDayExclusive != 224 ||
            (!string.IsNullOrWhiteSpace(snapshot.GameVersion) &&
             !string.Equals(snapshot.GameVersion, artifact.GameVersion, StringComparison.Ordinal)))
        {
            rejectionReason = "master_angler_weather_reserve_artifact_identity_mismatch";
            return false;
        }
        if (!MissingSpecies(snapshot, 72, out var missing) ||
            !TryReadLocationContexts(snapshot, out var locationContexts) ||
            !TryReadFutureFestivalDateKeys(snapshot, currentDay, out var festivalKeys))
        {
            rejectionReason = "master_angler_weather_reserve_authority_unavailable";
            return false;
        }

        var rainTotem = ReadStateFieldValue(snapshot, "player", "rain_totem");
        if (!rainTotem.HasValue ||
            rainTotem.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(rainTotem.Value, "projection_status"),
                "complete_current_native_rain_totem_context",
                StringComparison.Ordinal) ||
            !rainTotem.Value.TryGetProperty("context_routing", out var routing) ||
            routing.ValueKind != JsonValueKind.Object ||
            ReadBool(routing, "allow_rain_totem") != true)
        {
            rejectionReason = "master_angler_weather_reserve_rain_totem_context_unavailable";
            return false;
        }
        var affectedContext = ReadString(routing, "affected_location_context_id");
        if (string.IsNullOrWhiteSpace(affectedContext))
        {
            rejectionReason = "master_angler_weather_reserve_affected_context_unavailable";
            return false;
        }

        var inventoryQuantity = InventoryQuantity(snapshot, PineTarQualifiedItemId);
        if (inventoryQuantity >= PineTarRequiredQuantity)
        {
            rejectionReason = "master_angler_weather_reserve_pine_tar_inventory_already_sufficient";
            return false;
        }

        var targetsByDay = new SortedDictionary<int, HashSet<string>>();
        foreach (var target in missing.OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!artifact.WindowsBySpecies.TryGetValue(target, out var windows))
                continue;
            foreach (var window in windows.Where(window =>
                         string.Equals(window.SourceKind, "location_rule", StringComparison.Ordinal) &&
                         window.DynamicConditionCount == 0 &&
                         window.WeatherModes.Contains("rain", StringComparer.Ordinal) &&
                         !window.WeatherModes.Contains("sun", StringComparer.Ordinal) &&
                         !window.RequireMagicBait &&
                         locationContexts.TryGetValue(window.LocationId, out var contextId) &&
                         string.Equals(contextId, affectedContext, StringComparison.Ordinal)))
            {
                var first = Math.Max(currentDay + 2, window.FirstTotalDay);
                var last = Math.Min(
                    window.LastTotalDay,
                    artifact.DeadlineTotalDayExclusive - 1);
                for (var day = first; day <= last; day++)
                {
                    if (FutureDateKey(day) is not { } dateKey ||
                        festivalKeys.Contains(dateKey))
                    {
                        continue;
                    }
                    if (!targetsByDay.TryGetValue(day, out var ids))
                    {
                        ids = new HashSet<string>(StringComparer.Ordinal);
                        targetsByDay.Add(day, ids);
                    }
                    ids.Add(target);
                }
            }
        }
        foreach (var row in targetsByDay)
        {
            var useDay = row.Key - 1;
            var daysToUse = useDay - currentDay;
            var outputPerNewTapper = daysToUse / PineTapperCycleDays;
            var existingOutput = ProjectExistingPineTapperOutputs(
                snapshot,
                currentTime,
                daysToUse);
            var deficit = Math.Max(
                0,
                PineTarRequiredQuantity - inventoryQuantity - existingOutput);
            if (deficit == 0)
            {
                rejectionReason =
                    "master_angler_weather_reserve_existing_pine_tar_capacity_sufficient";
                return false;
            }
            if (outputPerNewTapper <= 0)
                continue;

            validation = new MasterAnglerRainTotemReserveIntentValidation
            {
                TargetTotalDay = row.Key,
                UseTotalDay = useDay,
                TargetQualifiedItemIds = row.Value
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                AffectedLocationContextId = affectedContext,
                WindowIndexPath = indexPath,
                WindowIndexSha256 = indexHash,
                InventoryPineTarQuantity = inventoryQuantity,
                ExistingTapperProjectedOutputQuantity = existingOutput,
                OutputQuantityPerNewTapper = outputPerNewTapper,
                RequiredAdditionalTapperCount =
                    (deficit + outputPerNewTapper - 1) / outputPerNewTapper
            };
            return true;
        }

        rejectionReason =
            "master_angler_weather_reserve_no_lead_time_feasible_future_rain_window";
        return false;
    }

    private static int InventoryQuantity(
        SnapshotEnvelope snapshot,
        string qualifiedItemId)
    {
        var inventory = ReadStateFieldValue(snapshot, "player", "inventory");
        return !inventory.HasValue || inventory.Value.ValueKind != JsonValueKind.Array
            ? 0
            : inventory.Value.EnumerateArray()
                .Where(row => string.Equals(
                    ReadString(row, "qualified_item_id"),
                    qualifiedItemId,
                    StringComparison.Ordinal))
                .Sum(row => Math.Max(0, ReadInt(row, "stack")));
    }

    private static int ProjectExistingPineTapperOutputs(
        SnapshotEnvelope snapshot,
        int currentTime,
        int daysToUse)
    {
        if (!TryMinutesUntilMorning(currentTime, daysToUse, out var budget))
            return 0;
        var machines = ReadStateFieldValue(snapshot, "farm", "machines");
        if (!machines.HasValue || machines.Value.ValueKind != JsonValueKind.Array)
            return 0;

        return machines.Value.EnumerateArray().Count(machine =>
            machine.ValueKind == JsonValueKind.Object &&
            machine.TryGetProperty("wild_tree_tapper_production", out var production) &&
            production.ValueKind == JsonValueKind.Object &&
            string.Equals(
                ReadString(production, "projection_status"),
                "exact_locked_base_pine_tapper_cycle",
                StringComparison.Ordinal) &&
            string.Equals(
                ReadString(production, "exact_output_qualified_item_id"),
                PineTarQualifiedItemId,
                StringComparison.Ordinal) &&
            string.Equals(
                ReadString(production, "current_output_qualified_item_id"),
                PineTarQualifiedItemId,
                StringComparison.Ordinal) &&
            ReadInt(production, "current_output_stack") > 0 &&
            (ReadBool(production, "ready_for_harvest") == true ||
             SnapshotValueReader.ReadInt(
                 production,
                 "minutes_until_ready",
                 int.MaxValue) <= budget));
    }

    private static bool TryMinutesUntilMorning(
        int currentTime,
        int daysElapsed,
        out int minutes)
    {
        minutes = 0;
        if (daysElapsed < 1 ||
            currentTime < 0 ||
            currentTime % 100 >= 60)
        {
            return false;
        }
        var currentMinutes = currentTime / 100 * 60 + currentTime % 100;
        minutes = 26 * 60 - currentMinutes + 400 +
            (daysElapsed - 1) * 1600;
        return minutes >= 0;
    }

    private static bool TryReadFutureFestivalDateKeys(
        SnapshotEnvelope snapshot,
        int currentDay,
        out HashSet<string> keys)
    {
        keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var calendar = ReadStateFieldValue(
            snapshot,
            "world_progress",
            "game_state_query_calendar_state");
        if (!calendar.HasValue ||
            calendar.Value.ValueKind != JsonValueKind.Object ||
            SnapshotValueReader.ReadInt(
                calendar.Value,
                "current_total_day",
                -1) != currentDay ||
            !calendar.Value.TryGetProperty("festival_date_keys", out var values) ||
            values.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        foreach (var value in values.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String ||
                string.IsNullOrWhiteSpace(value.GetString()))
            {
                return false;
            }
            keys.Add(value.GetString()!);
        }
        return true;
    }

    private static string? FutureDateKey(int totalDay)
    {
        if (totalDay < 0)
            return null;
        var dayOfYear = totalDay % 112;
        var seasons = new[] { "spring", "summer", "fall", "winter" };
        return seasons[dayOfYear / 28] +
            (dayOfYear % 28 + 1).ToString(CultureInfo.InvariantCulture);
    }
}
