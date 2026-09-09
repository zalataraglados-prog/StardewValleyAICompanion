using System;
using System.Collections.Generic;

namespace StardewAI.Core.Infrastructure
{
    public readonly struct RouteTileCoordinate
    {
        public RouteTileCoordinate(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X { get; }

        public int Y { get; }
    }

    public static class RouteConnectorStandTileResolver
    {
        public static RouteTileCoordinate[] ResolveCandidates(
            string kind,
            int connectorX,
            int connectorY,
            int width,
            int height,
            int? preferredStandX = null,
            int? preferredStandY = null)
        {
            if (width <= 0 || height <= 0 ||
                preferredStandX.HasValue != preferredStandY.HasValue)
            {
                return Array.Empty<RouteTileCoordinate>();
            }

            if (preferredStandX.HasValue)
            {
                return InBounds(
                    preferredStandX.Value,
                    preferredStandY!.Value,
                    width,
                    height)
                    ? new[]
                    {
                        new RouteTileCoordinate(
                            preferredStandX.Value,
                            preferredStandY.Value)
                    }
                    : Array.Empty<RouteTileCoordinate>();
            }

            if (string.Equals(kind, "warp", StringComparison.OrdinalIgnoreCase) &&
                TryResolveBoundaryWarp(
                    connectorX,
                    connectorY,
                    width,
                    height,
                    out var boundary))
            {
                return new[] { boundary };
            }

            if (string.Equals(kind, "building_door", StringComparison.OrdinalIgnoreCase))
            {
                var buildingStand = new RouteTileCoordinate(
                    connectorX,
                    connectorY + 1);
                return InBounds(
                    buildingStand.X,
                    buildingStand.Y,
                    width,
                    height)
                    ? new[] { buildingStand }
                    : Array.Empty<RouteTileCoordinate>();
            }

            return FilterInBounds(
                new[]
                {
                    new RouteTileCoordinate(connectorX + 1, connectorY),
                    new RouteTileCoordinate(connectorX - 1, connectorY),
                    new RouteTileCoordinate(connectorX, connectorY + 1),
                    new RouteTileCoordinate(connectorX, connectorY - 1)
                },
                width,
                height);
        }

        private static bool TryResolveBoundaryWarp(
            int x,
            int y,
            int width,
            int height,
            out RouteTileCoordinate result)
        {
            if (x < 0 && y >= 0 && y < height)
            {
                result = new RouteTileCoordinate(0, y);
                return true;
            }
            if (x >= width && y >= 0 && y < height)
            {
                result = new RouteTileCoordinate(width - 1, y);
                return true;
            }
            if (y < 0 && x >= 0 && x < width)
            {
                result = new RouteTileCoordinate(x, 0);
                return true;
            }
            if (y >= height && x >= 0 && x < width)
            {
                result = new RouteTileCoordinate(x, height - 1);
                return true;
            }

            result = default;
            return false;
        }

        private static RouteTileCoordinate[] FilterInBounds(
            IEnumerable<RouteTileCoordinate> candidates,
            int width,
            int height)
        {
            var result = new List<RouteTileCoordinate>();
            foreach (var candidate in candidates)
            {
                if (InBounds(candidate.X, candidate.Y, width, height))
                    result.Add(candidate);
            }
            return result.ToArray();
        }

        private static bool InBounds(
            int x,
            int y,
            int width,
            int height) =>
            x >= 0 && y >= 0 && x < width && y < height;
    }
}
