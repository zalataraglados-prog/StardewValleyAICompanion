using System;
using System.Collections.Generic;
using System.Text.Json;

namespace StardewAI.Core.Training
{
    public enum NpcFutureScheduleResolutionStatus
    {
        Exact,
        Conditional,
        NoSchedule,
        Blocked
    }

    public sealed class NpcPassiveFestivalScenario
    {
        public string FestivalId { get; set; } = string.Empty;

        public int DayIndex { get; set; }
    }

    public sealed class NpcSchedulePathTimingEvidence
    {
        public string ScheduleKey { get; set; } = string.Empty;

        public int ScheduleEntryOrdinal { get; set; }

        public string TargetLocationName { get; set; } = string.Empty;

        public int TargetTileX { get; set; }

        public int TargetTileY { get; set; }

        public int FacingDirection { get; set; }

        public int NativeRoutePointCount { get; set; }

        public int AdjacentRoutePixelDistance { get; set; }
    }

    public sealed class NpcFutureScheduleScenario
    {
        public int? Year { get; set; }

        public string Season { get; set; } = string.Empty;

        public int? DayOfMonth { get; set; }

        public string Weekday { get; set; } = string.Empty;

        public bool? IsGreenRain { get; set; }

        public bool? IsMarried { get; set; }

        public bool IslandScheduleStateComplete { get; set; }

        public string IslandScheduleName { get; set; } = string.Empty;

        public NpcPassiveFestivalScenario[]? ActivePassiveFestivals { get; set; }

        public int? AllPlayerFriendshipPoints { get; set; }

        public bool? ValleyIsRaining { get; set; }

        public bool? NpcLocationIsRaining { get; set; }

        public bool? Rain2Roll { get; set; }

        public ISet<string>? CurrentPlayerMailReceived { get; set; }

        public ISet<string>? MasterPlayerMailReceived { get; set; }

        public ISet<string>? WorldStateIds { get; set; }

        public IReadOnlyDictionary<string, int>? MaximumFarmerFriendshipHearts { get; set; }

        public IReadOnlyDictionary<string, bool>? LocationAccessibility { get; set; }

        public int? RealMillisecondsPerGameTenMinutes { get; set; }

        public NpcSchedulePathTimingEvidence[]? NativePathTimings { get; set; }

        public bool NativePathTimingEvidenceComplete { get; set; }
    }

    public sealed class NpcFutureScheduleEndpoint
    {
        public int CommandIndex { get; set; }

        public int ScheduledDepartureTime { get; set; }

        public int ScheduleEntryOrdinal { get; set; }

        public bool IsInitialPosition { get; set; }

        public bool ArrivalTimeRequested { get; set; }

        public int? RequestedArrivalTime { get; set; }

        public int? NativeRoutePointCount { get; set; }

        public int? NativeAdjacentRoutePixelDistance { get; set; }

        public int? NativeTravelGameMinutes { get; set; }

        public int? ScheduledArrivalTime { get; set; }

        public string LocationName { get; set; } = string.Empty;

        public int TileX { get; set; }

        public int TileY { get; set; }

        public int FacingDirection { get; set; }

        public string EndBehavior { get; set; } = string.Empty;

        public string EndMessage { get; set; } = string.Empty;

        public bool EndBehaviorComplete { get; set; } = true;
    }

    public sealed class NpcFutureScheduleResolution
    {
        public NpcFutureScheduleResolutionStatus Status { get; set; }

        public string Condition { get; set; } = string.Empty;

        public string NpcName { get; set; } = string.Empty;

        public string SelectedScheduleKey { get; set; } = string.Empty;

        public string ResolvedScheduleKey { get; set; } = string.Empty;

        public string RawScheduleSha256 { get; set; } = string.Empty;

        public string ProjectionScope { get; set; } = "native_key_and_static_endpoints";

        public bool NativePathRoutesResolved { get; set; }

        public bool ArrivalTimesResolved { get; set; }

        public bool TravelTimesResolved { get; set; }

        public bool EndpointBehaviorsResolved { get; set; }

        public string[] ResolutionTrace { get; set; } = Array.Empty<string>();

        public NpcFutureScheduleEndpoint[] Endpoints { get; set; } = Array.Empty<NpcFutureScheduleEndpoint>();

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public NpcFutureScheduleResolution[] Alternatives { get; set; } = Array.Empty<NpcFutureScheduleResolution>();
    }

    public sealed partial class NpcFutureScheduleResolver
    {
        public NpcFutureScheduleResolution Resolve(
            JsonElement scheduleCatalog,
            string npcName,
            NpcFutureScheduleScenario scenario)
        {
            if (string.IsNullOrWhiteSpace(npcName))
                return Blocked(npcName, "npc_name_missing");
            if (scenario is null)
                return Blocked(npcName, "future_schedule_scenario_missing");
            if (!TryReadCatalog(scheduleCatalog, npcName, out var catalogNpc, out var catalogReason))
                return Blocked(npcName, catalogReason);
            if (!TryValidateScenario(scenario, out var scenarioReason))
                return Blocked(npcName, scenarioReason);

            var selection = SelectSchedule(catalogNpc, scenario, scenario.Rain2Roll);
            if (selection.Kind != ScheduleSelectionKind.Rain2BranchRequired)
                return ResolveSelection(catalogNpc, scenario, selection);

            var rain2 = ResolveSelection(catalogNpc, scenario, SelectSchedule(catalogNpc, scenario, true));
            rain2.Condition = "rain2_roll=true";
            var ordinaryRain = ResolveSelection(catalogNpc, scenario, SelectSchedule(catalogNpc, scenario, false));
            ordinaryRain.Condition = "rain2_roll=false";
            return new NpcFutureScheduleResolution
            {
                Status = NpcFutureScheduleResolutionStatus.Conditional,
                NpcName = npcName,
                ProjectionScope = "conditional_native_key_and_static_endpoints",
                ResolutionTrace = selection.Trace.ToArray(),
                BlockingReasons = new[] { "rain2_random_branch_unknown" },
                Alternatives = new[] { rain2, ordinaryRain }
            };
        }

        private NpcFutureScheduleResolution ResolveSelection(
            CatalogNpc catalogNpc,
            NpcFutureScheduleScenario scenario,
            ScheduleSelection selection)
        {
            if (selection.Kind == ScheduleSelectionKind.Blocked)
                return Blocked(catalogNpc.Name, selection.Reason, selection.Trace);
            if (selection.Kind == ScheduleSelectionKind.NoSchedule)
            {
                return new NpcFutureScheduleResolution
                {
                    Status = NpcFutureScheduleResolutionStatus.NoSchedule,
                    NpcName = catalogNpc.Name,
                    ProjectionScope = "native_key_selection",
                    ResolutionTrace = selection.Trace.ToArray()
                };
            }

            return ParseSelectedSchedule(catalogNpc, scenario, selection.Key, selection.Trace);
        }

        private static bool TryValidateScenario(NpcFutureScheduleScenario scenario, out string reason)
        {
            reason = string.Empty;
            if (!scenario.Year.HasValue || scenario.Year.Value < 1)
                reason = "future_schedule_year_missing_or_invalid";
            else if (scenario.Season is not ("spring" or "summer" or "fall" or "winter"))
                reason = "future_schedule_season_missing_or_invalid";
            else if (!scenario.DayOfMonth.HasValue || scenario.DayOfMonth.Value < 1 || scenario.DayOfMonth.Value > 28)
                reason = "future_schedule_day_missing_or_invalid";
            else if (scenario.Weekday is not ("Mon" or "Tue" or "Wed" or "Thu" or "Fri" or "Sat" or "Sun"))
                reason = "future_schedule_weekday_missing_or_invalid";
            else if (!scenario.IsGreenRain.HasValue)
                reason = "future_schedule_green_rain_state_missing";
            else if (!scenario.IsMarried.HasValue)
                reason = "future_schedule_marriage_state_missing";
            else if (!scenario.IslandScheduleStateComplete)
                reason = "future_schedule_island_assignment_state_missing";
            else if (scenario.ActivePassiveFestivals is null)
                reason = "future_schedule_passive_festival_state_missing";
            else if (scenario.AllPlayerFriendshipPoints is null)
                reason = "future_schedule_all_player_friendship_missing";
            else
            {
                foreach (var festival in scenario.ActivePassiveFestivals)
                {
                    if (festival is null || string.IsNullOrWhiteSpace(festival.FestivalId) || festival.DayIndex < 1)
                    {
                        reason = "future_schedule_passive_festival_entry_invalid";
                        break;
                    }
                }
            }

            return reason.Length == 0;
        }

        private static NpcFutureScheduleResolution Blocked(
            string npcName,
            string reason,
            IReadOnlyCollection<string>? trace = null)
        {
            return new NpcFutureScheduleResolution
            {
                Status = NpcFutureScheduleResolutionStatus.Blocked,
                NpcName = npcName ?? string.Empty,
                ProjectionScope = "fail_closed",
                ResolutionTrace = trace is null ? Array.Empty<string>() : new List<string>(trace).ToArray(),
                BlockingReasons = new[] { reason }
            };
        }
    }
}
