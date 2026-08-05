using System;
using System.Collections.Generic;
using System.Globalization;
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
        private EventCandidate[] BuySupplyStageCandidates(
            SnapshotEnvelope snapshot,
            SmallModelActionParameter[] boundParameters)
        {
            if (ActiveShopMenuOpen(snapshot))
            {
                return Array.Empty<EventCandidate>();
            }

            var previews = BuyCandidatesFromShopPreview(snapshot)
                .Where(candidate => PurchaseIdentityMatches(candidate, boundParameters))
                .ToArray();
            if (previews.Length == 0)
            {
                return Array.Empty<EventCandidate>();
            }

            var graph = ReadStateFieldValue(snapshot, "locations", "route_graph");
            if (!graph.HasValue ||
                graph.Value.ValueKind != JsonValueKind.Object ||
                !graph.Value.TryGetProperty("edges", out var edges) ||
                edges.ValueKind != JsonValueKind.Array)
            {
                return previews
                    .Select(candidate => BlockedPurchaseStageCandidate(
                        candidate,
                        "purchase_shop_endpoint_graph_unavailable"))
                    .ToArray();
            }

            var graphEdges = edges.EnumerateArray()
                .Where(edge => edge.ValueKind == JsonValueKind.Object)
                .ToArray();
            var currentLocation = ReadStateFieldString(
                snapshot,
                "player",
                "location_id");
            var routeCandidates = RouteConnectorCandidates(snapshot, int.MaxValue);
            var interactionCandidates = InteractEndpointCandidates(snapshot);
            var results = new List<EventCandidate>();

            foreach (var preview in previews)
            {
                var endpoints = graphEdges
                    .Where(edge => string.Equals(
                        ReadString(edge, "kind"),
                        "shop_endpoint",
                        StringComparison.OrdinalIgnoreCase))
                    .Where(edge => string.Equals(
                        ReadString(edge, "shop_id"),
                        preview.ShopId,
                        StringComparison.OrdinalIgnoreCase))
                    .OrderBy(edge => ReadString(edge, "from_location"), StringComparer.Ordinal)
                    .ThenBy(edge => ReadInt(edge, "from_y"))
                    .ThenBy(edge => ReadInt(edge, "from_x"))
                    .ToArray();
                if (endpoints.Length == 0)
                {
                    results.Add(BlockedPurchaseStageCandidate(
                        preview,
                        "purchase_shop_endpoint_binding_missing"));
                    continue;
                }

                foreach (var endpoint in endpoints)
                {
                    results.Add(BuildPurchaseStageCandidate(
                        snapshot,
                        preview,
                        endpoint,
                        graphEdges,
                        currentLocation,
                        routeCandidates,
                        interactionCandidates));
                }
            }

            return results
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
        }

        private EventCandidate BuildPurchaseStageCandidate(
            SnapshotEnvelope snapshot,
            EconomicCandidate preview,
            JsonElement endpoint,
            JsonElement[] graphEdges,
            string currentLocation,
            EventCandidate[] routeCandidates,
            EventCandidate[] interactionCandidates)
        {
            var targetLocation = ReadString(endpoint, "from_location");
            var endpointX = ReadInt(endpoint, "from_x");
            var endpointY = ReadInt(endpoint, "from_y");
            var gate = PurchaseServiceGate(
                snapshot,
                endpoint,
                graphEdges,
                targetLocation);
            var continuation = PurchaseContinuationParameters(
                preview,
                targetLocation);

            if (!gate.AllowedNow)
            {
                return new EventCandidate
                {
                    CandidateId = PurchaseCandidateId(
                        preview,
                        "gate",
                        targetLocation,
                        endpointX,
                        endpointY),
                    Kind = "purchase_service_gate",
                    Available = false,
                    LocationId = targetLocation,
                    TileX = endpointX,
                    TileY = endpointY,
                    ExpectedEffect = "purchase_service_gate_checked_upstream=true",
                    ItemId = preview.ItemId,
                    QualifiedItemId = preview.QualifiedItemId,
                    DisplayName = preview.DisplayName,
                    Quantity = 1,
                    ShopId = preview.ShopId,
                    UnitPrice = preview.UnitPrice,
                    TotalValue = preview.UnitPrice,
                    AvailabilityClass = "purchase_service_unavailable",
                    AllowedNow = false,
                    AllowedToday = false,
                    EffectiveOpenTime = gate.OpenTime,
                    ClosesAt = gate.CloseTime,
                    GateReasons = gate.BlockReasons,
                    BlockReasons = preview.BlockReasons
                        .Concat(gate.BlockReasons)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray(),
                    Parameters = continuation
                };
            }

            if (string.Equals(
                    currentLocation,
                    targetLocation,
                    StringComparison.OrdinalIgnoreCase))
            {
                var interaction = interactionCandidates.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.ShopId,
                        preview.ShopId,
                        StringComparison.OrdinalIgnoreCase) &&
                    candidate.TileX == endpointX &&
                    candidate.TileY == endpointY);
                if (interaction is null)
                {
                    return BlockedPurchaseStageCandidate(
                        preview,
                        "purchase_current_shop_endpoint_not_rebound",
                        targetLocation,
                        endpointX,
                        endpointY,
                        continuation);
                }

                var reasons = preview.BlockReasons
                    .Concat(interaction.BlockReasons)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
                return new EventCandidate
                {
                    CandidateId = PurchaseCandidateId(
                        preview,
                        "interact",
                        targetLocation,
                        endpointX,
                        endpointY),
                    Kind = "interact_endpoint",
                    Available = preview.Available && interaction.Available && reasons.Length == 0,
                    LocationId = interaction.LocationId,
                    TileX = interaction.TileX,
                    TileY = interaction.TileY,
                    ExpectedEffect = interaction.ExpectedEffect +
                        ";purchase_target_shop_id=" + preview.ShopId +
                        ";purchase_target_qualified_item_id=" + preview.QualifiedItemId +
                        ";fresh_snapshot_after_shop_menu_open=true",
                    ItemId = preview.ItemId,
                    QualifiedItemId = preview.QualifiedItemId,
                    DisplayName = preview.DisplayName,
                    Quantity = 1,
                    ShopId = preview.ShopId,
                    UnitPrice = preview.UnitPrice,
                    TotalValue = preview.UnitPrice,
                    EstimatedTicks = interaction.EstimatedTicks,
                    EnergyCost = 0,
                    AvailabilityClass = "purchase_shop_endpoint_interaction",
                    AllowedNow = interaction.AllowedNow,
                    AllowedToday = interaction.AllowedToday,
                    NextOpenTime = interaction.NextOpenTime,
                    EffectiveOpenTime = interaction.EffectiveOpenTime,
                    ClosesAt = interaction.ClosesAt,
                    WaitCost = interaction.WaitCost,
                    GateReasons = interaction.GateReasons,
                    BlockReasons = reasons,
                    Parameters = interaction.Parameters
                        .Concat(continuation)
                        .ToArray()
                };
            }

            var routePlan = FindResolvedRoutePlan(
                snapshot,
                currentLocation,
                targetLocation,
                routeCandidates);
            var route = routePlan?.FirstConnectorCandidate;
            if (route is null)
            {
                return BlockedPurchaseStageCandidate(
                    preview,
                    "purchase_cross_map_route_unavailable",
                    targetLocation,
                    endpointX,
                    endpointY,
                    continuation);
            }

            var routeReasons = preview.BlockReasons
                .Concat(route.BlockReasons)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            return new EventCandidate
            {
                CandidateId = PurchaseCandidateId(
                    preview,
                    "route",
                    currentLocation,
                    route.TileX,
                    route.TileY),
                Kind = "route_connector_tile",
                Available = preview.Available && route.Available && routeReasons.Length == 0,
                LocationId = currentLocation,
                TileX = route.TileX,
                TileY = route.TileY,
                ExpectedEffect = "purchase_route_target_location=" + targetLocation +
                    ";purchase_target_shop_id=" + preview.ShopId +
                    ";purchase_target_qualified_item_id=" + preview.QualifiedItemId +
                    ";one_connector_then_fresh_snapshot=true",
                ItemId = preview.ItemId,
                QualifiedItemId = preview.QualifiedItemId,
                DisplayName = preview.DisplayName,
                Quantity = 1,
                ShopId = preview.ShopId,
                UnitPrice = preview.UnitPrice,
                TotalValue = preview.UnitPrice,
                EstimatedTicks = route.EstimatedTicks,
                EnergyCost = 0,
                AvailabilityClass = "purchase_cross_map_route_step",
                AllowedNow = route.AllowedNow,
                AllowedToday = route.AllowedToday,
                NextOpenTime = route.NextOpenTime,
                EffectiveOpenTime = gate.OpenTime,
                ClosesAt = gate.CloseTime,
                WaitCost = route.WaitCost,
                GateReasons = route.GateReasons,
                BlockReasons = routeReasons,
                Parameters = route.Parameters
                    .Concat(continuation)
                    .Concat(new[]
                    {
                        Parameter(
                            "purchase_route.remaining_connector_count",
                            (routePlan?.Path.Length ?? 0).ToString(
                                CultureInfo.InvariantCulture)),
                        Parameter(
                            "purchase_route.snapshot_policy",
                            "fresh_snapshot_after_each_connector_and_shop_open")
                    })
                    .ToArray()
            };
        }

    }
}
