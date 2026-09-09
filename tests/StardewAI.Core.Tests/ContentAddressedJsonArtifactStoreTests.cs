using System.Text.Json;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class ContentAddressedJsonArtifactStoreTests
{
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

    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(),
        "stardewai-content-addressed-json-tests",
        Guid.NewGuid().ToString("N"));
}
