using StardewAI.Testing;

namespace StardewAI.Core.Tests;

public sealed class NativeRandomStateFingerprintTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CaptureReadsCompleteStateWithoutAdvancingTheSequence(bool seeded)
    {
        var random = seeded ? new Random(42) : new Random();
        Assert.True(NativeRandomStateFingerprint.TryCapture(random, out var first));
        Assert.True(NativeRandomStateFingerprint.TryCapture(random, out var same));
        Assert.Equal(first, same);
        random.Next();
        Assert.True(NativeRandomStateFingerprint.TryCapture(random, out var changed));
        Assert.NotEqual(first, changed);
        Assert.Matches("^[0-9a-f]{64}$", first);
    }

    [Fact]
    public void FingerprintDoesNotAlterSubsequentRandomOutputs()
    {
        var observed = new Random(442159967);
        var control = new Random(442159967);
        for (var i = 0; i < 64; i++)
        {
            Assert.True(NativeRandomStateFingerprint.TryCapture(observed, out _));
            Assert.Equal(control.Next(), observed.Next());
            Assert.Equal(control.NextDouble(), observed.NextDouble());
        }
    }

    [Fact]
    public void UnknownRandomRuntimeDoesNotExecuteItsVirtualMethods()
    {
        var random = new ForeignRandom();
        Assert.False(NativeRandomStateFingerprint.TryCapture(random, out var fingerprint));
        Assert.Empty(fingerprint);
        Assert.False(random.Called);
        Assert.False(NativeRandomStateFingerprint.TryCapture(null!, out _));
    }

    private sealed class ForeignRandom : Random
    {
        public bool Called;
        public override int Next() { Called = true; throw new InvalidOperationException(); }
        public override int GetHashCode() { Called = true; throw new InvalidOperationException(); }
    }
}
