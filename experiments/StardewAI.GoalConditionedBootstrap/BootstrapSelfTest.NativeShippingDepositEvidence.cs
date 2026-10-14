using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyNativeShippingDepositEvidence()
    {
        const string itemId = "(O)24";
        var before = ShippingDepositSnapshot(
            slotStack: 2,
            binItemCount: 4,
            pendingValue: 200,
            money: 1_000,
            totalMoneyEarned: 4_000,
            gameTick: 10);
        var after = ShippingDepositSnapshot(
            slotStack: 1,
            binItemCount: 5,
            pendingValue: 250,
            money: 1_000,
            totalMoneyEarned: 4_000,
            gameTick: 11);
        var queue = ShippingDepositQueue(
            slotIndex: 3,
            unitPrice: 50,
            binTileX: 71);
        var receipt = ShippingDepositReceipt(
            queue,
            before,
            after,
            inventoryBefore: 2,
            inventoryAfter: 1,
            binBefore: 4,
            binAfter: 5);

        var evidence = NativeShippingDepositEvidenceVerifier.Verify(
            queue,
            receipt,
            before,
            after,
            itemId,
            expectedQuantity: 1);
        Require(
            evidence.Verified &&
            evidence.BeforeBinItemQuantity == 4 &&
            evidence.AfterBinItemQuantity == 5 &&
            evidence.BeforeBinVariantQuantity == 4 &&
            evidence.AfterBinVariantQuantity == 5 &&
            evidence.BinTileX == 71 &&
            evidence.BinScope == "shared",
            "Exact shipping deposit evidence was not admitted: " +
            string.Join(",", evidence.BlockingReasons));

        var singleBefore = ShippingDepositSnapshot(
            1, 0, 0, 1_000, 4_000, gameTick: 20);
        var singleAfter = ShippingDepositSnapshot(
            0, 1, 50, 1_000, 4_000, gameTick: 21);
        var singleReceipt = ShippingDepositReceipt(
            queue,
            singleBefore,
            singleAfter,
            inventoryBefore: 1,
            inventoryAfter: 0,
            binBefore: 0,
            binAfter: 1);
        var singleEvidence = NativeShippingDepositEvidenceVerifier.Verify(
            queue,
            singleReceipt,
            singleBefore,
            singleAfter,
            itemId,
            expectedQuantity: 1);
        Require(
            singleEvidence.Verified &&
            singleEvidence.AfterInventorySlotQuantity == 0,
            "A one-item stack reaching an empty slot was not admitted: " +
            string.Join(",", singleEvidence.BlockingReasons));

        RequireShippingDepositRejected(
            ShippingDepositQueue(4, 50, 71),
            ShippingDepositReceipt(
                ShippingDepositQueue(4, 50, 71),
                before,
                after,
                2,
                1,
                4,
                5),
            before,
            after,
            "shipping_deposit_before_inventory_slot_not_unique",
            "A shipping deposit bound to the wrong inventory slot was admitted.");
        RequireShippingDepositRejected(
            ShippingDepositQueue(3, 49, 71),
            ShippingDepositReceipt(
                ShippingDepositQueue(3, 49, 71),
                before,
                after,
                2,
                1,
                4,
                5),
            before,
            after,
            "shipping_deposit_inventory_unit_price_mismatch",
            "A shipping deposit with the wrong unit price was admitted.");
        RequireShippingDepositRejected(
            ShippingDepositQueue(3, 50, 72),
            ShippingDepositReceipt(
                ShippingDepositQueue(3, 50, 72),
                before,
                after,
                2,
                1,
                4,
                5),
            before,
            after,
            "shipping_deposit_before_bin_endpoint_not_unique",
            "A shipping deposit bound to the wrong bin endpoint was admitted.");

        var paidAfter = ShippingDepositSnapshot(
            slotStack: 1,
            binItemCount: 5,
            pendingValue: 250,
            money: 1_050,
            totalMoneyEarned: 4_050,
            gameTick: 11);
        RequireShippingDepositRejected(
            queue,
            ShippingDepositReceipt(queue, before, paidAfter, 2, 1, 4, 5),
            before,
            paidAfter,
            "shipping_deposit_money_changed",
            "Immediate shipping-bin payment was admitted as a native deposit.");

        var mismatchedFacts = ShippingDepositReceipt(
            queue,
            before,
            after,
            inventoryBefore: 3,
            inventoryAfter: 2,
            binBefore: 8,
            binAfter: 9);
        RequireShippingDepositRejected(
            queue,
            mismatchedFacts,
            before,
            after,
            "shipping_deposit_changed_fact_snapshot_mismatch",
            "Changed facts from another deposit state were admitted.");
    }

    private static void RequireShippingDepositRejected(
        ActionQueueItem[] queue,
        QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string expectedReason,
        string message)
    {
        var evidence = NativeShippingDepositEvidenceVerifier.Verify(
            queue,
            receipt,
            before,
            after,
            "(O)24",
            expectedQuantity: 1);
        Require(
            !evidence.Verified &&
            evidence.BlockingReasons.Contains(
                expectedReason,
                StringComparer.Ordinal),
            message + " Reasons: " +
            string.Join(",", evidence.BlockingReasons));
    }

}
