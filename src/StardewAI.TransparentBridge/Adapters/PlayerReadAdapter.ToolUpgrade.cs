using System.Text.Json;
using System.Text.Json.Serialization;
using StardewValley;
using StardewValley.GameData.Tools;
using StardewValley.Internal;
using StardewValley.Tools;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class PlayerReadAdapter
{
    internal const string ToolUpgradeNativeContract =
        "shared_route->Blacksmith_checkAction->answerDialogue(Upgrade)->ShopMenu(ClintUpgrade).receiveLeftClick_exact_row->Tool.actionWhenPurchased->two_day_wait->Blacksmith_checkAction->native_tool_claim";

    private static object ReadToolUpgrade(Farmer? player)
    {
        if (player is null || !StardewModdingAPI.Context.IsWorldReady)
            return new { schema_version = "tool_upgrade.v1", projection_status = "unavailable_world_or_player" };

        var blacksmith = Game1.getLocationFromName("Blacksmith");
        var counterTiles = ReadGeodeCounterTiles(blacksmith);
        var clint = Game1.getCharacterFromName("Clint", mustBeVillager: false);
        var clintAtBlacksmith = clint?.currentLocation?.NameOrUniqueName == "Blacksmith" &&
            blacksmith?.characters.Contains(clint) == true;
        var currentLocation = player.currentLocation?.NameOrUniqueName ?? string.Empty;
        var actorReady = Game1.activeClickableMenu is null && !Game1.dialogueUp && !Game1.eventUp &&
            player.CanMove && !player.UsingTool;
        var pending = player.toolBeingUpgraded.Value;
        var pendingDays = player.daysLeftForToolUpgrade.Value;
        var freeSlots = player.freeSpotsInInventory();
        var axeInventory = player.Items
            .Select((item, slot) => new { item, slot })
            .Where(row => row.item is Axe)
            .OrderByDescending(row => ((Axe)row.item!).UpgradeLevel)
            .ThenBy(row => row.slot)
            .FirstOrDefault();
        var axe = axeInventory?.item as Axe;
        var next = pending is null && axe is not null
            ? ReadNextAxeUpgrade(player, axe, axeInventory!.slot)
            : null;

        var serviceStatus = counterTiles.Length == 0 || blacksmith is null
            ? "blocked_blacksmith_endpoint_missing"
            : !clintAtBlacksmith
                ? "blocked_clint_not_at_blacksmith"
                : !actorReady
                    ? "blocked_menu_dialogue_event_or_actor_control"
                    : string.Equals(currentLocation, "Blacksmith", StringComparison.OrdinalIgnoreCase)
                        ? "ready"
                        : "route_to_blacksmith_required";
        var lifecycleStage = pending switch
        {
            Axe when pendingDays > 0 => "wait_for_axe_upgrade",
            Axe when pendingDays <= 0 && freeSlots <= 0 => "blocked_axe_claim_inventory_full",
            Axe when pendingDays <= 0 && serviceStatus == "ready" => "claim_ready_axe_upgrade",
            Axe when pendingDays <= 0 && serviceStatus == "route_to_blacksmith_required" =>
                "route_to_blacksmith_tool_claim",
            not null => "wait_for_other_tool_upgrade",
            null when next is null => "blocked_no_next_axe_upgrade",
            null when !next.RequirementsSatisfied => "blocked_tool_upgrade_requirements_missing",
            null when serviceStatus == "ready" => "submit_next_axe_upgrade",
            null when serviceStatus == "route_to_blacksmith_required" =>
                "route_to_blacksmith_tool_upgrade",
            _ => "blocked_blacksmith_service_unavailable"
        };

        var body = new
        {
            location_id = "Blacksmith",
            counter_action_tiles = counterTiles,
            current_location_id = currentLocation,
            clint = new
            {
                present_at_blacksmith = clintAtBlacksmith,
                current_location = clint?.currentLocation?.NameOrUniqueName ?? string.Empty,
                tile_x = clint?.TilePoint.X,
                tile_y = clint?.TilePoint.Y
            },
            actor_control_ready = actorReady,
            free_inventory_slots = freeSlots,
            money_before = player.Money,
            service_status = serviceStatus,
            lifecycle_stage = lifecycleStage,
            pending_tool = new
            {
                present = pending is not null,
                qualified_item_id = pending?.QualifiedItemId ?? string.Empty,
                item_id = pending?.ItemId ?? string.Empty,
                runtime_type = pending?.GetType().FullName ?? string.Empty,
                upgrade_level = pending?.UpgradeLevel ?? -1,
                days_left = pendingDays,
                claim_ready = pending is not null && pendingDays <= 0,
                claim_capacity_satisfied = pending is GenericTool || freeSlots > 0
            },
            current_axe = new
            {
                present = axe is not null,
                slot_index = axeInventory?.slot ?? -1,
                qualified_item_id = axe?.QualifiedItemId ?? string.Empty,
                item_id = axe?.ItemId ?? string.Empty,
                upgrade_level = axe?.UpgradeLevel ?? -1
            },
            next_axe_upgrade = next
        };
        return new
        {
            schema_version = "tool_upgrade.v1",
            projection_status = "complete_live_native_contract",
            projection_fingerprint = GeodeProcessingSha256(JsonSerializer.Serialize(body)),
            body.location_id,
            body.counter_action_tiles,
            body.current_location_id,
            body.clint,
            body.actor_control_ready,
            body.free_inventory_slots,
            body.money_before,
            body.service_status,
            body.lifecycle_stage,
            body.pending_tool,
            body.current_axe,
            body.next_axe_upgrade,
            native_contract = ToolUpgradeNativeContract,
            unit_of_work = "one_native_upgrade_submission_or_one_native_completed_tool_claim_per_action",
            direct_mutation_policy = "production_executor_must_not_write_money_inventory_toolBeingUpgraded_or_daysLeftForToolUpgrade"
        };
    }

    private static NextAxeUpgradeProjection? ReadNextAxeUpgrade(
        Farmer player,
        Axe current,
        int sourceSlot)
    {
        foreach (var pair in Game1.toolData
                     .Where(pair => string.Equals(
                         pair.Value.ConventionalUpgradeFrom,
                         current.ItemId,
                         StringComparison.Ordinal))
                     .OrderBy(pair => pair.Value.UpgradeLevel)
                     .ThenBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (ItemRegistry.Create("(T)" + pair.Key, allowNull: true) is not Axe target ||
                target.UpgradeLevel != current.UpgradeLevel + 1)
            {
                continue;
            }

            ToolUpgradeData? upgrade = ShopBuilder.GetToolUpgradeData(pair.Value, player);
            if (upgrade is null || !string.Equals(upgrade.RequireToolId, current.ItemId, StringComparison.Ordinal))
                continue;
            var tradeQid = string.IsNullOrWhiteSpace(upgrade.TradeItemId)
                ? string.Empty
                : ItemRegistry.QualifyItemId(upgrade.TradeItemId);
            var tradeAvailable = string.IsNullOrWhiteSpace(tradeQid)
                ? 0
                : player.Items.Where(item => item?.QualifiedItemId == tradeQid).Sum(item => item!.Stack);
            var requirementsSatisfied = player.Money >= upgrade.Price &&
                (string.IsNullOrWhiteSpace(tradeQid) || tradeAvailable >= upgrade.TradeItemAmount);
            return new NextAxeUpgradeProjection(
                true,
                sourceSlot,
                current.QualifiedItemId,
                current.ItemId,
                current.UpgradeLevel,
                target.QualifiedItemId,
                target.ItemId,
                target.UpgradeLevel,
                upgrade.Price,
                tradeQid,
                upgrade.TradeItemId ?? string.Empty,
                upgrade.TradeItemAmount,
                tradeAvailable,
                player.Money,
                requirementsSatisfied,
                "Game1.toolData.ConventionalUpgradeFrom+ShopBuilder.GetToolUpgradeData");
        }
        return null;
    }

    private sealed record NextAxeUpgradeProjection(
        [property: JsonPropertyName("available")] bool Available,
        [property: JsonPropertyName("source_slot_index")] int SourceSlotIndex,
        [property: JsonPropertyName("source_qualified_item_id")] string SourceQualifiedItemId,
        [property: JsonPropertyName("source_item_id")] string SourceItemId,
        [property: JsonPropertyName("source_upgrade_level")] int SourceUpgradeLevel,
        [property: JsonPropertyName("target_qualified_item_id")] string TargetQualifiedItemId,
        [property: JsonPropertyName("target_item_id")] string TargetItemId,
        [property: JsonPropertyName("target_upgrade_level")] int TargetUpgradeLevel,
        [property: JsonPropertyName("price_gold")] int PriceGold,
        [property: JsonPropertyName("trade_item_qualified_item_id")] string TradeItemQualifiedItemId,
        [property: JsonPropertyName("trade_item_id")] string TradeItemId,
        [property: JsonPropertyName("trade_item_amount")] int TradeItemAmount,
        [property: JsonPropertyName("trade_item_available")] int TradeItemAvailable,
        [property: JsonPropertyName("money_available")] int MoneyAvailable,
        [property: JsonPropertyName("requirements_satisfied")] bool RequirementsSatisfied,
        [property: JsonPropertyName("source_contract")] string SourceContract);
}
