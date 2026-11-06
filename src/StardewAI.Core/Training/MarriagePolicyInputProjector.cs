using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.State;
using static StardewAI.Core.Infrastructure.SnapshotValueReader;

namespace StardewAI.Core.Training;

public sealed class MarriagePolicyInputProjector
{
    public const string GoalId = "goal.social.marriage_and_house_upgrade";
    private readonly FriendshipPolicyInputProjector contact;
    private readonly JsonElement[] inventory;

    public MarriagePolicyInputProjector(SnapshotEnvelope snapshot, string? calibrationJson)
    {
        if (string.IsNullOrWhiteSpace(calibrationJson))
            throw new InvalidOperationException("Marriage structured policy requires an explicit compatible route timing calibration.");
        var state = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        if (state.Status != "ready_current_state_only")
            throw new InvalidOperationException("Marriage policy current facts are unreadable: " + string.Join(",", state.BlockingReasons));
        // This helper is contact-only: no Friendship threshold/binding is consumed.
        contact = new FriendshipPolicyInputProjector(snapshot, calibrationJson);
        var slots = ReadStateFieldValue(snapshot, "player", "inventory");
        if (!ReadableStatus(ReadStateFieldStatus(snapshot, "player", "inventory")) || slots?.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Marriage policy inventory is unreadable.");
        inventory = slots.Value.EnumerateArray().Select(row => row.Clone()).ToArray();
    }

    public PolicyEventCandidatePrediction[] ProjectCandidates(IEnumerable<PolicyEventCandidatePrediction> candidates)
    {
        var projected = contact.ProjectCandidates(contact.CertifyCurrentCandidates(candidates));
        foreach (var row in projected)
        {
            var features = row.PolicyFeatures!;
            var numeric = new List<NumericFeature>(features.Numeric);
            var flags = new List<BooleanFeature>(features.Boolean);
            var categories = new List<CategoricalFeature>(features.Categorical);
            foreach (var field in new[] { "friendship_points_before", "expected_friendship_delta", "expected_friendship_points_after", "gifts_today_before", "gifts_this_week_before" })
            {
                var value = row.Parameters.Where(value => value.Name == field).ToArray();
                if (value.Length == 1 && int.TryParse(value[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
                    numeric.Add(new() { Name = "marriage.preparation." + field, Value = number });
            }
            var items = inventory.Where(item => row.SlotIndex.HasValue &&
                item.TryGetProperty("slot_index", out var slot) && slot.TryGetInt32(out var index) && index == row.SlotIndex.Value &&
                item.TryGetProperty("qualified_item_id", out var id) && id.GetString() == row.QualifiedItemId).ToArray();
            var priceKnown = items.Length == 1 && items[0].TryGetProperty("sell_to_store_price", out var price) &&
                price.TryGetInt32(out var sale) && sale >= 0;
            if (priceKnown) numeric.Add(new() { Name = "marriage.preparation.current_sale_value", Value = items[0].GetProperty("sell_to_store_price").GetInt32() });
            flags.Add(new() { Name = "marriage.preparation.current_sale_value_known", Value = priceKnown });
            categories.Add(new() { Name = "marriage.preparation.role", Value = row.OptionId == "social.gift_npc" ? "gift_support_not_joint_outcome" : "other_original_family_not_a_gift_label" });
            features.Numeric = numeric.ToArray(); features.Boolean = flags.ToArray(); features.Categorical = categories.ToArray();
        }
        return projected;
    }
}
