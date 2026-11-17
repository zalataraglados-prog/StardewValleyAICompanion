using StardewAI.Contracts.Training;
using StardewAI.RuntimePrimitives;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        private NativeWateringToolMetadataFrame? wateringMetadataBefore;
        private readonly BoundedNativeObservationBuffer<NativeWateringToolMetadataCall> wateringMetadataCalls = new(512);
        private int wateringMetadataOrdinal;

        private NativeWateringToolMetadataFrame? CaptureWateringToolMetadata()
        {
            if (!request.ActionOriginWateringResources) return null;
            try
            {
                if (!NativeWateringObservationContract.IsCompleteRequest(request) || !active || !ReferenceEquals(current, this) ||
                    Environment.CurrentManagedThreadId != threadId || !ReferenceEquals(Game1.player, farmer) ||
                    !ReferenceEquals(farmer.currentLocation, capture.Location) || farmer.Items.Count > MaximumDebrisRows)
                    throw new InvalidDataException("Watering metadata lacks its original active scope.");
                var rows = new List<NativeWateringToolMetadataSlot>();
                for (var index = 0; index < farmer.Items.Count; index++)
                {
                    if (farmer.Items[index] is not Tool tool) continue;
                    if (tool.GetType().Assembly != typeof(Tool).Assembly || tool.itemId.Value is null)
                        throw new InvalidDataException("Watering metadata requires native tool identities without migration.");
                    var id = tool.QualifiedItemId;
                    if (id.Length > 256) throw new InvalidDataException("Watering tool identity exceeds its payload bound.");
                    rows.Add(new()
                    {
                        SlotIndex = index, ItemReferenceId = ReferenceIds.For(tool), RuntimeType = tool.GetType().FullName,
                        QualifiedItemId = id, InitialParentTileIndex = tool.InitialParentTileIndex,
                        CurrentParentTileIndex = tool.CurrentParentTileIndex, SwingTicker = tool.swingTicker,
                        HasBeenInInventory = tool.HasBeenInInventory, WaterLeft = tool is WateringCan can ? can.WaterLeft : null
                    });
                }
                return new()
                {
                    SchemaVersion = "native_watering_tool_metadata.v1", GameTick = Game1.ticks,
                    ActorReferenceId = ReferenceIds.For(farmer), Tools = rows.ToArray()
                };
            }
            catch (Exception error)
            {
                Reject("watering_metadata_capture_failed:" + error.GetType().Name);
                return null;
            }
        }

        private static NativeWateringToolMetadataCall? RecordWateringMetadataCall(Tool? tool, Farmer? actor, string kind,
            int? direction = null, int? motionFrame = null, GameLocation? location = null, bool? history = null)
        {
            var scope = current;
            if (scope is null || !scope.request.ActionOriginWateringResources || !ReferenceEquals(scope.farmer, actor) || tool is null) return null;
            try
            {
                if (!scope.active || Environment.CurrentManagedThreadId != scope.threadId ||
                    !ReferenceEquals(Game1.player, actor) || !ReferenceEquals(actor!.currentLocation, scope.capture.Location) ||
                    location is not null && !ReferenceEquals(location, scope.capture.Location) ||
                    tool.GetType().Assembly != typeof(Tool).Assembly || !actor.Items.Contains(tool) || tool.itemId.Value is null)
                    throw new InvalidDataException("Watering metadata call left its original actor/location/inventory.");
                var id = tool.QualifiedItemId;
                if (id.Length > 256 || scope.capture.Location.NameOrUniqueName.Length > 256)
                    throw new InvalidDataException("Watering metadata text exceeds its payload bound.");
                var row = new NativeWateringToolMetadataCall
                {
                    Ordinal = ++scope.wateringMetadataOrdinal, Kind = kind, GameTick = Game1.ticks,
                    ActorReferenceId = ReferenceIds.For(actor), LocationId = scope.capture.Location.NameOrUniqueName,
                    ItemReferenceId = ReferenceIds.For(tool), RuntimeType = tool.GetType().FullName,
                    QualifiedItemId = id, Direction = direction, FarmerMotionFrame = motionFrame,
                    FacingDirection = actor.FacingDirection, ToolPower = actor.toolPower.Value, InventoryHistoryValue = history
                };
                if (scope.wateringMetadataCalls.TryAdd(row)) return row;
                scope.Reject("watering_metadata_call_limit_exceeded");
            }
            catch (Exception error)
            {
                scope.Reject("watering_metadata_call_capture_failed:" + error.GetType().Name);
            }
            return null;
        }
    }
}
