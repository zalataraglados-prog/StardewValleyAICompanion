using System.Reflection;
using System.Security.Cryptography;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task ProductionPublicationRetainsFailedAttemptAndRetriesWithoutChangingItsSource(int failAtWrite)
    {
        var f = new Fixture();
        var sourceHash = PublicationHash(f.SourcePath);
        var method = typeof(CropTeacherManagedSeedOwner).Assembly.GetType("Program")!
            .GetMethod("CreateCropTeacherProductionAttemptPath", BindingFlags.NonPublic | BindingFlags.Static)!;
        string Attempt() => (string)method.Invoke(null, [Path.GetDirectoryName(f.Output)!])!;
        var failed = Attempt(); var writes = 0;
        await Assert.ThrowsAsync<IOException>(() => f.Build(failed, (path, json) =>
        {
            if (++writes == failAtWrite) throw new IOException("declared publication write failure");
            return File.WriteAllTextAsync(path, json);
        }));
        Assert.True(Directory.Exists(failed));
        Assert.False(File.Exists(Path.Combine(failed, "production-evidence.json")));
        var retained = Directory.GetFiles(failed).ToDictionary(path => path, PublicationHash);
        await Assert.ThrowsAsync<IOException>(() => f.Build(failed));
        var retry = Attempt(); Assert.NotEqual(failed, retry);
        Assert.False(Directory.Exists(retry));
        var evidence = await f.Build(retry);
        Assert.True(File.Exists(evidence.ManifestPath));
        Assert.Equal(sourceHash, PublicationHash(f.SourcePath));
        Assert.Equal(retained.OrderBy(pair => pair.Key), Directory.GetFiles(failed)
            .ToDictionary(path => path, PublicationHash).OrderBy(pair => pair.Key));
        await Assert.ThrowsAsync<IOException>(() => f.Build(retry));
    }

    private static string PublicationHash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
