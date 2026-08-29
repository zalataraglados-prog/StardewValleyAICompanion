using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupMonsterMuskFixture(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
            return Blocked(request, reasons.ToArray());

        var started = DateTimeOffset.UtcNow.ToString("O");
        var mine = Game1.getLocationFromName("Mine") ?? Game1.getFarm();
        Game1.currentLocation = mine;
        Game1.player.currentLocation = mine;
        Game1.exitActiveMenu();
        Game1.eventUp = false;
        Game1.fadeToBlack = false;
        Game1.player.swimming.Value = false;
        Game1.player.bathingClothes.Value = false;
        Game1.player.onBridge.Value = false;
        Game1.player.UsingTool = false;
        Game1.player.buffs.Remove("24");
        Game1.player.forceCanMove();
        var slot = EnsureInventoryItem("(O)879", 2);
        var musk = slot >= 0 && slot < Game1.player.Items.Count ? Game1.player.Items[slot] as StardewValley.Object : null;
        if (musk is not null)
            musk.Stack = 2;
        var verified = musk?.GetType() == typeof(StardewValley.Object) && musk.QualifiedItemId == "(O)879" &&
            musk.Stack == 2 && !Game1.player.hasBuff("24") && Game1.player.canMove;
        return new TrainingExecutionResult
        {
            RunId = request.RunId, QueueId = request.QueueId, QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash, OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked", FeedbackAvailable = true,
            StartedAt = started, CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_monster_musk",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "isolated_exact_base_monster_musk_ready", "buff_24_absent", "native_object_use_gate_ready" }
                : new[] { "slot=" + slot, "buff_24_active=" + Game1.player.hasBuff("24") },
            RequestedEffect = "player.monster_musk.ready=true",
            ObservedEffect = "location=" + mine.NameOrUniqueName + ";slot=" + slot + ";stack=" + (musk?.Stack ?? 0),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "monster_musk_fixture_post_state_mismatch" }
        };
    }
}
