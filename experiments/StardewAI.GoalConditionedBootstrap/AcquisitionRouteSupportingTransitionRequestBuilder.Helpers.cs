using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionRequestBuilder
{
    private static string SupportingTransitionKind(
        AcquisitionRouteTargetDateUnlock requirement,
        PolicyEventCandidatePrediction? candidate = null) =>
        candidate is
        {
            OptionId: "farm.establish_supported_machine_capacity",
            Kind: "craft_machine_item" or "place_machine_item"
        } && requirement.RouteKind is (
            "machine_output" or
            "native_machine_flavored_output" or
            "native_machine_item_query_output")
            ? "machine_capacity_establishment"
            :
        candidate is
        {
            OptionId: "inventory.transfer_item",
            Kind: "transfer_inventory_item"
        } && requirement.RouteKind is (
            "machine_output" or
            "native_machine_flavored_output" or
            "native_machine_item_query_output")
            ? "machine_input_material_transfer"
            :
        candidate is { OptionId: "economy.buy_supplies" } &&
        requirement.RouteKind is (
            "machine_output" or
            "native_machine_flavored_output" or
            "native_machine_item_query_output")
            ? "machine_input_purchase"
            :
        requirement.RouteKind switch
        {
            "harvests_as" when requirement.SourceId.StartsWith(
                "crop:",
                StringComparison.Ordinal) => "crop_planting",
            "machine_output" or
            "native_machine_flavored_output" or
            "native_machine_item_query_output" => "machine_input_load",
            _ => string.Empty
        };

    internal static AcquisitionPurchasePrerequisiteBinding?
        PurchasePrerequisite(
            AcquisitionRouteTargetDateProcessing route) =>
        route.UpstreamRoute.UpstreamRoute.CurrencyEvaluation?
            .PurchasePrerequisite;

    internal static bool PurchaseCandidateMatchesBinding(
        PolicyEventCandidatePrediction candidate,
        AcquisitionPurchasePrerequisiteBinding binding) =>
        candidate.OptionId == "economy.buy_supplies" &&
        candidate.Kind is (
            "route_connector_tile" or
            "interact_endpoint" or
            "buy_shop_item") &&
        candidate.Quantity == 1 &&
        candidate.ShopId == binding.ShopId &&
        candidate.QualifiedItemId == binding.QualifiedItemId &&
        candidate.UnitPrice == binding.UnitPrice &&
        ReadStringParameter(candidate, "continuation.shop_id") ==
            binding.ShopId &&
        ReadStringParameter(candidate, "continuation.qualified_item_id") ==
            binding.QualifiedItemId &&
        ReadNonNegativeIntParameter(
            candidate,
            "continuation.max_unit_price") == binding.UnitPrice &&
        ReadPositiveIntParameter(
            candidate,
            "continuation.quantity") == 1;

    internal static string PurchaseStage(
        PolicyEventCandidatePrediction candidate) => candidate.Kind switch
        {
            "buy_shop_item" => "purchase",
            "interact_endpoint" => "shop_interaction",
            "route_connector_tile" => "route_connector",
            _ => string.Empty
        };

    private static string ReadStringParameter(
        PolicyEventCandidatePrediction candidate,
        string name) =>
        CurrentTeacherFrontierSupport.TryReadUniqueParameter(
            candidate,
            name,
            out var value)
                ? value
                : string.Empty;

    private static int? ReadPositiveIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name) =>
        CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
            candidate,
            name,
            out var value) && value > 0
                ? value
                : null;

    private static int? ReadNonNegativeIntParameter(
        PolicyEventCandidatePrediction candidate,
        string name) =>
        CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
            candidate,
            name,
            out var value) && value >= 0
                ? value
                : null;

    private static bool TryStateInt(
        SnapshotEnvelope snapshot,
        string section,
        string field,
        out int value)
    {
        value = 0;
        return snapshot.State.TryGetValue(section, out var sectionValue) &&
            sectionValue.ValueKind == JsonValueKind.Object &&
            sectionValue.TryGetProperty(field, out var envelope) &&
            envelope.ValueKind == JsonValueKind.Object &&
            envelope.TryGetProperty("status", out var status) &&
            status.ValueKind == JsonValueKind.String &&
            status.GetString() is "available" or "derived" &&
            envelope.TryGetProperty("value", out var fieldValue) &&
            fieldValue.TryGetInt32(out value);
    }
}
