using System;
using System.Linq;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public static class ScheduledRouteWaitPolicy
{
    public static bool IsDeferredFestivalTransit(PolicyEventCandidatePrediction candidate) =>
        IsDeferredNativeGate(candidate) && candidate.BlockReasons.All(reason =>
            string.Equals(reason, "connector_active_festival_transit_closed", StringComparison.Ordinal));

    public static bool IsDeferredNativeGate(PolicyEventCandidatePrediction candidate) =>
        !candidate.Available && candidate.Kind == "route_connector_tile" && candidate.AllowedNow == false &&
        candidate.AllowedToday == true && candidate.TimelineStatus == "deferred" && candidate.ScheduledStartTime.HasValue &&
        candidate.ScheduledWaitCost is > 0 && candidate.BlockReasons is { Length: > 0 } reasons &&
        reasons.All(reason => reason is "connector_active_festival_transit_closed" or "connector_gate_closed");
}
