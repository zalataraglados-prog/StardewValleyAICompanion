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

public static partial class MasterAnglerWindowIntentValidator
{
    public static bool TryBuildRainTotemSupplyIntent(
        SnapshotEnvelope snapshot,
        string windowIndexPath,
        string windowIndexSha256,
        out SmallModelActionParameter[] parameters,
        out MasterAnglerRainTotemSupplyIntentValidation validation,
        out string rejectionReason)
    {
        parameters = Array.Empty<SmallModelActionParameter>();
        validation = new MasterAnglerRainTotemSupplyIntentValidation();
        rejectionReason = string.Empty;
        if (!TryLoadArtifact(
                windowIndexPath,
                windowIndexSha256,
                out var artifact,
                out rejectionReason))
        {
            return false;
        }

        var currentDay = ReadStateFieldInt(snapshot, "time", "total_days");
        var targetDay = currentDay + 1;
        if (currentDay < 0 ||
            artifact.DeadlineTotalDayExclusive != 224 ||
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
        if (!MissingSpecies(snapshot, 72, out var missing))
        {
            rejectionReason =
                "master_angler_weather_supply_exact_missing_set_unavailable";
            return false;
        }

        var rainTotem = ReadStateFieldValue(snapshot, "player", "rain_totem");
        if (!rainTotem.HasValue ||
            rainTotem.Value.ValueKind != JsonValueKind.Object ||
            !rainTotem.Value.TryGetProperty(
                "context_routing",
                out var routing) ||
            routing.ValueKind != JsonValueKind.Object)
        {
            rejectionReason =
                "master_angler_weather_supply_rain_totem_projection_unavailable";
            return false;
        }
        var affectedContext = ReadString(
            routing,
            "affected_location_context_id");
        if (string.IsNullOrWhiteSpace(affectedContext) ||
            !TryReadRainTotemWeatherTransition(
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
        var targetIds = missing
            .Where(target => artifact.WindowsBySpecies.TryGetValue(
                target,
                out var windows) &&
                windows.Any(window =>
                    string.Equals(
                        window.SourceKind,
                        "location_rule",
                        StringComparison.Ordinal) &&
                    targetDay >= window.FirstTotalDay &&
                    targetDay <= window.LastTotalDay &&
                    window.DynamicConditionCount == 0 &&
                    window.WeatherModes.Contains(
                        "rain",
                        StringComparer.Ordinal) &&
                    !window.WeatherModes.Contains(
                        "sun",
                        StringComparer.Ordinal) &&
                    window.MinimumFishingLevel <= fishingLevel &&
                    (!window.RequireMagicBait || hasMagicBait) &&
                    (window.TrainingRodAllowed != false ||
                     hasNonTrainingRod) &&
                    locationContexts.TryGetValue(
                        window.LocationId,
                        out var contextId) &&
                    string.Equals(
                        contextId,
                        affectedContext,
                        StringComparison.Ordinal)))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (targetIds.Length == 0)
        {
            rejectionReason =
                "master_angler_weather_supply_no_exact_tomorrow_rain_window";
            return false;
        }

        parameters = new[]
        {
            Parameter(
                WeatherSupplyStatusParameter,
                WeatherSupplyValidationStatus),
            Parameter(
                WeatherSupplyTargetDayParameter,
                targetDay.ToString(CultureInfo.InvariantCulture)),
            Parameter(
                WeatherSupplyTargetIdsParameter,
                JsonSerializer.Serialize(targetIds)),
            Parameter(
                WeatherSupplyAffectedContextParameter,
                affectedContext),
            Parameter(
                WeatherSupplyWindowIndexPathParameter,
                Path.GetFullPath(windowIndexPath)),
            Parameter(
                WeatherSupplyWindowIndexSha256Parameter,
                windowIndexSha256),
            Parameter(
                WeatherSupplyReplanRequiredParameter,
                "true")
        };
        if (TryValidateRainTotemSupplyIntent(
                snapshot,
                parameters,
                out validation,
                out rejectionReason))
        {
            return true;
        }

        parameters = Array.Empty<SmallModelActionParameter>();
        return false;
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
