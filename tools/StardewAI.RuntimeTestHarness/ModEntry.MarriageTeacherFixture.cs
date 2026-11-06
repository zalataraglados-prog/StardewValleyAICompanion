using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecutePrepareMarriageTeacherInventory(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0) return Blocked(request, reasons.ToArray());
        if (!Context.IsWorldReady || Game1.IsMultiplayer ||
            Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") != "1" ||
            Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") != request.RunId ||
            string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("STARDEWAI_SAVE_ISOLATION_PATH")))
            return Blocked(request, "marriage_teacher_inventory_requires_owned_isolated_singleplayer_training_run");
        var ids = new[] { "(O)66", "(O)276", "(O)460" };
        var slots = Game1.player.Items.Select((item, index) => (item, index)).Where(row => row.item is null).Take(ids.Length).Select(row => row.index).ToArray();
        if (slots.Length != ids.Length) return Blocked(request, "marriage_teacher_inventory_requires_three_empty_slots");
        var items = ids.Select(id => ItemRegistry.Create(id, 1)).ToArray();
        if (items.Where((item, index) => item.QualifiedItemId != ids[index] || item.Stack != 1 || item is not StardewValley.Object).Any())
            return Blocked(request, "marriage_teacher_inventory_native_item_identity_mismatch");
        for (var index = 0; index < slots.Length; index++) Game1.player.Items[slots[index]] = items[index];
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId, OptionId = request.OptionId,
            BeforeStateHash = request.BeforeStateHash, Status = "applied", FeedbackAvailable = true,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"), CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_prepare_marriage_teacher_inventory", PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = new[] { "declared_owned_item_prerequisites_only_not_acquisition_or_teacher_credit" },
            RequestedEffect = "declared_marriage_teacher_inventory_prerequisites", ObservedEffect = string.Join(";", slots.Select((slot, index) => slot + ":" + ids[index])),
            ChangedFacts = slots.Select((slot, index) => new SimulatedFactChange
                { Path = "player.inventory.slot_" + slot, Before = "empty", After = ids[index] + ";stack=1;declared_prerequisite=true" }).ToArray()
        };
    }
}
