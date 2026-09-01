using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] MachineRemoteRouteCandidates(
            SnapshotEnvelope snapshot,
            JsonElement machine,
            string machineLocation,
            int x,
            int y,
            string outputQualifiedId,
            string outputItemId,
            int outputStack,
            EventCandidate[] routeCandidates,
            string playerLocation)
        {
            var candidates = new List<EventCandidate>();
            if (MachineUsesIncubatorCompletion(machine))
            {
                return candidates.ToArray();
            }

            if (ReadBool(machine, "ready_for_harvest") == true &&
                (!string.IsNullOrWhiteSpace(outputQualifiedId) || !string.IsNullOrWhiteSpace(outputItemId)))
            {
                candidates.Add(MachineRemoteRouteCandidate(snapshot, "executor.collect_machine_output", machineLocation, x, y,
                    outputQualifiedId, outputItemId, outputStack, routeCandidates, playerLocation));
            }

            if (ReadInt(machine, "minutes_until_ready") <= 0 &&
                ReadBool(machine, "ready_for_harvest") != true &&
                ReadBool(machine, "machine_has_input") == true)
            {
                candidates.Add(MachineRemoteRouteCandidate(snapshot, "executor.load_machine_input", machineLocation, x, y,
                    string.Empty, string.Empty, 0, routeCandidates, playerLocation));
            }

            return candidates.ToArray();
        }

        private EventCandidate MachineRemoteRouteCandidate(
            SnapshotEnvelope snapshot,
            string continuationOptionId,
            string machineLocation,
            int machineX,
            int machineY,
            string qualifiedItemId,
            string itemId,
            int quantity,
            EventCandidate[] routeCandidates,
            string playerLocation)
        {
            var routePlan = FindResolvedRoutePlan(snapshot, playerLocation, machineLocation, routeCandidates);
            var routeCandidate = routePlan?.FirstActionCandidate;
            var reasons = routeCandidate is null
                ? new[] { "machine_cross_map_route_unavailable" }
                : routeCandidate.BlockReasons.Distinct(StringComparer.Ordinal).ToArray();
            var routeParameters = routeCandidate?.Parameters ?? Array.Empty<SmallModelActionParameter>();
            var continuationParameters = new[]
            {
                Parameter("continuation.option_id", continuationOptionId),
                Parameter("continuation.machine_location_id", machineLocation),
                Parameter("continuation.machine_tile_x", machineX.ToString()),
                Parameter("continuation.machine_tile_y", machineY.ToString()),
                Parameter("machine_route.remaining_connector_count", (routePlan?.Path.Length ?? 0).ToString()),
                Parameter("machine_route.snapshot_policy", "fresh_snapshot_after_each_connector")
            };
            return new EventCandidate
            {
                CandidateId = "machine-route:" + continuationOptionId + ":" + machineLocation + ":" + machineX + "," + machineY + ":via:" +
                    (routeCandidate?.TileX?.ToString() ?? "none") + "," + (routeCandidate?.TileY?.ToString() ?? "none"),
                Kind = "route_connector_tile",
                Available = routeCandidate is not null && reasons.Length == 0,
                LocationId = playerLocation,
                TileX = routeCandidate?.TileX,
                TileY = routeCandidate?.TileY,
                ExpectedEffect = "machine_route_target_location=" + machineLocation +
                    ";machine_route_target_tile=" + machineX + "," + machineY +
                    ";continuation_option_id=" + continuationOptionId +
                    ";one_connector_then_fresh_snapshot=true",
                ItemId = itemId,
                QualifiedItemId = qualifiedItemId,
                Quantity = quantity,
                EstimatedTicks = routeCandidate?.EstimatedTicks ?? -1,
                EnergyCost = 0,
                AvailabilityClass = routeCandidate is not null && reasons.Length == 0
                    ? "transparent_machine_cross_map_route_step"
                    : routeCandidate?.AllowedToday == true
                        ? "transparent_machine_cross_map_route_deferred"
                        : "transparent_machine_cross_map_route_blocked",
                AllowedNow = routeCandidate?.AllowedNow,
                AllowedToday = routeCandidate?.AllowedToday,
                NextOpenTime = routeCandidate?.NextOpenTime,
                EffectiveOpenTime = routeCandidate?.EffectiveOpenTime,
                ClosesAt = routeCandidate?.ClosesAt,
                WaitCost = routeCandidate?.WaitCost,
                GateReasons = routeCandidate?.GateReasons ?? Array.Empty<string>(),
                BlockReasons = reasons,
                Parameters = routeParameters.Concat(continuationParameters).ToArray()
            };
        }
    }
}
