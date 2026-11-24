using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class NativeSaveBoundaryVerifierTests
{
    [Fact]
    public void RequiresBothNewDayAndChangedSaveFingerprint()
    {
        var before = Fingerprint("before");
        var after = Fingerprint("after");

        Assert.False(NativeSaveBoundaryVerifier.Evaluate(
            "1:spring:1", "1:spring:1", before, after).Verified);
        Assert.False(NativeSaveBoundaryVerifier.Evaluate(
            "1:spring:1", "1:spring:2", before, before).Verified);

        var verified = NativeSaveBoundaryVerifier.Evaluate(
            "1:spring:1", "1:spring:2", before, after);
        Assert.True(verified.DayAdvanced);
        Assert.True(verified.SaveChanged);
        Assert.True(verified.Verified);
    }

    [Fact]
    public void AggregateFingerprintIsOrderIndependentAndContentSensitive()
    {
        var first = new SaveFileFingerprint("SaveGameInfo", 4, "aaaa");
        var second = new SaveFileFingerprint("Farm_123", 8, "bbbb");

        var ordered = NativeSaveBoundaryVerifier.ComputeAggregateSha256(
            new[] { first, second });
        var reversed = NativeSaveBoundaryVerifier.ComputeAggregateSha256(
            new[] { second, first });
        var changed = NativeSaveBoundaryVerifier.ComputeAggregateSha256(
            new[] { first, second with { Sha256 = "cccc" } });

        Assert.Equal(ordered, reversed);
        Assert.NotEqual(ordered, changed);
    }

    [Fact]
    public async Task RetryCaptureDoesNotMaskInvalidSlotConfiguration()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-save-boundary-" + Guid.NewGuid().ToString("N"));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
                root,
                "missing-slot",
                maxAttempts: 2,
                retryDelayMs: 1));

        Assert.Contains(
            "native_save_boundary_slot_not_found_under_isolation_root",
            error.Message);
    }

    [Fact]
    public void CheckpointRetainsExactBytesAndNeverOverwritesDisputedArchive()
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-checkpoint-" + Guid.NewGuid().ToString("N"));
        var slot = Path.Combine(root, "farm");
        Directory.CreateDirectory(slot);
        var save = Path.Combine(slot, "farm");
        var info = Path.Combine(slot, "SaveGameInfo");
        var retained = Path.Combine(root, "accepted-native-save.zip");
        try
        {
            File.WriteAllText(save, "saved farm");
            File.WriteAllText(info, "save info");
            var accepted = NativeSaveBoundaryVerifier.Capture(root, "farm");
            Assert.Throws<IOException>(() => NativeSaveCheckpointArchive.Retain(accepted, Path.Combine(slot, "forbidden.zip")));
            File.WriteAllText(save, "later farm");
            Assert.Throws<InvalidDataException>(() => NativeSaveCheckpointArchive.Retain(accepted, retained));
            Assert.False(File.Exists(retained));
            File.WriteAllText(save, "saved farm");

            NativeSaveCheckpointArchive.Retain(accepted, retained);
            var oldArchive = File.ReadAllBytes(retained);
            File.WriteAllText(save, "later farm");
            NativeSaveCheckpointArchive.Retain(accepted, retained);
            Assert.Equal(oldArchive, File.ReadAllBytes(retained));
            Assert.Equal("later farm", File.ReadAllText(save));

            File.WriteAllText(retained, "incomplete archive");
            Assert.Throws<InvalidDataException>(() => NativeSaveCheckpointArchive.Retain(accepted, retained));
            Assert.Equal("incomplete archive", File.ReadAllText(retained));
        }
        finally
        {
            foreach (var file in new[] { retained, save, info }) File.Delete(file);
            Directory.Delete(slot);
            Directory.Delete(root);
        }
    }

    private static SaveDirectoryFingerprint Fingerprint(string hash) =>
        new("slot", 2, 12, hash);
}
