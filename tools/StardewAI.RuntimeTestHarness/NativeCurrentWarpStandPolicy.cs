using System;
using System.Collections.Generic;

namespace StardewAI.RuntimeTestHarness;

internal readonly record struct ConnectorStandBounds(int X, int Y, int Width, int Height)
{
    public bool Known => Width > 0 && Height > 0;
    public bool Intersects(ConnectorStandBounds other) =>
        (long)X + Width > other.X && (long)other.X + other.Width > X &&
        (long)Y + Height > other.Y && (long)other.Y + other.Height > Y;
}

internal sealed record CurrentWarpStandFacts
{
    public string ConnectorKind { get; init; } = string.Empty;
    public bool ConnectorValidated { get; init; }
    public int CurrentTileX { get; init; }
    public int CurrentTileY { get; init; }
    public int StandTileX { get; init; }
    public int StandTileY { get; init; }
    public int WarpTileX { get; init; }
    public int WarpTileY { get; init; }
    public int TileSize { get; init; } = 64;
    public bool StandOnMap { get; init; }
    public bool StandHardWalkable { get; init; }
    public bool WarpHardWalkable { get; init; }
    public bool StandHasTouchAction { get; init; }
    public bool StandIsWarp { get; init; }
    public ConnectorStandBounds PlayerBounds { get; init; }
    public IReadOnlyList<ConnectorStandBounds>? CharacterBounds { get; init; }
}

internal static class NativeCurrentWarpStandPolicy
{
    public static bool CanUse(CurrentWarpStandFacts facts)
    {
        if (facts.ConnectorKind != "warp" || !facts.ConnectorValidated ||
            facts.CurrentTileX != facts.StandTileX || facts.CurrentTileY != facts.StandTileY ||
            Math.Abs(facts.StandTileX - facts.WarpTileX) + Math.Abs(facts.StandTileY - facts.WarpTileY) != 1 ||
            facts.TileSize <= 0 || !facts.StandOnMap || !facts.StandHardWalkable || !facts.WarpHardWalkable ||
            facts.StandHasTouchAction || facts.StandIsWarp || !facts.PlayerBounds.Known || facts.CharacterBounds is null)
            return false;

        // A farmer already on the stand need not enter its whole tile again.
        // Check the actual native footprint and its cardinal sweep toward the
        // adjacent warp, retaining rejection of a genuinely occupied exit.
        var dx = (facts.WarpTileX - facts.StandTileX) * facts.TileSize;
        var dy = (facts.WarpTileY - facts.StandTileY) * facts.TileSize;
        var footprint = new ConnectorStandBounds(
            facts.PlayerBounds.X + Math.Min(0, dx), facts.PlayerBounds.Y + Math.Min(0, dy),
            facts.PlayerBounds.Width + Math.Abs(dx), facts.PlayerBounds.Height + Math.Abs(dy));
        foreach (var bounds in facts.CharacterBounds)
            if (!bounds.Known || footprint.Intersects(bounds)) return false;
        return true;
    }
}
