using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool GeodePickupTutorialExpected(ActiveGeodeProcessing active) =>
        active.Pending.Request.GeodeFoundMailBefore == false &&
        active.Accepted.Any(row => row.ExpectedMailAdditions.Contains("geodeFound", StringComparer.Ordinal));

    private bool TryStartGeodePickupDialogue(ActiveGeodeProcessing active)
    {
        if (active.PickupDialogueAcknowledged || !GeodePickupTutorialExpected(active) ||
            Game1.activeClickableMenu is not DialogueBox dialogue || dialogue.isQuestion ||
            (dialogue.responses?.Length ?? 0) != 0 || dialogue.characterDialogue is not null || Game1.eventUp ||
            !Game1.player.mailReceived.Contains("geodeFound") || !GeodeReceiptMatches(active))
            return false;
        var expected = ItemRegistry.Create("(O)535").checkForSpecialItemHoldUpMeessage();
        if (string.IsNullOrWhiteSpace(expected) || !string.Equals(dialogue.getCurrentString(), expected, StringComparison.Ordinal))
            return false;
        active.PickupDialoguePending = new PendingExecution(active.Pending.Request);
        StartDialogueAdvance(active.PickupDialoguePending);
        if (ReferenceEquals(activeDialogueAdvance?.Pending, active.PickupDialoguePending))
            activeDialogueAdvance.UseNativeMenuClick = true;
        return true;
    }
}
