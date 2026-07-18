using System;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Infrastructure;

internal static partial class MachineInfrastructureProjectionEvaluator
{
    private static MachineCraftingProjection ReadMachineCraftingProjection(SnapshotEnvelope snapshot)
    {
        var context = ReadStateFieldValue(snapshot, "player", "machine_crafting");
        if (!context.HasValue || context.Value.ValueKind != JsonValueKind.Object)
        {
            return new MachineCraftingProjection();
        }

        var rows = context.Value.TryGetProperty("rows", out var rowArray) && rowArray.ValueKind == JsonValueKind.Array
            ? rowArray.EnumerateArray().Where(row => row.ValueKind == JsonValueKind.Object).ToArray()
            : Array.Empty<JsonElement>();
        var countStatuses = rows.Select(row => ReadString(row, "craftable_count_status")).Distinct(StringComparer.Ordinal).ToArray();
        var countStatus = countStatuses.Any(status => status.StartsWith("bounded_", StringComparison.Ordinal))
            ? "bounded_one_or_more_recipe_counts"
            : rows.Length == 0
                ? "not_applicable_no_known_machine_recipe"
                : "exact_all_known_machine_recipe_counts";
        var caskRows = rows.Where(row => ReadBool(row, "output_is_cask")).ToArray();
        var cask = caskRows.FirstOrDefault();
        var caskCraftable = cask.ValueKind == JsonValueKind.Object
            ? Math.Max(0, ReadInt(cask, "craftable_count_from_player_inventory"))
            : 0;
        var outputCount = rows.Sum(row =>
            Math.Max(0, ReadInt(row, "craftable_count_from_player_inventory")) *
            Math.Max(1, ReadInt(row, "output_count_per_craft", 1)));
        return new MachineCraftingProjection(
            ReadString(context.Value, "projection_status", "unavailable"),
            countStatus,
            rows.Length,
            rows.Count(row => string.Equals(ReadString(row, "craft_candidate_status"), "ready_for_native_personal_crafting_menu", StringComparison.Ordinal)),
            outputCount,
            Math.Max(0, ReadInt(context.Value, "unclassified_known_recipe_count")),
            caskRows.Length > 0,
            caskCraftable,
            cask.ValueKind == JsonValueKind.Object ? ReadString(cask, "craft_candidate_status") : "unavailable_cask_recipe_not_known",
            cask.ValueKind == JsonValueKind.Object ? ReadString(cask, "output_qualified_item_id") : string.Empty,
            cask.ValueKind == JsonValueKind.Object ? ReadString(cask, "placement_location_rule") : string.Empty);
    }

    private sealed record MachineCraftingProjection(
        string Status = "unavailable",
        string CountStatus = "unavailable",
        int KnownRecipeCount = 0,
        int ReadyRecipeCount = 0,
        int CraftableOutputCount = 0,
        int UnclassifiedKnownRecipeCount = 0,
        bool CaskRecipeKnown = false,
        int CaskCraftableCount = 0,
        string CaskCandidateStatus = "unavailable",
        string CaskOutputQualifiedItemId = "",
        string CaskPlacementLocationRule = "");
}
