using System;

namespace StardewAI.Core.Execution;

public static class ConnectorRouteExecutionBudgetPolicy
{
    public const string ShortestPathTilesParameter =
        "compiler_context.route_shortest_path_tiles";
    public const string RecoveryRouteStepKind = "rolling_route_home_connector";

    // Planning runs while the world keeps moving, so a fresh native path can
    // need a short bounded detour around characters or transient occupancy.
    public const int DynamicOccupancyDetourAllowanceTiles = 16;
    public const int MaximumMovementTiles = 512;

    public static int MovementTileLimit(int transparentShortestPathTiles) =>
        Math.Max(
            1,
            Math.Min(
                MaximumMovementTiles,
                Math.Max(0, transparentShortestPathTiles) +
                DynamicOccupancyDetourAllowanceTiles));

    public static int EstimatedTicks(int transparentShortestPathTiles) =>
        Math.Max(60, MovementTileLimit(transparentShortestPathTiles) * 60);

    // Keep a clock increment for connector transit and coarse clock rounding.
    public static int RecoveryTravelGameMinutes(int transparentShortestPathTiles) =>
        10 + GameClockBudgetPolicy.MovementTilesToGameMinutes(
            MovementTileLimit(transparentShortestPathTiles));
}
