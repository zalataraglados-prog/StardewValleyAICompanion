using System.IO;
using StardewAI.Core.Training;

namespace StardewAI.Backend.Tests;

public sealed class NativeHoeCompletedCycleMetadataTests
{
    [Theory]
    [InlineData(0, 4, 2, 1)]
    [InlineData(21, 25, 23, 22)]
    [InlineData(-1, 3, 1, 0)]
    [InlineData(-21, -17, -19, -20)]
    [InlineData(int.MinValue, int.MinValue + 4, int.MinValue + 2, int.MinValue + 1)]
    [InlineData(int.MaxValue, int.MinValue + 3, int.MinValue + 1, int.MinValue)]
    [InlineData(int.MaxValue - 1, int.MinValue + 2, int.MinValue, int.MaxValue)]
    [InlineData(int.MaxValue - 2, int.MinValue + 1, int.MaxValue, int.MaxValue - 1)]
    [InlineData(int.MaxValue - 3, int.MinValue, int.MaxValue - 1, int.MaxValue - 2)]
    [InlineData(int.MaxValue - 4, int.MaxValue, int.MaxValue - 2, int.MaxValue - 3)]
    public void CompletedSpriteUsesDirectionalNativeOffsetAndUncheckedWrap(
        int initial, int north, int eastOrWest, int south)
    {
        Assert.Equal(north, NativeHoeCompletedCycleMetadata.ExpectedSpriteIndex(initial, 0));
        Assert.Equal(eastOrWest, NativeHoeCompletedCycleMetadata.ExpectedSpriteIndex(initial, 1));
        Assert.Equal(south, NativeHoeCompletedCycleMetadata.ExpectedSpriteIndex(initial, 2));
        Assert.Equal(eastOrWest, NativeHoeCompletedCycleMetadata.ExpectedSpriteIndex(initial, 3));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    [InlineData(21)]
    [InlineData(int.MinValue)]
    [InlineData(int.MaxValue)]
    public void InvalidFacingIsRejectedForAnyInitialIndex(int facing)
    {
        foreach (var initial in new[] { 0, 21, -21, int.MinValue, int.MaxValue })
            Assert.Throws<InvalidDataException>(() =>
                NativeHoeCompletedCycleMetadata.ExpectedSpriteIndex(initial, facing));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(21, 22)]
    [InlineData(123456789, 123456790)]
    [InlineData(-123456789, -123456788)]
    [InlineData(-1, 0)]
    [InlineData(int.MinValue, int.MinValue + 1)]
    [InlineData(int.MaxValue - 1, int.MaxValue)]
    [InlineData(int.MaxValue, int.MinValue)]
    public void SwingTickerIncrementsExactlyOnceWithUncheckedWrap(int before, int expected)
    {
        Assert.Equal(expected, NativeHoeCompletedCycleMetadata.ExpectedSwingTicker(before));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public void HistoryPreservesBeforeUnlessVerifiedNativeEquipSetsIt(
        bool before, bool equippedDuringCycle, bool expected)
    {
        Assert.Equal(expected, NativeHoeCompletedCycleMetadata.ExpectedInventoryHistory(before, equippedDuringCycle));
    }
}
