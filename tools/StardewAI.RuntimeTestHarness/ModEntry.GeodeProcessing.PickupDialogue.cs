using StardewValley;
using StardewValley.Menus;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private static bool GeodeFoundTutorialExpected(ActiveGeodeProcessing active) =>
        active.Pending.Request.GeodeFoundMailBefore == false &&
        active.Accepted.Any(row => row.ExpectedMailAdditions.Contains("geodeFound", StringComparer.Ordinal));

    private static bool GeodeArtifactTutorialExpected(ActiveGeodeProcessing active) =>
        active.Pending.Request.GeodeArchaeologyFoundCount == 0 &&
        active.Accepted.Any(row => row.PickupEffectKind == "artifact_discovery" &&
            ItemRegistry.Create(row.QualifiedItemId) is StardewValley.Object { Type: "Arch" });

    private static bool GeodePickupTutorialExpected(ActiveGeodeProcessing active) =>
        GeodeFoundTutorialExpected(active) || GeodeArtifactTutorialExpected(active);

    private bool TryStartGeodePickupDialogue(ActiveGeodeProcessing active)
    {
        if (active.PickupDialogueAcknowledged || !GeodePickupTutorialExpected(active) ||
            Game1.activeClickableMenu is not DialogueBox dialogue || dialogue.isQuestion ||
            (dialogue.responses?.Length ?? 0) != 0 || dialogue.characterDialogue is not null || Game1.eventUp ||
            !GeodeReceiptMatches(active))
            return false;
        var artifactTutorial = GeodeArtifactTutorialExpected(active) &&
            active.ActualOutput is { PickupEffectKind: "artifact_discovery" } &&
            Game1.player.mailReceived.Contains("artifactFound");
        if (!artifactTutorial && (!GeodeFoundTutorialExpected(active) ||
            !Game1.player.mailReceived.Contains("geodeFound")))
            return false;
        var expected = ItemRegistry.Create(artifactTutorial ? active.ActualOutput!.QualifiedItemId : "(O)535")
            .checkForSpecialItemHoldUpMeessage();
        if (string.IsNullOrWhiteSpace(expected) || !string.Equals(dialogue.getCurrentString(), expected, StringComparison.Ordinal))
            return false;
        active.PickupDialogueKind = artifactTutorial ? "artifact_found" : "geode_found";
        active.PickupDialoguePending = new PendingExecution(active.Pending.Request);
        StartDialogueAdvance(active.PickupDialoguePending);
        if (ReferenceEquals(activeDialogueAdvance?.Pending, active.PickupDialoguePending))
            activeDialogueAdvance.UseNativeMenuClick = true;
        return true;
    }
}
