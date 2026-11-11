using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class CropSeedResourceBoundary
{
    private static readonly HashSet<string> CommunityCandidateFields = JsonSerializer.SerializeToNode(new CommunityCenterDonationCandidateRef())!
        .AsObject().Select(row => row.Key).ToHashSet(StringComparer.Ordinal);

    // Native eligibility/future-outcome previews are not donations. Settle inventory separately; never credit these previews as progress.
    internal static JsonObject? CompareHarvestCommunityCenterProgress(SnapshotEnvelope before, SnapshotEnvelope after)
    {
        var first = Field(before, "world_progress", "community_center", required: false);
        var next = Field(after, "world_progress", "community_center", required: false);
        if (!Readable(first) || !Readable(next))
        {
            EqualHarvestResourceField("world_progress", "community_center", first, next);
            return next;
        }
        var priorRows = Array(Value(first!)["bundle_rows"], "harvest community-center bundle rows");
        var currentRows = Array(Value(next!)["bundle_rows"], "harvest community-center bundle rows");
        Require(priorRows.All(row => row is JsonObject) && currentRows.All(row => row is JsonObject) &&
                priorRows.Count == currentRows.Count && priorRows.OfType<JsonObject>().Select(row => Int(row, "bundle_id"))
                    .Distinct().Count() == priorRows.Count,
            "Harvest community-center bundle identities are invalid.");
        for (var index = 0; index < priorRows.Count; index++)
        {
            var prior = priorRows[index]!; var current = currentRows[index]!;
            Require(Int(prior, "bundle_id") == Int(current, "bundle_id"), "Harvest community-center bundle order/identity changed.");
            VerifyCommunityCandidateInventory(prior, before);
            VerifyCommunityCandidateInventory(current, after);
            // Keep every backing progress/reward/mail/ingredient/unknown field in the original resource comparison.
            current["donation_candidates"] = prior["donation_candidates"]?.DeepClone();
        }
        EqualHarvestResourceField("world_progress", "community_center", first, next);
        return next;
    }

    private static void VerifyCommunityCandidateInventory(JsonNode bundle, SnapshotEnvelope snapshot)
    {
        var candidates = Array(bundle["donation_candidates"], "harvest community-center inventory previews");
        Require(candidates.All(row => row is JsonObject), "Harvest community-center inventory preview is not an object.");
        if (candidates.Count == 0) return;
        var slots = Rows(Value(Field(snapshot, "player", "inventory")!));
        var ingredients = Array(bundle["ingredients"], "harvest community-center ingredients");
        var seen = new HashSet<int>();
        foreach (var raw in candidates.OfType<JsonObject>())
        {
            Require(raw.Select(row => row.Key).ToHashSet(StringComparer.Ordinal).SetEquals(CommunityCandidateFields),
                "Harvest community-center preview has missing or unknown fields.");
            var candidate = raw.Deserialize<CommunityCenterDonationCandidateRef>()!;
            Require(candidate.InventorySlotIndex >= 0 && seen.Add(candidate.InventorySlotIndex) && slots.ContainsKey(candidate.InventorySlotIndex),
                "Harvest community-center preview inventory slot is invalid or duplicated.");
            var inventory = slots[candidate.InventorySlotIndex];
            Require(inventory["is_empty"]?.GetValue<bool>() != true &&
                    inventory["item_id"]?.GetValue<string>() == candidate.ItemId &&
                    inventory["qualified_item_id"]?.GetValue<string>() == candidate.QualifiedItemId &&
                    inventory["runtime_type"]?.GetValue<string>() == candidate.RuntimeType &&
                    Int(inventory, "quality") == candidate.Quality && Int(inventory, "stack") == candidate.StackBefore,
                "Harvest community-center preview differs from its raw inventory item.");
            var matching = ingredients.OfType<JsonObject>().Where(row => Int(row, "ingredient_index") == candidate.IngredientIndex).ToArray();
            Require(matching is [var ingredient] && !HarvestMirrorBoolean(ingredient, "completed") &&
                    candidate.RequiredStack > 0 && candidate.RequiredStack == Int(ingredient, "required_stack") &&
                    candidate.Quality >= Int(ingredient, "minimum_quality") && candidate.StackBefore >= candidate.RequiredStack &&
                    candidate.StackAfter == candidate.StackBefore - candidate.RequiredStack &&
                    candidate.CompletedIngredientCountBefore == Int(bundle, "completed_ingredient_count") &&
                    candidate.CompletedIngredientCountBefore < Int(bundle, "required_slot_count"),
                "Harvest community-center preview ingredient/quantity binding is inconsistent.");
            var total = slots.Values.Where(row => row["is_empty"]?.GetValue<bool>() != true &&
                    row["qualified_item_id"]?.GetValue<string>() == candidate.QualifiedItemId)
                .Sum(row => (long)Int(row, "stack"));
            Require(total >= candidate.RequiredStack && total <= int.MaxValue &&
                    candidate.InventoryItemTotalBefore == total && candidate.InventoryItemTotalAfter == total - candidate.RequiredStack,
                "Harvest community-center preview inventory total is inconsistent.");
        }
    }
}
