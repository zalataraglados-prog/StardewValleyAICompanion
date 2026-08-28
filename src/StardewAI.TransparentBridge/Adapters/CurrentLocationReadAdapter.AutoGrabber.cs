using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Objects;
using StardewObject = StardewValley.Object;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class CurrentLocationReadAdapter
{
    internal const string AutoGrabberNativeContract =
        "GameLocation.checkAction->Object.checkForAction_(BC)165->CheckForActionOnAutoGrabber->ItemGrabMenu->receiveLeftClick->grabItemFromAutoGrabber->player.inventory";

    private static object? ReadAutoGrabberCollection(
        GameLocation location,
        Vector2 tile,
        StardewObject item,
        Farmer player)
    {
        if (item.GetType() != typeof(StardewObject) ||
            !item.bigCraftable.Value ||
            !string.Equals(item.ItemId, "165", StringComparison.Ordinal) ||
            !string.Equals(item.QualifiedItemId, "(BC)165", StringComparison.Ordinal) ||
            item.heldObject.Value is not Chest chest)
        {
            return null;
        }

        var projectedInventory = player.Items.Select(CloneInventoryItem).ToList();
        var before = new List<AutoGrabberContentProjection>();
        var transferable = new List<AutoGrabberContentProjection>();
        var remaining = new List<AutoGrabberContentProjection>();
        for (var slot = 0; slot < chest.Items.Count; slot++)
        {
            var content = chest.Items[slot];
            if (content is null)
                continue;
            var row = AutoGrabberContentProjection.From(slot, content);
            before.Add(row);
            var candidate = CloneInventoryItem(content)!;
            if (Utility.canItemBeAddedToThisInventoryList(candidate, projectedInventory) &&
                Utility.addItemToThisInventoryList(candidate, projectedInventory) is null)
            {
                transferable.Add(row);
            }
            else
            {
                remaining.Add(row);
            }
        }

        var stands = ReadSafeObjectInteractionStands(location, tile.ToPoint());
        var status = before.Count == 0
            ? "blocked_empty"
            : transferable.Count == 0
                ? "blocked_inventory_rejects_all_stacks"
                : stands.All(stand => !stand.available)
                    ? "blocked_no_adjacent_stand"
                    : "ready";
        return new
        {
            status,
            canonical_item_id = item.ItemId,
            canonical_qualified_item_id = item.QualifiedItemId,
            held_container_runtime_type = chest.GetType().FullName,
            contents_before_json = JsonSerializer.Serialize(before),
            transferable_contents_json = JsonSerializer.Serialize(transferable),
            remaining_contents_json = JsonSerializer.Serialize(remaining),
            content_stack_count_before = before.Count,
            transferable_stack_count = transferable.Count,
            expected_stack_count_after = remaining.Count,
            content_quantity_before = before.Sum(row => row.Quantity),
            expected_transfer_quantity = transferable.Sum(row => row.Quantity),
            expected_quantity_after = remaining.Sum(row => row.Quantity),
            expected_native_location_action_return = true,
            target_runtime_type = item.GetType().FullName,
            stand_tiles = stands,
            has_available_adjacent_stand = stands.Any(stand => stand.available),
            interaction_kind = "location_object_menu_transaction",
            expected_action_type = "AutoGrabber",
            native_contract = AutoGrabberNativeContract
        };
    }

    private static Item? CloneInventoryItem(Item? item)
    {
        if (item is null)
            return null;
        var clone = item.getOne();
        clone.Stack = item.Stack;
        return clone;
    }
}

internal sealed record AutoGrabberContentProjection(
    int SourceSlotIndex,
    string RuntimeType,
    string QualifiedItemId,
    int Quality,
    string SourceUnitStateSha256,
    string InventoryUnitStateSha256,
    int Quantity)
{
    public static AutoGrabberContentProjection From(int slot, Item item)
    {
        var source = ClearanceOutputItemProjection.From(item);
        var receipt = ClearanceOutputItemProjection.FromInventoryReceipt(item);
        return new(
            slot,
            source.RuntimeType,
            source.QualifiedItemId,
            source.Quality,
            source.UnitStateSha256,
            receipt.UnitStateSha256,
            source.Quantity);
    }
}
