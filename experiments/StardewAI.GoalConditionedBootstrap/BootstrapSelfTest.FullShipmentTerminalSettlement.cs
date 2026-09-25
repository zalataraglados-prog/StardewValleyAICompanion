using System.Text.Json;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyFullShipmentTerminalSettlementEvidence()
    {
        var required = new[] { "(O)24", "(O)60", "(O)80" };
        var before = FullShipmentTerminalSnapshot(
            "full-shipment-terminal-before",
            totalDay: 91,
            terminalShippedCount: 0,
            terminalBinCount: 1,
            achievement34: false);
        var after = FullShipmentTerminalSnapshot(
            "full-shipment-terminal-after",
            totalDay: 92,
            terminalShippedCount: 1,
            terminalBinCount: 0,
            achievement34: true);
        var evidence = FullShipmentTerminalSettlementVerifier.Verify(
            required,
            before,
            after);
        Require(
            evidence.Verified &&
            evidence.TerminalQualifiedItemId == "(O)24" &&
            evidence.BeforeShippedItemCount == 2 &&
            evidence.AfterShippedItemCount == 3 &&
            evidence.BeforeTerminalBinCount == 1 &&
            evidence.AfterTerminalBinCount == 0 &&
            evidence.Achievement34Before == false &&
            evidence.Achievement34After == true &&
            evidence.BeforeTotalDay == 91 &&
            evidence.AfterTotalDay == 92,
            "Full Shipment fresh terminal settlement was not admitted: " +
            string.Join(",", evidence.BlockingReasons));

        var missingAchievement = FullShipmentTerminalSettlementVerifier.Verify(
            required,
            before,
            FullShipmentTerminalSnapshot(
                "full-shipment-terminal-no-achievement",
                totalDay: 92,
                terminalShippedCount: 1,
                terminalBinCount: 0,
                achievement34: false));
        Require(
            !missingAchievement.Verified &&
            missingAchievement.BlockingReasons.Contains(
                "full_shipment_terminal_achievement_34_transition_missing"),
            "Full Shipment terminal settlement without achievement 34 was admitted.");

        var unclearedBin = FullShipmentTerminalSettlementVerifier.Verify(
            required,
            before,
            FullShipmentTerminalSnapshot(
                "full-shipment-terminal-uncleared-bin",
                totalDay: 92,
                terminalShippedCount: 1,
                terminalBinCount: 1,
                achievement34: true));
        Require(
            !unclearedBin.Verified &&
            unclearedBin.BlockingReasons.Contains(
                "full_shipment_terminal_shipping_bin_settlement_mismatch"),
            "Full Shipment terminal settlement with an uncleared bin was admitted.");

        var sameDay = FullShipmentTerminalSettlementVerifier.Verify(
            required,
            before,
            FullShipmentTerminalSnapshot(
                "full-shipment-terminal-same-day",
                totalDay: 91,
                terminalShippedCount: 1,
                terminalBinCount: 0,
                achievement34: true));
        Require(
            !sameDay.Verified &&
            sameDay.BlockingReasons.Contains(
                "full_shipment_terminal_native_day_transition_missing"),
            "Full Shipment terminal settlement without a native day transition was admitted.");

        var wrongAuthority = FullShipmentTerminalSettlementVerifier.Verify(
            required.Append("(O)82").ToArray(),
            before,
            after);
        Require(
            !wrongAuthority.Verified &&
            wrongAuthority.BlockingReasons.Any(reason =>
                reason.EndsWith(
                    "denominator_or_aggregate_mismatch",
                    StringComparison.Ordinal)),
            "Full Shipment terminal settlement accepted a drifted authority denominator.");
    }

    private static SnapshotEnvelope FullShipmentTerminalSnapshot(
        string stateHash,
        int totalDay,
        int terminalShippedCount,
        int terminalBinCount,
        bool achievement34)
    {
        var items = new[]
        {
            new
            {
                item_id = "24",
                qualified_item_id = "(O)24",
                current_shipped_count = terminalShippedCount,
                shipped = terminalShippedCount > 0
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
                current_shipped_count = 1,
                shipped = true
            }
        };
        var shippedCount = items.Count(item => item.shipped);
        var shippingCollection = new Dictionary<string, int>
        {
            ["60"] = 2,
            ["80"] = 1
        };
        if (terminalShippedCount > 0)
            shippingCollection["24"] = terminalShippedCount;
        var contents = terminalBinCount > 0
            ? new[]
            {
                new
                {
                    item_id = "24",
                    qualified_item_id = "(O)24",
                    count = terminalBinCount
                }
            }
            : Array.Empty<object>();
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
                    missing_item_ids = terminalShippedCount > 0
                        ? Array.Empty<string>()
                        : new[] { "24" }
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
