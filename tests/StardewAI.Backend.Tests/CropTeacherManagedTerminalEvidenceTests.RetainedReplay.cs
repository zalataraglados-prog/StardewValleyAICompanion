using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherManagedTerminalEvidenceTests
{
    [Fact]
    public async Task RetainedTerminalReplayPreservesOriginalBytesRevenueAndNoSaveOrTrainingClaims()
    {
        var golden = await Shared.Value;
        var f = new Case(golden);
        var built = await f.Build();
        var bytes = RetainedBytes(f.Output);
        // The current accepted trace can contain later load evidence, but it is not a native step or old input.
        var later = f.Output + "-later-load.json";
        File.WriteAllText(later, "{}");
        f.Hashes[later] = Hash(later);
        var replay = await Replay(f, golden);
        Assert.Equal(built, replay);
        Assert.Equal(Hash(replay.ManifestPath), replay.ManifestSha256);
        var manifest = Read<JsonElement>(replay.ManifestPath);
        Assert.False(manifest.GetProperty("semantic_saved_content_verified").GetBoolean());
        Assert.False(manifest.GetProperty("formal_product_training_authorized").GetBoolean());
        Assert.False(manifest.GetProperty("input_artifact_hashes").TryGetProperty(later, out _));
        AssertRetainedBytes(bytes, f.Output);
        await Assert.ThrowsAsync<IOException>(() => f.Build());
        AssertRetainedBytes(bytes, f.Output);
    }

    [Fact]
    public async Task RetainedTerminalReplayMissingDirectoryAndEachPartialOutputRejectWithoutRepair()
    {
        var golden = await Shared.Value;
        var missing = new Case(golden);
        await Assert.ThrowsAsync<InvalidDataException>(() => Replay(missing, golden));
        Assert.False(Directory.Exists(missing.Output));
        var complete = new Case(golden);
        await complete.Build();
        var original = RetainedBytes(complete.Output);
        foreach (var omitted in original.Keys)
        {
            var partial = new Case(golden);
            Directory.CreateDirectory(partial.Output);
            foreach (var (path, bytes) in original)
                if (path != omitted) File.WriteAllBytes(Path.Combine(partial.Output, Path.GetFileName(path)), bytes);
            var before = RetainedBytes(partial.Output);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => Replay(partial, golden));
            Assert.Contains("missing or nonregular", error.Message);
            AssertRetainedBytes(before, partial.Output);
        }
        AssertRetainedBytes(original, complete.Output);
    }

    [Fact]
    public async Task RetainedTerminalReplayRejectsEveryModifiedEmittedProofOrRequestWithoutWriting()
    {
        var golden = await Shared.Value;
        foreach (var name in new[] { "recurrence-manifest", "recurrence-proof", "terminal-request", "terminal-proof" })
        {
            var f = new Case(golden);
            await f.Build();
            EditRetained(Path.Combine(f.Output, name + ".json"), node => node["foreign_added_field"] = true);
            var before = RetainedBytes(f.Output);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => Replay(f, golden));
            Assert.Contains("emitted output hash differs", error.Message);
            AssertRetainedBytes(before, f.Output);
        }
    }

    [Fact]
    public async Task RetainedTerminalReplaySameCoreRejectsRehashedProofMutationIncludingUnknownFields()
    {
        var golden = await Shared.Value;
        foreach (var name in new[] { "recurrence-manifest", "recurrence-proof", "terminal-request", "terminal-proof" })
        {
            var f = new Case(golden);
            var built = await f.Build();
            var path = Path.Combine(f.Output, name + ".json");
            EditRetained(path, node => node["foreign_added_field"] = true);
            EditRetained(built.ManifestPath, node => node["input_artifact_hashes"]![path] = Hash(path));
            var before = RetainedBytes(f.Output);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => Replay(f, golden));
            Assert.Contains("writer changed original proof output", error.Message);
            AssertRetainedBytes(before, f.Output);
        }
    }

    [Fact]
    public async Task RetainedTerminalReplayChecksFinalFlagsHeadTargetRevenueTransitionsAndUnknownFields()
    {
        var golden = await Shared.Value;
        foreach (var change in new Action<JsonObject>[]
        {
            node => node["semantic_saved_content_verified"] = true,
            node => node["formal_product_training_authorized"] = true,
            node => node["terminal_settlement_verified"] = false,
            node => node["schema_version"] = "foreign.v1",
            node => node["accepted_snapshot_path"] = golden.ProductionFixture.SourcePath,
            node => node["accepted_ledger_path"] = golden.ProductionFixture.LedgerPath,
            node => node["native_run_plan_path"] = golden.ProductionFixture.SourcePath,
            node => node["target"] = new JsonObject(),
            node => node["settled_revenue"] = -1,
            node => node["owner_terminal_transitions"] = new JsonArray(),
            node => node["unknown_added_field"] = true
        })
        {
            var f = new Case(golden);
            var built = await f.Build();
            EditRetained(built.ManifestPath, change);
            var before = RetainedBytes(f.Output);
            await Assert.ThrowsAsync<InvalidDataException>(() => Replay(f, golden));
            AssertRetainedBytes(before, f.Output);
        }
    }

    [Fact]
    public async Task RetainedTerminalReplayRejectsForeignInputHashTamperingAndUnpinnedProductionAuthority()
    {
        var golden = await Shared.Value;
        foreach (var mode in new[] { "foreign", "hash", "production_hash", "production_unpinned", "native_source_unpinned" })
        {
            var f = new Case(golden);
            var built = await f.Build();
            if (mode == "foreign")
            {
                var foreign = f.Output + "-foreign.json";
                File.WriteAllText(foreign, "{}");
                EditRetained(built.ManifestPath, node => node["input_artifact_hashes"]![foreign] = Hash(foreign));
            }
            if (mode == "hash")
                EditRetained(built.ManifestPath, node => node["input_artifact_hashes"]![golden.ProductionFixture.NativePlanPath] = new string('0', 64));
            if (mode == "production_hash") f.Production = f.Production with { ManifestSha256 = new string('0', 64) };
            if (mode == "production_unpinned") f.Hashes.Remove(f.Production.ManifestPath);
            if (mode == "native_source_unpinned") f.Hashes.Remove(golden.FinalLedgerPath);
            var before = RetainedBytes(f.Output);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => Replay(f, golden));
            Assert.Contains(mode.StartsWith("production", StringComparison.Ordinal) ? "production manifest is not pinned" : "lacks authoritative", error.Message);
            AssertRetainedBytes(before, f.Output);
        }
    }

    [Fact]
    public async Task RetainedTerminalReplayUsesOnlyTracePinnedProductionMapForDerivedProofInputs()
    {
        var golden = await Shared.Value;
        var f = new Case(golden);
        var built = await f.Build();
        var productionInputs = Read<JsonElement>(f.Production.ManifestPath).GetProperty("input_artifact_hashes")
            .EnumerateObject().Select(row => row.Name).ToArray();
        Assert.Contains(f.Production.DailyCapacityProofPaths[0], productionInputs);
        foreach (var path in productionInputs) f.Hashes.Remove(path);
        var before = RetainedBytes(f.Output);
        Assert.Equal(built, await Replay(f, golden));
        AssertRetainedBytes(before, f.Output);
    }

    [Fact]
    public async Task RetainedTerminalReplayDoesNotTrustWarmCachedFinalManifestOrProof()
    {
        var golden = await Shared.Value;
        foreach (var name in new[] { "terminal-evidence", "terminal-proof" })
        {
            var f = new Case(golden);
            await f.Build();
            var path = Path.Combine(f.Output, name + ".json");
            using var warm = InvocationReadSession.Start(new[] { path }, enabled: true);
            _ = ContentAddressedJsonArtifactReader.ReadAllText(path);
            EditRetained(path, node => node["warm_cache_mutation"] = true);
            var before = RetainedBytes(f.Output);
            await Assert.ThrowsAsync<InvalidDataException>(() => Replay(f, golden));
            AssertRetainedBytes(before, f.Output);
        }
    }

    private static async Task<CropTeacherManagedTerminalEvidence> Replay(Case f, Golden golden)
    {
        var trace = (CropTeacherManagedProductionTrace)typeof(CropTeacherManagedProductionTrace)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic).Single().Invoke(new object[]
            { golden.ProductionFixture.NativePlanPath, f.FinalPath, golden.FinalLedgerPath, f.Transitions.ToArray(), f.Hashes });
        var type = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("StardewAI.LiveTrainingLoop.CropTeacherTerminalSettlementEvidence")!;
        Task<CropTeacherManagedTerminalEvidence> task;
        try
        {
            task = (Task<CropTeacherManagedTerminalEvidence>)type.GetMethod("ReadAsync", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, new object[] { trace, f.Production, f.Target, f.Output })!;
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
        return await task;
    }

    private static void EditRetained(string path, Action<JsonObject> change)
    {
        var node = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        change(node);
        File.WriteAllText(path, node.ToJsonString(JsonOptions));
    }

    private static Dictionary<string, byte[]> RetainedBytes(string output) => Directory.EnumerateFiles(output)
        .ToDictionary(path => path, File.ReadAllBytes);

    private static void AssertRetainedBytes(Dictionary<string, byte[]> before, string output)
    {
        var after = RetainedBytes(output);
        Assert.Equal(before.Keys.OrderBy(path => path), after.Keys.OrderBy(path => path));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
    }
}
