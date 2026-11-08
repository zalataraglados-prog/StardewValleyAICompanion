using StardewAI.Core.Infrastructure;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public async Task SupportingRouteWarmCacheCannotHideChangedCompressedSnapshotBlob()
    {
        using var f = new Fixture();
        var (transition, _) = CreateSupportingRoute(f);
        using var cas = new RouteCasFiles();
        await ContentAddressedJsonArtifactStore.WriteAsync(cas.SnapshotPath, File.ReadAllText(transition.AfterSnapshotPath),
            ContentAddressedJsonArtifactStore.ContentAddressedGzipMode);
        transition = transition with { AfterSnapshotPath = cas.SnapshotPath };
        using var warm = InvocationReadSession.Start(new[] { cas.SnapshotPath });
        _ = ContentAddressedJsonArtifactReader.ReadAllText(cas.SnapshotPath);
        var route = OpenRoute(f, transition);
        var manifest = File.ReadAllBytes(cas.SnapshotPath);
        var blob = Directory.EnumerateDirectories(cas.BlobRoot).SelectMany(Directory.EnumerateFiles).First();
        File.WriteAllBytes(blob, new byte[] { 0, 1, 2 });
        Assert.Equal(manifest, File.ReadAllBytes(cas.SnapshotPath));
        Assert.ThrowsAny<InvalidDataException>(() => route.RebindRequest);
    }

    private sealed class RouteCasFiles : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "route-cas-" + Guid.NewGuid().ToString("N"));
        internal string SnapshotPath => Path.Combine(root, "after.json");
        internal string BlobRoot => Path.Combine(root, "_snapshot-blobs");
        public void Dispose()
        {
            if (Directory.Exists(BlobRoot))
            {
                foreach (var prefix in Directory.EnumerateDirectories(BlobRoot))
                {
                    foreach (var blob in Directory.EnumerateFiles(prefix)) File.Delete(blob);
                    Directory.Delete(prefix);
                }
                Directory.Delete(BlobRoot);
            }
            if (!Directory.Exists(root)) return;
            foreach (var file in Directory.EnumerateFiles(root)) File.Delete(file);
            Directory.Delete(root);
        }
    }
}
