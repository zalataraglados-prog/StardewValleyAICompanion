using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Backend.Tests;

internal static class CropTeacherNativeSupportFixture
{
    internal static void CompleteResources(SnapshotEnvelope snapshot, int money)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var clock = JsonNode.Parse(snapshot.State["time"].GetRawText())!;
        clock["time"] = JsonSerializer.SerializeToNode(new { status = "available", confidence = 1d, value = 600 }, options);
        snapshot.State["time"] = JsonSerializer.SerializeToElement(clock, options);
        snapshot.InGameTime = new FieldEnvelope<int?> { Status = FieldStatus.Available, Value = 600 };
        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!;
        player["money"]!["value"] = money;
        player["shop_currency_balances"] = JsonSerializer.SerializeToNode(new
        {
            status = "available", confidence = 1d, value = new
            {
                schema_version = "shop_currency_balances.v1",
                projection_status = "complete_locked_base_1.6.15_shop_menu_currency_domain",
                supported_currency_ids = NativeShopCurrencies.All.Select(row => row.Id),
                rows = NativeShopCurrencies.All.Select(row => new { currency_id = row.Id, currency_key = row.Key,
                    balance = row.Id == 0 ? money : 0 })
            }
        }, options);
        player["money"]!["confidence"] = 1d;
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player, options);
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!;
        farm["material_inventory_graph"]!["confidence"] = 1d;
        var node = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!;
        node["location_id"] = "Farm";
        node["capacity"] = 12;
        node["slots"]![0]!["quality"] = 0;
        node["slots"]![0]!["maximum_stack_size"] = 999;
        node["slots"]![0]!["runtime_type"] = "StardewValley.Object";
        farm["crops"] = JsonSerializer.SerializeToNode(new { status = "available", confidence = 1d, value = Array.Empty<object>() }, options);
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm, options);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }
}
