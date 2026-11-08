namespace StardewAI.Core.Tests;

public sealed class CanonicalObjectUnitSourceGuardTests
{
    [Fact]
    public void BothNativeModulesCompileTheSamePinnedFactoryWithoutSharingModEntries()
    {
        var root = Root();
        var source = File.ReadAllText(Path.Combine(root, "src", "SharedNative", "CanonicalVanillaObjectUnit.cs"));
        var bridge = File.ReadAllText(Path.Combine(root, "src", "StardewAI.TransparentBridge", "StardewAI.TransparentBridge.csproj"));
        var harness = File.ReadAllText(Path.Combine(root, "tools", "StardewAI.RuntimeTestHarness", "StardewAI.RuntimeTestHarness.csproj"));
        Assert.Contains("SharedNative\\CanonicalVanillaObjectUnit.cs", bridge);
        Assert.Contains("SharedNative\\CanonicalVanillaObjectUnit.cs", harness);
        Assert.Contains("hash.ComputeHash(stream)", source);
        Assert.Contains("source.GetType() != typeof(ObjectItem)", source);
        Assert.Contains("data.ItemType.GetType() != typeof(ObjectDataDefinition)", source);
        Assert.Contains("var patches = Harmony.GetPatchInfo(method)", source);
        Assert.Contains("patches is null || !patches.Owners.Any()", source);
        Assert.Contains("nameof(Item.CopyFieldsFrom)", source);
        Assert.Contains("InitializerSetters.All(Unpatched)", source);
        Assert.Contains("RegistryMethods.All(Unpatched)", source);
        Assert.Contains("nameof(ItemRegistry.GetDataOrErrorItem)", source);
        Assert.Contains("unit.CopyFieldsFrom(source)", source);
        Assert.DoesNotContain("Game1.random", source);
        Assert.DoesNotContain("ItemRegistry.Create(", source);
        Assert.DoesNotContain(".getOne(", source);
    }

    [Fact]
    public void OriginalHashCallersKeepAllCanonicalNormalizationAndTheirNativeFallback()
    {
        var bridge = File.ReadAllText(Path.Combine(Root(), "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.ObjectClearance.cs"));
        var harness = RuntimeHarnessSources.LoadFile("ModEntry.MovementSleep.ObstacleClearance.cs");
        foreach (var source in new[] { bridge, harness })
        {
            Assert.Contains("CanonicalVanillaObjectUnit.TryClone(item", source);
            Assert.Contains("? nativeUnit : item.getOne()", source);
            Assert.Contains("unit.Stack = 1", source);
            Assert.Contains("unitTool.swingTicker = sourceTool.swingTicker", source);
            Assert.Contains("objectUnit.Flipped = false", source);
            Assert.Contains("SaveSerializer.GetSerializer(unit.GetType()).Serialize(stream, unit)", source);
        }
        Assert.Contains("unit.HasBeenInInventory = true", bridge);
        Assert.Contains("unit.HasBeenInInventory = hasBeenInInventory", harness);
        Assert.Contains("CanonicalVanillaObjectUnit.TryCreateStandard(id", bridge);
        Assert.Contains("? unit : ItemRegistry.Create(id)", bridge);
    }

    [Fact]
    public void DropProjectionKeepsLiveItemsQualityFixupsStatusesAndNativeFallback()
    {
        var source = File.ReadAllText(Path.Combine(Root(), "src", "SharedNative", "CanonicalVanillaObjectUnit.cs"));
        var debris = File.ReadAllText(Path.Combine(Root(), "src", "StardewAI.TransparentBridge", "Adapters", "CurrentLocationReadAdapter.Debris.cs"));
        Assert.Contains("var projectedItem = debris.item", debris);
        Assert.Contains("if (projectedItem is null", debris);
        Assert.Contains("CanonicalVanillaObjectUnit.TryCreateProjection(", debris);
        Assert.Contains("qualifiedItemId, debris.itemQuality, out var ordinaryItem", debris);
        Assert.Contains(": ItemRegistry.Create(", debris);
        Assert.Contains("projected.Quality = quality", source);
        Assert.Contains("projected.FixQuality()", source);
        Assert.Contains("nameof(Item.Quality)", source);
        Assert.Contains("nameof(Item.FixQuality)", source);
        Assert.Contains("MethodBase.GetMethodFromHandle(method.MethodHandle, method.DeclaringType.TypeHandle)", source);
        Assert.Contains("if (quality != 0)", source);
        foreach (var status in new[] { "exact_live_item", "exact_item_registry_projection", "item_registry_projection_unavailable" })
            Assert.Contains(status, debris);
        Assert.Contains("item_quality = debris.itemQuality", debris);
        Assert.Contains("item = SummarizeItem(projectedItem)", debris);
        Assert.Contains("debris.Chunks", debris);
    }

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root missing.");
    }
}
