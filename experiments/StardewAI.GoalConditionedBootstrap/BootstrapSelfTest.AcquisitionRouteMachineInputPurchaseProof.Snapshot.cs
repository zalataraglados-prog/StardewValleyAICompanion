using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static SnapshotEnvelope WriteMachineInputPurchaseProofBeforeSnapshot(
        string templatePath,
        string outputPath)
    {
        _ = WriteMachineInputLoadProofBeforeSnapshot(templatePath, outputPath);
        var root = JsonNode.Parse(File.ReadAllText(outputPath))!.AsObject();
        SetMachineInputPurchaseProofState(
            root,
            inputQuantity: 0,
            money: 1_000,
            shopMenuOpen: true,
            readAtTick: 40);
        root["state"]!["farm"]!["machines"] =
            MachineMaterialTransferProofMachines(
                afterTransfer: false,
                readAtTick: 40);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static SnapshotEnvelope WriteMachineInputPurchaseProofAfterSnapshot(
        string beforePath,
        string outputPath)
    {
        var root = JsonNode.Parse(File.ReadAllText(beforePath))!.AsObject();
        SetMachineInputPurchaseProofState(
            root,
            inputQuantity: 1,
            money: 920,
            shopMenuOpen: false,
            readAtTick: 42);
        root["state"]!["farm"]!["machines"] =
            MachineMaterialTransferProofMachines(
                afterTransfer: true,
                readAtTick: 42);
        return WriteMachineCapacityProofSnapshot(root, outputPath);
    }

    private static void SetMachineInputPurchaseProofState(
        JsonObject root,
        int inputQuantity,
        int money,
        bool shopMenuOpen,
        int readAtTick)
    {
        root["game_tick"] = readAtTick;
        var player = root["state"]!["player"]!.AsObject();
        player["location_id"]!["value"] = "FixtureShop";
        player["tile_x"]!["value"] = 2;
        player["tile_y"]!["value"] = 5;
        player["inventory"]!["value"] = inputQuantity == 0
            ? new JsonArray()
            : JsonSerializer.SerializeToNode(new[]
            {
                MachineInputPurchaseProofInventoryItem(inputQuantity)
            }, JsonDefaults.Options);
        player["inventory_capacity"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                occupied_stacks = inputQuantity > 0 ? 1 : 0,
                empty_slots = inputQuantity > 0 ? 35 : 36,
                has_empty_slot = true
            },
            status = "available",
            source = MachineInputPurchaseProofSource(),
            adapter = "test",
            read_at_tick = readAtTick,
            confidence = 1
        }, JsonDefaults.Options);
        player["money"]!["value"] = money;
        SetMachineInputPurchaseProofEnvelopeMetadata(
            player["money"]!.AsObject(),
            readAtTick);
        player["shop_currency_balances"]!["value"] =
            JsonSerializer.SerializeToNode(new
            {
                schema_version = "shop_currency_balances.v1",
                projection_status =
                    "complete_locked_base_1.6.15_shop_menu_currency_domain",
                supported_currency_ids = new[] { 0, 1, 2, 4 },
                rows = new object[]
                {
                    new
                    {
                        currency_id = 0,
                        currency_key = "money",
                        balance = money
                    },
                    new
                    {
                        currency_id = 1,
                        currency_key = "star_tokens",
                        balance = 0
                    },
                    new
                    {
                        currency_id = 2,
                        currency_key = "club_coins",
                        balance = 0
                    },
                    new
                    {
                        currency_id = 4,
                        currency_key = "qi_gems",
                        balance = 0
                    }
                }
            }, JsonDefaults.Options);
        SetMachineInputPurchaseProofEnvelopeMetadata(
            player["shop_currency_balances"]!.AsObject(),
            readAtTick);

        SetMachineInputLoadProofMaterialInventory(root, inputQuantity);
        SetMachineInputPurchaseProofEnvelopeMetadata(
            root["state"]!["farm"]!["material_inventory_graph"]!.AsObject(),
            readAtTick);
        AddMachineInputPurchaseProofReturnRoute(root, readAtTick);

        var menus = root["state"]!["menus"]!.AsObject();
        menus["active_menu"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                is_open = shopMenuOpen,
                type = shopMenuOpen ? "ShopMenu" : "none"
            },
            status = "available",
            source = MachineInputPurchaseProofSource(),
            adapter = "test",
            read_at_tick = readAtTick,
            confidence = 1
        }, JsonDefaults.Options);
        menus["sleep_prompt_context"] = JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                prompt_open = false,
                can_confirm_sleep = false,
                confirm_executor_enabled = false,
                confirm_action_key = "Sleep_Yes"
            },
            status = "available",
            source = MachineInputPurchaseProofSource(),
            adapter = "test",
            read_at_tick = readAtTick,
            confidence = 1
        }, JsonDefaults.Options);
        menus["shop_stock"] = shopMenuOpen
            ? MachineInputPurchaseProofShopStock(money, readAtTick)
            : JsonSerializer.SerializeToNode(new
            {
                value = (object?)null,
                status = "unavailable",
                source = MachineInputPurchaseProofSource(),
                adapter = "test",
                read_at_tick = readAtTick,
                confidence = 1
            }, JsonDefaults.Options);
    }

    private static JsonNode MachineInputPurchaseProofShopStock(
        int money,
        int readAtTick) => JsonSerializer.SerializeToNode(new
        {
            value = new
            {
                kind = "shop_stock",
                shop_id = "FixtureShop",
                read_only = false,
                safety_timer = 0,
                entry_count = 1,
                entries = new[]
                {
                    new
                    {
                        item_id = "262",
                        qualified_item_id = "(O)262",
                        display_name = "Wheat Seeds",
                        stack = 1,
                        quality = 0,
                        synced_key = "fixture-wheat-seed",
                        price = 80,
                        stock = 999,
                        infinite_stock = false,
                        currency_balance = money,
                        can_buy_item = true,
                        can_afford_one_with_currency = money >= 80,
                        can_afford_one_with_trade_item = true,
                        could_inventory_accept = true,
                        executor_purchase_enabled = true,
                        executor_block_reasons = Array.Empty<string>()
                    }
                }
            },
            status = "available",
            source = MachineInputPurchaseProofSource(),
            adapter = "test",
            read_at_tick = readAtTick,
            confidence = 1
        }, JsonDefaults.Options)!;

    private static object MachineInputPurchaseProofInventoryItem(int quantity) =>
        new
        {
            slot_index = 0,
            item_id = "262",
            qualified_item_id = "(O)262",
            stack = quantity,
            quality = 0,
            maximum_stack_size = 999,
            is_empty = false,
            sale_price = 25
        };

    private static object MachineInputPurchaseProofSource() => new
    {
        kind = "test",
        path = "machine-input-purchase-proof"
    };

    private static void SetMachineInputPurchaseProofEnvelopeMetadata(
        JsonObject envelope,
        int readAtTick)
    {
        envelope["status"] = "available";
        envelope["source"] = JsonSerializer.SerializeToNode(
            MachineInputPurchaseProofSource(),
            JsonDefaults.Options);
        envelope["adapter"] = "test";
        envelope["read_at_tick"] = readAtTick;
        envelope["confidence"] = 1;
    }

    private static void AddMachineInputPurchaseProofReturnRoute(
        JsonObject root,
        int readAtTick)
    {
        var routeGraph = root["state"]!["locations"]!["route_graph"]!
            .AsObject();
        SetMachineInputPurchaseProofEnvelopeMetadata(routeGraph, readAtTick);
        var edges = routeGraph["value"]!["edges"]!.AsArray();
        if (!edges.Any(edge =>
                edge?["from_location"]?.GetValue<string>() == "FixtureShop" &&
                edge["target_location"]?.GetValue<string>() == "Town"))
        {
            edges.Add(JsonSerializer.SerializeToNode(new
            {
                kind = "building_door",
                from_location = "FixtureShop",
                from_x = 1,
                from_y = 5,
                target_location = "Town",
                target_x = 4,
                target_y = 4,
                resolved = true
            }, JsonDefaults.Options));
        }

        if (!edges.Any(edge =>
                edge?["from_location"]?.GetValue<string>() == "Town" &&
                edge["target_location"]?.GetValue<string>() == "Farm"))
        {
            edges.Add(JsonSerializer.SerializeToNode(new
            {
                kind = "warp",
                from_location = "Town",
                from_x = 1,
                from_y = 5,
                target_location = "Farm",
                target_x = 2,
                target_y = 4,
                resolved = true
            }, JsonDefaults.Options));
        }
    }
}
