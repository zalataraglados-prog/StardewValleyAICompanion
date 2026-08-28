using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private void TickNativeObjectInteractionDomain()
    {
        TickHousePlantRotation();
        TickSingingStone();
        TickFluteBlockTuning();
        TickSlimeBallCollection();
        TickFeedHopperWithdrawal();
        TickAutoGrabberCollection();
        TickMiniObeliskUse();
        TickFarmComputerReport();
    }

    private void ResetNativeObjectInteractionDomain()
    {
        var restoreSlot = nativeObjectInteractions.HousePlant?.RestoreSlotIndex ??
            nativeObjectInteractions.SingingStone?.RestoreSlotIndex ??
            nativeObjectInteractions.FluteBlock?.RestoreSlotIndex ??
            nativeObjectInteractions.SlimeBall?.RestoreSlotIndex ??
            nativeObjectInteractions.FeedHopper?.RestoreSlotIndex ??
            nativeObjectInteractions.AutoGrabber?.RestoreSlotIndex ??
            nativeObjectInteractions.MiniObelisk?.RestoreSlotIndex;
        restoreSlot ??= nativeObjectInteractions.FarmComputer?.RestoreSlotIndex;

        nativeObjectInteractions.HousePlant = null;
        nativeObjectInteractions.SingingStone = null;
        nativeObjectInteractions.FluteBlock = null;
        nativeObjectInteractions.SlimeBall = null;
        nativeObjectInteractions.FeedHopper = null;
        nativeObjectInteractions.AutoGrabber = null;
        nativeObjectInteractions.MiniObelisk = null;
        nativeObjectInteractions.FarmComputer = null;

        if (restoreSlot is int slot && Context.IsWorldReady)
            Game1.player.CurrentToolIndex = slot;
    }

    private sealed class NativeObjectInteractionDomainState
    {
        public ActiveHousePlantRotation? HousePlant { get; set; }
        public ActiveSingingStone? SingingStone { get; set; }
        public ActiveFluteBlock? FluteBlock { get; set; }
        public ActiveSlimeBallCollection? SlimeBall { get; set; }
        public ActiveFeedHopperWithdrawal? FeedHopper { get; set; }
        public ActiveAutoGrabberCollection? AutoGrabber { get; set; }
        public ActiveMiniObeliskUse? MiniObelisk { get; set; }
        public ActiveFarmComputer? FarmComputer { get; set; }

        public bool IsActive =>
            HousePlant is not null ||
            SingingStone is not null ||
            FluteBlock is not null ||
            SlimeBall is not null ||
            FeedHopper is not null ||
            AutoGrabber is not null ||
            MiniObelisk is not null ||
            FarmComputer is not null;
    }
}
