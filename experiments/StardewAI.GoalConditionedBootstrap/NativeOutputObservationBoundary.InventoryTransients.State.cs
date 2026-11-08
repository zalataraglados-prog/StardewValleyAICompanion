using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class NativeOutputObservationBoundary
{
    private static NativeObservedItemTransientState ReadItemTransient(ActionOriginItemTransientState? state,
        NativeObservedInventoryPayloadSlot slot, string actorId, string actorReference,
        Dictionary<Guid, string> references, ref int characters)
    {
        var itemStart = characters;
        Require(state is not null && state.SchemaVersion == "native_item_transients.v1" && state.TempDataPresent.HasValue,
            "native item transient schema or container availability is missing");
        var flag = ReadTransientText(state!.SetFlagOnPickup, ref characters);
        var tempData = ReadTransientTempData(state.TempDataPresent!.Value, state.TempDataEntries, ref characters);
        var cache = ReadTransientCache(state.Cache, ref characters);
        NativeObservedToolTransientState? tool = null;
        if (slot.IsTool == true)
        {
            Require(state.Tool is not null && state.Tool.PlayUseSounds.HasValue,
                "native tool transient fields are unavailable");
            var raw = state.Tool!;
            tool = new(ReadTransientActor(raw.LastUser, actorId, actorReference, references), raw.PlayUseSounds!.Value,
                ReadTransientText(raw.Description, ref characters), ReadTransientText(raw.DisplayName, ref characters));
        }
        else Require(state.Tool is null, "non-tool inventory slot has fabricated tool transients");
        NativeObservedPickaxeTransientState? pickaxe = null;
        if (slot.RuntimeType == "StardewValley.Tools.Pickaxe")
        {
            Require(state.Pickaxe?.BoulderTileX is not null && state.Pickaxe.BoulderTileY.HasValue &&
                    state.Pickaxe.HitsToBoulder.HasValue, "private native Pickaxe state is unavailable");
            pickaxe = new(state.Pickaxe!.BoulderTileX!.Value, state.Pickaxe.BoulderTileY!.Value, state.Pickaxe.HitsToBoulder!.Value);
        }
        else Require(state.Pickaxe is null, "non-Pickaxe inventory slot has fabricated private hit state");
        Require(characters - itemStart <= 65536, "native per-item transient character budget exceeded");
        return new(flag, tempData, cache, tool, pickaxe);
    }

    private static IReadOnlyList<NativeObservedTempDataEntry>? ReadTransientTempData(bool present,
        ActionOriginTempDataEntry[]? entries, ref int characters)
    {
        if (!present)
        {
            Require(entries is null, "absent native tempData has fabricated entries");
            return null;
        }
        Require(entries is { Length: <= 256 }, "native tempData is incomplete or exceeds its entry bound");
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<NativeObservedTempDataEntry>();
        foreach (var entry in entries!)
        {
            Require(entry is not null, "native tempData entry is unavailable");
            CountTransientText(entry!.Key, 256, ref characters);
            Require(keys.Add(entry.Key!), "native tempData contains duplicate keys");
            switch (entry.Kind)
            {
                case "null":
                    Require(entry.BooleanValue is null && entry.StringValue is null, "null native tempData value has extra fields");
                    break;
                case "bool":
                    Require(entry.BooleanValue.HasValue && entry.StringValue is null, "native boolean tempData value is incomplete");
                    break;
                case "string":
                    Require(entry.BooleanValue is null, "native string tempData value has extra boolean state");
                    CountTransientText(entry.StringValue, 4096, ref characters);
                    break;
                default: throw new InvalidDataException("Native transient tempData value type is unsupported.");
            }
            result.Add(new(entry.Key!, entry.Kind!, entry.BooleanValue, entry.StringValue));
        }
        return result.AsReadOnly();
    }

    private static NativeObservedItemCacheState ReadTransientCache(ActionOriginItemCacheState? cache, ref int characters)
    {
        Require(cache is not null && cache.ContextTagsDirty.HasValue && cache.ContextTagsPresent.HasValue,
            "native item cache availability is missing");
        var qualified = ReadTransientText(cache!.QualifiedItemId, ref characters);
        if (!cache.ContextTagsPresent!.Value)
        {
            Require(cache.ContextTags is null, "absent native tag cache contains values");
            return new(qualified, cache.ContextTagsDirty!.Value, null);
        }
        Require(cache.ContextTags is { Length: <= 256 }, "native tag cache is unavailable or oversized");
        var unique = new HashSet<string?>(StringComparer.Ordinal);
        var tags = new List<string?>();
        foreach (var tag in cache.ContextTags!)
        {
            if (tag is not null) CountTransientText(tag, 256, ref characters);
            Require(unique.Add(tag), "native tag cache contains duplicate values");
            tags.Add(tag);
        }
        return new(qualified, cache.ContextTagsDirty!.Value, tags.AsReadOnly());
    }
}
