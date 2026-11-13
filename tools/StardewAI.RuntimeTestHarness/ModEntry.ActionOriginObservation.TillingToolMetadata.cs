using System.Reflection;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        private NativeTillingToolMetadataContext? tillingMetadataBefore;
        private static FieldInfo? lastEquippedToolField;

        private NativeTillingToolMetadataContext? CaptureTillingToolMetadata()
        {
            if (!request.ActionOriginTillingResources) return null;
            try
            {
                if (!NativeTillingObservationContract.IsCompleteRequest(request) || !active || !ReferenceEquals(current, this) ||
                    Environment.CurrentManagedThreadId != threadId || !ReferenceEquals(Game1.player, farmer) ||
                    !ReferenceEquals(farmer.currentLocation, capture.Location))
                    throw new InvalidDataException("Tilling metadata lacks its original active scope.");
                var hoe = FindTool<Hoe>() ?? throw new InvalidDataException("Original first Hoe is unavailable.");
                if (hoe.GetType() != typeof(Hoe)) throw new InvalidDataException("Tilling metadata requires the native Hoe type.");
                lastEquippedToolField ??= typeof(Farmer).GetField("_lastEquippedTool", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                if (lastEquippedToolField?.FieldType != typeof(Tool)) throw new InvalidDataException("Native equipped-tool field differs.");
                var last = (Tool?)lastEquippedToolField.GetValue(farmer);
                if (last is not null && last.GetType().Assembly != typeof(Tool).Assembly)
                    throw new InvalidDataException("Custom previously equipped tool is outside the native metadata scope.");
                return new()
                {
                    SchemaVersion = "native_tilling_tool_metadata_context.v1",
                    GameTick = Game1.ticks, ActorReferenceId = ReferenceIds.For(farmer), HoeReferenceId = ReferenceIds.For(hoe),
                    HoeSlotIndex = farmer.Items.IndexOf(hoe), LastEquippedToolIsNull = last is null,
                    LastEquippedToolReferenceId = last is null ? null : ReferenceIds.For(last),
                    LastEquippedToolRuntimeType = last?.GetType().FullName, FacingDirection = farmer.FacingDirection,
                    CurrentToolSlotIndex = farmer.CurrentToolIndex, ToolPower = farmer.toolPower.Value,
                    UsingTool = farmer.UsingTool, PauseForSingleAnimation = farmer.FarmerSprite.PauseForSingleAnimation,
                    CanReleaseTool = farmer.canReleaseTool, InitialParentTileIndex = hoe.InitialParentTileIndex,
                    CurrentParentTileIndex = hoe.CurrentParentTileIndex, SwingTicker = hoe.swingTicker,
                    HasBeenInInventory = hoe.HasBeenInInventory
                };
            }
            catch (Exception error)
            {
                Reject("tilling_tool_metadata_capture_failed:" + error.GetType().Name);
                return null;
            }
        }
    }
}
