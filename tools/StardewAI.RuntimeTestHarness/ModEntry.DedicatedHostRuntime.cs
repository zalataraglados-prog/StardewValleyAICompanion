using System.Reflection;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private bool dedicatedHostRuntimeNormalized;
    private bool dedicatedHostRuntimeWarningLogged;
    private bool vanillaHostReadyLogged;
    private bool vanillaHostFailureLogged;
    private uint vanillaHostValidationTicks;
    private int? vanillaHostLastTime;
    private uint vanillaHostStalledTicks;

    private static bool IsDedicatedHostAiMode()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("STARDEWAI_DEDICATED_HOST_MODE"),
            "1",
            StringComparison.Ordinal);
    }

    private static bool IsVanillaAiHostMode()
    {
        return string.Equals(
            Environment.GetEnvironmentVariable("STARDEWAI_VANILLA_HOST_MODE"),
            "1",
            StringComparison.Ordinal);
    }

    private static bool IsAiHostRuntimeMode()
    {
        return IsDedicatedHostAiMode() || IsVanillaAiHostMode();
    }

    private void OnDedicatedHostSaveLoaded(object? sender, SaveLoadedEventArgs e)
    {
        dedicatedHostRuntimeNormalized = false;
        vanillaHostReadyLogged = false;
        vanillaHostFailureLogged = false;
        vanillaHostValidationTicks = 0;
        vanillaHostLastTime = null;
        vanillaHostStalledTicks = 0;
        NormalizeDedicatedHostRuntime();
        EnsureJoinableCabin();
    }

    private void EnsureJoinableCabin()
    {
        if (!IsVanillaAiHostMode() ||
            !string.Equals(
                Environment.GetEnvironmentVariable("STARDEWAI_ENSURE_JOINABLE_CABIN"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var farm = Game1.getFarm();
        var pathLayer = farm?.Map?.GetLayer("Paths");
        if (farm is null || pathLayer is null)
        {
            Monitor.Log("Joinable cabin could not be restored: the live Farm Paths layer is unavailable.", LogLevel.Error);
            return;
        }

        var cabinRows = farm.buildings
            .Where(building => building.isCabin)
            .Select(building => new
            {
                Building = building,
                Indoors = building.GetIndoors() as Cabin
            })
            .ToArray();

        static bool IsJoinable(Cabin? indoors)
        {
            var owner = indoors?.owner;
            return owner is null || owner.UniqueMultiplayerID == 0 || !owner.isCustomized.Value;
        }

        static bool IsOnMap(Building building, xTile.Layers.Layer layer)
        {
            return building.tileX.Value >= 0 &&
                   building.tileY.Value >= 0 &&
                   building.tileX.Value + building.tilesWide.Value <= layer.LayerWidth &&
                   building.tileY.Value + building.tilesHigh.Value <= layer.LayerHeight;
        }

        if (cabinRows.Any(row => IsJoinable(row.Indoors) && IsOnMap(row.Building, pathLayer)))
        {
            Monitor.Log("A visible joinable cabin already exists; no cabin migration was needed.", LogLevel.Info);
            return;
        }

        var cabinRow = cabinRows
            .Where(row => IsJoinable(row.Indoors))
            .OrderByDescending(row => row.Indoors?.owner is not null &&
                                      row.Indoors.owner.UniqueMultiplayerID != 0)
            .FirstOrDefault();
        if (cabinRow is null || cabinRow.Indoors is null)
        {
            Monitor.Log("Joinable cabin could not be restored: no unclaimed cabin slot exists.", LogLevel.Error);
            return;
        }

        var designatedPositions = new List<(int Order, Point Position)>();
        for (var x = 0; x < pathLayer.LayerWidth; x++)
        {
            for (var y = 0; y < pathLayer.LayerHeight; y++)
            {
                var tile = pathLayer.Tiles[x, y];
                if (tile is null || (tile.TileIndex != 29 && tile.TileIndex != 30))
                {
                    continue;
                }

                if (tile.Properties.TryGetValue("Order", out var orderValue) &&
                    int.TryParse(orderValue?.ToString(), out var order))
                {
                    designatedPositions.Add((order, new Point(x, y)));
                }
            }
        }

        var target = designatedPositions
            .OrderBy(row => row.Order)
            .Select(row => row.Position)
            .FirstOrDefault(position => !farm.buildings.Any(other =>
                !ReferenceEquals(other, cabinRow.Building) &&
                BuildingFootprintsOverlap(cabinRow.Building, position, other)));
        if (target == Point.Zero && designatedPositions.All(row => row.Position != Point.Zero))
        {
            Monitor.Log("Joinable cabin could not be restored: every live map-designated cabin position is occupied.", LogLevel.Error);
            return;
        }

        cabinRow.Building.tileX.Value = target.X;
        cabinRow.Building.tileY.Value = target.Y;
        var cabinDoor = cabinRow.Building.getPointForHumanDoor();
        foreach (var warp in cabinRow.Indoors.warps.Where(warp =>
                     string.Equals(warp.TargetName, "Farm", StringComparison.Ordinal)))
        {
            warp.TargetX = cabinDoor.X;
            warp.TargetY = cabinDoor.Y;
        }

        farm.removeObjectsAndSpawned(
            target.X,
            target.Y,
            cabinRow.Building.tilesWide.Value,
            cabinRow.Building.tilesHigh.Value);
        farm.removeObjectsAndSpawned(cabinDoor.X, cabinDoor.Y + 1, 1, 1);

        var owner = cabinRow.Indoors.owner;
        Monitor.Log(
            $"Restored joinable cabin at live map-designated tile ({target.X},{target.Y}); " +
            $"farmhand_id={owner?.UniqueMultiplayerID ?? 0}, customized={owner?.isCustomized.Value ?? false}.",
            LogLevel.Info);
    }

    private static bool BuildingFootprintsOverlap(Building moving, Point target, Building other)
    {
        var movingBounds = new Rectangle(
            target.X,
            target.Y,
            moving.tilesWide.Value,
            moving.tilesHigh.Value);
        var otherBounds = new Rectangle(
            other.tileX.Value,
            other.tileY.Value,
            other.tilesWide.Value,
            other.tilesHigh.Value);
        return movingBounds.Intersects(otherBounds);
    }

    private void OnDedicatedHostVisibilityTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!Context.IsWorldReady || !Context.IsMainPlayer || !e.IsMultipleOf(30))
        {
            return;
        }

        NormalizeDedicatedHostRuntime();
        ValidateVanillaHostRuntime();
    }

    private void ValidateVanillaHostRuntime()
    {
        if (!IsVanillaAiHostMode() || !Context.IsWorldReady)
        {
            return;
        }

        vanillaHostValidationTicks += 30;
        var multiplayerReady = Context.IsMultiplayer &&
                               Context.IsMainPlayer &&
                               Game1.IsServer &&
                               Game1.server is not null &&
                               Game1.options?.enableServer == true;

        if (multiplayerReady && !vanillaHostReadyLogged)
        {
            vanillaHostReadyLogged = true;
            Monitor.Log(
                "Vanilla AI host ready: local farmer is the visible main player and the original multiplayer server is active.",
                LogLevel.Info);
        }
        else if (!multiplayerReady && vanillaHostValidationTicks >= 300 && !vanillaHostFailureLogged)
        {
            vanillaHostFailureLogged = true;
            Monitor.Log(
                $"Vanilla AI host failed readiness gate: multiplayer={Context.IsMultiplayer}, main_player={Context.IsMainPlayer}, game_server={Game1.IsServer}, server_object={Game1.server is not null}, enable_server={Game1.options?.enableServer}.",
                LogLevel.Error);
        }

        if (Game1.options is not null)
        {
            Game1.options.pauseWhenOutOfFocus = false;
        }

        if (Game1.activeClickableMenu is not null || Game1.eventUp || Game1.paused)
        {
            vanillaHostLastTime = Game1.timeOfDay;
            vanillaHostStalledTicks = 0;
            return;
        }

        if (vanillaHostLastTime == Game1.timeOfDay)
        {
            vanillaHostStalledTicks += 30;
        }
        else
        {
            vanillaHostLastTime = Game1.timeOfDay;
            vanillaHostStalledTicks = 0;
        }

        if (vanillaHostStalledTicks == 3600)
        {
            Monitor.Log(
                $"Vanilla AI host clock has not advanced for 60 seconds at {Game1.timeOfDay}; inspect game pause, event, and executor state.",
                LogLevel.Error);
        }
    }

    private void NormalizeDedicatedHostRuntime()
    {
        var player = Game1.player;
        if (!Context.IsWorldReady || !Context.IsMainPlayer || player is null)
        {
            return;
        }

        var junimoHidingDisabled = TryDisableJunimoHostHiding();
        var transitionActive = Game1.locationRequest is not null ||
                               Game1.fadeToBlack ||
                               Game1.currentLocation?.currentEvent is not null;

        if (!transitionActive)
        {
            Game1.displayFarmer = true;
            player.hidden.Value = false;
            player.ignoreCollisions = false;
        }

        if (!dedicatedHostRuntimeNormalized && !transitionActive)
        {
            dedicatedHostRuntimeNormalized = true;
            Monitor.Log(
                $"Dedicated AI host normalized: visible=true, collisions=true, junimo_hiding_disabled={junimoHidingDisabled}.",
                StardewModdingAPI.LogLevel.Info);
        }
    }

    private bool TryDisableJunimoHostHiding()
    {
        try
        {
            var junimoAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(assembly => string.Equals(
                    assembly.GetName().Name,
                    "JunimoServer",
                    StringComparison.Ordinal));
            var alwaysOnType = junimoAssembly?.GetType(
                "JunimoServer.Services.AlwaysOn.AlwaysOnServer",
                throwOnError: false);
            var hiddenField = alwaysOnType?.GetField(
                "PlayerIsHidden",
                BindingFlags.Public | BindingFlags.Static);
            if (hiddenField is null || hiddenField.FieldType != typeof(bool))
            {
                return false;
            }

            hiddenField.SetValue(null, false);
            return true;
        }
        catch (Exception ex)
        {
            if (!dedicatedHostRuntimeWarningLogged)
            {
                dedicatedHostRuntimeWarningLogged = true;
                Monitor.Log(
                    $"Could not disable Junimo host hiding: {ex.GetType().Name}: {ex.Message}",
                    StardewModdingAPI.LogLevel.Warn);
            }

            return false;
        }
    }
}
