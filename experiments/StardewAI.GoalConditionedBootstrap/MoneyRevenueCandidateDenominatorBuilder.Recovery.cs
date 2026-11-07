using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueCandidateDenominatorBuilder
{
    private static bool IsNativeShopRecoveryPrerequisite(
        RevenueActionNodeDefinition definition,
        EventCandidate candidate,
        SnapshotEnvelope snapshot)
    {
        // Closing a shop is a prerequisite of day recovery, never a settlement receipt.
        if (!RequiredFieldAvailable(snapshot, "menus.active_menu") ||
            !RequiredFieldAvailable(snapshot, "player.location_id"))
            return false;
        var menu = snapshot.State["menus"].GetProperty("active_menu").GetProperty("value");
        var location = snapshot.State["player"].GetProperty("location_id").GetProperty("value");
        return definition.DeterministicSupport && !definition.PolicyChoice &&
            definition.OptionId == "recovery.stabilize_day" &&
            definition.CandidateKinds.Any(kind => kind is
                "recovery_sleep_immediately" or "recovery_sleep_before_collapse" or
                "recovery_resume_sleep_prompt") &&
            menu.ValueKind == JsonValueKind.Object &&
            menu.TryGetProperty("is_open", out var open) && open.ValueKind == JsonValueKind.True &&
            ReadOptionalString(menu, "type") == "ShopMenu" &&
            candidate.CandidateId == "recovery:close_blocking_menu" &&
            candidate.Kind == "recovery_close_menu" && candidate.Available &&
            candidate.BlockReasons.Length == 0 &&
            candidate.Quantity == 0 && candidate.UnitPrice == 0 && candidate.TotalValue == 0 &&
            location.ValueKind == JsonValueKind.String && candidate.LocationId == location.GetString() &&
            !string.IsNullOrWhiteSpace(candidate.LocationId) &&
            candidate.ExpectedEffect == "menu_not_blocking_execution" &&
            candidate.Parameters is { Length: 1 } &&
            candidate.Parameters[0].Name == "execution_option_id" &&
            candidate.Parameters[0].Value == "executor.close_menu";
    }
}
