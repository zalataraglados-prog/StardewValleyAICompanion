using System.Reflection;
using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class NativeOutputSnapshotReadApiTests
{
    [Fact]
    public void WrapperAndInterfaceArePublicNonNestedWithOnlyTheExactReadMethod()
    {
        foreach (var type in new[] { typeof(INativeOutputSnapshotReadApi), typeof(NativeOutputSnapshotReadApi) })
        {
            Assert.True(type.IsPublic);
            Assert.False(type.IsNested);
            var method = Assert.Single(type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly));
            Assert.Equal("ReadNativeOutputSnapshotAnchor", method.Name);
            Assert.Equal(typeof(string), method.ReturnType);
            Assert.Empty(method.GetParameters());
        }
        Assert.True(typeof(NativeOutputSnapshotReadApi).IsSealed);
        Assert.True(typeof(INativeOutputSnapshotReadApi).IsAssignableFrom(typeof(NativeOutputSnapshotReadApi)));
    }

    [Fact]
    public void ConstructionDoesNotCaptureAndEveryExplicitReadDelegatesWithoutCachingOrRewriting()
    {
        int calls = 0;
        INativeOutputSnapshotReadApi api = new NativeOutputSnapshotReadApi(() => "raw-anchor-" + ++calls);
        Assert.Equal(0, calls);
        Assert.Equal("raw-anchor-1", api.ReadNativeOutputSnapshotAnchor());
        Assert.Equal("raw-anchor-2", api.ReadNativeOutputSnapshotAnchor());
        Assert.Equal(2, calls);
    }

    [Fact]
    public void NullDelegateIsRejectedAndDelegateFailuresAreNotReplacedBySuccessfulAnchors()
    {
        Assert.Equal("read", Assert.Throws<ArgumentNullException>(() => new NativeOutputSnapshotReadApi(null!)).ParamName);
        var error = new InvalidOperationException("declared-fixture-failure");
        var api = new NativeOutputSnapshotReadApi(() => throw error);
        Assert.Same(error, Assert.Throws<InvalidOperationException>(() => api.ReadNativeOutputSnapshotAnchor()));
    }

    // These guards inspect source wiring only; they are NOT native execution/calibration evidence.
    [Fact]
    public void SourceGuardExportAndEntryInitializationAreLazyAndContainNoCapture()
    {
        var partial = BridgeSource("ModEntry.NativeOutputSnapshotApi.cs");
        var entry = BridgeSource("ModEntry.cs");
        Assert.Contains("public override object GetApi() => new NativeOutputSnapshotReadApi(ReadNativeOutputSnapshotAnchor);", partial);
        Assert.Contains("Volatile.Write(ref nativeOutputSnapshotGameThreadId, Environment.CurrentManagedThreadId)", partial);
        var body = Between(entry, "public override void Entry(IModHelper helper)", "private void OnUpdateTicked");
        Assert.Equal(1, body.Split("InitializeNativeOutputSnapshotApi();").Length - 1);
        Assert.True(body.IndexOf("InitializeNativeOutputSnapshotApi();", StringComparison.Ordinal) <
                    body.IndexOf("config = helper.ReadConfig", StringComparison.Ordinal));
        Assert.DoesNotContain("ReadNativeOutputSnapshotAnchor()", body);
        Assert.DoesNotContain("RefreshSnapshotCache", body);
        Assert.DoesNotContain("Task.Run", partial);
        Assert.DoesNotContain("UpdateTicked +=", partial);
        Assert.DoesNotContain("Timer", partial);
    }

    [Fact]
    public void SourceGuardRejectsWrongThreadWorldMultiplayerCollectorAndMissingIsolationBeforeCapture()
    {
        var source = BridgeSource("ModEntry.NativeOutputSnapshotApi.cs");
        var guards = Between(source, "private string ReadNativeOutputSnapshotAnchor()", "var beforeTick");
        foreach (var required in new[]
        {
            "Volatile.Read(ref nativeOutputSnapshotGameThreadId) != Environment.CurrentManagedThreadId",
            "!Context.IsWorldReady", "Context.IsMultiplayer || Game1.IsMultiplayer", "stateCollector is null",
            "Environment.GetEnvironmentVariable(\"STARDEWAI_TRAINING_MODE\")",
            "Environment.GetEnvironmentVariable(\"STARDEWAI_TRAINING_RUN_ID\")",
            "Environment.GetEnvironmentVariable(\"STARDEWAI_SAVE_ISOLATION_PATH\")",
            "trainingMode != \"1\" || string.IsNullOrWhiteSpace(runId) || string.IsNullOrWhiteSpace(isolationPath)"
        }) Assert.Contains(required, guards);
        Assert.DoesNotContain("RefreshSnapshotCache", guards);
        foreach (var diagnostic in new[] { "requires_entry_game_thread", "world_not_ready", "requires_singleplayer",
                     "collector_unavailable", "requires_isolated_training_run" })
            Assert.Contains("NativeOutputSnapshotUnavailable(\"snapshot_anchor_" + diagnostic + "\")", guards);
    }

    [Fact]
    public void SourceGuardReusesFreshFullCacheAndRequiresSameTickWithoutGameplayWritesOrSecondCollector()
    {
        var source = BridgeSource("ModEntry.NativeOutputSnapshotApi.cs");
        Assert.Contains("var beforeTick = unchecked((long)Game1.ticks);", source);
        Assert.Contains("var snapshot = RefreshSnapshotCache(\"full\", publishSnapshotEvent: false);", source);
        Assert.Contains("var afterTick = unchecked((long)Game1.ticks);", source);
        Assert.Contains("beforeTick < 0 || beforeTick != afterTick || snapshot.GameTick != beforeTick", source);
        Assert.Contains("snapshot_anchor_capture_crossed_game_tick", source);
        foreach (var forbidden in new[] { "BuildSnapshot(", "BuildSnapshotWithoutEvent(", "CreateStateCollector(",
                     "new TransparentStateCollector", "new JsonSerializerOptions", "profileSnapshots[", "latestSnapshot =",
                     "HttpClient", "GetLatestSnapshotAsync", "Game1.ticks =", "Game1.timeOfDay =", "Game1.player.",
                     "Thread.Sleep", "Task.Delay", "SnapshotProfileContext.Current =" })
            Assert.DoesNotContain(forbidden, source);

        var original = BridgeSource("ModEntry.cs");
        Assert.Contains("CacheSnapshot(cacheKey, snapshot, publishSnapshotEvent);", original);
        Assert.Contains("profileSnapshots[cacheKey] = snapshot;", original);
        var build = Between(original, "private SnapshotEnvelope BuildSnapshotWithoutEvent(", "private SnapshotEnvelope RefreshSnapshotCache(bool");
        Assert.Contains("finally", build);
        Assert.Contains("SnapshotProfileContext.Current = previousProfile;", build);
        Assert.Contains("SnapshotProfileContext.SitePreparationBounds =", build);
    }

    [Fact]
    public void SourceGuardAvailableJsonContainsOnlyFreshMetadataAndFalseAuthorizationFlags()
    {
        var source = BridgeSource("ModEntry.NativeOutputSnapshotApi.cs");
        var success = Between(source, "return JsonSerializer.Serialize(new", "catch (Exception error)");
        foreach (var required in new[]
        {
            "schema_version = \"native_output_snapshot_anchor.v1\"", "status = \"available\"", "snapshot_profile = \"full\"",
            "state_hash = snapshot.StateHash", "game_tick = snapshot.GameTick", "game_version = snapshot.GameVersion",
            "save_id = snapshot.SaveId.Value", "player_id = snapshot.PlayerId.Value", "run_id = snapshotRunId",
            "save_isolation_path = snapshotIsolationPath", "diagnostics = Array.Empty<string>()",
            "native_outcome_verified = false", "formal_product_training_authorized = false", "}, jsonOptions)"
        }) Assert.Contains(required, success);
        Assert.DoesNotContain("snapshot.State,", success);
        Assert.Contains("snapshotMode != \"1\" || snapshotRunId != runId || snapshotIsolationPath != isolationPath", source);
        Assert.Contains("snapshot.SaveId.Status != FieldStatus.Available", source);
        Assert.Contains("snapshot.PlayerId.Status != FieldStatus.Available", source);
        Assert.Contains("status.GetString() == FieldStatus.Available", source);
        Assert.Contains("value.ValueKind == JsonValueKind.String", source);
        Assert.Contains("snapshot.StateHash.Length != 64 || !snapshot.StateHash.All(Uri.IsHexDigit)", source);
    }

    [Fact]
    public void SourceGuardFailuresHaveDiagnosticsButNoInventedHashTickOrCachedSuccessFallback()
    {
        var source = BridgeSource("ModEntry.NativeOutputSnapshotApi.cs");
        var failure = source[source.IndexOf("private string NativeOutputSnapshotUnavailable", StringComparison.Ordinal)..];
        Assert.Contains("status = \"unavailable\"", failure);
        Assert.Contains("diagnostics = new[] { diagnostic }", failure);
        Assert.Contains("native_outcome_verified = false", failure);
        Assert.Contains("formal_product_training_authorized = false", failure);
        Assert.DoesNotContain("state_hash", failure);
        Assert.DoesNotContain("game_tick", failure);
        Assert.DoesNotContain("status = \"available\"", failure);
        var caught = Between(source, "catch (Exception error)", "private static string? NativeOutputSnapshotEnvironment");
        Assert.Contains("NativeOutputSnapshotUnavailable(\"snapshot_anchor_capture_failed:\" + error.GetType().Name)", caught);
        Assert.DoesNotContain("latestSnapshot", caught);
    }

    private static string BridgeSource(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "StardewValleyAICompanion.sln")))
            directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory!.FullName, "src", "StardewAI.TransparentBridge", file));
    }

    private static string Between(string source, string start, string end)
    {
        int first = source.IndexOf(start, StringComparison.Ordinal);
        Assert.True(first >= 0, "Missing source guard start: " + start);
        int last = source.IndexOf(end, first + start.Length, StringComparison.Ordinal);
        Assert.True(last > first, "Missing source guard end: " + end);
        return source[first..last];
    }
}
