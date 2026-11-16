using System;
using System.IO;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed class NativeHoeSingleTileEnergyTests
{
    [Theory]
    [InlineData(270f, 270, 0, false, false, 268f)]
    [InlineData(100f, 270, 3, false, false, 98.3f)]
    [InlineData(100f, 270, 10, false, false, 99f)]
    [InlineData(100f, 270, 20, false, false, 100f)]
    [InlineData(100f, 270, 30, false, false, 101f)]
    [InlineData(-15f, 270, 0, false, false, -16f)]
    [InlineData(-20f, 270, 0, false, false, -16f)]
    [InlineData(105f, 100, 0, false, false, 100f)]
    [InlineData(100f, 100, 30, false, false, 100f)]
    [InlineData(1f, 0, 30, false, false, 0f)]
    [InlineData(100f, 270, 0, false, true, 100f)]
    [InlineData(105f, 100, 0, false, true, 105f)]
    [InlineData(-20f, 270, 0, false, true, -20f)]
    [InlineData(100f, 270, 30, false, true, 101f)]
    [InlineData(105f, 100, 30, false, true, 100f)]
    [InlineData(105f, 100, 20, false, true, 100f)]
    [InlineData(-20f, 270, 20, false, true, -16f)]
    [InlineData(105f, 100, 0, true, false, 105f)]
    [InlineData(-20f, 270, 30, true, true, -20f)]
    public void ExpectedAfterAndVerifyFollowNativeSetterBranches(float before, int maximum, int level,
        bool efficient, bool protection, float expected)
    {
        Assert.Equal(expected, NativeHoeSingleTileEnergy.ExpectedAfter(before, maximum, level, efficient, protection));
        NativeHoeSingleTileEnergy.Verify(before, expected, maximum, level, efficient, protection);
        Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.Verify(before,
            MathF.BitIncrement(expected), maximum, level, efficient, protection));
        Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.Verify(before,
            MathF.BitDecrement(expected), maximum, level, efficient, protection));
    }

    [Fact]
    public void NontrivialFarmingLevelKeepsFloatOperationOrderRatherThanDoubleOrRoundedCost()
    {
        // 2f - (2f - 3f * 0.1f) is 0x3e999998, not rounded 0.3f (0x3e99999a).
        var expected = BitConverter.Int32BitsToSingle(0x3e999998);
        var actual = NativeHoeSingleTileEnergy.ExpectedAfter(2f, 270, 3, false, false);
        Assert.Equal(0x3e999998, BitConverter.SingleToInt32Bits(actual));
        NativeHoeSingleTileEnergy.Verify(2f, expected, 270, 3, false, false);
        Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.Verify(2f, 0.3f, 270, 3, false, false));
    }

    [Fact]
    public void RoundedEqualRequestStillInvokesSetterEvenWithDecreaseProtection()
    {
        Assert.Equal(270f, NativeHoeSingleTileEnergy.ExpectedAfter(float.MaxValue, 270, 0, false, true));
        Assert.Equal(float.MaxValue, NativeHoeSingleTileEnergy.ExpectedAfter(float.MaxValue, 270, 0, true, true));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void NonfiniteBeforeOrAfterIsRejectedEvenWhenSetterWouldBeSkipped(float invalid)
    {
        foreach (var efficient in new[] { false, true })
        foreach (var protection in new[] { false, true })
        {
            Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.ExpectedAfter(invalid, 270, 0, efficient, protection));
            Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.Verify(invalid, 100f, 270, 0, efficient, protection));
            Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.Verify(100f, invalid, 270, 0, efficient, protection));
        }
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(270, -1)]
    [InlineData(-1, -1)]
    public void NegativeMaximumOrEffectiveLevelIsRejectedBeforeSkipBranches(int maximum, int level)
    {
        foreach (var efficient in new[] { false, true })
        foreach (var protection in new[] { false, true })
        {
            Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.ExpectedAfter(100f, maximum, level, efficient, protection));
            Assert.Throws<InvalidDataException>(() => NativeHoeSingleTileEnergy.Verify(100f, 100f, maximum, level, efficient, protection));
        }
    }
}
