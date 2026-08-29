using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupWarpTotemFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());
        if (!RuntimeWarpTotemVariants.ContainsKey(request.ItemId))
            return BlockedWithPrimitive(request, "debug_setup_warp_totem", request.ItemId,
                "item_id=unsupported", "warp_totem_fixture_exact_variant_required");

        var started = DateTimeOffset.UtcNow.ToString("O");
        Game1.exitActiveMenu();
        Game1.eventUp = false;
        Game1.fadeToBlack = false;
        Game1.displayFarmer = true;
        Game1.player.swimming.Value = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.player.UsingTool = false;
        Game1.player.temporarilyInvincible = false;
        Game1.player.temporaryInvincibilityTimer = 0;
        Game1.player.freezePause = 0;
        Game1.player.forceCanMove();
        var slot = EnsureInventoryItem("(O)" + request.ItemId, 2);
        var totem = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] as StardewValley.Object : null;
        if (totem is not null)
            totem.Stack = 2;
        Game1.warpFarmer("FarmHouse", 4, 4, flip: false);
        var verified = totem?.GetType() == typeof(StardewValley.Object) &&
            totem.QualifiedItemId == "(O)" + request.ItemId && totem.Stack == 2;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_warp_totem",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_warp_totem_ready", "native_source_location_warp_requested" }
                : new[] { "slot=" + slot, "item=" + (totem?.QualifiedItemId ?? "missing") },
            RequestedEffect = "player.warp_totem.variant=(O)" + request.ItemId + ";source_location=FarmHouse",
            ObservedEffect = "slot=" + slot + ";stack=" + (totem?.Stack ?? 0) +
                ";warp_request=FarmHouse:4,4",
            BlockReasons = verified ? Array.Empty<string>() : new[] { "warp_totem_fixture_post_state_mismatch" }
        };
    }
}
