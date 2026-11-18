using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class NativeToolUpdateMetadataTests
{
    [Theory]
    [InlineData(NativeToolSpriteKind.WateringCan, "(T)WateringCan", 0, 6, 2, 277)]
    [InlineData(NativeToolSpriteKind.WateringCan, "(T)WateringCan", 1, 4, 1, 275)]
    [InlineData(NativeToolSpriteKind.WateringCan, "(T)WateringCan", 1, 5, 1, 276)]
    [InlineData(NativeToolSpriteKind.WateringCan, "(T)WateringCan", 2, 6, 2, 274)]
    [InlineData(NativeToolSpriteKind.WateringCan, "(T)WateringCan", 3, 3, 3, 275)]
    [InlineData(NativeToolSpriteKind.Other, "(T)Hoe", 0, 0, 0, 276)]
    [InlineData(NativeToolSpriteKind.Other, "(T)Hoe", 0, 2, 0, 277)]
    [InlineData(NativeToolSpriteKind.Other, "(T)Axe", 1, 1, 1, 1001)]
    [InlineData(NativeToolSpriteKind.Other, "(T)Hoe", 2, 1, 2, 999)]
    [InlineData(NativeToolSpriteKind.Other, "(T)Hoe", 2, 2, 2, 274)]
    [InlineData(NativeToolSpriteKind.FishingRod, "(T)BambooPole", 0, 0, 0, 276)]
    [InlineData(NativeToolSpriteKind.FishingRod, "(T)BambooPole", 1, 1, 1, 999)]
    [InlineData(NativeToolSpriteKind.WateringCan, "(T)non_base_can", 0, 1, 0, 278)]
    [InlineData(NativeToolSpriteKind.Other, "(T)WateringCan", 1, 6, 1, 276)]
    [InlineData(NativeToolSpriteKind.WateringCan, "(T)WateringCan", 42, 1, 2, 273)]
    [InlineData(NativeToolSpriteKind.Other, "(T)Hoe", 1, -1, 1, 275)]
    public void ProjectsOneActualNativeUpdateRatherThanACompletedCycle(NativeToolSpriteKind kind,
        string id, int direction, int frame, int facing, int expected) =>
        Assert.Equal(expected, NativeToolUpdateMetadata.ApplySpriteUpdate(273, 999, direction, frame, facing, kind, id));

    [Fact]
    public void NonResetBranchRetainsThePreviousCursorAcrossCalls()
    {
        var first = NativeToolUpdateMetadata.ApplySpriteUpdate(273, 500, 1, 1, 1, NativeToolSpriteKind.Other, "(T)Hoe");
        Assert.Equal(502, first);
        Assert.Equal(504, NativeToolUpdateMetadata.ApplySpriteUpdate(273, first, 1, 1, 1, NativeToolSpriteKind.Other, "(T)Hoe"));
    }

    [Fact]
    public void NativeIntegerArithmeticWrapsAndUnknownCategoriesAreNotInvented()
    {
        Assert.Equal(int.MinValue + 2, NativeToolUpdateMetadata.ApplySpriteUpdate(int.MaxValue, 0, 1, 6, 1,
            NativeToolSpriteKind.WateringCan, "(T)WateringCan"));
        Assert.Equal(int.MinValue + 1, NativeToolUpdateMetadata.ApplySpriteUpdate(0, int.MaxValue, 1, 1, 1,
            NativeToolSpriteKind.Other, "(T)Hoe"));
        Assert.Throws<InvalidDataException>(() => NativeToolUpdateMetadata.ApplySpriteUpdate(0, 0, 0, 0, 0,
            (NativeToolSpriteKind)99, "(T)Hoe"));
    }
}
