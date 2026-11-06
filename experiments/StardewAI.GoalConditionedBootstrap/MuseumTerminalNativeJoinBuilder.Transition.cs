using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumTerminalNativeJoinBuilder
{
    private static MuseumTerminalTransition ValidateTransition(CurrentMuseumCollectionIntake intake, GoalRequirementSet set,
        SnapshotEnvelope before, SnapshotEnvelope after, string itemId, int slot)
    {
        Require(intake.Items is { Length: 95 } && intake.DonatedCount == 94 && intake.MissingItemCount == 1 &&
            intake.SourceStateHash == before.StateHash && intake.Items.Count(row => row.Donated) == 94 &&
            intake.Items.Select(row => row.RequirementId).Distinct(StringComparer.Ordinal).Count() == 95 &&
            set.RequiredGroupCount == 95 && set.Groups.Length == 95 &&
            intake.Items.Select(row => row.RequirementId).ToHashSet(StringComparer.Ordinal).SetEquals(set.Groups.Select(row => row.RequirementId)) &&
            intake.Items.All(row => set.Groups.Single(group => group.RequirementId == row.RequirementId).Alternatives.Single().QualifiedItemId == row.QualifiedItemId) &&
            intake.Items.Single(row => !row.Donated).QualifiedItemId == itemId && !intake.NativeAchievementReceived && !intake.CollectionComplete,
            "Museum join lost the exact complete original missing-item denominator.");
        var progress = CurrentCollectionTeacherFrontierBuilder.ReadMuseumProgressValue(Value(after, "world_progress", "museum"), set);
        Require(progress.Available && progress.AggregateCompletedCount == 95 && progress.Requirements.Count == 95 &&
            progress.Requirements.Values.All(row => row.Completed), "Museum native full95 denominator is incomplete.");
        var items = intake.Items.Select(row => new MuseumTerminalJoinedItem(row.RequirementId, row.QualifiedItemId,
            row.Donated, progress.Requirements[row.RequirementId].Completed)).ToArray();
        Require(items.Count(row => row.DonatedBefore != row.DonatedAfter) == 1 &&
            items.Single(row => row.DonatedBefore != row.DonatedAfter).QualifiedItemId == itemId &&
            !Achievement(before) && Achievement(after), "Museum native unique donation/achievement5 transition differs.");
        var previous = Slot(before, slot);
        var current = Slot(after, slot);
        Require(previous.TryGetProperty("qualified_item_id", out var previousId) && previousId.GetString() == itemId &&
            Integer(previous, "stack") > 0 && Integer(previous, "quality") >= 0, "Museum selected native inventory slot differs.");
        var stackBefore = Integer(previous, "stack");
        var empty = current.TryGetProperty("is_empty", out var isEmpty) && isEmpty.ValueKind == JsonValueKind.True;
        var stackAfter = empty ? 0 : Integer(current, "stack");
        Require(stackAfter == stackBefore - 1 && (empty ||
            current.TryGetProperty("qualified_item_id", out var currentId) && currentId.GetString() == itemId &&
            Integer(current, "quality") == Integer(previous, "quality")), "Museum exact selected slot did not consume one copy.");
        var totalBefore = ExactInventoryReceiptVerifier.InventoryCount(before, itemId, Integer(previous, "quality"));
        var totalAfter = ExactInventoryReceiptVerifier.InventoryCount(after, itemId, Integer(previous, "quality"));
        var menu = Value(after, "menus", "active_menu");
        Require(totalBefore.HasValue && totalAfter.HasValue && totalBefore.Value - totalAfter.Value == 1 &&
            menu.ValueKind == JsonValueKind.Object && menu.TryGetProperty("is_open", out var open) && open.ValueKind == JsonValueKind.False,
            "Museum total inventory delta or native menu exit differs.");
        return new(items, stackBefore, stackAfter);
    }

    private static bool Achievement(SnapshotEnvelope snapshot)
    {
        var museum = Value(snapshot, "world_progress", "museum");
        var achievements = Value(snapshot, "world_progress", "achievements");
        Require(achievements.ValueKind == JsonValueKind.Array && achievements.EnumerateArray().All(row =>
            row.ValueKind == JsonValueKind.Number && row.TryGetInt32(out _)) &&
            museum.TryGetProperty("complete_collection_achievement_received", out var received) &&
            received.ValueKind is JsonValueKind.True or JsonValueKind.False, "Museum native achievement facts are unavailable.");
        var achievement = achievements.EnumerateArray().Any(row => row.GetInt32() == 5);
        Require(museum.GetProperty("complete_collection_achievement_received").GetBoolean() == achievement,
            "Museum native achievement projections disagree.");
        return achievement;
    }

    private static JsonElement Slot(SnapshotEnvelope snapshot, int slot)
    {
        var inventory = Value(snapshot, "player", "inventory");
        Require(slot >= 0 && inventory.ValueKind == JsonValueKind.Array, "Museum native inventory is unavailable.");
        var matches = inventory.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object &&
            row.TryGetProperty("slot_index", out var index) && index.ValueKind == JsonValueKind.Number && index.TryGetInt32(out var value) && value == slot).ToArray();
        Require(matches.Length == 1, "Museum native selected slot is missing or ambiguous.");
        return matches[0];
    }

    private static int Integer(JsonElement row, string name)
    {
        Require(row.TryGetProperty(name, out var field) && field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out _),
            "Museum native exact integer missing: " + name);
        return field.GetInt32();
    }
}

internal sealed record MuseumTerminalTransition(MuseumTerminalJoinedItem[] Items, int StackBefore, int StackAfter);
