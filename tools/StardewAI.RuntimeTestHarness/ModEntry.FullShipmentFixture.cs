using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePrepareFullShipmentTerminalSleep(
        TrainingExecutionRequest request)
    {
        return ExecutePrepareNativeSleepFixture(
            request,
            "debug_prepare_full_shipment_terminal_sleep",
            "full_shipment_terminal_native_sleep_path_ready");
    }

    private TrainingExecutionResult ExecuteSetupFullShipmentTerminal(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        const string primitive = "debug_setup_full_shipment_terminal";
        const string requested =
            "world_progress.full_shipment_progress=single_missing_item;" +
            "farm.shipping_bins.contents=[];achievement_34=false";
        var farm = Game1.getFarm();
        if (farm is null)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "farm=missing",
                "full_shipment_fixture_farm_missing");
        }
        if (!request.FullShipmentExpectedEligibleItemCount.HasValue ||
            request.FullShipmentExpectedEligibleItemCount.Value <= 0)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "eligible_item_count=unresolved",
                "full_shipment_fixture_expected_denominator_missing");
        }
        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "terminal_item=unresolved",
                "full_shipment_fixture_terminal_item_missing");
        }

        var eligible = Game1.objectData
            .Where(pair =>
                pair.Value is not null &&
                pair.Value.Category != -7 &&
                pair.Value.Category != -2 &&
                StardewValley.Object.isPotentialBasicShipped(
                    pair.Key,
                    pair.Value.Category,
                    pair.Value.Type))
            .Select(pair => new
            {
                ItemId = pair.Key,
                QualifiedItemId =
                    ItemRegistry.QualifyItemId(pair.Key) ?? "(O)" + pair.Key
            })
            .OrderBy(pair => pair.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (eligible.Length !=
            request.FullShipmentExpectedEligibleItemCount.Value)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "eligible_item_count=" + eligible.Length,
                "full_shipment_fixture_native_denominator_drift");
        }

        var terminal = eligible.SingleOrDefault(pair => string.Equals(
            pair.QualifiedItemId,
            request.QualifiedItemId,
            StringComparison.Ordinal));
        if (terminal is null)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "terminal_item=" + request.QualifiedItemId,
                "full_shipment_fixture_terminal_item_not_native_eligible");
        }
        var completedBin = farm.buildings
            .OfType<StardewValley.Buildings.ShippingBin>()
            .FirstOrDefault(bin => bin.daysOfConstructionLeft.Value <= 0);
        if (completedBin is null)
        {
            return BlockedWithPrimitive(
                request,
                primitive,
                requested,
                "shipping_bin=missing",
                "full_shipment_fixture_completed_bin_missing");
        }

        var startedAt = DateTimeOffset.UtcNow.ToString("O");
        var master = Game1.MasterPlayer;
        Game1.exitActiveMenu();
        Game1.currentLocation = farm;
        Game1.player.currentLocation = farm;
        foreach (var item in eligible)
        {
            if (string.Equals(
                    item.QualifiedItemId,
                    terminal.QualifiedItemId,
                    StringComparison.Ordinal))
            {
                master.basicShipped.Remove(item.ItemId);
            }
            else
            {
                master.basicShipped[item.ItemId] = 1;
            }
        }
        master.achievements.Remove(34);
        farm.getShippingBin(Game1.player).Clear();
        farm.lastItemShipped = null;
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (string.Equals(
                    Game1.player.Items[index]?.QualifiedItemId,
                    terminal.QualifiedItemId,
                    StringComparison.Ordinal))
            {
                Game1.player.Items[index] = null;
            }
        }
        var terminalSlot = EnsureInventoryItem(terminal.QualifiedItemId, 1);
        var placement = ExecuteSetupShippingTarget(request);

        var shippedCount = eligible.Count(item =>
            master.basicShipped.TryGetValue(item.ItemId, out var count) &&
            count > 0);
        var terminalCount = master.basicShipped.TryGetValue(
            terminal.ItemId,
            out var existingTerminalCount)
            ? existingTerminalCount
            : 0;
        var terminalInventoryCount = Game1.player.Items
            .Where(item => string.Equals(
                item?.QualifiedItemId,
                terminal.QualifiedItemId,
                StringComparison.Ordinal))
            .Sum(item => item?.Stack ?? 0);
        var verified =
            terminalSlot >= 0 &&
            terminalInventoryCount == 1 &&
            shippedCount == eligible.Length - 1 &&
            terminalCount == 0 &&
            farm.getShippingBin(Game1.player).Count == 0 &&
            !master.achievements.Contains(34) &&
            placement.Status == "applied" &&
            placement.PrimitiveVerificationStatus == "verified";
        var observed =
            "eligible_item_count=" + eligible.Length +
            ";shipped_eligible_item_count=" + shippedCount +
            ";terminal_qualified_item_id=" + terminal.QualifiedItemId +
            ";terminal_shipped_count=" + terminalCount +
            ";terminal_inventory_slot=" + terminalSlot +
            ";terminal_inventory_count=" + terminalInventoryCount +
            ";shipping_bin_count=" + farm.getShippingBin(Game1.player).Count +
            ";achievement_34=" +
                master.achievements.Contains(34).ToString().ToLowerInvariant() +
            ";shipping_endpoint_fixture=" + placement.Status;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = startedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = primitive,
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_full_shipment_denominator_matched",
                    "single_terminal_item_missing",
                    "terminal_copy_installed",
                    "shipping_bin_cleared",
                    "achievement_34_cleared"
                }
                : new[] { "full_shipment_terminal_fixture_projection_mismatch" },
            RequestedEffect = requested,
            ObservedEffect = observed,
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "world_progress.full_shipment_progress.shipped_eligible_item_count",
                    Before = string.Empty,
                    After = shippedCount.ToString()
                },
                new SimulatedFactChange
                {
                    Path = "world_progress.full_shipment_progress.missing_item_ids",
                    Before = string.Empty,
                    After = terminal.ItemId
                },
                new SimulatedFactChange
                {
                    Path = "player.inventory.slot_index",
                    Before = string.Empty,
                    After = terminalSlot.ToString()
                }
            }
        };
    }
}
