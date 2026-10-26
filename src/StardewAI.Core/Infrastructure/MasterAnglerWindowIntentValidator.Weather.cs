using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure
{
    public static partial class MasterAnglerWindowIntentValidator
    {
        public static bool TryValidateCurrentWeatherForLocation(
            SnapshotEnvelope snapshot,
            string locationId,
            IEnumerable<string> allowedWeatherModes,
            out string canonicalWeather,
            out string rejectionReason)
        {
            canonicalWeather = string.Empty;
            rejectionReason = string.Empty;
            var allowed = allowedWeatherModes
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToHashSet(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(locationId) || allowed.Count == 0)
            {
                rejectionReason =
                    "master_angler_target_weather_window_contract_invalid";
                return false;
            }

            if (!TryReadCanonicalWeatherForLocation(
                    snapshot,
                    locationId,
                    out canonicalWeather))
            {
                rejectionReason =
                    "master_angler_target_location_weather_unavailable";
                return false;
            }
            if (!allowed.Contains(canonicalWeather))
            {
                rejectionReason =
                    "master_angler_target_weather_window_mismatch";
                return false;
            }
            return true;
        }

        private static bool TryReadCanonicalWeatherForLocation(
            SnapshotEnvelope snapshot,
            string locationId,
            out string weather)
        {
            weather = string.Empty;
            if (TryReadLocationContexts(snapshot, out var locationContexts) &&
                locationContexts.TryGetValue(locationId, out var contextId))
            {
                var rows = ReadStateFieldValue(
                    snapshot,
                    "time",
                    "location_context_weather");
                if (rows.HasValue && rows.Value.ValueKind == JsonValueKind.Array)
                {
                    var matches = rows.Value.EnumerateArray()
                        .Where(row => string.Equals(
                            ReadString(row, "location_context_id"),
                            contextId,
                            StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    if (matches.Length == 1)
                    {
                        weather = CanonicalWeather(matches[0]);
                        return true;
                    }
                }
            }

            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            if (!string.Equals(
                    currentLocation,
                    locationId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var raw = ReadStateFieldString(snapshot, "time", "weather");
            var greenRain = ReadStateFieldValue(
                snapshot,
                "time",
                "is_green_rain");
            if (string.IsNullOrWhiteSpace(raw) ||
                !greenRain.HasValue ||
                greenRain.Value.ValueKind is not (
                    JsonValueKind.True or JsonValueKind.False))
            {
                return false;
            }
            weather = greenRain.Value.GetBoolean()
                ? "green_rain"
                : raw is "lightning" or "storm"
                    ? "storm"
                    : raw == "rain"
                        ? "rain"
                        : "sun";
            return true;
        }

        private static string CanonicalWeather(JsonElement row) =>
            ReadBool(row, "is_green_rain") == true
                ? "green_rain"
                : ReadBool(row, "is_lightning") == true
                    ? "storm"
                    : ReadBool(row, "is_raining") == true
                        ? "rain"
                        : "sun";
    }
}
