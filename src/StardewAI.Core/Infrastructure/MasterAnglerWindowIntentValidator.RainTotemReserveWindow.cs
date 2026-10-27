using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

public static partial class MasterAnglerWindowIntentValidator
{
    private sealed record FutureRainTotemReserveWindow(
        int CurrentTotalDay,
        int CurrentTime,
        int TargetTotalDay,
        int UseTotalDay,
        int DaysToUse,
        string[] TargetQualifiedItemIds,
        string AffectedLocationContextId,
        string WindowIndexPath,
        string WindowIndexSha256);

    private static bool TryProjectRainTotemReserveWindow(
        SnapshotEnvelope snapshot,
        ParsedArtifact artifact,
        string indexPath,
        string indexHash,
        int minimumDaysToUse,
        out FutureRainTotemReserveWindow projection,
        out string rejectionReason)
    {
        projection = new(0, 0, 0, 0, 0, [], string.Empty, string.Empty, string.Empty);
        rejectionReason = string.Empty;
        var currentDay = ReadStateFieldInt(snapshot, "time", "total_days");
        var currentTime = ReadStateFieldInt(snapshot, "time", "time");
        if (currentDay < 0 || minimumDaysToUse < 1 ||
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
        if (!rainTotem.HasValue || rainTotem.Value.ValueKind != JsonValueKind.Object ||
            !string.Equals(ReadString(rainTotem.Value, "projection_status"),
                "complete_current_native_rain_totem_context", StringComparison.Ordinal) ||
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
                var first = Math.Max(currentDay + minimumDaysToUse + 1, window.FirstTotalDay);
                var last = Math.Min(window.LastTotalDay, artifact.DeadlineTotalDayExclusive - 1);
                for (var day = first; day <= last; day++)
                {
                    if (FutureDateKey(day) is not { } dateKey || festivalKeys.Contains(dateKey))
                        continue;
                    if (!targetsByDay.TryGetValue(day, out var ids))
                    {
                        ids = new HashSet<string>(StringComparer.Ordinal);
                        targetsByDay.Add(day, ids);
                    }
                    ids.Add(target);
                }
            }
        }
        var firstTarget = targetsByDay.FirstOrDefault();
        if (firstTarget.Value is null)
        {
            rejectionReason = "master_angler_weather_reserve_no_lead_time_feasible_future_rain_window";
            return false;
        }
        projection = new(
            currentDay,
            currentTime,
            firstTarget.Key,
            firstTarget.Key - 1,
            firstTarget.Key - 1 - currentDay,
            firstTarget.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            affectedContext,
            Path.GetFullPath(indexPath),
            indexHash);
        return true;
    }
}
