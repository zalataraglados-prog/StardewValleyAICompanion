using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupSingleGiftItem(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        if (!request.SlotIndex.HasValue ||
            request.SlotIndex.Value < 0 ||
            request.SlotIndex.Value >= Game1.player.Items.Count)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_single_gift_item",
                "player.inventory.single_gift_item=ready",
                "slot=invalid",
                "single_gift_fixture_slot_invalid");
        }

        if (string.IsNullOrWhiteSpace(request.QualifiedItemId))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_single_gift_item",
                "player.inventory.single_gift_item=ready",
                "item=missing",
                "single_gift_fixture_item_required");
        }

        var slot = request.SlotIndex.Value;
        if (Game1.player.Items[slot] is Tool)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_single_gift_item",
                "player.inventory.single_gift_item=ready",
                "slot=tool",
                "single_gift_fixture_slot_contains_tool");
        }

        var startedAt = DateTimeOffset.UtcNow.ToString("O");
        var removedNonToolCount = 0;
        for (var index = 0; index < Game1.player.Items.Count; index++)
        {
            if (Game1.player.Items[index] is not null &&
                Game1.player.Items[index] is not Tool)
            {
                Game1.player.Items[index] = null;
                removedNonToolCount++;
            }
        }

        var gift = ItemRegistry.Create(request.QualifiedItemId, 1);
        if (gift.QualifiedItemId != request.QualifiedItemId ||
            gift is Tool ||
            gift.Stack != 1)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_single_gift_item",
                "player.inventory.single_gift_item=ready",
                "item=" + gift.QualifiedItemId + ";stack=" + gift.Stack,
                "single_gift_fixture_item_creation_mismatch");
        }

        Game1.player.Items[slot] = gift;
        var nonTools = Game1.player.Items
            .Select((item, index) => new { item, index })
            .Where(row => row.item is not null && row.item is not Tool)
            .ToArray();
        var verified = nonTools.Length == 1 &&
            nonTools[0].index == slot &&
            nonTools[0].item!.QualifiedItemId == request.QualifiedItemId &&
            nonTools[0].item.Stack == 1;
        var observed = "slot=" + slot +
            ";item=" + (nonTools.FirstOrDefault()?.item?.QualifiedItemId ?? "none") +
            ";stack=" + (nonTools.FirstOrDefault()?.item?.Stack.ToString() ?? "none") +
            ";non_tool_count=" + nonTools.Length +
            ";removed_non_tool_count=" + removedNonToolCount;

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
            PrimitiveKind = "debug_setup_single_gift_item",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_runtime_single_gift_item_ready" }
                : new[] { "isolated_runtime_single_gift_item_not_unique" },
            RequestedEffect = "player.inventory.single_gift_item=ready",
            ObservedEffect = observed,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "single_gift_fixture_not_verified" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "player.inventory.single_gift_item",
                        Before = "uncontrolled",
                        After = observed
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
