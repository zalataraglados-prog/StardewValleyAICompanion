using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static ActionQueueItem[] ShippingDepositQueue(
        int slotIndex,
        int unitPrice,
        int binTileX) =>
    [
        new ActionQueueItem
        {
            QueueItemId = "shipping-deposit-item",
            OptionId = "executor.ship_inventory_item_to_bin",
            NormalizedCommand = new NormalizedCommand
            {
                OptionId = "executor.ship_inventory_item_to_bin",
                Parameters =
                [
                    ShippingParameter("slot_index", slotIndex.ToString()),
                    ShippingParameter("qualified_item_id", "(O)24"),
                    ShippingParameter("quantity", "1"),
                    ShippingParameter(
                        "expected_unit_price",
                        unitPrice.ToString()),
                    ShippingParameter("target_location", "Farm"),
                    ShippingParameter("target_tile_x", binTileX.ToString()),
                    ShippingParameter("target_tile_y", "14")
                ]
            }
        }
    ];

    private static SmallModelActionParameter ShippingParameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private static QueueExecutionReceiptEnvelope ShippingDepositReceipt(
        ActionQueueItem[] queue,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        int inventoryBefore,
        int inventoryAfter,
        int binBefore,
        int binAfter) => new()
        {
            RunId = "shipping-deposit-run",
            QueueId = "shipping-deposit-queue",
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = queue.Length,
            ExecutedItemCount = queue.Length,
            SelectedCandidateCompleted = true,
            StepResults =
        [
            new QueueExecutionStepReceipt
            {
                QueueItemIndex = 0,
                QueueItemCount = queue.Length,
                OriginalPlannedItemCount = queue.Length,
                QueueId = "shipping-deposit-queue",
                QueueItemId = queue[0].QueueItemId,
                OptionId = queue[0].OptionId,
                SourceStateHash = before.StateHash,
                CompiledCommandStateHash = before.StateHash,
                SelectedQueueCandidateCompleted = true,
                AfterStateHash = after.StateHash,
                StateHashChanged = true,
                BeforeGameTick = before.GameTick,
                AfterGameTick = after.GameTick,
                AfterSnapshotFresh = true,
                Status = "applied",
                PrimitiveKind = "ship_inventory_item_to_bin",
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons =
                [
                    "native_shipping_bin_transfer_observed"
                ],
                EffectiveQueueItem = JsonSerializer.SerializeToElement(
                    queue[0],
                    JsonDefaults.Options),
                ChangedFacts = JsonSerializer.SerializeToElement(new[]
                {
                    new
                    {
                        path = "player.inventory.(O)24.count",
                        before = inventoryBefore,
                        after = inventoryAfter
                    },
                    new
                    {
                        path = "farm.shipping_bin.(O)24.count",
                        before = binBefore,
                        after = binAfter
                    }
                })
            }
        ]
        };

    private static SnapshotEnvelope ShippingDepositSnapshot(
        int slotStack,
        int binItemCount,
        long pendingValue,
        int money,
        long totalMoneyEarned,
        long gameTick)
    {
        object[] binContents = binItemCount > 0
            ?
            [
                new
                {
                    item_id = "24",
                    qualified_item_id = "(O)24",
                    quality = 1,
                    count = binItemCount,
                    unit_sell_to_store_price = 50,
                    total_sell_to_store_price = binItemCount * 50
                }
            ]
            : Array.Empty<object>();
        object[] inventoryRows = slotStack > 0
            ?
            [
                new
                {
                    slot_index = 3,
                    item_id = "24",
                    qualified_item_id = "(O)24",
                    stack = slotStack,
                    quality = 1,
                    sell_to_store_price = 50,
                    is_empty = false
                }
            ]
            :
            [
                new
                {
                    slot_index = 3,
                    item_id = (string?)null,
                    qualified_item_id = (string?)null,
                    stack = (int?)null,
                    quality = (int?)null,
                    sell_to_store_price = (int?)null,
                    is_empty = true
                }
            ];
        var state = JsonSerializer.SerializeToElement(new
        {
            time = new
            {
                total_days = Available(3)
            },
            player = new
            {
                money = Available(money),
                total_money_earned = Available(totalMoneyEarned),
                inventory = Available(inventoryRows)
            },
            farm = new
            {
                shipping_bins = Available(new[]
                {
                    ShippingBin(71, 14, binContents, pendingValue),
                    ShippingBin(80, 20, binContents, pendingValue)
                })
            }
        });
        var stateDictionary = state.EnumerateObject().ToDictionary(
            property => property.Name,
            property => property.Value.Clone(),
            StringComparer.Ordinal);
        return new SnapshotEnvelope
        {
            GameVersion = "1.6.15",
            SaveId = new FieldEnvelope<string?>
            {
                Value = "shipping-deposit-save",
                Status = FieldStatus.Available
            },
            PlayerId = new FieldEnvelope<string?>
            {
                Value = "123",
                Status = FieldStatus.Available
            },
            State = stateDictionary,
            StateHash = SnapshotHash.ComputeStateHash(stateDictionary),
            GameTick = gameTick,
            Completeness = "complete"
        };
    }

    private static object ShippingBin(
        int tileX,
        int tileY,
        object contents,
        long pendingValue) => new
        {
            tile_x = tileX,
            tile_y = tileY,
            completed = true,
            bin_scope = "shared",
            player_id = 123L,
            contents,
            pending_settlement_value = pendingValue,
            pending_settlement_value_complete = true,
            contents_truncated = false
        };
}
