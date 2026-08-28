using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void TickNativeObjectInteractionDomain()
    {
        TickHousePlantRotation();
        TickSingingStone();
        TickSlimeBallCollection();
        TickFeedHopperWithdrawal();
        TickAutoGrabberCollection();
    }

    private void ResetNativeObjectInteractionDomain()
    {
        var restoreSlot = nativeObjectInteractions.HousePlant?.RestoreSlotIndex ??
            nativeObjectInteractions.SingingStone?.RestoreSlotIndex ??
            nativeObjectInteractions.SlimeBall?.RestoreSlotIndex ??
            nativeObjectInteractions.FeedHopper?.RestoreSlotIndex ??
            nativeObjectInteractions.AutoGrabber?.RestoreSlotIndex;

        nativeObjectInteractions.HousePlant = null;
        nativeObjectInteractions.SingingStone = null;
        nativeObjectInteractions.SlimeBall = null;
        nativeObjectInteractions.FeedHopper = null;
        nativeObjectInteractions.AutoGrabber = null;

        if (restoreSlot is int slot && Context.IsWorldReady)
            Game1.player.CurrentToolIndex = slot;
    }

    private sealed class NativeObjectInteractionDomainState
    {
        public ActiveHousePlantRotation? HousePlant { get; set; }
        public ActiveSingingStone? SingingStone { get; set; }
        public ActiveSlimeBallCollection? SlimeBall { get; set; }
        public ActiveFeedHopperWithdrawal? FeedHopper { get; set; }
        public ActiveAutoGrabberCollection? AutoGrabber { get; set; }

        public bool IsActive =>
            HousePlant is not null ||
            SingingStone is not null ||
            SlimeBall is not null ||
            FeedHopper is not null ||
            AutoGrabber is not null;
    }
}
