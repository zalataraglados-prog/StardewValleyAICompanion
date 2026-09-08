using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace StardewAI.Core.Training
{
    public sealed partial class NpcFutureScheduleResolver
    {
        private NpcFutureScheduleResolution ParseSelectedSchedule(
            CatalogNpc npc,
            NpcFutureScheduleScenario scenario,
            string selectedKey,
            IReadOnlyCollection<string> selectionTrace)
        {
            return ParseScheduleKey(
                npc,
                scenario,
                selectedKey,
                selectedKey,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new List<string>(selectionTrace));
        }

        private NpcFutureScheduleResolution ParseScheduleKey(
            CatalogNpc npc,
            NpcFutureScheduleScenario scenario,
            string selectedKey,
            string currentKey,
            HashSet<string> visited,
            List<string> trace)
        {
            if (!visited.Add(currentKey))
                return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_goto_cycle");
            if (!npc.Entries.TryGetValue(currentKey, out var entry))
                return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_referenced_key_missing");

            trace.Add("parse:" + currentKey);
            var commands = SplitCommands(entry.Raw);
            if (commands.Length == 0)
                return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_raw_commands_empty", entry.RawSha256);

            var commandIndex = 0;
            var firstTokens = SplitTokens(commands[0]);
            if (commands[0].Contains("GOTO", StringComparison.Ordinal))
            {
                if (!TryReadControlTarget(firstTokens, "GOTO", out var target))
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_goto_malformed", entry.RawSha256);
                return ResolveGoto(npc, scenario, selectedKey, currentKey, target, visited, trace, entry.RawSha256, true);
            }

            if (commands[0].Contains("NOT", StringComparison.Ordinal))
            {
                if (firstTokens.Length < 4 || firstTokens.Length % 2 != 0 ||
                    firstTokens[0] != "NOT" || !string.Equals(firstTokens[1], "friendship", StringComparison.OrdinalIgnoreCase))
                {
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_not_friendship_malformed", entry.RawSha256);
                }
                if (scenario.MaximumFarmerFriendshipHearts is null)
                {
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_friendship_condition_state_missing", entry.RawSha256);
                }

                var nativeFallback = false;
                for (var index = 2; index < firstTokens.Length; index += 2)
                {
                    if (!int.TryParse(firstTokens[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var threshold))
                        continue;
                    var hearts = scenario.MaximumFarmerFriendshipHearts.TryGetValue(firstTokens[index], out var value)
                        ? value
                        : 0;
                    if (hearts >= threshold)
                    {
                        nativeFallback = true;
                        break;
                    }
                }
                if (nativeFallback)
                {
                    trace.Add("not_friendship_met:fallback_spring");
                    return ResolveGoto(npc, scenario, selectedKey, currentKey, "spring", visited, trace, entry.RawSha256, false);
                }
                commandIndex = 1;
            }
            else if (commands[0].Contains("MAIL", StringComparison.Ordinal))
            {
                if (firstTokens.Length < 2 || firstTokens[0] != "MAIL")
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_mail_control_malformed", entry.RawSha256);
                if (scenario.MasterPlayerMailReceived is null || scenario.WorldStateIds is null)
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_mail_and_world_state_missing", entry.RawSha256);
                var conditionId = firstTokens[1];
                var conditionMet = scenario.MasterPlayerMailReceived.Contains(conditionId) ||
                    scenario.WorldStateIds.Contains(conditionId);
                commandIndex = conditionMet ? 2 : 1;
                trace.Add("mail:" + conditionId + "=" + conditionMet.ToString().ToLowerInvariant());
            }

            if (commandIndex >= commands.Length)
                return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_control_branch_missing", entry.RawSha256);
            if (commands[commandIndex].Contains("GOTO", StringComparison.Ordinal))
            {
                var tokens = SplitTokens(commands[commandIndex]);
                if (!TryReadControlTarget(tokens, "GOTO", out var target))
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_branch_goto_malformed", entry.RawSha256);
                if (string.Equals(target, "NO_SCHEDULE", StringComparison.OrdinalIgnoreCase))
                {
                    trace.Add("goto:no_schedule");
                    return new NpcFutureScheduleResolution
                    {
                        Status = NpcFutureScheduleResolutionStatus.NoSchedule,
                        NpcName = npc.Name,
                        SelectedScheduleKey = selectedKey,
                        ResolvedScheduleKey = currentKey,
                        RawScheduleSha256 = entry.RawSha256,
                        ProjectionScope = "native_key_selection",
                        ResolutionTrace = trace.ToArray()
                    };
                }
                return ResolveGoto(npc, scenario, selectedKey, currentKey, target, visited, trace, entry.RawSha256, false);
            }

            return ParseEndpoints(npc, scenario, selectedKey, currentKey, entry, commands, commandIndex, visited, trace);
        }

        private NpcFutureScheduleResolution ResolveGoto(
            CatalogNpc npc,
            NpcFutureScheduleScenario scenario,
            string selectedKey,
            string currentKey,
            string target,
            HashSet<string> visited,
            List<string> trace,
            string rawSha256,
            bool firstCommand)
        {
            if (string.Equals(target, "season", StringComparison.OrdinalIgnoreCase))
            {
                target = scenario.Season;
                if (firstCommand && !npc.Entries.ContainsKey(target))
                    target = "spring";
            }
            if (!npc.Entries.ContainsKey(target))
            {
                if (!firstCommand || !npc.Entries.ContainsKey("spring"))
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_goto_target_missing", rawSha256);
                trace.Add("goto_missing:" + target + ":fallback_spring");
                target = "spring";
            }
            else
            {
                trace.Add("goto:" + target);
            }

            return ParseScheduleKey(npc, scenario, selectedKey, target, visited, trace);
        }

        private NpcFutureScheduleResolution ParseEndpoints(
            CatalogNpc npc,
            NpcFutureScheduleScenario scenario,
            string selectedKey,
            string currentKey,
            CatalogEntry entry,
            string[] commands,
            int commandIndex,
            HashSet<string> visited,
            List<string> trace)
        {
            var endpoints = new List<NpcFutureScheduleEndpoint>();
            var usedTimes = new HashSet<int>();
            var previousLocation = scenario.IsMarried == true ? "BusStop" : npc.DefaultMap;
            var previousDepartureTime = 610;
            var scheduleEntryOrdinal = 0;
            var arrivalTimeCount = 0;
            var travelTimeCount = 0;
            string? deferredPathTimingReason = null;

            for (var index = commandIndex; index < commands.Length; index++)
            {
                var tokens = SplitTokens(commands[index]);
                if (tokens.Length < 2)
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_endpoint_malformed", entry.RawSha256);

                var timeToken = tokens[0];
                var arrivalTimeRequested = timeToken.Length > 0 && timeToken[0] == 'a';
                if (arrivalTimeRequested)
                    timeToken = timeToken.Substring(1);
                if (!int.TryParse(timeToken, NumberStyles.Integer, CultureInfo.InvariantCulture, out var time))
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_time_invalid", entry.RawSha256);

                var location = tokens[1];
                var tileX = 0;
                var tileY = 0;
                var facing = 2;
                var tailIndex = 2;
                var behaviorComplete = true;
                if (location == "bed")
                {
                    if (scenario.IsMarried == true)
                    {
                        location = "BusStop";
                        tileX = 9;
                        tileY = 23;
                        facing = 3;
                    }
                    else
                    {
                        ResolveUnmarriedBedTarget(npc, out location, out tileX, out tileY);
                    }
                    behaviorComplete = false;
                }
                else if (int.TryParse(location, NumberStyles.Integer, CultureInfo.InvariantCulture, out tileX))
                {
                    location = previousLocation;
                    if (tokens.Length < 3 ||
                        !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out tileY))
                    {
                        return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_same_map_endpoint_invalid", entry.RawSha256);
                    }
                    tailIndex = 3;
                    if (tokens.Length > tailIndex &&
                        int.TryParse(tokens[tailIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFacing))
                    {
                        facing = parsedFacing;
                        tailIndex++;
                    }
                }
                else
                {
                    if (tokens.Length < 4 ||
                        !int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out tileX) ||
                        !int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out tileY))
                    {
                        return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_location_endpoint_invalid", entry.RawSha256);
                    }
                    tailIndex = 4;
                    if (tokens.Length > tailIndex &&
                        int.TryParse(tokens[tailIndex], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedFacing))
                    {
                        facing = parsedFacing;
                        tailIndex++;
                    }
                }

                if (!TryApplyLocationAccessibility(
                        npc,
                        scenario,
                        ref location,
                        ref tileX,
                        ref tileY,
                        ref facing,
                        out var fallbackKey,
                        out var accessibilityReason))
                {
                    return ParseBlocked(npc, selectedKey, currentKey, trace, accessibilityReason, entry.RawSha256);
                }
                if (fallbackKey.Length > 0)
                {
                    trace.Add("inaccessible:" + location + ":fallback:" + fallbackKey);
                    return ParseScheduleKey(npc, scenario, selectedKey, fallbackKey, visited, trace);
                }

                ReadEndpointTail(commands[index], tokens, tailIndex, out var endBehavior, out var endMessage);
                NpcSchedulePathTimingEvidence? pathTiming = null;
                int? nativeTravelGameMinutes = null;
                int? scheduledArrivalTime = null;
                var requestedArrivalTime = arrivalTimeRequested ? time : (int?)null;
                if (time != 0)
                {
                    var hasPathTiming = TryResolvePathTiming(
                            scenario,
                            selectedKey,
                            scheduleEntryOrdinal,
                            location,
                            tileX,
                            tileY,
                            facing,
                            out var resolvedTravelGameMinutes,
                            out pathTiming,
                            out var timingReason);
                    if (!hasPathTiming &&
                        (arrivalTimeRequested || scenario.NativePathTimingEvidenceComplete))
                    {
                        // Native parsing discards the entire schedule when a later endpoint
                        // triggers a location-accessibility fallback. Keep scanning so that
                        // discarded prefix endpoints don't require timing from the realized
                        // fallback schedule; an otherwise final schedule still fails closed.
                        deferredPathTimingReason ??= timingReason;
                    }
                    if (hasPathTiming)
                    {
                        nativeTravelGameMinutes = resolvedTravelGameMinutes;
                        if (arrivalTimeRequested)
                        {
                            time = ResolveArrivalDepartureTime(
                                time,
                                previousDepartureTime,
                                resolvedTravelGameMinutes);
                            arrivalTimeCount++;
                        }
                        scheduledArrivalTime = AddGameMinutes(
                            time,
                            resolvedTravelGameMinutes);
                        travelTimeCount++;
                    }
                }
                if (time != 0 && !usedTimes.Add(time))
                    return ParseBlocked(npc, selectedKey, currentKey, trace, "future_schedule_duplicate_time", entry.RawSha256);
                endpoints.Add(new NpcFutureScheduleEndpoint
                {
                    CommandIndex = index,
                    ScheduledDepartureTime = time,
                    ScheduleEntryOrdinal = time == 0 ? -1 : scheduleEntryOrdinal,
                    IsInitialPosition = time == 0,
                    ArrivalTimeRequested = arrivalTimeRequested,
                    RequestedArrivalTime = requestedArrivalTime,
                    NativeRoutePointCount = pathTiming?.NativeRoutePointCount,
                    NativeAdjacentRoutePixelDistance = pathTiming?.AdjacentRoutePixelDistance,
                    NativeTravelGameMinutes = nativeTravelGameMinutes,
                    ScheduledArrivalTime = scheduledArrivalTime,
                    LocationName = location,
                    TileX = tileX,
                    TileY = tileY,
                    FacingDirection = facing,
                    EndBehavior = endBehavior,
                    EndMessage = endMessage,
                    EndBehaviorComplete = behaviorComplete
                });
                previousLocation = location;
                if (time != 0)
                {
                    previousDepartureTime = time;
                    scheduleEntryOrdinal++;
                }
            }

            if (deferredPathTimingReason is not null)
            {
                return ParseBlocked(
                    npc,
                    selectedKey,
                    currentKey,
                    trace,
                    deferredPathTimingReason,
                    entry.RawSha256);
            }

            trace.Add("endpoints:" + endpoints.Count.ToString(CultureInfo.InvariantCulture));
            return new NpcFutureScheduleResolution
            {
                Status = NpcFutureScheduleResolutionStatus.Exact,
                NpcName = npc.Name,
                SelectedScheduleKey = selectedKey,
                ResolvedScheduleKey = currentKey,
                RawScheduleSha256 = entry.RawSha256,
                ProjectionScope = arrivalTimeCount > 0
                    ? "native_key_static_endpoints_and_evidenced_arrival_timing"
                    : "native_key_and_static_endpoints",
                NativePathRoutesResolved = false,
                ArrivalTimesResolved = arrivalTimeCount > 0,
                TravelTimesResolved = travelTimeCount > 0 &&
                    travelTimeCount == endpoints.Count(endpoint => !endpoint.IsInitialPosition),
                EndpointBehaviorsResolved = endpoints.All(endpoint => endpoint.EndBehaviorComplete),
                ResolutionTrace = trace.ToArray(),
                Endpoints = endpoints.ToArray()
            };
        }

        private static bool TryApplyLocationAccessibility(
            CatalogNpc npc,
            NpcFutureScheduleScenario scenario,
            ref string location,
            ref int tileX,
            ref int tileY,
            ref int facing,
            out string fallbackKey,
            out string reason)
        {
            fallbackKey = string.Empty;
            reason = string.Empty;
            if (location is not ("JojaMart" or "Railroad" or "CommunityCenter"))
                return true;
            if (scenario.LocationAccessibility is null ||
                !scenario.LocationAccessibility.TryGetValue(location, out var accessible))
            {
                reason = "future_schedule_location_accessibility_missing:" + location;
                return false;
            }
            if (accessible)
                return true;

            if (location is "JojaMart" or "Railroad")
            {
                var replacementKey = location + "_Replacement";
                if (npc.Entries.TryGetValue(replacementKey, out var replacement))
                {
                    var replacementTokens = SplitTokens(replacement.Raw);
                    if (replacementTokens.Length < 4 ||
                        !int.TryParse(replacementTokens[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out tileX) ||
                        !int.TryParse(replacementTokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out tileY) ||
                        !int.TryParse(replacementTokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out facing))
                    {
                        reason = "future_schedule_location_replacement_invalid:" + replacementKey;
                        return false;
                    }
                    location = replacementTokens[0];
                    return true;
                }
            }

            fallbackKey = npc.Entries.ContainsKey("default") ? "default" : "spring";
            if (!npc.Entries.ContainsKey(fallbackKey))
            {
                reason = "future_schedule_accessibility_fallback_missing";
                fallbackKey = string.Empty;
                return false;
            }
            return true;
        }

        private static void ResolveUnmarriedBedTarget(
            CatalogNpc npc,
            out string location,
            out int tileX,
            out int tileY)
        {
            location = npc.DefaultMap;
            tileX = npc.DefaultTileX;
            tileY = npc.DefaultTileY;
            var bedSourceKey = npc.Entries.ContainsKey("default") ? "default" : "spring";
            if (!npc.Entries.TryGetValue(bedSourceKey, out var bedSource))
                return;
            var commands = SplitCommands(bedSource.Raw);
            if (commands.Length == 0)
                return;
            var tokens = SplitTokens(commands[commands.Length - 1]);
            if (tokens.Length > 3 &&
                int.TryParse(tokens[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedX) &&
                int.TryParse(tokens[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedY))
            {
                location = tokens[1];
                tileX = parsedX;
                tileY = parsedY;
            }
        }

        private static void ReadEndpointTail(
            string rawCommand,
            string[] tokens,
            int tailIndex,
            out string behavior,
            out string message)
        {
            behavior = string.Empty;
            message = string.Empty;
            if (tailIndex >= tokens.Length)
                return;
            if (tokens[tailIndex].Length > 0 && tokens[tailIndex][0] == '"')
            {
                var quoteIndex = rawCommand.IndexOf('"');
                message = quoteIndex >= 0 ? rawCommand.Substring(quoteIndex) : string.Empty;
                return;
            }

            behavior = tokens[tailIndex];
            tailIndex++;
            if (tailIndex < tokens.Length && tokens[tailIndex].Length > 0 && tokens[tailIndex][0] == '"')
            {
                var quoteIndex = rawCommand.IndexOf('"');
                message = quoteIndex >= 0
                    ? rawCommand.Substring(quoteIndex).Replace("\"", string.Empty, StringComparison.Ordinal)
                    : string.Empty;
            }
        }

        private static bool TryReadControlTarget(string[] tokens, string control, out string target)
        {
            target = string.Empty;
            if (tokens.Length < 2 || tokens[0] != control || string.IsNullOrWhiteSpace(tokens[1]))
                return false;
            target = tokens[1];
            return true;
        }

        private static string[] SplitCommands(string raw) => raw
            .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(command => command.Trim())
            .Where(command => command.Length > 0)
            .ToArray();

        private static string[] SplitTokens(string command) => command
            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        private static NpcFutureScheduleResolution ParseBlocked(
            CatalogNpc npc,
            string selectedKey,
            string resolvedKey,
            IReadOnlyCollection<string> trace,
            string reason,
            string rawSha256 = "")
        {
            return new NpcFutureScheduleResolution
            {
                Status = NpcFutureScheduleResolutionStatus.Blocked,
                NpcName = npc.Name,
                SelectedScheduleKey = selectedKey,
                ResolvedScheduleKey = resolvedKey,
                RawScheduleSha256 = rawSha256,
                ProjectionScope = "fail_closed",
                ResolutionTrace = new List<string>(trace).ToArray(),
                BlockingReasons = new[] { reason }
            };
        }
    }
}
