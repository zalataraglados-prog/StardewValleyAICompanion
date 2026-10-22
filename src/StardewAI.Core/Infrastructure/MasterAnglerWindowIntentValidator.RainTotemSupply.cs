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

public sealed class MasterAnglerRainTotemSupplyIntentValidation
{
    public int TargetTotalDay { get; init; }
    public string[] TargetQualifiedItemIds { get; init; } =
        Array.Empty<string>();
    public string AffectedLocationContextId { get; init; } = string.Empty;
    public string WindowIndexPath { get; init; } = string.Empty;
    public string WindowIndexSha256 { get; init; } = string.Empty;
}

public static partial class MasterAnglerWindowIntentValidator
{
    public const string WeatherSupplyStatusParameter =
        "master_angler_weather_supply_status";
    public const string WeatherSupplyTargetDayParameter =
        "master_angler_weather_supply_target_total_day";
    public const string WeatherSupplyTargetIdsParameter =
        "master_angler_weather_supply_target_qualified_item_ids_json";
    public const string WeatherSupplyAffectedContextParameter =
        "master_angler_weather_supply_affected_context_id";
    public const string WeatherSupplyWindowIndexPathParameter =
        "master_angler_weather_supply_window_index_path";
    public const string WeatherSupplyWindowIndexSha256Parameter =
        "master_angler_weather_supply_window_index_sha256";
    public const string WeatherSupplyReplanRequiredParameter =
        "master_angler_weather_supply_replan_required";

    private const string WeatherSupplyValidationStatus =
        "authoritative_tomorrow_rain_window_match";

    public static bool HasRainTotemSupplyIntent(
        IEnumerable<SmallModelActionParameter> parameters) =>
        parameters.Any(parameter =>
            string.Equals(
                parameter.Name,
                WeatherSupplyStatusParameter,
                StringComparison.Ordinal) ||
            string.Equals(
                parameter.Name,
                "continuation." + WeatherSupplyStatusParameter,
                StringComparison.Ordinal));

    public static bool TryValidateRainTotemSupplyIntent(
        SnapshotEnvelope snapshot,
        IEnumerable<SmallModelActionParameter> parameters,
        out MasterAnglerRainTotemSupplyIntentValidation validation,
        out string rejectionReason)
    {
        validation = new MasterAnglerRainTotemSupplyIntentValidation();
        rejectionReason = string.Empty;
        var values = parameters.ToArray();
        if (!TryRead(values, WeatherSupplyStatusParameter, out var status) ||
            !TryReadInt(values, WeatherSupplyTargetDayParameter, out var targetDay) ||
            !TryRead(values, WeatherSupplyTargetIdsParameter, out var targetIdsJson) ||
            !TryRead(values, WeatherSupplyAffectedContextParameter, out var affectedContext) ||
            !TryRead(values, WeatherSupplyWindowIndexPathParameter, out var indexPath) ||
            !TryRead(values, WeatherSupplyWindowIndexSha256Parameter, out var expectedHash) ||
            !TryRead(values, WeatherSupplyReplanRequiredParameter, out var replanRequired))
        {
            rejectionReason =
                "master_angler_weather_supply_contract_incomplete_or_duplicated";
            return false;
        }
        if (!string.Equals(
                status,
                WeatherSupplyValidationStatus,
                StringComparison.Ordinal) ||
            !string.Equals(replanRequired, "true", StringComparison.Ordinal) ||
            string.IsNullOrWhiteSpace(affectedContext) ||
            !IsSha256(expectedHash) ||
            !TryReadQualifiedItemIds(targetIdsJson, out var targetIds))
        {
            rejectionReason = "master_angler_weather_supply_contract_invalid";
            return false;
        }

        var currentDay = ReadStateFieldInt(snapshot, "time", "total_days");
        if (currentDay < 0 || targetDay != currentDay + 1)
        {
            rejectionReason =
                "master_angler_weather_supply_target_day_not_tomorrow";
            return false;
        }
        if (!MissingSpecies(snapshot, 72, out var missing) ||
            targetIds.Any(target => !missing.Contains(target)))
        {
            rejectionReason =
                "master_angler_weather_supply_target_not_in_exact_missing_set";
            return false;
        }

        if (!TryLoadArtifact(
                indexPath,
                expectedHash,
                out var artifact,
                out rejectionReason))
        {
            return false;
        }
        if (artifact.DeadlineTotalDayExclusive != 224 ||
            targetDay >= artifact.DeadlineTotalDayExclusive ||
            (!string.IsNullOrWhiteSpace(snapshot.GameVersion) &&
             !string.Equals(
                 snapshot.GameVersion,
                 artifact.GameVersion,
                 StringComparison.Ordinal)))
        {
            rejectionReason =
                "master_angler_weather_supply_artifact_identity_mismatch";
            return false;
        }

        if (!TryReadRainTotemWeatherTransition(
                snapshot,
                targetDay,
                affectedContext,
                out rejectionReason) ||
            !TryReadLocationContexts(snapshot, out var locationContexts))
        {
            if (string.IsNullOrWhiteSpace(rejectionReason))
            {
                rejectionReason =
                    "master_angler_weather_supply_location_context_unavailable";
            }
            return false;
        }

        var fishingLevel = ReadFishingLevel(snapshot);
        var hasMagicBait = AnyRodContextFlag(
            snapshot,
            "has_magic_bait",
            expected: true);
        var hasNonTrainingRod = AnyRodContextFlag(
            snapshot,
            "uses_training_rod",
            expected: false);
        foreach (var target in targetIds)
        {
            if (!artifact.WindowsBySpecies.TryGetValue(target, out var windows) ||
                !windows.Any(window =>
                    string.Equals(
                        window.SourceKind,
                        "location_rule",
                        StringComparison.Ordinal) &&
                    targetDay >= window.FirstTotalDay &&
                    targetDay <= window.LastTotalDay &&
                    window.DynamicConditionCount == 0 &&
                    window.WeatherModes.Contains("rain", StringComparer.Ordinal) &&
                    !window.WeatherModes.Contains("sun", StringComparer.Ordinal) &&
                    window.MinimumFishingLevel <= fishingLevel &&
                    (!window.RequireMagicBait || hasMagicBait) &&
                    (window.TrainingRodAllowed != false || hasNonTrainingRod) &&
                    locationContexts.TryGetValue(
                        window.LocationId,
                        out var contextId) &&
                    string.Equals(
                        contextId,
                        affectedContext,
                        StringComparison.Ordinal)))
            {
                rejectionReason =
                    "master_angler_weather_supply_target_has_no_exact_tomorrow_rain_window";
                return false;
            }
        }

        validation = new MasterAnglerRainTotemSupplyIntentValidation
        {
            TargetTotalDay = targetDay,
            TargetQualifiedItemIds = targetIds,
            AffectedLocationContextId = affectedContext,
            WindowIndexPath = Path.GetFullPath(indexPath),
            WindowIndexSha256 = expectedHash
        };
        return true;
    }

    private static bool TryReadRainTotemWeatherTransition(
        SnapshotEnvelope snapshot,
        int targetDay,
        string affectedContext,
        out string rejectionReason)
    {
        rejectionReason = string.Empty;
        var projection = ReadStateFieldValue(snapshot, "player", "rain_totem");
        if (!projection.HasValue ||
            projection.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(
                ReadString(projection.Value, "projection_status"),
                "complete_current_native_rain_totem_context",
                StringComparison.Ordinal) ||
            !projection.Value.TryGetProperty("context_routing", out var routing) ||
            routing.ValueKind != JsonValueKind.Object ||
            !projection.Value.TryGetProperty(
                "weather_transition",
                out var transition) ||
            transition.ValueKind != JsonValueKind.Object)
        {
            rejectionReason =
                "master_angler_weather_supply_rain_totem_projection_unavailable";
            return false;
        }
        if (!string.Equals(
                ReadString(routing, "affected_location_context_id"),
                affectedContext,
                StringComparison.Ordinal) ||
            ReadBool(routing, "allow_rain_totem") != true)
        {
            rejectionReason =
                "master_angler_weather_supply_rain_totem_context_mismatch";
            return false;
        }
        if (ReadBool(transition, "tomorrow_is_default_festival") == true ||
            ReadInt(transition, "tomorrow_total_days") != targetDay ||
            string.Equals(
                ReadString(transition, "affected_weather_before"),
                "Rain",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(transition, "affected_weather_after"),
                "Rain",
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadString(transition, "effective_tomorrow_weather"),
                "Rain",
                StringComparison.Ordinal) ||
            ReadBool(transition, "rain_will_take_effect_tomorrow") != true)
        {
            rejectionReason =
                "master_angler_weather_supply_rain_totem_would_not_create_needed_rain";
            return false;
        }
        return true;
    }

    private static bool TryReadLocationContexts(
        SnapshotEnvelope snapshot,
        out IReadOnlyDictionary<string, string> contexts)
    {
        contexts = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var evidence = ReadStateFieldValue(
            snapshot,
            "locations",
            "social_route_date_evidence");
        if (!evidence.HasValue ||
            evidence.Value.ValueKind != JsonValueKind.Object ||
            !evidence.Value.TryGetProperty("locations", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows.EnumerateArray())
        {
            var location = ReadString(row, "location_id");
            var context = ReadString(row, "location_context_id");
            if (string.IsNullOrWhiteSpace(location) ||
                string.IsNullOrWhiteSpace(context) ||
                !result.TryAdd(location, context))
            {
                return false;
            }
        }
        contexts = result;
        return result.Count > 0;
    }

    private static bool TryReadQualifiedItemIds(
        string json,
        out string[] values)
    {
        values = Array.Empty<string>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return false;
            values = document.RootElement.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString() ?? string.Empty)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return values.Length > 0 && values.All(value =>
                value.StartsWith("(O)", StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
