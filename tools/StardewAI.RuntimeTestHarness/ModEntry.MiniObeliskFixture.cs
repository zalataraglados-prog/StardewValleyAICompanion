using Microsoft.Xna.Framework;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMiniObeliskFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_mini_obelisk", "isolated_mini_obelisk_pair=ready",
                MiniObeliskFixtureObserved(), reasons.ToArray());
        }

        var farm = Game1.getFarm();
        foreach (var tile in farm.objects.Pairs
            .Where(pair => string.Equals(
                pair.Value.QualifiedItemId, MiniObeliskQualifiedItemId, StringComparison.Ordinal))
            .Select(pair => pair.Key)
            .ToArray())
        {
            farm.objects.Remove(tile);
        }
        if (!TryFindMiniObeliskFixtureLayout(farm, out var source, out _, out var destination))
        {
            return BlockedWithPrimitive(request, "debug_setup_mini_obelisk", "isolated_mini_obelisk_pair=ready",
                MiniObeliskFixtureObserved(), "mini_obelisk_fixture_layout_unavailable");
        }
        var emptySlot = Enumerable.Range(0, Math.Min(12, Game1.player.Items.Count))
            .FirstOrDefault(index => Game1.player.Items[index] is null, -1);
        if (emptySlot < 0)
        {
            return BlockedWithPrimitive(request, "debug_setup_mini_obelisk", "isolated_mini_obelisk_pair=ready",
                MiniObeliskFixtureObserved(), "mini_obelisk_fixture_empty_toolbar_slot_unavailable");
        }

        Game1.exitActiveMenu();
        var sourceObject = new StardewObject(source.ToVector2(), "238");
        var destinationObject = new StardewObject(destination.ToVector2(), "238");
        farm.objects[source.ToVector2()] = sourceObject;
        farm.objects[destination.ToVector2()] = destinationObject;
        var nativePair = ReadRuntimeNativeMiniObeliskPair(farm);
        var pairReferencesMatch = nativePair is not null &&
            ((ReferenceEquals(nativePair.First.Item, sourceObject) &&
              ReferenceEquals(nativePair.Second.Item, destinationObject)) ||
             (ReferenceEquals(nativePair.First.Item, destinationObject) &&
              ReferenceEquals(nativePair.Second.Item, sourceObject)));
        var nativeSource = nativePair?.First.Tile.ToPoint() ?? default;
        var nativeDestinationTarget = nativePair?.Second.Tile.ToPoint() ?? default;
        var stand = nativePair is null
            ? default
            : Neighbors(nativeSource).FirstOrDefault(tile =>
                IsClearMiniObeliskFixtureTile(farm, tile) &&
                !IsDestructiveObjectTrap(farm, tile) &&
                ReadRuntimeNativeMiniObeliskDestination(tile, nativePair).ToPoint() == nativeDestinationTarget);
        var nativeDestination = nativePair is null || stand == default
            ? Vector2.Zero
            : ReadRuntimeNativeMiniObeliskDestination(stand, nativePair);
        var landing = nativePair is null || stand == default
            ? null
            : ReadRuntimeFirstNativeMiniObeliskLanding(farm, nativeDestination);
        if (nativePair is null || !pairReferencesMatch || stand == default || landing is null ||
            nativeDestination.ToPoint() != nativeDestinationTarget)
        {
            farm.objects.Remove(source.ToVector2());
            farm.objects.Remove(destination.ToVector2());
            return BlockedWithPrimitive(request, "debug_setup_mini_obelisk", "isolated_mini_obelisk_pair=ready",
                MiniObeliskFixtureObserved(), "mini_obelisk_fixture_native_order_or_landing_mismatch");
        }

        Game1.warpFarmer(farm.NameOrUniqueName, stand.X, stand.Y, false);
        Game1.player.Position = stand.ToVector2() * Game1.tileSize;
        var verified = IsExactBaseMiniObelisk(sourceObject) && IsExactBaseMiniObelisk(destinationObject) &&
            Game1.player.Items[emptySlot] is null && Game1.player.TilePoint == stand &&
            landing.Value.ToPoint() != destination;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mini_obelisk",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_native_order_replayed_mini_obelisk_pair_and_empty_toolbar_slot_installed" }
                : new[] { "mini_obelisk_fixture_setup_mismatch" },
            RequestedEffect = "qualified_item_id=(BC)238;native_pair_count=2;native_pair_order=replayed_from_location_objects",
            ObservedEffect = MiniObeliskFixtureObserved(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "mini_obelisk_fixture_setup_mismatch" }
        };
    }

    private static bool TryFindMiniObeliskFixtureLayout(
        GameLocation location,
        out Point source,
        out Point stand,
        out Point destination)
    {
        source = default;
        stand = default;
        destination = default;
        var width = location.Map.Layers[0].LayerWidth;
        var height = location.Map.Layers[0].LayerHeight;
        for (var sourceY = 2; sourceY < height - 2; sourceY++)
        for (var sourceX = 2; sourceX < width - 2; sourceX++)
        {
            var sourceCandidate = new Point(sourceX, sourceY);
            if (!IsClearMiniObeliskFixtureTile(location, sourceCandidate))
                continue;
            var standCandidate = Neighbors(sourceCandidate).FirstOrDefault(tile =>
                IsClearMiniObeliskFixtureTile(location, tile) && !IsDestructiveObjectTrap(location, tile));
            if (standCandidate == default)
                continue;

            for (var destinationY = 2; destinationY < height - 2; destinationY++)
            for (var destinationX = 2; destinationX < width - 2; destinationX++)
            {
                var destinationCandidate = new Point(destinationX, destinationY);
                if (ManhattanDistance(sourceCandidate, destinationCandidate) < 12 ||
                    !IsClearMiniObeliskFixtureTile(location, destinationCandidate))
                {
                    continue;
                }
                var landing = ReadRuntimeFirstNativeMiniObeliskLanding(
                    location, destinationCandidate.ToVector2());
                if (landing is null || landing.Value.ToPoint() == sourceCandidate ||
                    landing.Value.ToPoint() == standCandidate)
                {
                    continue;
                }
                source = sourceCandidate;
                stand = standCandidate;
                destination = destinationCandidate;
                return true;
            }
        }
        return false;
    }

    private static bool IsClearMiniObeliskFixtureTile(GameLocation location, Point tile) =>
        IsTileOnMap(location, tile) &&
        !location.objects.ContainsKey(tile.ToVector2()) &&
        !location.terrainFeatures.ContainsKey(tile.ToVector2()) &&
        IsTileWalkable(location, tile) &&
        !IsTileOccupiedByCharacter(location, tile);

    private static string MiniObeliskFixtureObserved()
    {
        if (Game1.currentLocation is null)
            return "native_pair=missing";
        var pair = ReadRuntimeNativeMiniObeliskPair(Game1.currentLocation);
        if (pair is null)
            return "native_pair=missing";
        return "native_pair=" + (int)pair.First.Tile.X + "," + (int)pair.First.Tile.Y + "|" +
            (int)pair.Second.Tile.X + "," + (int)pair.Second.Tile.Y +
            ";qualified_item_ids=" + pair.First.Item.QualifiedItemId + "|" + pair.Second.Item.QualifiedItemId +
            ";player_tile=" + Game1.player.TilePoint.X + "," + Game1.player.TilePoint.Y;
    }
}
