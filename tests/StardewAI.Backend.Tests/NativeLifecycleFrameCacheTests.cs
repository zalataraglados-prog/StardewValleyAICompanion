using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

// Cache fixtures only, not native game or production evidence.
public sealed class NativeLifecycleFrameCacheTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Type CacheType = typeof(MoneyRevenueCropNativeLifecycleProofBuilder)
        .GetNestedType("NativeLifecycleFrameCache", BindingFlags.NonPublic)!;

    [Fact]
    public void NativeLifecycleFramesAreBoundedAndReloadEveryOriginalFieldAfterEviction()
    {
        var cache = Cache(); var root = Root();
        var snapshots = Enumerable.Range(0, 5).Select(Snapshot).ToArray();
        for (var index = 0; index < snapshots.Length; index++)
        {
            var path = Path.Combine(root, index + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(snapshots[index], JsonOptions));
            Call(cache, "Bind", path, snapshots[index]);
            Assert.InRange((int)Property(cache, "ResidentCount"), 1, 2);
        }
        var restored = Assert.IsType<SnapshotEnvelope>(Call(cache, "Read", Path.Combine(root, "0.json")));
        Assert.NotSame(snapshots[0], restored);
        Assert.Equal(JsonSerializer.Serialize(snapshots[0], JsonOptions), JsonSerializer.Serialize(restored, JsonOptions));
        Assert.Equal(8192, restored.State["unrelated_full_state"].GetProperty("payload").GetString()!.Length);
        Assert.Equal(2, Property(cache, "PeakResidentCount"));
        Call(cache, "ValidateInputsUnchanged");
    }

    [Fact]
    public void NativeLifecycleCacheNeverUsesClaimedStateHashAsWholeFrameIdentity()
    {
        var cache = Cache(); var root = Root();
        var first = Snapshot(0); var second = Snapshot(1);
        Assert.Equal(first.StateHash, second.StateHash);
        foreach (var (name, snapshot) in new[] { ("first", first), ("second", second) })
        {
            var path = Path.Combine(root, name + ".json");
            File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions)); Call(cache, "Bind", path, snapshot);
        }
        Assert.Same(first, Call(cache, "Read", Path.Combine(root, "first.json")));
        Assert.Same(second, Call(cache, "Read", Path.Combine(root, "second.json")));
    }

    [Theory]
    [InlineData("Read")]
    [InlineData("ValidateInputsUnchanged")]
    public void NativeLifecycleCacheRejectsChangedOriginalFilesEvenOnCacheHits(string operation)
    {
        var cache = Cache(); var path = Path.Combine(Root(), "frame.json"); var snapshot = Snapshot(0);
        File.WriteAllText(path, JsonSerializer.Serialize(snapshot, JsonOptions)); Call(cache, "Bind", path, snapshot);
        File.WriteAllText(path, JsonSerializer.Serialize(Snapshot(1), JsonOptions));
        var error = Assert.Throws<TargetInvocationException>(() => Call(cache, operation,
            operation == "Read" ? new object[] { path } : Array.Empty<object>()));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }

    private static SnapshotEnvelope Snapshot(int index) => new()
    {
        StateHash = "same_declared_hash_not_a_cache_key", GameTick = index,
        State = new() { ["unrelated_full_state"] = JsonSerializer.SerializeToElement(new { payload = new string('x', 8192), index }) }
    };
    private static object Cache() => Activator.CreateInstance(CacheType, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
        null, [2], null)!;
    private static object? Call(object cache, string method, params object[] args) => CacheType
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Instance)!.Invoke(cache, args);
    private static object Property(object cache, string name) => CacheType
        .GetProperty(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(cache)!;
    private static string Root()
    {
        var path = Path.Combine(Path.GetTempPath(), "native-frame-cache-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path); return path;
    }
}
