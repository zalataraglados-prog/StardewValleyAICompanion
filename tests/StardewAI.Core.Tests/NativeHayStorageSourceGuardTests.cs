using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed class NativeHayStorageSourceGuardTests
{
    [Fact]
    public void CanonicalHayReadDoesNotRequireAComputerHopperOrConstructedSilo()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) directory = directory.Parent;
        var root = directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
        var adapter = Path.Combine(root, "src", "StardewAI.TransparentBridge", "Adapters");
        var collect = File.ReadAllText(Path.Combine(adapter, "FarmReadAdapter.cs"));
        var source = File.ReadAllText(Path.Combine(adapter, "HayStorageReadSupport.cs"));
        Assert.Contains("[\"hay_storage\"] = SnapshotProfileContext.IncludesHayStorage", collect);
        Assert.Contains("HayStorageReadSupport.ReadInventory(farm)", collect);
        Assert.Contains("includeInteriors: false, includeGenerated: false", source);
        Assert.Contains("ReferenceEqualityComparer.Instance", source);
        Assert.Contains("projection.NoninteriorLocationIds = noninteriors.Select", source);
        Assert.Contains("StoredHay = location.piecesOfHay.Value", source);
        Assert.Contains("HayCapacity = readable ? location.GetHayCapacity() : null", source);
        Assert.Contains("BindingFlags.DeclaredOnly", source);
        Assert.Contains("info is null || !info.Owners.Any()", source);
        Assert.DoesNotContain("MachineLocationTopology.ReadPersistentLocations(", source);
        Assert.DoesNotContain("piecesOfHay.Value =", source);
        Assert.DoesNotContain(".tryToAddHay(", source);
        Assert.DoesNotContain(".StoreHayInAnySilo(", source);
        Assert.DoesNotContain("Game1.random", source);
        Assert.DoesNotContain("ReadFarmComputerReport(", source);
    }

    [Fact]
    public void UnexplainedHayMutationRemainsPersistentInTheOriginalResourceInvariant()
    {
        var first = Snapshot(1); var next = Snapshot(2);
        var method = typeof(CropTeacherRefillApproachNativeTrace).Assembly.GetType(
            "StardewAI.GoalConditionedBootstrap.FullShipmentRecoverySettlementBoundaryProjector")!
            .GetMethod("TryProjectExpectedResourceMutationInvariant", BindingFlags.Static | BindingFlags.NonPublic)!;
        var before = new object?[] { first, null }; var after = new object?[] { next, null };
        Assert.True((bool)method.Invoke(null, before)!);
        Assert.True((bool)method.Invoke(null, after)!);
        Assert.NotEqual(before[1], after[1]);
    }

    [Fact]
    public void HayNavigationContextCannotChangeTheOriginalPersistentResourceInvariant()
    {
        var first = Snapshot(1); var next = Snapshot(1);
        first.State["current_location"] = JsonSerializer.SerializeToElement(new { hay_storage_context = new { source_location = "Farm" } });
        next.State["current_location"] = JsonSerializer.SerializeToElement(new { hay_storage_context = new { source_location = "FarmHouse" } });
        var method = typeof(CropTeacherRefillApproachNativeTrace).Assembly.GetType(
            "StardewAI.GoalConditionedBootstrap.FullShipmentRecoverySettlementBoundaryProjector")!
            .GetMethod("TryProjectExpectedResourceMutationInvariant", BindingFlags.Static | BindingFlags.NonPublic)!;
        var before = new object?[] { first, null }; var after = new object?[] { next, null };
        Assert.True((bool)method.Invoke(null, before)!);
        Assert.True((bool)method.Invoke(null, after)!);
        Assert.Equal(before[1], after[1]);
    }

    private static SnapshotEnvelope Snapshot(int hay) => new()
    {
        SaveId = new() { Status = "available", Value = "hay-source" },
        PlayerId = new() { Status = "available", Value = "actor" }, GameVersion = "1.6.15",
        State = new()
        {
            ["time"] = JsonSerializer.SerializeToElement(new { total_days = new { status = "available", value = 40 } }),
            ["farm"] = JsonSerializer.SerializeToElement(new { hay_storage = new { status = "available", value = new { stored_hay = hay } } })
        }
    };
}
