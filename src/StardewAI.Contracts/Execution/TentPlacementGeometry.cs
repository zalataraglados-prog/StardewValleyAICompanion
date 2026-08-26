using System;

namespace StardewAI.Contracts.Execution;

public sealed class TentPlacementGeometry
{
    public TentPlacementGeometry(
        int direction,
        string directionName,
        int standTileX,
        int standTileY,
        int targetTileX,
        int targetTileY,
        int rectangleX,
        int rectangleY,
        int rectangleWidth,
        int rectangleHeight,
        int anchorTileX,
        int anchorTileY)
    {
        Direction = direction;
        DirectionName = directionName;
        StandTileX = standTileX;
        StandTileY = standTileY;
        TargetTileX = targetTileX;
        TargetTileY = targetTileY;
        RectangleX = rectangleX;
        RectangleY = rectangleY;
        RectangleWidth = rectangleWidth;
        RectangleHeight = rectangleHeight;
        AnchorTileX = anchorTileX;
        AnchorTileY = anchorTileY;
    }

    public int Direction { get; }
    public string DirectionName { get; }
    public int StandTileX { get; }
    public int StandTileY { get; }
    public int TargetTileX { get; }
    public int TargetTileY { get; }
    public int RectangleX { get; }
    public int RectangleY { get; }
    public int RectangleWidth { get; }
    public int RectangleHeight { get; }
    public int AnchorTileX { get; }
    public int AnchorTileY { get; }
}

public static class TentPlacementGeometryResolver
{
    public const int RectangleWidth = 3;
    public const int RectangleHeight = 2;

    public static bool TryResolve(
        int standTileX,
        int standTileY,
        int targetTileX,
        int targetTileY,
        out TentPlacementGeometry geometry)
    {
        var dx = targetTileX - standTileX;
        var dy = targetTileY - standTileY;
        var direction = (dx, dy) switch
        {
            (0, -1) => 0,
            (1, 0) => 1,
            (0, 1) => 2,
            (-1, 0) => 3,
            _ => -1
        };
        if (direction < 0)
        {
            geometry = null!;
            return false;
        }

        var rectangleX = direction switch
        {
            0 or 2 => targetTileX - 1,
            1 => targetTileX,
            3 => targetTileX - 2,
            _ => targetTileX
        };
        var rectangleY = direction switch
        {
            0 or 1 or 3 => targetTileY - 1,
            2 => targetTileY,
            _ => targetTileY
        };
        geometry = new TentPlacementGeometry(
            direction,
            direction switch { 0 => "up", 1 => "right", 2 => "down", _ => "left" },
            standTileX,
            standTileY,
            targetTileX,
            targetTileY,
            rectangleX,
            rectangleY,
            RectangleWidth,
            RectangleHeight,
            rectangleX + 1,
            rectangleY + 1);
        return true;
    }

    public static TentPlacementGeometry ResolveFromStand(int standTileX, int standTileY, int direction)
    {
        var (dx, dy) = direction switch
        {
            0 => (0, -1),
            1 => (1, 0),
            2 => (0, 1),
            3 => (-1, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, "Tent placement direction must be 0..3.")
        };
        _ = TryResolve(standTileX, standTileY, standTileX + dx, standTileY + dy, out var geometry);
        return geometry;
    }
}
