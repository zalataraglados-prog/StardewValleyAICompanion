using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Core.WorldModel;

namespace StardewAI.Core.Tests;

public sealed class NativeHayStorageProjectorTests
{
    [Fact]
    public void UsesCurrentThenFarmThenNativeNoninteriorOrderWithoutDoubleCountingStorage()
    {
        var (source, context) = Fixture();
        var before = JsonSerializer.Serialize(new { source, context });
        var result = NativeHayStorageProjector.Store(source, context, 8);
        Assert.Equal(new[] { "Shed", "Farm", "Farm", "IslandFarm", "Town" }, result.AttemptedLocationIds);
        Assert.Equal(0, result.UnstoredQuantity);
        Assert.Equal(2, result.Locations.Single(row => row.LocationId == "Shed").StoredHayAfter);
        Assert.Equal(4, result.Locations.Single(row => row.LocationId == "Farm").StoredHayAfter);
        Assert.Equal(2, result.Locations.Single(row => row.LocationId == "IslandFarm").StoredHayAfter);
        Assert.Equal(1, result.Locations.Single(row => row.LocationId == "Town").StoredHayAfter);
        Assert.Equal(0, result.Locations.Single(row => row.LocationId == "Beach").StoredHayAfter);
        Assert.Equal(before, JsonSerializer.Serialize(new { source, context }));
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData(0, 0, 2, 0, 0)]
    [InlineData(1, 0, 2, 1, 0)]
    [InlineData(5, 0, 2, 2, 3)]
    [InlineData(1, 5, 2, 2, 4)]
    [InlineData(0, 5, 2, 2, 3)]
    [InlineData(1, int.MaxValue, 0, 0, 0)]
    public void PreservesNativeOvercapacityZeroAndUncheckedArithmetic(int offered, int stored, int capacity, int after, int unstored)
    {
        var (source, context) = One(stored, capacity);
        var result = NativeHayStorageProjector.Store(source, context, offered);
        Assert.Equal(after, result.Locations.Single().StoredHayAfter);
        Assert.Equal(unstored, result.UnstoredQuantity);
    }

    [Fact]
    public void NativeFarmNameBranchAndEarlyStopAreNotInferredFromAnIdOrSortedOrder()
    {
        var (source, context) = Fixture();
        context.SourceStorage!.Name = "Farm";
        context.NoninteriorLocationIds = new[] { "IslandFarm", "Town", "Farm", "Beach" };
        var result = NativeHayStorageProjector.Store(source, context, 3);
        Assert.Equal(new[] { "Shed", "IslandFarm" }, result.AttemptedLocationIds);
        Assert.Equal(1, result.Locations.Single(row => row.LocationId == "Farm").StoredHayAfter);
        Assert.Equal(1, result.Locations.Single(row => row.LocationId == "IslandFarm").StoredHayAfter);
    }

    [Theory]
    [InlineData("status")]
    [InlineData("rule")]
    [InlineData("schema")]
    [InlineData("diagnostic")]
    [InlineData("missing-location")]
    [InlineData("extra-location")]
    [InlineData("duplicate-location")]
    [InlineData("capacity")]
    [InlineData("capacity-status")]
    [InlineData("negative-offer")]
    [InlineData("context-schema")]
    [InlineData("context-status")]
    [InlineData("context-diagnostic")]
    [InlineData("context-source")]
    [InlineData("context-scope")]
    [InlineData("context-farm")]
    [InlineData("context-foreign-id")]
    [InlineData("source-echo-stock")]
    [InlineData("source-echo-capacity")]
    [InlineData("source-echo-name")]
    [InlineData("source-echo-type")]
    [InlineData("source-echo-building-count")]
    public void RejectsIncompleteOrUnsupportedNativeInputs(string change)
    {
        var (source, context) = Fixture();
        switch (change)
        {
            case "status": source.Status = "unavailable"; break;
            case "rule": context.NativeRuleStatus = "patched"; break;
            case "schema": source.SchemaVersion = "hay_storage.v2"; break;
            case "diagnostic": source.Diagnostics = new[] { "missing" }; break;
            case "missing-location": source.Locations = source.Locations.Where(row => row.LocationId != "Town").ToArray(); break;
            case "extra-location": source.Locations = source.Locations.Append(Row("Unbound", 0, 1)).ToArray(); break;
            case "duplicate-location": source.Locations = source.Locations.Append(source.Locations[0]).ToArray(); break;
            case "capacity": source.Locations[0].HayCapacity = null; break;
            case "capacity-status": source.Locations[0].CapacityStatus = "unavailable"; break;
            case "context-schema": context.SchemaVersion = "unknown"; break;
            case "context-status": context.Status = "unavailable"; break;
            case "context-diagnostic": context.Diagnostics = new[] { "unknown" }; break;
            case "context-source": context.SourceStorage = null; break;
            case "context-scope": context.NoninteriorLocationIds = null!; break;
            case "context-farm": context.FarmLocationId = ""; break;
            case "context-foreign-id": context.NoninteriorLocationIds = new[] { "Foreign" }; break;
            case "source-echo-stock": context.SourceStorage = Row("Farm", 2, 4); break;
            case "source-echo-capacity": context.SourceStorage = Row("Farm", 1, 5); break;
            case "source-echo-name": context.SourceStorage = Row("Farm", 1, 4); context.SourceStorage.Name = "Other"; break;
            case "source-echo-type": context.SourceStorage = Row("Farm", 1, 4); context.SourceStorage.RuntimeType = "Other"; break;
            case "source-echo-building-count": context.SourceStorage = Row("Farm", 1, 4, buildings: 2); break;
        }
        Assert.Throws<InvalidDataException>(() => NativeHayStorageProjector.Store(source, context, change == "negative-offer" ? -1 : 1));
    }

    [Fact]
    public void CurrentCanonicalStorageAndRepeatedNativeReferencesShareOneStock()
    {
        var (source, context) = One(0, 2);
        context.NoninteriorLocationIds = new[] { "Farm", "Farm" };
        var result = NativeHayStorageProjector.Store(source, context, 5);
        Assert.Equal(new[] { "Farm", "Farm", "Farm" }, result.AttemptedLocationIds);
        Assert.Equal(2, Assert.Single(result.Locations).StoredHayAfter);
        Assert.Equal(3, result.UnstoredQuantity);
    }

    private static (HayStorageSnapshot Inventory, HayStorageContext Context) Fixture() => (new()
    {
        Status = "available",
        Locations = new[] { Row("Farm", 1, 4), Row("Beach", 0, 100, buildings: 0), Row("IslandFarm", 0, 2), Row("Town", 0, 2) }
    }, new()
    {
        Status = "available", NativeRuleStatus = "vanilla_1_6_15_unpatched", SourceStorage = Row("Shed", 0, 2), FarmLocationId = "Farm",
        NoninteriorLocationIds = new[] { "Farm", "Beach", "IslandFarm", "Town" }
    });
    private static (HayStorageSnapshot Inventory, HayStorageContext Context) One(int stored, int capacity) => (new()
    {
        Status = "available", Locations = new[] { Row("Farm", stored, capacity) }
    }, new()
    {
        Status = "available", NativeRuleStatus = "vanilla_1_6_15_unpatched", SourceStorage = Row("Farm", stored, capacity), FarmLocationId = "Farm",
        NoninteriorLocationIds = new[] { "Farm" }
    });
    private static HayStorageLocation Row(string id, int stored, int capacity, int buildings = 1) => new()
    {
        LocationId = id, Name = id, RuntimeType = "StardewValley.GameLocation", BuildingCount = buildings,
        StoredHay = stored, HayCapacity = capacity, CapacityStatus = "exact_native_getter"
    };
}
