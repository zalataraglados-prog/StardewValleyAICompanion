using StardewAI.RuntimePrimitives;

namespace StardewAI.Core.Tests;

public sealed class NativeStorageAccessTests
{
    [Fact]
    public void UnownedWoodenFarmChestIsUsableWithOnlyTheCurrentFarmer()
    {
        Assert.True(NativeStorageAccess.CanUseUnownedWoodenFarmChest(
            0, 17, "(BC)130", true, true, new long[] { 17 }));
    }

    [Theory]
    [InlineData(17, "(BC)130", true, true, 17)]
    [InlineData(0, "(BC)130", false, true, 17)]
    [InlineData(0, "(BC)130", true, false, 17)]
    [InlineData(0, "(BC)232", true, true, 17)]
    [InlineData(0, "(BC)130", true, true, 31)]
    public void OtherOwnershipOrNativeContextDoesNotGrantAccess(
        long ownerId, string itemId, bool standalone, bool ordinaryFarm, long farmerId)
    {
        Assert.False(NativeStorageAccess.CanUseUnownedWoodenFarmChest(
            ownerId, 17, itemId, standalone, ordinaryFarm, new[] { farmerId }));
    }

    [Fact]
    public void OfflineFarmhandStillPreventsSingleplayerAccess()
    {
        Assert.False(NativeStorageAccess.CanUseUnownedWoodenFarmChest(
            0, 17, "(BC)130", true, true, new long[] { 17, 31 }));
    }
}
