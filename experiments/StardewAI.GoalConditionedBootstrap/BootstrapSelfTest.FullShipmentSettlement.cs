using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void RunFullShipmentSettlement()
    {
        VerifyFullShipmentSettlementEvidence();
        VerifyFullShipmentTerminalSettlementEvidence();
    }

    private static void VerifyFullShipmentSettlementEvidence()
    {
        var required = new[] { "(O)24", "(O)60", "(O)80" };
        var before = FullShipmentSettlementSnapshot(
            "full-shipment-settlement-before",
            totalDay: 40,
            parsnipShipped: false,
            obsidianShipped: false,
            parsnipBinCount: 1,
            achievement34: false);
        var after = FullShipmentSettlementSnapshot(
            "full-shipment-settlement-after",
            totalDay: 41,
            parsnipShipped: true,
            obsidianShipped: false,
            parsnipBinCount: 0,
            achievement34: false);
        var evidence = FullShipmentSettlementVerifier.Verify(
            required,
            before,
            after);
        Require(
            evidence.Verified &&
            evidence.SettledQualifiedItemId == "(O)24" &&
            evidence.BeforeShippedItemCount == 1 &&
            evidence.AfterShippedItemCount == 2 &&
            evidence.BeforeMissingItemCount == 2 &&
            evidence.AfterMissingItemCount == 1 &&
            evidence.BeforeSettledItemBinCount == 1 &&
            evidence.AfterSettledItemBinCount == 0 &&
            evidence.BeforeTotalDay == 40 &&
            evidence.AfterTotalDay == 41 &&
            !evidence.TerminalTransition,
            "Full Shipment ordinary settlement was not admitted: " +
            string.Join(",", evidence.BlockingReasons));

        var twoItems = FullShipmentSettlementVerifier.Verify(
            required,
            before,
            FullShipmentSettlementSnapshot(
                "full-shipment-settlement-two-items",
                totalDay: 41,
                parsnipShipped: true,
                obsidianShipped: true,
                parsnipBinCount: 0,
                achievement34: true));
        Require(
            !twoItems.Verified &&
            twoItems.BlockingReasons.Contains(
                "full_shipment_settlement_not_exactly_one_new_item"),
            "A two-item Full Shipment transition entered one recurrence step.");

        var prematureAchievement = FullShipmentSettlementVerifier.Verify(
            required,
            before,
            FullShipmentSettlementSnapshot(
                "full-shipment-settlement-premature-achievement",
                totalDay: 41,
                parsnipShipped: true,
                obsidianShipped: false,
                parsnipBinCount: 0,
                achievement34: true));
        Require(
            !prematureAchievement.Verified &&
            prematureAchievement.BlockingReasons.Contains(
                "full_shipment_settlement_achievement_34_state_mismatch"),
            "A nonterminal Full Shipment step admitted achievement 34.");

        var terminal = FullShipmentSettlementVerifier.Verify(
            required,
            FullShipmentTerminalSnapshot(
                "full-shipment-settlement-terminal-before",
                totalDay: 223,
                terminalShippedCount: 0,
                terminalBinCount: 1,
                achievement34: false),
            FullShipmentTerminalSnapshot(
                "full-shipment-settlement-terminal-after",
                totalDay: 224,
                terminalShippedCount: 1,
                terminalBinCount: 0,
                achievement34: true));
        Require(
            terminal.Verified && terminal.TerminalTransition,
            "The shared verifier did not identify the dedicated terminal boundary.");
    }

    private static SnapshotEnvelope FullShipmentSettlementSnapshot(
        string stateHash,
        int totalDay,
        bool parsnipShipped,
        bool obsidianShipped,
        int parsnipBinCount,
        bool achievement34)
    {
        var items = new[]
        {
            new
            {
                item_id = "24",
                qualified_item_id = "(O)24",
                current_shipped_count = parsnipShipped ? 1 : 0,
                shipped = parsnipShipped
            },
            new
            {
                item_id = "60",
                qualified_item_id = "(O)60",
                current_shipped_count = 2,
                shipped = true
            },
            new
            {
                item_id = "80",
                qualified_item_id = "(O)80",
                current_shipped_count = obsidianShipped ? 1 : 0,
                shipped = obsidianShipped
            }
        };
        var shippedCount = items.Count(item => item.shipped);
        var shippingCollection = new Dictionary<string, int>
        {
            ["60"] = 2
        };
        if (parsnipShipped)
            shippingCollection["24"] = 1;
        if (obsidianShipped)
            shippingCollection["80"] = 1;
        var contents = parsnipBinCount > 0
            ? new[]
            {
                new
                {
                    item_id = "24",
                    qualified_item_id = "(O)24",
                    count = parsnipBinCount
                }
            }
            : Array.Empty<object>();
        var missingIds = items
            .Where(item => !item.shipped)
            .Select(item => item.item_id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var json = JsonSerializer.SerializeToElement(new
        {
            time = new
            {
                total_days = Available(totalDay)
            },
            farm = new
            {
                shipping_bins = Available(new[]
                {
                    new
                    {
                        contents
                    }
                })
            },
            world_progress = new
            {
                shipping_collection = Available(shippingCollection),
                achievements = Available(
                    achievement34 ? new[] { 5, 34 } : new[] { 5 }),
                full_shipment_progress = Available(new
                {
                    eligible_item_count = items.Length,
                    shipped_eligible_item_count = shippedCount,
                    missing_item_count = items.Length - shippedCount,
                    completion_ratio = shippedCount / (double)items.Length,
                    complete = shippedCount == items.Length,
                    items,
                    missing_item_ids = missingIds
                })
            }
        });
        return new SnapshotEnvelope
        {
            StateHash = stateHash,
            GameTick = totalDay * 100L,
            State = json.EnumerateObject().ToDictionary(
                property => property.Name,
                property => property.Value.Clone(),
                StringComparer.Ordinal)
        };
    }
}
