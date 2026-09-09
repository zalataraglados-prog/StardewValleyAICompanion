using System;
using System.Collections.Generic;

namespace StardewAI.Core.Training
{
    public sealed partial class NpcFutureScheduleResolver
    {
        private enum ScheduleSelectionKind
        {
            Exact,
            Rain2BranchRequired,
            NoSchedule,
            Blocked
        }

        private sealed class ScheduleSelection
        {
            public ScheduleSelectionKind Kind { get; set; }

            public string Key { get; set; } = string.Empty;

            public string Reason { get; set; } = string.Empty;

            public List<string> Trace { get; set; } = new List<string>();
        }

        private static ScheduleSelection SelectSchedule(
            CatalogNpc npc,
            NpcFutureScheduleScenario scenario,
            bool? rain2Roll)
        {
            var trace = new List<string>();
            if (!npc.HasMasterSchedule)
                return NoSchedule(trace, "master_schedule_missing");

            if (scenario.IsGreenRain == true && scenario.Year == 1 &&
                TrySelect(npc, "GreenRain", trace, out var selection))
            {
                return selection;
            }

            if (!string.IsNullOrWhiteSpace(scenario.IslandScheduleName))
            {
                trace.Add("island_schedule:" + scenario.IslandScheduleName);
                return BlockedSelection(trace, "island_schedule_uses_runtime_schedule_instance_not_raw_catalog");
            }

            foreach (var festival in scenario.ActivePassiveFestivals!)
            {
                if (scenario.IsMarried == true)
                {
                    if (TrySelect(
                            npc,
                            "marriage_" + festival.FestivalId + "_" + festival.DayIndex,
                            trace,
                            out selection) ||
                        TrySelect(npc, "marriage_" + festival.FestivalId, trace, out selection))
                    {
                        return selection;
                    }
                }
                else if (TrySelect(
                             npc,
                             festival.FestivalId + "_" + festival.DayIndex,
                             trace,
                             out selection) ||
                         TrySelect(npc, festival.FestivalId, trace, out selection))
                {
                    return selection;
                }
            }

            var season = scenario.Season;
            var day = scenario.DayOfMonth!.Value;
            var weekday = scenario.Weekday;
            if (scenario.IsMarried == true)
            {
                if (TrySelect(npc, "marriage_" + season + "_" + day, trace, out selection))
                    return selection;

                var worksToday =
                    npc.Name == "Penny" && weekday is "Tue" or "Wed" or "Fri" ||
                    npc.Name == "Maru" && weekday is "Tue" or "Thu" ||
                    npc.Name == "Harvey" && weekday is "Tue" or "Thu";
                if (worksToday && TrySelect(npc, "marriageJob", trace, out selection))
                    return selection;
                if (!scenario.ValleyIsRaining.HasValue)
                    return BlockedSelection(trace, "future_schedule_valley_rain_state_missing_for_marriage");
                if (scenario.ValleyIsRaining == false &&
                    TrySelect(npc, "marriage_" + weekday, trace, out selection))
                {
                    return selection;
                }
                return NoSchedule(trace, "married_schedule_has_no_matching_native_key");
            }

            if (TrySelect(npc, season + "_" + day, trace, out selection))
                return selection;

            var friendshipHeartSelector = Math.Max(0, scenario.AllPlayerFriendshipPoints!.Value) / 250;
            for (var hearts = friendshipHeartSelector; hearts > 0; hearts -= 2)
            {
                if (TrySelect(npc, day + "_" + hearts, trace, out selection))
                    return selection;
            }
            if (TrySelect(npc, day.ToString(System.Globalization.CultureInfo.InvariantCulture), trace, out selection))
                return selection;

            if (npc.Name == "Pam")
            {
                if (scenario.CurrentPlayerMailReceived is null)
                    return BlockedSelection(trace, "future_schedule_current_player_mail_state_missing_for_pam_bus");
                if (scenario.CurrentPlayerMailReceived.Contains("ccVault") &&
                    TrySelect(npc, "bus", trace, out selection))
                {
                    return selection;
                }
            }

            if (!scenario.NpcLocationIsRaining.HasValue)
                return BlockedSelection(trace, "future_schedule_npc_location_rain_state_missing");
            if (scenario.NpcLocationIsRaining == true)
            {
                if (npc.Entries.ContainsKey("rain2"))
                {
                    if (!rain2Roll.HasValue)
                    {
                        trace.Add("conditional:rain2");
                        return new ScheduleSelection
                        {
                            Kind = ScheduleSelectionKind.Rain2BranchRequired,
                            Trace = trace
                        };
                    }
                    if (rain2Roll.Value && TrySelect(npc, "rain2", trace, out selection))
                        return selection;
                    if (!rain2Roll.Value)
                        trace.Add("rain2_roll:false");
                }
                if (TrySelect(npc, "rain", trace, out selection))
                    return selection;
            }

            for (var hearts = friendshipHeartSelector; hearts > 0; hearts -= 2)
            {
                if (TrySelect(npc, season + "_" + weekday + "_" + hearts, trace, out selection))
                    return selection;
            }
            if (TrySelect(npc, season + "_" + weekday, trace, out selection))
                return selection;
            for (var hearts = friendshipHeartSelector; hearts > 0; hearts -= 2)
            {
                if (TrySelect(npc, weekday + "_" + hearts, trace, out selection))
                    return selection;
            }
            if (TrySelect(npc, weekday, trace, out selection) ||
                TrySelect(npc, season, trace, out selection) ||
                TrySelect(npc, "spring_" + weekday, trace, out selection) ||
                TrySelect(npc, "spring", trace, out selection))
            {
                return selection;
            }

            return NoSchedule(trace, "no_native_schedule_key_matched");
        }

        private static bool TrySelect(
            CatalogNpc npc,
            string key,
            List<string> trace,
            out ScheduleSelection selection)
        {
            if (npc.Entries.ContainsKey(key))
            {
                trace.Add("selected:" + key);
                selection = new ScheduleSelection
                {
                    Kind = ScheduleSelectionKind.Exact,
                    Key = key,
                    Trace = trace
                };
                return true;
            }

            trace.Add("missing:" + key);
            selection = new ScheduleSelection();
            return false;
        }

        private static ScheduleSelection NoSchedule(List<string> trace, string reason)
        {
            trace.Add("no_schedule:" + reason);
            return new ScheduleSelection
            {
                Kind = ScheduleSelectionKind.NoSchedule,
                Reason = reason,
                Trace = trace
            };
        }

        private static ScheduleSelection BlockedSelection(List<string> trace, string reason)
        {
            trace.Add("blocked:" + reason);
            return new ScheduleSelection
            {
                Kind = ScheduleSelectionKind.Blocked,
                Reason = reason,
                Trace = trace
            };
        }
    }
}
