using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.OptionRegistry
{
    public sealed partial class CandidateOptionAvailabilityEvaluator
    {
        private EventCandidate[] ShipItemStageCandidates(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] boundParameters)
        {
            var physicalCandidates = ShipCandidates(snapshot)
                .Where(candidate => ShippingIdentityMatches(candidate, boundParameters))
                .ToArray();
            if (physicalCandidates.Length == 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            if (!string.Equals(currentLocation, "Farm", StringComparison.OrdinalIgnoreCase))
            {
                var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue)
                    .Where(candidate => candidate.Kind == "route_connector_tile")
                    .ToArray();
                var routePlan = FindResolvedRoutePlan(
                    snapshot,
                    currentLocation,
                    "Farm",
                    routeCandidates);
                return physicalCandidates
                    .Select(candidate => ShippingRouteStageCandidate(
                        candidate,
                        currentLocation,
                        routePlan))
                    .ToArray();
            }

            var playerX = ReadStateFieldInt(snapshot, "player", "tile_x");
            var playerY = ReadStateFieldInt(snapshot, "player", "tile_y");
            return physicalCandidates
                .Select(candidate => ShippingFarmStageCandidate(
                    candidate,
                    playerX,
                    playerY))
                .ToArray();
        }

        private static EventCandidate ShippingRouteStageCandidate(
            EventCandidate physical,
            string currentLocation,
            ResolvedRoutePlan? routePlan)
        {
            var route = routePlan?.FirstActionCandidate;
            var continuation = ShippingContinuationParameters(physical);
            var reasons = physical.BlockReasons
                .Concat(route?.BlockReasons ?? new[] { "shipping_cross_map_route_unavailable" })
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var routeParameters = route?.Parameters ?? Array.Empty<SmallModelActionParameter>();
            return new EventCandidate
            {
                CandidateId = physical.CandidateId + ":route:" + currentLocation + ":" +
                    (route?.TileX?.ToString(CultureInfo.InvariantCulture) ?? "none") + "," +
                    (route?.TileY?.ToString(CultureInfo.InvariantCulture) ?? "none"),
                Kind = "route_connector_tile",
                Available = physical.Available && route is not null && route.Available && reasons.Length == 0,
                LocationId = currentLocation,
                TileX = route?.TileX,
                TileY = route?.TileY,
                ExpectedEffect = "shipping_route_target_location=Farm" +
                    ";shipping_target_qualified_item_id=" + physical.QualifiedItemId +
                    ";one_connector_then_fresh_snapshot=true",
                ItemId = physical.ItemId,
                QualifiedItemId = physical.QualifiedItemId,
                DisplayName = physical.DisplayName,
                SlotIndex = physical.SlotIndex,
                Quantity = physical.Quantity,
                ShopId = physical.ShopId,
                UnitPrice = physical.UnitPrice,
                TotalValue = physical.TotalValue,
                EstimatedTicks = route?.EstimatedTicks ?? -1,
                EnergyCost = 0,
                AvailabilityClass = route is null
                    ? "shipping_cross_map_route_blocked"
                    : "shipping_cross_map_route_step",
                AllowedNow = route?.AllowedNow,
                AllowedToday = route?.AllowedToday,
                NextOpenTime = route?.NextOpenTime,
                EffectiveOpenTime = route?.EffectiveOpenTime,
                ClosesAt = route?.ClosesAt,
                WaitCost = route?.WaitCost,
                GateReasons = route?.GateReasons ?? Array.Empty<string>(),
                BlockReasons = reasons,
                Parameters = routeParameters
                    .Concat(continuation)
                    .Concat(new[]
                    {
                        Parameter(
                            "shipping_route.remaining_connector_count",
                            (routePlan?.Path.Length ?? 0).ToString(CultureInfo.InvariantCulture)),
                        Parameter(
                            "shipping_route.snapshot_policy",
                            "fresh_snapshot_after_each_connector_and_bin_approach")
                    })
                    .ToArray(),
                FullShipmentKnown = physical.FullShipmentKnown,
                FullShipmentEligible = physical.FullShipmentEligible,
                FullShipmentCurrentShippedCount = physical.FullShipmentCurrentShippedCount,
                FullShipmentAlreadyShipped = physical.FullShipmentAlreadyShipped,
                FullShipmentContributes = physical.FullShipmentContributes,
                AvailableStack = physical.AvailableStack
            };
        }

        private static EventCandidate ShippingFarmStageCandidate(
            EventCandidate physical,
            int playerX,
            int playerY)
        {
            var standX = ReadParameterInt(physical.Parameters, "route_stand_tile_x");
            var standY = ReadParameterInt(physical.Parameters, "route_stand_tile_y");
            var atStand = standX.HasValue && standY.HasValue &&
                playerX == standX.Value && playerY == standY.Value;
            var stage = atStand ? "deposit" : "approach";
            return CloneCandidate(
                physical,
                candidateId: physical.CandidateId + ":" + stage,
                expectedEffect: physical.ExpectedEffect +
                    ";shipping_stage=" + stage +
                    ";fresh_snapshot_after_stage=true",
                parameters: physical.Parameters
                    .Concat(ShippingContinuationParameters(physical))
                    .Concat(new[]
                    {
                        Parameter("shipping_stage", stage),
                        Parameter(
                            "shipping_route.snapshot_policy",
                            "fresh_snapshot_after_each_connector_and_bin_approach")
                    })
                    .ToArray());
        }

        private static bool ShippingIdentityMatches(
            EventCandidate candidate,
            SmallModelActionParameter[] boundParameters)
        {
            var qualifiedItemId = ReadParameter(
                boundParameters,
                "continuation.qualified_item_id");
            var slotIndex = ReadParameterInt(
                boundParameters,
                "continuation.slot_index");
            var quantity = ReadParameterInt(
                boundParameters,
                "continuation.quantity");
            var expectedUnitPrice = ReadParameterInt(
                boundParameters,
                "continuation.expected_unit_price");
            var binLocation = ReadParameter(
                boundParameters,
                "continuation.bin_location");
            var binTileX = ReadParameterInt(
                boundParameters,
                "continuation.bin_tile_x");
            var binTileY = ReadParameterInt(
                boundParameters,
                "continuation.bin_tile_y");
            return (string.IsNullOrWhiteSpace(qualifiedItemId) ||
                    string.Equals(candidate.QualifiedItemId, qualifiedItemId, StringComparison.Ordinal)) &&
                (!slotIndex.HasValue || candidate.SlotIndex == slotIndex) &&
                (!quantity.HasValue || candidate.Quantity == quantity) &&
                (!expectedUnitPrice.HasValue || candidate.UnitPrice == expectedUnitPrice) &&
                (string.IsNullOrWhiteSpace(binLocation) ||
                    string.Equals(
                        ReadParameter(candidate.Parameters, "bin_location"),
                        binLocation,
                        StringComparison.OrdinalIgnoreCase)) &&
                (!binTileX.HasValue ||
                    ReadParameterInt(candidate.Parameters, "bin_tile_x") == binTileX) &&
                (!binTileY.HasValue ||
                    ReadParameterInt(candidate.Parameters, "bin_tile_y") == binTileY);
        }

        private static SmallModelActionParameter[] ShippingContinuationParameters(
            EventCandidate candidate)
        {
            return new[]
            {
                Parameter("continuation.option_id", "economy.ship_items"),
                Parameter("continuation.target_location", "Farm"),
                Parameter("continuation.item_id", candidate.ItemId),
                Parameter("continuation.qualified_item_id", candidate.QualifiedItemId),
                Parameter(
                    "continuation.slot_index",
                    candidate.SlotIndex?.ToString(CultureInfo.InvariantCulture) ?? string.Empty),
                Parameter(
                    "continuation.quantity",
                    candidate.Quantity.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "continuation.expected_unit_price",
                    candidate.UnitPrice.ToString(CultureInfo.InvariantCulture)),
                Parameter(
                    "continuation.bin_location",
                    ReadParameter(candidate.Parameters, "bin_location")),
                Parameter(
                    "continuation.bin_tile_x",
                    ReadParameter(candidate.Parameters, "bin_tile_x")),
                Parameter(
                    "continuation.bin_tile_y",
                    ReadParameter(candidate.Parameters, "bin_tile_y")),
                Parameter(
                    "continuation.stand_tile_x",
                    ReadParameter(candidate.Parameters, "route_stand_tile_x")),
                Parameter(
                    "continuation.stand_tile_y",
                    ReadParameter(candidate.Parameters, "route_stand_tile_y"))
            };
        }
    }
}
