using StardewAI.Contracts.Execution;

namespace StardewAI.GoalConditionedBootstrap;

internal static class FullShipmentSettlementSupport
{
    public static string[] RequiredQualifiedItemIds(
        AuthoritativeRequirementInventoryReport inventory)
    {
        if (inventory.SchemaVersion !=
                "authoritative_goal_requirement_inventory.v1" ||
            !inventory.DenominatorComplete)
        {
            throw new InvalidDataException(
                "Full Shipment authoritative requirement inventory is incomplete.");
        }
        var set = CurrentTeacherFrontierSupport.SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            "full_shipment",
            "requirement inventory");
        if (set.RequiredGroupCount != 154 ||
            set.Groups.Length != set.RequiredGroupCount ||
            set.Groups.Any(group =>
                group.SelectionRule != "all_required" ||
                group.RequiredAlternativeCount != 1 ||
                group.Alternatives.Length != 1 ||
                group.Alternatives[0].MatchKind != "item_id" ||
                group.Alternatives[0].Amount != 1 ||
                group.Alternatives[0].MinimumQuality != 0 ||
                group.Alternatives[0].QualifiedItemId !=
                    "(O)" + group.Alternatives[0].ItemId))
        {
            throw new InvalidDataException(
                "Full Shipment authoritative requirement denominator drifted.");
        }
        var result = set.Groups
            .Select(group => group.Alternatives[0].QualifiedItemId)
            .Order(StringComparer.Ordinal)
            .ToArray();
        if (result.Distinct(StringComparer.Ordinal).Count() != result.Length)
        {
            throw new InvalidDataException(
                "Full Shipment authoritative requirement identities are duplicated.");
        }
        return result;
    }

    public static bool IsSingleNativeSleepQueue(ActionQueueEnvelope queue)
    {
        var items = queue.Items ?? Array.Empty<ActionQueueItem>();
        if (items.Length != 1)
            return false;
        var item = items[0];
        var parameters = item.NormalizedCommand?.Parameters ??
            Array.Empty<SmallModelActionParameter>();
        return item.OptionId == "executor.sleep" ||
            item.OptionId == "recovery.stabilize_day" &&
            parameters.Any(parameter =>
                parameter.Name == "execution_option_id" &&
                parameter.Value == "executor.sleep");
    }
}
