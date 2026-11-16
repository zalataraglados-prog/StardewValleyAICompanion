using System.Text.Json;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void OrderedNativeVariantsVerifyEveryPrefixWithoutChangingTheWholeObligation()
    {
        using var f = new NativeVariantTerminalFixture(interleaved: true);
        var source = f.Shared.Source.Before;
        long[] pending = [0, 175, 393, 568, 743, 918, 1093];
        for (var count = 0; count <= 6; count++)
        {
            Assert.Equal(pending[count], f.Target.PendingValueAt(count));
            f.Target.VerifyNativeVariantSource(source, count, 1050);
            if (count < 6) source = f.Deposit(source, count);
        }
        Assert.Equal(6, f.Target.RequiredQuantity);
        Assert.Equal(f.Target, JsonSerializer.Deserialize<CropTeacherTerminalShippingTarget>(
            JsonSerializer.Serialize(f.Target, JsonOptions), JsonOptions));
        Assert.Throws<InvalidDataException>(() => f.Target.VariantAt(6));
        Assert.Throws<InvalidDataException>(() => f.Target.PendingValueAt(-1));
        Assert.Throws<InvalidDataException>(() => f.Target.PendingValueAt(7));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("long")]
    [InlineData("foreign_slot")]
    [InlineData("overdraw")]
    [InlineData("missing_order")]
    [InlineData("legacy_with_order")]
    [InlineData("unknown_version")]
    public void OrderedNativeVariantsRejectAnOrderThatChangesTheSelectedDenominator(string fault)
    {
        using var f = new NativeVariantTerminalFixture(interleaved: true);
        int[] slots = [f.NormalSlot, f.SilverSlot, f.NormalSlot, f.NormalSlot, f.NormalSlot, f.NormalSlot];
        if (fault == "short") slots = slots[..^1];
        if (fault == "long") slots = [.. slots, f.NormalSlot];
        if (fault == "foreign_slot") slots[2] = Math.Max(f.NormalSlot, f.SilverSlot) + 100;
        if (fault == "overdraw") slots[2] = f.SilverSlot;
        CropTeacherTerminalDepositOrder? order = null;
        foreach (var slot in slots.Reverse()) order = new(slot, order);
        var target = f.Target with { NativeVariants = f.Target.NativeVariants! with
        {
            DepositOrder = fault == "missing_order" ? null : order,
            SchemaVersion = fault == "legacy_with_order" ? "crop_teacher_native_terminal_variants.v1" :
                fault == "unknown_version" ? "unknown.v1" : "crop_teacher_native_terminal_variants.v2"
        } };
        Assert.Throws<InvalidDataException>(() => target.ReadNativeVariants().ToArray());
        Assert.Throws<InvalidDataException>(() => target.PendingValueAt(2));
    }

    [Fact]
    public void OrderedNativeVariantsDoNotReinterpretTheLegacyGroupedCursor()
    {
        using var f = new NativeVariantTerminalFixture(interleaved: true);
        var afterTwo = f.Deposit(f.Deposit(f.Shared.Source.Before, 0), 1);
        f.Target.VerifyNativeVariantSource(afterTwo, 2, 1050);
        var legacy = f.Target with { NativeVariants = f.Target.NativeVariants! with
        { SchemaVersion = "crop_teacher_native_terminal_variants.v1", DepositOrder = null } };
        Assert.Equal(f.SilverSlot, f.Target.VariantAt(1).SlotIndex);
        Assert.Equal(f.NormalSlot, legacy.VariantAt(1).SlotIndex);
        Assert.Equal(350, legacy.PendingValueAt(2));
        Assert.Throws<InvalidDataException>(() => legacy.VerifyNativeVariantSource(afterTwo, 2, 1050));
        Assert.Throws<InvalidDataException>(() => f.Target.VerifyNativeVariantSource(afterTwo, 1, 1050));
        Assert.Throws<InvalidDataException>(() => f.Target.VerifyNativeVariantSource(afterTwo, 3, 1050));
        Assert.DoesNotContain("depositOrder", JsonSerializer.Serialize(legacy, JsonOptions), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("crop_teacher_native_variant_terminal_request.v1")]
    [InlineData("crop_teacher_native_variant_terminal_request.v2")]
    public void LegacySettlementWrappersDoNotSilentlyAdmitTheNewOrderedMode(string schema)
    {
        using var f = new NativeVariantTerminalFixture(interleaved: true);
        var path = Path.Combine(f.Root, "ordered-legacy-request.json");
        Fixture.Save(path, new CropTeacherNativeVariantTerminalRequest(schema,
            f.Target with { AcceptedDepositCount = 6 }, new MoneyRevenueCropTerminalSettlementProofRequest()));
        var error = Assert.Throws<InvalidDataException>(() => CropTeacherNativeVariantTerminalProof.Build(path));
        Assert.Contains("invalid explicit variant request", error.Message, StringComparison.Ordinal);
    }
}
