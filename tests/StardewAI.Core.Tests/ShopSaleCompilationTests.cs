using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class ShopSaleCompilationTests
{
    [Fact]
    public void ExactSaleCandidateCompilesIntoPendingNativeExecutorRequest()
    {
        var snapshot = Snapshot(unitPrice: 35);
        var plan = new DailyPlanCompiler().Compile(
            new[] { Candidate(unitPrice: 35) },
            snapshot.StateHash);

        var queue = new ActionQueueCompiler().Compile(plan, snapshot);

        Assert.Equal(2, queue.Items.Length);
        var sale = queue.Items[0];
        Assert.Equal("executor.sell_shop_item", sale.OptionId);
        Assert.True(
            sale.Status == "pending",
            "sale blockers: " + string.Join(",", sale.BlockingReasons));
        Assert.Contains(sale.NormalizedCommand.Parameters, row => row.Name == "slot_index" && row.Value == "0");
        Assert.Contains(sale.NormalizedCommand.Parameters, row => row.Name == "expected_unit_price" && row.Value == "35");
        Assert.Contains(sale.NormalizedCommand.Steps, row => row.StepType == "sell_shop_item");
    }

    [Fact]
    public void SalePriceDriftBlocksBeforeRuntimeInput()
    {
        var snapshot = Snapshot(unitPrice: 34);
        var plan = new DailyPlanCompiler().Compile(
            new[] { Candidate(unitPrice: 35) },
            snapshot.StateHash);

        var sale = new ActionQueueCompiler().Compile(plan, snapshot).Items[0];

        Assert.Equal("blocked", sale.Status);
        Assert.Contains("sell_unit_price_drift", sale.BlockingReasons);
    }

    private static PolicyEventCandidatePrediction Candidate(int unitPrice)
    {
        return new PolicyEventCandidatePrediction
        {
            CandidateId = "sell:0",
            Kind = "sell_shop_item",
            TimelineStatus = "ready_now",
            ShopId = "SeedShop",
            ItemId = "24",
            QualifiedItemId = "(O)24",
            SlotIndex = 0,
            Quantity = 3,
            UnitPrice = unitPrice,
            TotalValue = unitPrice * 3,
            CanShopSell = true
        };
    }

    private static SnapshotEnvelope Snapshot(int unitPrice)
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            $$"""
            {
              "player": {
                "money": {"value":500,"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "inventory": {"value":[{"slot_index":0,"item_id":"24","qualified_item_id":"(O)24","stack":3,"category":-75,"context_tags":["item_parsnip"],"sell_to_store_price":{{unitPrice}},"protected_from_auto_sell":false,"auto_sell_protection_reasons":[],"is_empty":false}],"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              },
              "menus": {
                "active_menu": {"value":{"is_open":true,"type":"ShopMenu"},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1},
                "sell_context": {"value":{"shop_id":"SeedShop","currency":0,"read_only":false,"safety_timer":0,"held_item_present":false,"storage_shop":false,"sell_percentage":1.0,"custom_on_sell_present":false,"categories_to_sell":[-75],"tag_groups_to_sell":[]},"status":"available","source":{"kind":"game_object","path":"test"},"adapter":"test","read_at_tick":1,"confidence":1}
              }
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        return new SnapshotEnvelope
        {
            StateHash = SnapshotHash.ComputeStateHash(state),
            State = state
        };
    }
}
