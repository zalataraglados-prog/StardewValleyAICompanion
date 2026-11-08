using System.Reflection;
using HarmonyLib;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

internal static class HayStorageReadSupport
{
    private static MethodInfo? EnumerationMethod => typeof(Utility).GetMethod(nameof(Utility.ForEachLocation),
        new[] { typeof(Func<GameLocation, bool>), typeof(bool), typeof(bool) });

    internal static HayStorageSnapshot ReadInventory(Farm farm)
    {
        if (!HayMethodUnpatched(EnumerationMethod)) return new HayStorageSnapshot
        { Diagnostics = new[] { "patched_or_missing_native_location_enumeration" } };
        var diagnostics = new List<string>();
        var references = new HashSet<GameLocation>(ReferenceEqualityComparer.Instance);
        var locations = ReadNoninteriors().Append(farm).Where(references.Add).ToArray();
        ValidateIdentities(locations, diagnostics);
        var rows = locations.OrderBy(location => location.NameOrUniqueName, StringComparer.Ordinal)
            .Select(location => ReadLocation(location, diagnostics)).ToArray();
        return new HayStorageSnapshot
        {
            Status = diagnostics.Count == 0 ? "available" : "unavailable",
            Locations = rows, Diagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    internal static HayStorageContext ReadContext(Farm farm, GameLocation? current)
    {
        if (current is null) return new HayStorageContext
        { FarmLocationId = farm.NameOrUniqueName, Diagnostics = new[] { "current_location_unavailable" } };
        var projection = new HayStorageContext { FarmLocationId = farm.NameOrUniqueName };
        var diagnostics = new List<string>();
        var nativeMethods = new MethodBase?[]
        {
            typeof(GameLocation).GetMethod(nameof(GameLocation.StoreHayInAnySilo)),
            typeof(GameLocation).GetMethod(nameof(GameLocation.tryToAddHay)),
            EnumerationMethod
        };
        var nativeRules = Game1.version == "1.6.15" && nativeMethods.All(HayMethodUnpatched);
        projection.NativeRuleStatus = nativeRules ? "vanilla_1_6_15_unpatched" : "unsupported_or_patched_native_storage_rules";
        if (!nativeRules) diagnostics.Add(projection.NativeRuleStatus);
        var noninteriors = new List<GameLocation>();
        // Match the native fallback order; persistent machine topology sorts/deduplicates and cannot substitute for it.
        if (nativeRules)
            noninteriors.AddRange(ReadNoninteriors());
        projection.NoninteriorLocationIds = noninteriors.Select(location => location.NameOrUniqueName).ToArray();
        ValidateIdentities(noninteriors, diagnostics);
        projection.SourceStorage = ReadLocation(current, diagnostics);
        projection.Diagnostics = diagnostics.Distinct(StringComparer.Ordinal).ToArray();
        projection.Status = projection.Diagnostics.Length == 0 ? "available" : "unavailable";
        return projection;
    }

    private static GameLocation[] ReadNoninteriors()
    {
        var locations = new List<GameLocation>();
        Utility.ForEachLocation(location => { locations.Add(location); return true; }, includeInteriors: false, includeGenerated: false);
        return locations.ToArray();
    }

    private static void ValidateIdentities(IEnumerable<GameLocation> values, List<string> diagnostics)
    {
        var references = new HashSet<GameLocation>(ReferenceEqualityComparer.Instance);
        var locations = values.Where(references.Add).ToArray();
        if (locations.Any(location => string.IsNullOrWhiteSpace(location.NameOrUniqueName) || string.IsNullOrWhiteSpace(location.Name)) ||
            locations.GroupBy(location => location.NameOrUniqueName, StringComparer.Ordinal).Any(group => group.Count() != 1))
            diagnostics.Add("ambiguous_or_missing_hay_location_identity");
    }

    private static HayStorageLocation ReadLocation(GameLocation location, List<string> diagnostics)
    {
        var method = location.GetType().GetMethod(nameof(GameLocation.GetHayCapacity), Type.EmptyTypes);
        var declared = method?.DeclaringType?.GetMethod(nameof(GameLocation.GetHayCapacity),
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null, Type.EmptyTypes, null);
        var readable = declared?.DeclaringType?.Assembly == typeof(GameLocation).Assembly && HayMethodUnpatched(declared);
        if (!readable) diagnostics.Add("custom_or_patched_hay_capacity:" + location.NameOrUniqueName);
        return new HayStorageLocation
        {
            LocationId = location.NameOrUniqueName, Name = location.Name, RuntimeType = location.GetType().FullName ?? location.GetType().Name,
            BuildingCount = location.buildings.Count, StoredHay = location.piecesOfHay.Value,
            HayCapacity = readable ? location.GetHayCapacity() : null,
            CapacityStatus = readable ? "exact_native_getter" : "unavailable_custom_or_patched_getter"
        };
    }

    private static bool HayMethodUnpatched(MethodBase? method)
    {
        if (method is null) return false;
        var info = Harmony.GetPatchInfo(method);
        return info is null || !info.Owners.Any();
    }
}
