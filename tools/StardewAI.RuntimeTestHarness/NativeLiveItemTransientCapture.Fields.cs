using System.Reflection;
using StardewValley;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

internal static partial class NativeLiveItemTransientCapture
{
    // Exact pinned native declarations, not a reflected property/object-graph traversal.
    private static readonly FieldInfo QualifiedItemIdField = RequireField(typeof(Item), "_qualifiedItemId", typeof(string));
    private static readonly FieldInfo ContextTagsField = RequireField(typeof(Item), "_contextTags", typeof(HashSet<string>));
    private static readonly FieldInfo ContextTagsDirtyField = RequireField(typeof(Item), "_contextTagsDirty", typeof(bool));
    private static readonly FieldInfo DescriptionField = RequireField(typeof(Tool), "_description", typeof(string));
    private static readonly FieldInfo DisplayNameField = RequireField(typeof(Tool), "displayName", typeof(string));
    private static readonly FieldInfo BoulderTileXField = RequireField(typeof(Pickaxe), "boulderTileX", typeof(int));
    private static readonly FieldInfo BoulderTileYField = RequireField(typeof(Pickaxe), "boulderTileY", typeof(int));
    private static readonly FieldInfo HitsToBoulderField = RequireField(typeof(Pickaxe), "hitsToBoulder", typeof(int));
    private static readonly FieldInfo ItemStowedField = RequireField(typeof(Farmer), "_itemStowed", typeof(bool));

    private static FieldInfo RequireField(Type declaringType, string name, Type fieldType)
    {
        var field = declaringType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
        if (field is null || field.DeclaringType != declaringType || field.IsStatic || field.FieldType != fieldType)
            throw new InvalidDataException("Native transient field layout is unavailable or unsupported: " + declaringType.FullName + "." + name);
        return field;
    }

    private static T? ReadNullable<T>(FieldInfo field, object instance) where T : class
    {
        var value = field.GetValue(instance);
        if (value is null) return null;
        if (value is T typed) return typed;
        throw new InvalidDataException("Native transient reference field is unreadable: " + field.Name);
    }

    private static T ReadValue<T>(FieldInfo field, object instance) where T : struct
    {
        if (field.GetValue(instance) is T typed) return typed;
        throw new InvalidDataException("Native transient value field is unreadable: " + field.Name);
    }
}
