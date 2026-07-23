using System;
using StardewAI.Contracts.Execution;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Execution
{
    public sealed partial class ActionQueueCompiler
    {
        private static CompiledActionStep[] CompileSellShopItemStep(SmallModelAction action)
        {
            var slotIndex = ReadIntParameter(action, "slot_index");
            var qualifiedItemId = ReadParameter(action, "qualified_item_id");
            var quantity = ReadIntParameter(action, "quantity");
            var expectedUnitPrice = ReadIntParameter(action, "expected_unit_price");
            if (!slotIndex.HasValue ||
                string.IsNullOrWhiteSpace(qualifiedItemId) ||
                !quantity.HasValue ||
                quantity.Value <= 0 ||
                !expectedUnitPrice.HasValue ||
                expectedUnitPrice.Value <= 0)
            {
                return Array.Empty<CompiledActionStep>();
            }

            return new[]
            {
                Step(
                    "sell_shop_item",
                    "inventory_slot[" + slotIndex.Value + "]=" + qualifiedItemId + "x" + quantity.Value,
                    "player.inventory_count_decreases;player.money_increases",
                    20)
            };
        }
    }
}
