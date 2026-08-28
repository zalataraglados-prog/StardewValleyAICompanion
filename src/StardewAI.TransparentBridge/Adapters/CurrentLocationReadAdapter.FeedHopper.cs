using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Locations;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string FeedHopperNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)99->CheckForActionOnFeedHopper->root_location.piecesOfHay_minus_exact_withdrawal->player.inventory_(O)178_plus_exact_withdrawal";

    private static object? ReadFeedHopperWithdrawal(
        GameLocation location,
        Vector2 tile,
        StardewObject item,
        Farmer player)
    {
        if (location is not AnimalHouse house ||
            item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.ItemId, "99", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, "(BC)99", StringComparison.Ordinal) ||
            !string.Equals(item.Name, "Feed Hopper", StringComparison.Ordinal))
        {
            return null;
        }

        var root = location.GetRootLocation();
        var siloHay = Math.Max(0, root.piecesOfHay.Value);
        var animalCount = house.animalsThatLiveHere.Count;
        var animalLimit = Math.Max(0, house.animalLimit.Value);
        var placedHayCount = Math.Max(0, house.numberOfObjectsWithName("Hay"));
        var remainingTroughCapacity = Math.Max(0, animalLimit - placedHayCount);
        var unfedAnimalCount = Math.Max(0, animalCount - placedHayCount);
        var expectedWithdrawal = siloHay > 0
            ? Math.Min(Math.Max(1, Math.Min(animalCount, siloHay)), remainingTroughCapacity)
            : 0;
        expectedWithdrawal = Math.Max(0, expectedWithdrawal);
        var inventoryAccepts = expectedWithdrawal > 0 &&
            player.couldInventoryAcceptThisItem("(O)178", expectedWithdrawal, 0);
        var stands = ReadSafeObjectInteractionStands(location, tile.ToPoint());
        var status = unfedAnimalCount <= 0
            ? "blocked_no_unfed_animals"
            : siloHay <= 0
                ? "blocked_silo_empty"
                : expectedWithdrawal <= 0
                    ? "blocked_trough_capacity"
                    : !inventoryAccepts
                        ? "blocked_inventory_rejects_exact_stack"
                        : stands.All(stand => !stand.available)
                            ? "blocked_no_adjacent_stand"
                            : "ready";

        return new
        {
            status,
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            hay_qualified_item_id = "(O)178",
            root_location_id = root.NameOrUniqueName,
            silo_hay_before = siloHay,
            animal_count = animalCount,
            animal_limit = animalLimit,
            placed_hay_count = placedHayCount,
            remaining_trough_capacity = remainingTroughCapacity,
            unfed_animal_count = unfedAnimalCount,
            expected_withdrawal_quantity = expectedWithdrawal,
            inventory_accepts_exact_withdrawal = inventoryAccepts,
            expected_silo_hay_after = siloHay - expectedWithdrawal,
            expected_inventory_hay_delta = expectedWithdrawal,
            expected_native_location_action_return = true,
            target_runtime_type = item.GetType().FullName,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object",
            expected_action_type = "FeedHopper",
            native_contract = FeedHopperNativeContract
        };
    }
}
