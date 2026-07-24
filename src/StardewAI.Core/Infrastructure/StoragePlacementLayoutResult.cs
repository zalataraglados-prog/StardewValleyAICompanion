using System;
using System.Collections.Generic;
using System.Linq;

namespace StardewAI.Core.Infrastructure;

internal sealed class StoragePlacementLayoutResult
{
    public string Status { get; init; } = "blocked";

    public int? TargetTileX { get; init; }

    public int? TargetTileY { get; init; }

    public int? StandTileX { get; init; }

    public int? StandTileY { get; init; }

    public int RouteDistanceTiles { get; init; } = -1;

    public int BaselineReachableTileCount { get; init; }

    public int ReachableTileCountAfterPlacement { get; init; }

    public int ProtectedAccessGroupCount { get; init; }

    public string ProjectionBasis { get; init; } = string.Empty;

    public string[] BlockingReasons { get; init; } =
        Array.Empty<string>();

    public static StoragePlacementLayoutResult Blocked(
        IEnumerable<string> reasons,
        int baselineReachableTileCount = 0,
        int protectedAccessGroupCount = 0)
    {
        return new StoragePlacementLayoutResult
        {
            Status = "blocked",
            BaselineReachableTileCount =
                baselineReachableTileCount,
            ProtectedAccessGroupCount =
                protectedAccessGroupCount,
            ProjectionBasis =
                "native_legal_range+collision_grid_virtual_occupancy_bfs+protected_endpoint_and_storage_access",
            BlockingReasons = reasons
                .Distinct(StringComparer.Ordinal)
                .ToArray()
        };
    }
}
