using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewAI.Contracts.State;

namespace StardewAI.Core.WorldModel;

public sealed record HayStorageLocationMutation(string LocationId, int StoredHayBefore, int StoredHayAfter);
public sealed record HayStorageAllocation(int OfferedQuantity, int UnstoredQuantity,
    IReadOnlyList<HayStorageLocationMutation> Locations, IReadOnlyList<string> AttemptedLocationIds)
{
    public bool NativeOutcomeVerified => false;
    public bool FormalProductTrainingAuthorized => false;
}

// Pure projection of a given offer. It neither predicts the grass RNG roll nor stores hay.
public static class NativeHayStorageProjector
{
    public static HayStorageAllocation Store(HayStorageSnapshot source, HayStorageContext context, int offeredQuantity)
    {
        Require(offeredQuantity >= 0 && source.SchemaVersion == "hay_storage_inventory.v1" && source.Status == "available" &&
                source.LocationScope == "noninterior_locations_and_farm" && source.Diagnostics is { Length: 0 } && source.Locations is { Length: > 0 } &&
                context.SchemaVersion == "hay_storage_context.v1" && context.Status == "available" &&
                context.NativeRuleStatus == "vanilla_1_6_15_unpatched" && context.Diagnostics is { Length: 0 } &&
                context.SourceStorage is not null && context.NoninteriorLocationIds is not null,
            "Hay storage lacks its complete original readable native rule scope.");
        var locations = new Dictionary<string, HayStorageLocation>(StringComparer.Ordinal);
        foreach (var value in source.Locations!)
        {
            var row = ValidLocation(value);
            Require(locations.TryAdd(row.LocationId, row), "Hay storage repeats a canonical location.");
        }
        Require(locations.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(context.NoninteriorLocationIds!.Append(context.FarmLocationId)),
            "Hay storage canonical location scope differs from the native enumeration/farm scope.");
        var current = ValidLocation(context.SourceStorage);
        if (locations.TryGetValue(current.LocationId, out var existing))
            Require(SameLocation(existing, current), "Hay storage navigation echo disagrees with canonical resources.");
        else locations.Add(current.LocationId, current);
        Require(!string.IsNullOrWhiteSpace(context.FarmLocationId) && locations.ContainsKey(context.FarmLocationId) &&
                context.NoninteriorLocationIds!.All(id => !string.IsNullOrWhiteSpace(id) && locations.ContainsKey(id)),
            "Hay storage native topology has missing, foreign or unbound locations.");
        var before = locations.ToDictionary(pair => pair.Key, pair => pair.Value.StoredHay, StringComparer.Ordinal);
        var after = new Dictionary<string, int>(before, StringComparer.Ordinal);
        var attempted = new List<string>();
        var remaining = offeredQuantity;
        void TryStore(string id)
        {
            attempted.Add(id);
            // Preserve native integer arithmetic and over-capacity behavior; don't clamp or invent free capacity.
            var accepted = Math.Min(unchecked(locations[id].HayCapacity!.Value - after[id]), remaining);
            after[id] = unchecked(after[id] + accepted);
            remaining = unchecked(remaining - accepted);
        }
        var sourceId = context.SourceStorage!.LocationId;
        TryStore(sourceId);
        if (remaining > 0 && locations[sourceId].Name != "Farm") TryStore(context.FarmLocationId);
        if (remaining > 0)
            foreach (var id in context.NoninteriorLocationIds!)
            {
                if (locations[id].BuildingCount > 0) TryStore(id);
                if (remaining <= 0) break;
            }
        return new(offeredQuantity, remaining <= 0 ? 0 : remaining,
            locations.Keys.OrderBy(id => id, StringComparer.Ordinal).Select(id => new HayStorageLocationMutation(id, before[id], after[id])).ToList().AsReadOnly(),
            attempted.AsReadOnly());
    }

    private static bool SameLocation(HayStorageLocation left, HayStorageLocation right) => left.Name == right.Name &&
        left.RuntimeType == right.RuntimeType && left.BuildingCount == right.BuildingCount && left.StoredHay == right.StoredHay &&
        left.HayCapacity == right.HayCapacity && left.CapacityStatus == right.CapacityStatus;

    private static HayStorageLocation ValidLocation(HayStorageLocation? row)
    {
        if (row is null || string.IsNullOrWhiteSpace(row.LocationId) || string.IsNullOrWhiteSpace(row.Name) ||
            string.IsNullOrWhiteSpace(row.RuntimeType) || row.BuildingCount < 0 || !row.HayCapacity.HasValue ||
            row.CapacityStatus != "exact_native_getter")
            throw new InvalidDataException("Hay storage location identity/capacity is incomplete.");
        return row;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
