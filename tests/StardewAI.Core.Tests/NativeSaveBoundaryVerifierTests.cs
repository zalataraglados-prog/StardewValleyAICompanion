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

    private static SaveDirectoryFingerprint Fingerprint(string hash) =>
        new("slot", 2, 12, hash);
}
