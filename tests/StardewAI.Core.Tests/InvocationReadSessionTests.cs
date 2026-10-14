using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using StardewAI.Core.Infrastructure;

namespace StardewAI.Core.Tests;

public sealed class InvocationReadSessionTests
{
    [Fact]
    public void ReusesPhysicalReadMaterializationAndFullHashWithinInvocation()
    {
        var path = NewFile("plain.json", "{\"value\":1}");
        using var session = InvocationReadSession.Start(new[] { path });

        var first = ContentAddressedJsonArtifactReader.ReadAllText(path);
        var second = ContentAddressedJsonArtifactReader.ReadAllText(path);
        var firstHash = InvocationReadSession.HashFile(path, out var firstHashRead);
        var secondHash = InvocationReadSession.HashFile(path, out var secondHashRead);
        var snapshot = session.Snapshot();

        Assert.Equal(first, second);
        Assert.Equal(Sha256(File.ReadAllBytes(path)), firstHash);
        Assert.Equal(firstHash, secondHash);
        Assert.True(firstHashRead.CacheHit);
        Assert.True(secondHashRead.CacheHit);
        Assert.Equal(1, snapshot.PhysicalReadCount);
        Assert.Equal(1, snapshot.FullHashCount);
        Assert.Equal(1, snapshot.MaterializationCount);
        Assert.True(snapshot.CacheHitCount >= 3);
        Assert.Equal(1, snapshot.EntryCount);
        Assert.True(snapshot.RetainedRawBytes > 0);
        Assert.True(snapshot.RetainedStoredTextBytes > 0);
        Assert.True(snapshot.RetainedBytes >=
            snapshot.RetainedRawBytes + snapshot.RetainedStoredTextBytes);
        Assert.Equal(snapshot.RetainedBytes, snapshot.MaxEntryRetainedBytes);
    }

    [Fact]
    public void ProhibitedOutputPathsAndRootsAreNeverReusable()
    {
        var root = NewRoot();
        var exact = NewFile(root, "exact.json", "{}");
        var nested = NewFile(root, Path.Combine("output", "nested.json"), "{}");

        using var session = InvocationReadSession.Start(
            new[] { exact, nested },
            prohibitedPaths: new[] { exact },
            prohibitedRoots: new[] { Path.Combine(root, "output") });

        Assert.False(InvocationReadSession.IsPathReusable(exact));
        Assert.False(InvocationReadSession.IsPathReusable(nested));
        Assert.Equal(0, session.Snapshot().RegisteredPathCount);
    }

    [Fact]
    public void DisabledModePreservesReadResultsWithoutCaching()
    {
        var path = NewFile("disabled.json", "{\"value\":2}");
        using var session = InvocationReadSession.Start(
            new[] { path },
            enabled: false);

        var first = ContentAddressedJsonArtifactReader.ReadAllText(path);
        var second = ContentAddressedJsonArtifactReader.ReadAllText(path);
        var snapshot = session.Snapshot();

        Assert.Equal(first, second);
        Assert.False(InvocationReadSession.IsPathReusable(path));
        Assert.False(snapshot.Enabled);
        Assert.Equal(0, snapshot.EntryCount);
        Assert.Equal(0, snapshot.PhysicalReadCount);
        Assert.Equal(0, snapshot.CacheHitCount);
        Assert.Equal(0, snapshot.RetainedBytes);
    }

    [Fact]
    public void DisposedSessionNeverLeaksContentIntoNextInvocation()
    {
        var path = NewFile("lifetime.json", "{\"value\":3}");
        using (InvocationReadSession.Start(new[] { path }))
        {
            Assert.Contains(":3", ContentAddressedJsonArtifactReader.ReadAllText(path));
        }

        File.WriteAllText(path, "{\"value\":4}");
        using (InvocationReadSession.Start(new[] { path }))
        {
            Assert.Contains(":4", ContentAddressedJsonArtifactReader.ReadAllText(path));
        }
    }

    [Fact]
    public void NestedSessionRestoresItsOuterInvocationScope()
    {
        var outerPath = NewFile("outer.json", "{\"value\":5}");
        var innerPath = NewFile("inner.json", "{\"value\":6}");
        using var outer = InvocationReadSession.Start(new[] { outerPath });
        Assert.True(InvocationReadSession.IsPathReusable(outerPath));

        using (InvocationReadSession.Start(new[] { innerPath }))
        {
            Assert.False(InvocationReadSession.IsPathReusable(outerPath));
            Assert.True(InvocationReadSession.IsPathReusable(innerPath));
        }

        Assert.True(InvocationReadSession.IsPathReusable(outerPath));
        Assert.False(InvocationReadSession.IsPathReusable(innerPath));
    }

    [Fact]
    public void CorruptedContentAddressedBlobStillFailsClosed()
    {
        var (manifestPath, blobPath, _) = NewContentAddressedArtifact();
        File.WriteAllBytes(blobPath, new byte[] { 1, 2, 3 });
        using var session = InvocationReadSession.Start(new[] { manifestPath });

        Assert.ThrowsAny<InvalidDataException>(() =>
            ContentAddressedJsonArtifactReader.ReadAllText(manifestPath));
        Assert.Equal(0, session.Snapshot().MaterializationCount);
    }

    [Fact]
    public void VerifiedContentAddressedMaterializationIsReused()
    {
        var (manifestPath, blobPath, logicalJson) = NewContentAddressedArtifact();
        using var session = InvocationReadSession.Start(new[] { manifestPath });

        var first = ContentAddressedJsonArtifactReader.ReadAllText(manifestPath);
        File.WriteAllBytes(blobPath, new byte[] { 1, 2, 3 });
        var second = ContentAddressedJsonArtifactReader.ReadAllText(manifestPath);

        Assert.Equal(logicalJson, first);
        Assert.Equal(first, second);
        Assert.Equal(1, session.Snapshot().MaterializationCount);
    }

    private static (string ManifestPath, string BlobPath, string LogicalJson)
        NewContentAddressedArtifact()
    {
        var root = NewRoot();
        const string logicalJson = "{\"value\":{\"nested\":true}}";
        var valueBytes = Encoding.UTF8.GetBytes("{\"nested\":true}");
        var valueHash = Sha256(valueBytes);
        var blobPath = Path.Combine(
            root,
            "_snapshot-blobs",
            valueHash[..2],
            valueHash + ".json.gz");
        Directory.CreateDirectory(Path.GetDirectoryName(blobPath)!);
        using (var stream = File.Create(blobPath))
        using (var gzip = new GZipStream(stream, CompressionMode.Compress))
            gzip.Write(valueBytes);

        var logicalBytes = Encoding.UTF8.GetBytes(logicalJson);
        var manifest = $$"""
        {
          "schema_version":"stardewai.content_addressed_json.v1",
          "blob_directory":"_snapshot-blobs",
          "logical_bytes":{{logicalBytes.Length}},
          "logical_sha256":"{{Sha256(logicalBytes)}}",
          "root_properties":[
            {
              "name":"value",
              "kind":"value",
              "sha256":"{{valueHash}}",
              "uncompressed_bytes":{{valueBytes.Length}}
            }
          ]
        }
        """;
        var manifestPath = NewFile(root, "artifact.json", manifest);
        return (manifestPath, blobPath, logicalJson);
    }

    private static string NewFile(string name, string content) =>
        NewFile(NewRoot(), name, content);

    private static string NewFile(string root, string name, string content)
    {
        var path = Path.Combine(root, name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private static string NewRoot() => Path.Combine(
        Path.GetTempPath(),
        "stardewai-invocation-read-session-tests",
        Guid.NewGuid().ToString("N"));

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
