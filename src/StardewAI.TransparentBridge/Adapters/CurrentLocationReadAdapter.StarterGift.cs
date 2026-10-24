using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Objects;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string StarterGiftNativeContract = "FarmHouse_owned_by_current_player->Chest.checkForAction_giftbox_starter->dumpContents_addQuest_and_addItemByMenuIfNecessaryElseHoldUp->remove_empty_giftbox";

    private static object ReadStarterGiftClaim(GameLocation location)
    {
        var isFarmHouse = location is FarmHouse;
        var ownedFarmHouse = location is FarmHouse house && house.IsOwnedByCurrentPlayer;
        var matches = location.objects.Pairs
            .Where(pair => pair.Value is Chest chest &&
                chest.GetType() == typeof(Chest) &&
                !chest.playerChest.Value &&
                chest.giftbox.Value &&
                chest.giftboxIsStarterGift.Value)
            .OrderBy(pair => pair.Key.Y)
            .ThenBy(pair => pair.Key.X)
            .ToArray();
        var match = matches.Length == 1 ? matches[0] : default;
        var chest = matches.Length == 1 ? (Chest)match.Value : null;
        var tile = matches.Length == 1 ? match.Key : Vector2.Zero;
        var expectedQuestId = Game1.whichFarm == 7 ? "132" : "6";
        var questPresentBefore = Game1.player.hasQuest(expectedQuestId);
        var contents = chest?.Items
            .Where(item => item is not null)
            .Select(item => ClearanceOutputItemProjection.FromInventoryReceipt(item!))
            .GroupBy(item => new
            {
                item.RuntimeType,
                item.QualifiedItemId,
                item.Quality,
                item.UnitStateSha256
            })
            .Select(group => new ClearanceOutputItemProjection(
                group.Key.RuntimeType,
                group.Key.QualifiedItemId,
                group.Key.Quality,
                group.Key.UnitStateSha256,
                group.Sum(item => item.Quantity)))
            .OrderBy(item => item.QualifiedItemId, StringComparer.Ordinal)
            .ThenBy(item => item.Quality)
            .ThenBy(item => item.RuntimeType, StringComparer.Ordinal)
            .ToArray() ?? Array.Empty<ClearanceOutputItemProjection>();
        var outputJson = JsonSerializer.Serialize(contents);
        var outputsAccepted = chest is not null && chest.Items
            .Where(item => item is not null)
            .All(item =>
            {
                Game1.player.GetItemReceiveBehavior(item!, out var needsInventorySpace, out _);
                return !needsInventorySpace || Game1.player.couldInventoryAcceptThisItem(item!);
            });
        var standTiles = chest is null
            ? Array.Empty<StarterGiftStandProjection>()
            : new[]
            {
                new Point((int)tile.X, (int)tile.Y - 1),
                new Point((int)tile.X, (int)tile.Y + 1),
                new Point((int)tile.X - 1, (int)tile.Y),
                new Point((int)tile.X + 1, (int)tile.Y)
            }.Select(stand =>
            {
                var onMap = location.isTileOnMap(stand.ToVector2());
                var collisionBlocked = !onMap || location.isCollidingPosition(
                    new Rectangle(
                        stand.X * Game1.tileSize + 1,
                        stand.Y * Game1.tileSize + 1,
                        Game1.tileSize - 2,
                        Game1.tileSize - 2),
                    Game1.viewport,
                    Game1.player);
                return new StarterGiftStandProjection(
                    stand.X,
                    stand.Y,
                    onMap,
                    collisionBlocked,
                    onMap && !collisionBlocked);
            }).ToArray();
        var anyStand = standTiles.Any(row => row.Available);
        var status = !isFarmHouse
            ? "not_current_farmhouse"
            : !ownedFarmHouse
                ? "blocked_farmhouse_not_owned_by_current_player"
                : matches.Length == 0
                    ? "absent_or_already_claimed"
                    : matches.Length > 1
                        ? "blocked_ambiguous_starter_gift_chests"
                        : chest!.Items.Count == 0 || contents.Length == 0
                            ? "blocked_empty_starter_gift"
                            : questPresentBefore
                                ? "blocked_expected_starter_quest_already_present"
                                : !outputsAccepted
                                    ? "blocked_inventory_cannot_accept_all_outputs"
                                    : !anyStand
                                        ? "blocked_no_adjacent_stand"
                                        : "ready";
        var fingerprintSource = string.Join("|",
            location.NameOrUniqueName,
            (int)tile.X,
            (int)tile.Y,
            chest?.giftboxIndex.Value ?? -1,
            expectedQuestId,
            outputJson,
            StarterGiftNativeContract);

        return new
        {
            status,
            location_id = location.NameOrUniqueName,
            location_runtime_type = location.GetType().FullName,
            is_farmhouse = isFarmHouse,
            farmhouse_owned_by_current_player = ownedFarmHouse,
            target_tile_x = chest is null ? (int?)null : (int)tile.X,
            target_tile_y = chest is null ? (int?)null : (int)tile.Y,
            target_runtime_type = chest?.GetType().FullName ?? string.Empty,
            qualified_item_id = chest?.QualifiedItemId ?? string.Empty,
            exact_starter_gift_present = chest is not null,
            player_chest = chest?.playerChest.Value,
            giftbox = chest?.giftbox.Value,
            giftbox_is_starter_gift = chest?.giftboxIsStarterGift.Value,
            giftbox_index = chest?.giftboxIndex.Value,
            owner_player_id = chest?.owner.Value,
            content_slot_count = chest?.Items.Count ?? 0,
            expected_output_items = contents,
            expected_output_items_json = outputJson,
            inventory_accepts_all_outputs = outputsAccepted,
            expected_quest_id = expectedQuestId,
            expected_quest_present_before = questPresentBefore,
            expected_quest_present_after = true,
            farm_type = Game1.whichFarm,
            stand_tiles = standTiles,
            interaction_kind = "location_object",
            expected_action_type = "StarterGiftChest",
            native_contract = StarterGiftNativeContract,
            projection_fingerprint = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(fingerprintSource))).ToLowerInvariant()
        };
    }
}

internal sealed record StarterGiftStandProjection(
    [property: System.Text.Json.Serialization.JsonPropertyName("tile_x")] int TileX,
    [property: System.Text.Json.Serialization.JsonPropertyName("tile_y")] int TileY,
    [property: System.Text.Json.Serialization.JsonPropertyName("on_map")] bool OnMap,
    [property: System.Text.Json.Serialization.JsonPropertyName("collision_blocked")] bool CollisionBlocked,
    [property: System.Text.Json.Serialization.JsonPropertyName("available")] bool Available);
