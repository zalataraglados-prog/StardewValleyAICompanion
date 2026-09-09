using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Training
{
    public enum FutureRouteDateEvidenceProductionStatus
    {
        Produced,
        Blocked
    }

    public sealed class FutureRouteDateEvidenceRequest
    {
        public int TotalDays { get; set; }

        public string StartLocation { get; set; } = string.Empty;

        public int StartTileX { get; set; }

        public int StartTileY { get; set; }

        public int EarliestDepartureTime { get; set; }

        public string TargetLocation { get; set; } = string.Empty;

        public int TargetTileX { get; set; }

        public int TargetTileY { get; set; }
    }

    public sealed class FutureRouteDateEvidenceProduction
    {
        public FutureRouteDateEvidenceProductionStatus Status { get; set; }

        public FutureRouteAccessScenario? Scenario { get; set; }

        public int SuccessfulRouteVariantCount { get; set; }

        public string[] BlockingReasons { get; set; } = Array.Empty<string>();

        public string Scope { get; set; } =
            "current_date_native_static_topology_gate_and_explicit_timing_evidence";
    }

    internal sealed class FutureRouteDateEvidenceContext
    {
        public FutureRouteDateEvidenceContext(
            int totalDays,
            TransparentRouteGraphResolver graph,
            SocialRouteDateEvidenceIndex dateIndex,
            IReadOnlyDictionary<string, HashSet<(int X, int Y)>> connectorTiles)
        {
            TotalDays = totalDays;
            Graph = graph;
            DateIndex = dateIndex;
            ConnectorTiles = connectorTiles;
        }

        public int TotalDays { get; }

        public TransparentRouteGraphResolver Graph { get; }

        public SocialRouteDateEvidenceIndex DateIndex { get; }

        public IReadOnlyDictionary<string, HashSet<(int X, int Y)>> ConnectorTiles { get; }
    }

    public sealed partial class FutureRouteDateEvidenceProducer
    {
        public FutureRouteDateEvidenceProduction Produce(
            JsonElement routeGraph,
            JsonElement socialRouteDateEvidence,
            FutureRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing)
        {
            var inputBlock = ValidateInputs(request, timing);
            if (inputBlock is not null)
                return Blocked(inputBlock);
            if (!TryCreateContext(
                    routeGraph,
                    socialRouteDateEvidence,
                    request.TotalDays,
                    out var context,
                    out var contextBlocks))
            {
                return Blocked(contextBlocks);
            }
            return Produce(context, request, timing);
        }

        internal FutureRouteDateEvidenceProduction Produce(
            FutureRouteDateEvidenceContext context,
            FutureRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing)
        {
            var inputBlock = ValidateInputs(request, timing);
            if (inputBlock is not null)
                return Blocked(inputBlock);
            if (context is null || context.TotalDays != request.TotalDays)
                return Blocked("future_route_date_context_mismatch");
            var search = SearchFeasibleRoute(
                context.Graph,
                context.DateIndex,
                context.ConnectorTiles,
                request,
                timing);
            if (!search.Success)
                return Blocked(search.BlockingReasons);
            if (search.FinalApproach is null)
                return Blocked("future_route_target_approach_evidence_missing");

            return new FutureRouteDateEvidenceProduction
            {
                Status = FutureRouteDateEvidenceProductionStatus.Produced,
                SuccessfulRouteVariantCount = 1,
                Scenario = new FutureRouteAccessScenario
                {
                    TotalDays = request.TotalDays,
                    StartLocation = request.StartLocation,
                    StartTileX = request.StartTileX,
                    StartTileY = request.StartTileY,
                    EarliestDepartureTime = request.EarliestDepartureTime,
                    SegmentEvidence = search.Segments,
                    ApproachEvidence = new[] { search.FinalApproach },
                    ProducedPaths = new[] { search.Path }
                },
                BlockingReasons = Array.Empty<string>()
            };
        }

        internal static bool TryCreateContext(
            JsonElement routeGraph,
            JsonElement socialRouteDateEvidence,
            int totalDays,
            out FutureRouteDateEvidenceContext context,
            out string[] blockingReasons)
        {
            context = null!;
            if (!SocialRouteDateEvidenceIndex.TryCreate(
                    socialRouteDateEvidence,
                    totalDays,
                    out var dateIndex,
                    out blockingReasons))
            {
                return false;
            }
            if (!TransparentRouteGraphResolver.TryCreate(routeGraph, out var graph))
            {
                blockingReasons = new[] { "future_route_graph_invalid" };
                return false;
            }
            context = new FutureRouteDateEvidenceContext(
                totalDays,
                graph,
                dateIndex,
                ReadConnectorTiles(routeGraph));
            blockingReasons = Array.Empty<string>();
            return true;
        }

        private static (RouteTileCoordinate Tile, int Distance)? SelectReachableStand(
            SocialRouteDateMapEvidence map,
            int startX,
            int startY,
            IEnumerable<RouteTileCoordinate> candidates,
            ISet<(int X, int Y)> blocked)
        {
            return candidates
                .Select((tile, ordinal) => new
                {
                    Tile = tile,
                    Ordinal = ordinal,
                    Distance = map.ShortestDistance(
                        startX,
                        startY,
                        tile.X,
                        tile.Y,
                        blocked)
                })
                .Where(candidate => candidate.Distance.HasValue)
                .OrderBy(candidate => candidate.Distance)
                .ThenBy(candidate => candidate.Ordinal)
                .Select(candidate => ((RouteTileCoordinate Tile, int Distance)?)(
                    candidate.Tile,
                    candidate.Distance!.Value))
                .FirstOrDefault();
        }

        private static bool TryResolveGate(
            SocialRouteDateMapEvidence map,
            TransparentRouteEdge edge,
            out bool complete,
            out bool allowed,
            out int? openTime,
            out int? closeTime)
        {
            complete = true;
            allowed = true;
            openTime = null;
            closeTime = null;
            if (edge.Kind is "warp" or "building_door")
                return true;
            if (edge.Kind is not ("action_warp" or "touch_action_warp" or "locked_door_warp"))
                return false;

            var expectedGateKind = edge.Kind == "locked_door_warp"
                ? "locked_door_warp"
                : "warp_action";
            var matches = map.ActionGates.Where(gate =>
                string.Equals(
                    SocialRouteDateEvidenceIndex.ReadString(gate, "kind"),
                    expectedGateKind,
                    StringComparison.OrdinalIgnoreCase) &&
                SocialRouteDateEvidenceIndex.ReadInt(gate, "tile_x", int.MinValue) == edge.FromX &&
                SocialRouteDateEvidenceIndex.ReadInt(gate, "tile_y", int.MinValue) == edge.FromY &&
                string.Equals(
                    SocialRouteDateEvidenceIndex.ReadString(gate, "target_location"),
                    edge.TargetLocation,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (matches.Length != 1)
                return false;

            var gate = matches[0];
            var dateAllowed = SocialRouteDateEvidenceIndex.ReadBool(
                gate,
                "allowed_on_capture_date");
            if (!dateAllowed.HasValue)
                return false;
            allowed = dateAllowed.Value;
            if (edge.Kind != "locked_door_warp" ||
                SocialRouteDateEvidenceIndex.ReadBool(
                    gate,
                    "time_unrestricted_on_capture_date") == true)
            {
                return true;
            }

            openTime = ReadNullableInt(gate, "effective_open_time");
            closeTime = ReadNullableInt(gate, "effective_close_time");
            return openTime.HasValue && closeTime.HasValue;
        }

        private static IReadOnlyDictionary<string, HashSet<(int X, int Y)>>
            ReadConnectorTiles(JsonElement routeGraph)
        {
            var result = new Dictionary<string, HashSet<(int X, int Y)>>(
                StringComparer.OrdinalIgnoreCase);
            if (routeGraph.ValueKind != JsonValueKind.Object ||
                !routeGraph.TryGetProperty("edges", out var edges) ||
                edges.ValueKind != JsonValueKind.Array)
            {
                return result;
            }
            foreach (var edge in edges.EnumerateArray())
            {
                if (SocialRouteDateEvidenceIndex.ReadBool(edge, "resolved") != true)
                    continue;
                var location = SocialRouteDateEvidenceIndex.ReadString(edge, "from_location");
                var x = ReadNullableInt(edge, "from_x");
                var y = ReadNullableInt(edge, "from_y");
                if (string.IsNullOrWhiteSpace(location) || !x.HasValue || !y.HasValue)
                    continue;
                if (!result.TryGetValue(location, out var tiles))
                {
                    tiles = new HashSet<(int X, int Y)>();
                    result[location] = tiles;
                }
                tiles.Add((x.Value, y.Value));
            }
            return result;
        }

        private static string? ValidateInputs(
            FutureRouteDateEvidenceRequest request,
            FutureRouteTimingCalibration timing)
        {
            if (request is null || timing is null)
                return "future_route_date_evidence_input_missing";
            if (request.TotalDays < 0 ||
                request.TotalDays != timing.TotalDays ||
                string.IsNullOrWhiteSpace(request.StartLocation) ||
                string.IsNullOrWhiteSpace(request.TargetLocation) ||
                !IsValidTime(request.EarliestDepartureTime))
            {
                return "future_route_date_evidence_request_invalid";
            }
            if (!timing.StateComplete ||
                string.IsNullOrWhiteSpace(timing.EvidenceId) ||
                timing.GameMinuteNumeratorPerTile <= 0 ||
                timing.GameMinuteDenominatorPerTile <= 0 ||
                timing.ConnectorTransitionGameMinutes < 0)
            {
                return "future_route_timing_calibration_incomplete";
            }
            return null;
        }

        private static int ToGameMinutes(
            int tiles,
            FutureRouteTimingCalibration timing) =>
            tiles <= 0
                ? 0
                : (int)Math.Ceiling(
                    tiles * (double)timing.GameMinuteNumeratorPerTile /
                    timing.GameMinuteDenominatorPerTile);

        private static int? ReadNullableInt(JsonElement source, string name) =>
            source.ValueKind == JsonValueKind.Object &&
            source.TryGetProperty(name, out var value) &&
            value.ValueKind == JsonValueKind.Number &&
            value.TryGetInt32(out var parsed)
                ? parsed
                : null;

        private static FutureRouteDateEvidenceProduction Blocked(string reason) =>
            Blocked(new[] { reason });

        private static FutureRouteDateEvidenceProduction Blocked(string[] reasons) =>
            new()
            {
                Status = FutureRouteDateEvidenceProductionStatus.Blocked,
                BlockingReasons = reasons
            };

        private static bool IsValidTime(int value) =>
            value >= 0 && value <= 2800 && value % 100 < 60;

        private static readonly HashSet<(int X, int Y)> EmptyTiles = new();
    }
}
