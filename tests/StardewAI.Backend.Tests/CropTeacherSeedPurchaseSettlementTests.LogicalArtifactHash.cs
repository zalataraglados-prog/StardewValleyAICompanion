using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task LogicalArtifactHashMatchesOriginalReaderAcrossFormats(bool cas, bool large)
    {
        using var files = NewLogicalHashFiles();
        var path = files.SnapshotPath;
        var value = new JsonObject
        {
            ["schema_version"] = "snapshot.v1", ["state_hash"] = "original",
            ["escaped"] = "\u4e2d\u6587\n\"\\", ["payload"] = new string('x', large ? 256_000 : 3),
            ["state"] = new JsonObject { ["time"] = new JsonObject { ["value"] = 600 },
                ["items"] = new JsonArray(1, null, "\u03b1"), ["empty"] = new JsonObject() }
        };
        if (large)
            for (var index = 0; index < 16; index++)
                value["state"]!["region" + index] = new string((char)('a' + index), 16_384);
        await ContentAddressedJsonArtifactStore.WriteAsync(path, value.ToJsonString(), cas
            ? ContentAddressedJsonArtifactStore.ContentAddressedGzipMode : ContentAddressedJsonArtifactStore.PlainMode);
        var expected = LogicalTextHash(ContentAddressedJsonArtifactReader.ReadAllText(path));
        Assert.Equal(expected, ContentAddressedJsonArtifactReader.ComputeLogicalSha256(path));
        Assert.Equal(value.ToJsonString(), ContentAddressedJsonArtifactReader.ReadAllText(path));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LogicalArtifactHashPreservesPlainWhitespaceAndBomSemantics(bool bom)
    {
        using var files = NewLogicalHashFiles();
        var path = files.SnapshotPath;
        const string text = " [ 1, null, \"\\u4e2d\" ] \n";
        File.WriteAllText(path, text, new UTF8Encoding(bom));
        Assert.Equal(LogicalTextHash(ContentAddressedJsonArtifactReader.ReadAllText(path)),
            ContentAddressedJsonArtifactReader.ComputeLogicalSha256(path));
    }

    [Fact]
    public void LogicalArtifactHashRereadsPlainInputDespiteWarmCallerAndUnchangedMetadata()
    {
        using var files = NewLogicalHashFiles();
        var path = files.SnapshotPath;
        File.WriteAllText(path, "{\"x\":1}");
        var modified = File.GetLastWriteTimeUtc(path);
        using var warm = InvocationReadSession.Start(new[] { path });
        _ = ContentAddressedJsonArtifactReader.ReadAllText(path);
        File.WriteAllText(path, "{\"x\":2}");
        File.SetLastWriteTimeUtc(path, modified);
        Assert.Equal(LogicalTextHash("{\"x\":2}"), ContentAddressedJsonArtifactReader.ComputeLogicalSha256(path));
    }

    [Fact]
    public async Task LogicalArtifactHashRejectsChangedBlobDespiteWarmMaterialization()
    {
        using var files = NewLogicalHashFiles();
        var path = files.SnapshotPath;
        await ContentAddressedJsonArtifactStore.WriteAsync(path, "{\"state_hash\":\"AAAA\",\"state\":{}}",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        using var warm = InvocationReadSession.Start(new[] { path });
        _ = ContentAddressedJsonArtifactReader.ReadAllText(path);
        var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        var reference = manifest["root_properties"]!.AsArray().Single(row => row!["name"]!.GetValue<string>() == "state_hash")!;
        var hash = reference["sha256"]!.GetValue<string>();
        var blob = Path.Combine(files.BlobRoot, hash[..2], hash + ".json.gz");
        var modified = File.GetLastWriteTimeUtc(blob);
        using (var output = File.Create(blob))
        using (var gzip = new GZipStream(output, CompressionLevel.Optimal))
            gzip.Write(Encoding.UTF8.GetBytes("\"BBBB\""));
        File.SetLastWriteTimeUtc(blob, modified);
        Assert.Throws<InvalidDataException>(() => ContentAddressedJsonArtifactReader.ComputeLogicalSha256(path));
    }

    [Theory]
    [InlineData("logical_sha256")]
    [InlineData("logical_bytes")]
    [InlineData("blob_directory")]
    [InlineData("kind")]
    public async Task LogicalArtifactHashKeepsOriginalManifestRejections(string fault)
    {
        using var files = NewLogicalHashFiles();
        var path = files.SnapshotPath;
        await ContentAddressedJsonArtifactStore.WriteAsync(path, "{\"state_hash\":\"a\",\"state\":{\"time\":600}}",
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        var manifest = JsonNode.Parse(File.ReadAllText(path))!;
        switch (fault)
        {
            case "logical_sha256": manifest[fault] = new string('0', 64); break;
            case "logical_bytes": manifest[fault] = manifest[fault]!.GetValue<long>() + 1; break;
            case "blob_directory": manifest[fault] = "foreign"; break;
            case "kind": manifest["root_properties"]![0]![fault] = "foreign"; break;
        }
        Fixture.Save(path, manifest);
        Assert.Throws<InvalidDataException>(() => ContentAddressedJsonArtifactReader.ReadAllText(path));
        Assert.Throws<InvalidDataException>(() => ContentAddressedJsonArtifactReader.ComputeLogicalSha256(path));
    }

    private static string LogicalTextHash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static RouteCasFiles NewLogicalHashFiles()
    {
        var files = new RouteCasFiles();
        Directory.CreateDirectory(Path.GetDirectoryName(files.SnapshotPath)!);
        return files;
    }
}
