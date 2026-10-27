using System.Text.Json;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class ContentAddressedJsonArtifactStoreTests : IDisposable
{
    private readonly List<string> roots = new();

    [Fact]
    public async Task RoundTripPreservesSnapshotAndDeduplicatesUnchangedSections()
    {
        var root = NewRoot();
        var firstPath = Path.Combine(root, "live-snapshots", "before-snapshot-0001.json");
        var secondPath = Path.Combine(root, "live-snapshots", "before-snapshot-0002.json");
        Directory.CreateDirectory(Path.GetDirectoryName(firstPath)!);
        const string first =
            "{\"schema_version\":\"snapshot.v1\",\"state_hash\":\"a\",\"state\":{\"locations\":{\"value\":[1,2,3]},\"player\":{\"tile_x\":1},\"time\":{\"value\":600}}}";
        const string second =
            "{\"schema_version\":\"snapshot.v1\",\"state_hash\":\"b\",\"state\":{\"locations\":{\"value\":[1,2,3]},\"player\":{\"tile_x\":2},\"time\":{\"value\":600}}}";

        await ContentAddressedJsonArtifactStore.WriteAsync(
            firstPath,
            first,
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        await ContentAddressedJsonArtifactStore.WriteAsync(
            secondPath,
            second,
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);

        Assert.Equal(first, ContentAddressedJsonArtifactStore.ReadAllText(firstPath));
        Assert.Equal(first, ContentAddressedJsonArtifactReader.ReadAllText(firstPath));
        Assert.Equal(second, await ContentAddressedJsonArtifactStore.ReadAllTextAsync(secondPath));
        using var manifest = JsonDocument.Parse(File.ReadAllText(firstPath));
        Assert.Equal(
            ContentAddressedJsonArtifactStore.ManifestSchemaVersion,
            manifest.RootElement.GetProperty("schema_version").GetString());
        var blobs = Directory.GetFiles(
            Path.Combine(root, "live-snapshots", "_snapshot-blobs"),
            "*.json.gz",
            SearchOption.AllDirectories);
        Assert.Equal(7, blobs.Length);
    }

    [Fact]
    public async Task ReaderRejectsCorruptedBlob()
    {
        var root = NewRoot();
        var path = Path.Combine(root, "live-snapshots", "before-snapshot-0001.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string snapshot =
            "{\"schema_version\":\"snapshot.v1\",\"state\":{\"time\":{\"value\":600}}}";
        await ContentAddressedJsonArtifactStore.WriteAsync(
            path,
            snapshot,
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        var blob = Directory.GetFiles(
            Path.Combine(root, "live-snapshots", "_snapshot-blobs"),
            "*.json.gz",
            SearchOption.AllDirectories)[0];
        await File.WriteAllBytesAsync(blob, new byte[] { 1, 2, 3 });

        Assert.ThrowsAny<InvalidDataException>(() =>
            ContentAddressedJsonArtifactStore.ReadAllText(path));
    }

    [Fact]
    public async Task PlainModeRetainsOrdinaryJsonCompatibility()
    {
        var root = NewRoot();
        var path = Path.Combine(root, "live-snapshots", "before-snapshot-0001.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string snapshot = "{\"state\":{\"time\":600}}";

        await ContentAddressedJsonArtifactStore.WriteAsync(
            path,
            snapshot,
            ContentAddressedJsonArtifactStore.PlainMode);

        Assert.Equal(snapshot, File.ReadAllText(path));
        Assert.Equal(snapshot, ContentAddressedJsonArtifactStore.ReadAllText(path));
    }

    [Fact]
    public async Task ContentAddressedModeCanonicalizesFormattingWithoutChangingData()
    {
        var root = NewRoot();
        var path = Path.Combine(root, "live-snapshots", "before-snapshot-0001.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string formatted =
            "{\n  \"schema_version\": \"snapshot.v1\",\n  \"state\": {\n    \"time\": { \"value\": 600 }\n  }\n}";

        await ContentAddressedJsonArtifactStore.WriteAsync(
            path,
            formatted,
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);

        Assert.Equal(
            "{\"schema_version\":\"snapshot.v1\",\"state\":{\"time\":{\"value\":600}}}",
            ContentAddressedJsonArtifactStore.ReadAllText(path));
    }

    [Fact]
    public async Task BlobPruneFailsClosedWhenAnyLogicalJsonIsInvalid()
    {
        var root = NewRoot();
        var snapshots = Path.Combine(root, "live-snapshots");
        var firstPath = Path.Combine(snapshots, "before-snapshot-0001.json");
        var secondPath = Path.Combine(snapshots, "before-snapshot-0002.json");
        Directory.CreateDirectory(snapshots);
        const string first =
            "{\"schema_version\":\"snapshot.v1\",\"state_hash\":\"a\",\"state\":{\"locations\":{\"value\":[1]},\"player\":{\"tile_x\":1}}}";
        const string second =
            "{\"schema_version\":\"snapshot.v1\",\"state_hash\":\"b\",\"state\":{\"locations\":{\"value\":[1]},\"player\":{\"tile_x\":2}}}";
        await ContentAddressedJsonArtifactStore.WriteAsync(
            firstPath,
            first,
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        await ContentAddressedJsonArtifactStore.WriteAsync(
            secondPath,
            second,
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        File.Delete(firstPath);
        File.WriteAllText(Path.Combine(snapshots, "partial.json"), "{");
        var blobRoot = Path.Combine(snapshots, "_snapshot-blobs");
        var before = Directory.GetFiles(
            blobRoot,
            "*.json.gz",
            SearchOption.AllDirectories).Length;

        var result = ContentAddressedJsonArtifactRetention
            .PruneUnreferencedBlobs(snapshots);

        Assert.False(result.Completed);
        Assert.Equal(0, result.DeletedBlobCount);
        Assert.StartsWith(
            "logical_json_invalid:",
            result.SkipReason,
            StringComparison.Ordinal);
        Assert.Equal(
            before,
            Directory.GetFiles(
                blobRoot,
                "*.json.gz",
                SearchOption.AllDirectories).Length);
        Assert.Equal(
            second,
            ContentAddressedJsonArtifactStore.ReadAllText(secondPath));
    }

    public void Dispose()
    {
        foreach (var root in roots)
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private string NewRoot()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-content-addressed-json-tests",
            Guid.NewGuid().ToString("N"));
        roots.Add(root);
        return root;
    }
}
