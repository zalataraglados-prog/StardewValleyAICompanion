using System.Globalization;
using StardewAI.Contracts.Training;
using StardewValley;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private sealed partial class NativeCollectionObservation
    {
        private ActionOriginItemTransientState ReadItemTransientPayload(NativeLiveItemTransientState state) => new()
        {
            SchemaVersion = "native_item_transients.v1",
            SetFlagOnPickup = ObservedText(state.SetFlagOnPickup),
            TempDataPresent = state.TempData is not null,
            TempDataEntries = state.TempData?.Select(entry => new ActionOriginTempDataEntry
            {
                Key = entry.Key, Kind = entry.Kind, BooleanValue = entry.BooleanValue, StringValue = entry.StringValue
            }).ToArray(),
            Cache = new()
            {
                QualifiedItemId = ObservedText(state.Cache.QualifiedItemId), ContextTagsDirty = state.Cache.ContextTagsDirty,
                ContextTagsPresent = state.Cache.ContextTags is not null, ContextTags = state.Cache.ContextTags?.ToArray()
            },
            Tool = state.Tool is null ? null : new()
            {
                LastUser = state.Tool.LastUser is null ? new() { IsNull = true } : new()
                {
                    IsNull = false, ReferenceId = ReferenceIds.For(state.Tool.LastUser),
                    ActorId = state.Tool.LastUser.UniqueMultiplayerID.ToString(CultureInfo.InvariantCulture),
                    IsActor = ReferenceEquals(state.Tool.LastUser, farmer)
                },
                PlayUseSounds = state.Tool.PlayUseSounds, Description = ObservedText(state.Tool.Description),
                DisplayName = ObservedText(state.Tool.DisplayName)
            },
            Pickaxe = state.Pickaxe is null ? null : new()
            {
                BoulderTileX = state.Pickaxe.BoulderTileX, BoulderTileY = state.Pickaxe.BoulderTileY,
                HitsToBoulder = state.Pickaxe.HitsToBoulder
            },
            Object = state.Object is null ? null : new()
            {
                HasHeldObject = state.Object.HasHeldObject, HasLightSource = state.Object.HasLightSource,
                ShakeTimer = state.Object.ShakeTimer, Health = state.Object.Health
            }
        };

        private ActionOriginSelectionTransientState ReadSelectionTransientPayload(NativeLiveSelectionState state) => new()
        {
            SchemaVersion = "native_selection_transients.v1", ActorReferenceId = ReferenceIds.For(farmer),
            ItemStowed = state.ItemStowed, TemporaryItem = ObservedItemReference(state.TemporaryItem),
            CurrentItem = ObservedItemReference(state.CurrentItem)
        };

        private static ActionOriginNullableText ObservedText(string? value) => new() { IsNull = value is null, Value = value };

        private static ActionOriginNativeItemReference ObservedItemReference(Item? item)
        {
            if (item is null) return new() { IsNull = true };
            var type = item.GetType();
            if (type.Assembly != typeof(Item).Assembly || !NativeInventoryPayloadTypes.TryGet(type.FullName, out var descriptor) ||
                descriptor.IsTool != (item is Tool))
                throw new InvalidDataException("Native selection reference runtime type is unsupported.");
            return new() { IsNull = false, ReferenceId = ReferenceIds.For(item), RuntimeType = type.FullName, IsTool = item is Tool };
        }
    }
}
