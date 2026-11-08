using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewModdingAPI;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private NativeOutputSnapshotAnchor CaptureOutputSnapshotAnchor(PendingExecution pending, ActionOriginOutputCapture? capture)
    {
        if (capture is null || pending.OutputObservation is null || !Context.IsWorldReady ||
            !ReferenceEquals(Game1.currentLocation, capture.Location))
            throw new InvalidOperationException("Snapshot anchor lacks its original active native observation.");
        var api = Helper.ModRegistry.GetApi<INativeOutputSnapshotReadApi>("StardewAI.TransparentBridge")
            ?? throw new InvalidOperationException("Native output snapshot read API is unavailable.");
        // Capture/cache the full frame while the same collection observer is still active.
        var json = api.ReadNativeOutputSnapshotAnchor();
        return NativeOutputSnapshotAnchor.Read(json, pending.Request, Game1.ticks,
            Game1.player.farmName.Value, Game1.player.UniqueMultiplayerID.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
