using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.ItemTypeDefinitions;
using ObjectItem = StardewValley.Object;

namespace StardewAI.NativePrimitives;

internal static class CanonicalVanillaObjectUnit
{
    private const string NativeSha256 = "7f1e5b8e58d2758b78570ba771bbeb03d33522f62188bf6c32edf0cf626deaee";
    private static readonly Lazy<bool> MatchingBinary = new(() =>
    {
        try
        {
            using var stream = File.OpenRead(typeof(ObjectItem).Assembly.Location);
            using var hash = SHA256.Create();
            return Convert.ToHexString(hash.ComputeHash(stream)).Equals(NativeSha256, StringComparison.OrdinalIgnoreCase);
        }
        catch { return false; }
    });

    public static bool TryClone(Item source, out ObjectItem unit)
    {
        unit = null!;
        if (source.GetType() != typeof(ObjectItem) || ((ObjectItem)source).bigCraftable.Value ||
            !Eligible(source.ItemId) || !NativeCloneMethodsUnpatched()) return false;
        unit = CreateDetached(source.ItemId);
        unit.CopyFieldsFrom(source);
        unit.Flipped = false;
        return true;
    }

    public static bool TryCreateStandard(string qualifiedItemId, out ObjectItem unit)
    {
        unit = null!;
        if (!qualifiedItemId.StartsWith("(O)", StringComparison.Ordinal) ||
            !Eligible(qualifiedItemId.Substring(3)) || !NativeCloneMethodsUnpatched() ||
            !Unpatched(typeof(ObjectDataDefinition).GetMethod(nameof(ObjectDataDefinition.CreateItem))) ||
            typeof(ItemRegistry).GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.Name == nameof(ItemRegistry.Create)).Any(method => !Unpatched(method))) return false;
        var data = ItemRegistry.GetData(qualifiedItemId);
        if (data is null || data.IsErrorItem || data.QualifiedItemId != qualifiedItemId ||
            data.ItemType.GetType() != typeof(ObjectDataDefinition)) return false;
        var tags = ItemContextTagManager.GetBaseContextTags(data.ItemId);
        if (tags.Contains("torch_item") || tags.Contains("item_type_ring") || data.ItemId is "812" or "801") return false;
        unit = CreateDetached(data.ItemId);
        unit.Flipped = false;
        return true;
    }

    private static bool Eligible(string id) => MatchingBinary.Value && !string.IsNullOrWhiteSpace(id) &&
        !ItemRegistry.IsQualifiedItemId(id) && Game1.objectData.ContainsKey(id);

    private static readonly MethodBase?[] CloneMethods = new MethodBase?[]
    {
        typeof(ObjectItem).GetConstructor(Type.EmptyTypes),
        typeof(ObjectItem).GetConstructor(new[] { typeof(string), typeof(int), typeof(bool), typeof(int), typeof(int) }),
        typeof(Item).GetMethod(nameof(Item.getOne)), typeof(Item).GetMethod(nameof(Item.CopyFieldsFrom)),
        typeof(ObjectItem).GetMethod("GetOneNew", BindingFlags.Instance | BindingFlags.NonPublic),
        typeof(ObjectItem).GetMethod("GetOneCopyFrom", BindingFlags.Instance | BindingFlags.NonPublic),
        typeof(Item).GetMethod("GetOneCopyFrom", BindingFlags.Instance | BindingFlags.NonPublic)
    };
    private static readonly MethodInfo?[] InitializerSetters = new[]
    {
        nameof(Item.ItemId), nameof(Item.Stack), nameof(ObjectItem.Price), nameof(ObjectItem.Edibility),
        nameof(ObjectItem.Type), nameof(Item.Category), nameof(ObjectItem.CanBeSetDown), nameof(ObjectItem.CanBeGrabbed),
        nameof(ObjectItem.IsSpawnedObject), nameof(ObjectItem.Fragility), nameof(ObjectItem.MinutesUntilReady)
    }.Select(name => typeof(ObjectItem).GetProperty(name)?.SetMethod).ToArray();

    private static bool NativeCloneMethodsUnpatched() => CloneMethods.All(Unpatched) && InitializerSetters.All(Unpatched);

    private static bool Unpatched(MethodBase? method) => method is not null && Harmony.GetPatchInfo(method) is null;

    private static ObjectItem CreateDetached(string id)
    {
        var data = Game1.objectData[id];
        var unit = new ObjectItem { ItemId = id, Stack = 1 };
        unit.ResetParentSheetIndex();
        unit.name = data.Name ?? ItemRegistry.GetDataOrErrorItem(unit.QualifiedItemId).InternalName;
        unit.Price = data.Price;
        unit.Edibility = data.Edibility;
        unit.Type = data.Type;
        unit.Category = data.Category;
        unit.CanBeSetDown = true;
        unit.CanBeGrabbed = true;
        unit.IsSpawnedObject = false;
        // Preserve the pinned native constructor defaults, omitting only its discarded random flip.
        if (id is "463" or "464") unit.scale = new Vector2(1, 1);
        if (id == "449" || unit.IsWeeds() || unit.IsTwig()) unit.Fragility = 2;
        else if (unit.name.Contains("Fence")) unit.scale = new Vector2(10, 0);
        else if (unit.IsBreakableStone()) unit.MinutesUntilReady = id switch
        { "8" => 4, "10" => 8, "12" => 16, "14" => 12, "25" => 8, _ => 1 };
        if (unit.Category == -22) unit.scale.Y = 1;
        return unit;
    }
}
