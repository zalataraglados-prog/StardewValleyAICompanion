using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

internal sealed record NativeLiveItemTempDataEntry(string Key, string Kind, bool? BooleanValue, string? StringValue);
internal sealed record NativeLiveItemCacheState(string? QualifiedItemId, bool ContextTagsDirty, IReadOnlyList<string>? ContextTags);
internal sealed record NativeLiveToolTransientState(Farmer? LastUser, bool PlayUseSounds, string? Description, string? DisplayName);
internal sealed record NativeLivePickaxeTransientState(int BoulderTileX, int BoulderTileY, int HitsToBoulder);
internal sealed record NativeLiveItemTransientState(string? SetFlagOnPickup, IReadOnlyList<NativeLiveItemTempDataEntry>? TempData,
    NativeLiveItemCacheState Cache, NativeLiveToolTransientState? Tool, NativeLivePickaxeTransientState? Pickaxe, int Characters,
    NativeLiveObjectTransientState? Object = null);
internal sealed record NativeLiveSelectionState(bool ItemStowed, Item? TemporaryItem, Item? CurrentItem, Tool? CurrentTool);

// Detached values of a SPECIFIED subset of native transients, not full item/world state.
// Caller owns scope/reference binding and any permitted cache/action transition policy.
internal static partial class NativeLiveItemTransientCapture
{
    private const int MaximumEntries = 256;

    internal static NativeLiveItemTransientState Capture(Item item, int maximumCharacters)
    {
        if (maximumCharacters is < 1 or > 65536)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        ArgumentNullException.ThrowIfNull(item);
        var type = item.GetType();
        if (type.Assembly != typeof(Item).Assembly || !NativeInventoryPayloadTypes.TryGet(type.FullName, out var descriptor) ||
            descriptor.IsTool != typeof(Tool).IsAssignableFrom(type))
            throw new InvalidDataException("Native transient item runtime type is unsupported: " + type.FullName);

        var budget = new CharacterBudget(maximumCharacters);
        var flag = budget.String(item.SetFlagOnPickup);
        IReadOnlyList<NativeLiveItemTempDataEntry>? tempData = null;
        var source = item.tempData;
        if (source is not null)
        {
            CheckCount(source.Count);
            var entries = new List<NativeLiveItemTempDataEntry>(source.Count);
            foreach (var entry in source)
            {
                CheckCount(entries.Count + 1);
                var key = budget.String(entry.Key, 256)!;
                entries.Add(entry.Value switch
                {
                    null => new(key, "null", null, null),
                    bool boolean => new(key, "bool", boolean, null),
                    string text => new(key, "string", null, budget.String(text)),
                    _ => throw new InvalidDataException("Unsupported native tempData value type: " + entry.Value.GetType().FullName)
                });
            }
            // The list and its immutable bool/string/null records no longer alias the dictionary.
            tempData = entries.AsReadOnly();
        }

        var qualified = budget.String(ReadNullable<string>(QualifiedItemIdField, item));
        var dirty = ReadValue<bool>(ContextTagsDirtyField, item);
        IReadOnlyList<string>? tags = null;
        var tagSet = ReadNullable<HashSet<string>>(ContextTagsField, item);
        if (tagSet is not null)
        {
            CheckCount(tagSet.Count);
            var copy = new List<string>(tagSet.Count);
            foreach (var tag in tagSet)
            {
                CheckCount(copy.Count + 1);
                // A raw CLR HashSet may contain null; retain it rather than inventing empty/tag text.
                copy.Add(budget.String(tag, 256)!);
            }
            tags = copy.AsReadOnly();
        }

        NativeLiveToolTransientState? toolState = null;
        if (item is Tool tool)
            toolState = new(tool.lastUser, tool.PlayUseSounds,
                budget.String(ReadNullable<string>(DescriptionField, tool)),
                budget.String(ReadNullable<string>(DisplayNameField, tool)));
        NativeLivePickaxeTransientState? pickaxeState = null;
        if (item is Pickaxe pickaxe)
            pickaxeState = new(ReadValue<int>(BoulderTileXField, pickaxe), ReadValue<int>(BoulderTileYField, pickaxe),
                ReadValue<int>(HitsToBoulderField, pickaxe));

        return new(flag, tempData, new(qualified, dirty, tags), toolState, pickaxeState, budget.Characters, CaptureObject(item));
    }

    internal static NativeLiveSelectionState CaptureSelection(Farmer farmer)
    {
        ArgumentNullException.ThrowIfNull(farmer);
        // Native getters are pure references: TemporaryItem takes precedence even when stowed.
        // Preserve native exceptions/branch behavior; never repair index, stow or temporary state.
        return new(ReadValue<bool>(ItemStowedField, farmer), farmer.TemporaryItem, farmer.CurrentItem, farmer.CurrentTool);
    }

    private static void CheckCount(int count)
    {
        if (count is < 0 or > MaximumEntries)
            throw new InvalidDataException("Native transient container exceeds its 256-entry bound.");
    }

    private sealed class CharacterBudget
    {
        private readonly int maximum;
        internal int Characters { get; private set; }
        internal CharacterBudget(int maximum) => this.maximum = maximum;
        internal string? String(string? value, int fieldMaximum = 4096)
        {
            int length = value?.Length ?? 0;
            if (length > fieldMaximum || length > maximum - Characters || !NativeObservationText.IsWellFormedUtf16(value))
                throw new InvalidDataException("Native transient text exceeds its budget or cannot round-trip through JSON without replacement.");
            Characters += length;
            return value;
        }
    }
}
