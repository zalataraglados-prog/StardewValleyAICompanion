using System.Security.Cryptography;
using System.Text.Json;
using StardewAI.LiveTrainingLoop;
using static StardewAI.Backend.Tests.CropTeacherSeedPurchaseSettlementTests;

namespace StardewAI.Backend.Tests;

internal sealed class CropTeacherSeedRecoveryTestSupport : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "crop-runtime-recovery-" + Guid.NewGuid().ToString("N"));
    internal string Output => Path.Combine(root, "evidence");

    internal CropTeacherSeedRecoveryTestSupport() => Directory.CreateDirectory(root);

    internal Task<CropTeacherSeedRecoveredBoundary> Recover(HttpClient http, LiveTrainingOptions options,
        string runtime, CropTeacherManagedSeedScopes scopes, CropTeacherRunStep step, string? expectedIntent = null) =>
        InvokeLive<Task<CropTeacherSeedRecoveredBoundary>>("RecoverCropTeacherSeedRuntimeAsync", http, options, runtime,
            Output, expectedIntent ?? Hash(Path.Combine(runtime, "support-intent.json")), scopes, step);

    internal static string Hash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    internal static void FailBeforeWrite(string path, string name)
    {
        if (Path.GetFileName(path) == name) throw new IOException("Crash before original reply file was recorded.");
    }

    internal static void AssertSameLedger(CropTeacherSeedRecoveredBoundary recovered, object expected)
    {
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(recovered.ReadLedger()));
        recovered.ValidateArtifactsUnchanged();
    }

    public void Dispose()
    {
        foreach (var directory in new[] { Path.Combine(Output, "teacher-ledgers"), Output, root })
        {
            if (!Directory.Exists(directory)) continue;
            foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }
    }
}
