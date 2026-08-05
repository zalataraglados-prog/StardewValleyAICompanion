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
            return ShopObjectiveStageCandidates(
                snapshot,
                previews,
                "purchase",
                PurchaseContinuationParameters);
        }

        private EventCandidate[] ShopObjectiveStageCandidates(
            SnapshotEnvelope snapshot,
            EconomicCandidate[] previews,
            string objectivePrefix,
            Func<EconomicCandidate, string, SmallModelActionParameter[]> continuationFactory)
        {
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
                    .Select(candidate => BlockedShopObjectiveStageCandidate(
                        candidate,
                        objectivePrefix,
                        objectivePrefix + "_shop_endpoint_graph_unavailable"))
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
                    results.Add(BlockedShopObjectiveStageCandidate(
                        preview,
                        objectivePrefix,
                        objectivePrefix + "_shop_endpoint_binding_missing"));
                    continue;
                }

                foreach (var endpoint in endpoints)
                {
                    results.Add(BuildShopObjectiveStageCandidate(
                        snapshot,
                        preview,
                        endpoint,
                        graphEdges,
                        currentLocation,
                        routeCandidates,
                        interactionCandidates,
                        objectivePrefix,
                        continuationFactory(preview, ReadString(endpoint, "from_location"))));
                }
            }

            return results
                .GroupBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToArray();
        }

        private EventCandidate BuildShopObjectiveStageCandidate(
            SnapshotEnvelope snapshot,
            EconomicCandidate preview,
            JsonElement endpoint,
            JsonElement[] graphEdges,
            string currentLocation,
            EventCandidate[] routeCandidates,
            EventCandidate[] interactionCandidates,
            string objectivePrefix,
            SmallModelActionParameter[] continuation)
        {
            var targetLocation = ReadString(endpoint, "from_location");
            var endpointX = ReadInt(endpoint, "from_x");
            var endpointY = ReadInt(endpoint, "from_y");
            var gate = PurchaseServiceGate(
                snapshot,
                endpoint,
                graphEdges,
                targetLocation);

            if (!gate.AllowedNow)
            {
                return new EventCandidate
                {
                    CandidateId = ShopObjectiveCandidateId(
                        preview,
                        objectivePrefix,
                        "gate",
                        targetLocation,
                        endpointX,
                        endpointY),
                    Kind = objectivePrefix + "_service_gate",
                    Available = false,
                    LocationId = targetLocation,
                    TileX = endpointX,
                    TileY = endpointY,
                    ExpectedEffect = objectivePrefix + "_service_gate_checked_upstream=true",
                    ItemId = preview.ItemId,
                    QualifiedItemId = preview.QualifiedItemId,
                    DisplayName = preview.DisplayName,
                    SlotIndex = preview.SlotIndex,
                    Quantity = preview.Quantity,
                    ShopId = preview.ShopId,
                    UnitPrice = preview.UnitPrice,
                    TotalValue = preview.TotalValue,
                    AvailabilityClass = objectivePrefix + "_service_unavailable",
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
                    return BlockedShopObjectiveStageCandidate(
                        preview,
                        objectivePrefix,
                        objectivePrefix + "_current_shop_endpoint_not_rebound",
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
                    CandidateId = ShopObjectiveCandidateId(
                        preview,
                        objectivePrefix,
                        "interact",
                        targetLocation,
                        endpointX,
                        endpointY),
                    Kind = "interact_endpoint",
                    Available = preview.Available && interaction.Available && reasons.Length == 0,
                    LocationId = interaction.LocationId,
                    TileX = interaction.TileX,
                    TileY = interaction.TileY,
                    ExpectedEffect = interaction.ExpectedEffect + ";" +
                        objectivePrefix + "_target_shop_id=" + preview.ShopId + ";" +
                        objectivePrefix + "_target_qualified_item_id=" + preview.QualifiedItemId +
                        ";fresh_snapshot_after_shop_menu_open=true",
                    ItemId = preview.ItemId,
                    QualifiedItemId = preview.QualifiedItemId,
                    DisplayName = preview.DisplayName,
                    SlotIndex = preview.SlotIndex,
                    Quantity = preview.Quantity,
                    ShopId = preview.ShopId,
                    UnitPrice = preview.UnitPrice,
                    TotalValue = preview.TotalValue,
                    EstimatedTicks = interaction.EstimatedTicks,
                    EnergyCost = 0,
                    AvailabilityClass = objectivePrefix + "_shop_endpoint_interaction",
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
                return BlockedShopObjectiveStageCandidate(
                    preview,
                    objectivePrefix,
                    objectivePrefix + "_cross_map_route_unavailable",
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
                CandidateId = ShopObjectiveCandidateId(
                    preview,
                    objectivePrefix,
                    "route",
                    currentLocation,
                    route.TileX,
                    route.TileY),
                Kind = "route_connector_tile",
                Available = preview.Available && route.Available && routeReasons.Length == 0,
                LocationId = currentLocation,
                TileX = route.TileX,
                TileY = route.TileY,
                ExpectedEffect = objectivePrefix + "_route_target_location=" + targetLocation + ";" +
                    objectivePrefix + "_target_shop_id=" + preview.ShopId + ";" +
                    objectivePrefix + "_target_qualified_item_id=" + preview.QualifiedItemId +
                    ";one_connector_then_fresh_snapshot=true",
                ItemId = preview.ItemId,
                QualifiedItemId = preview.QualifiedItemId,
                DisplayName = preview.DisplayName,
                SlotIndex = preview.SlotIndex,
                Quantity = preview.Quantity,
                ShopId = preview.ShopId,
                UnitPrice = preview.UnitPrice,
                TotalValue = preview.TotalValue,
                EstimatedTicks = route.EstimatedTicks,
                EnergyCost = 0,
                AvailabilityClass = objectivePrefix + "_cross_map_route_step",
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
                            objectivePrefix + "_route.remaining_connector_count",
                            (routePlan?.Path.Length ?? 0).ToString(
                                CultureInfo.InvariantCulture)),
                        Parameter(
                            objectivePrefix + "_route.snapshot_policy",
                            "fresh_snapshot_after_each_connector_and_shop_open")
                    })
                    .ToArray()
            };
        }

    }
}
