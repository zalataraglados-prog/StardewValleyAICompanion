using System.Reflection;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherProductionEvidenceTests
{
    [Fact]
    public void ReservedRetentionProtectsOnlyExistingOwnedCreditAndLeavesLegacyLossDebitUnchanged()
    {
        var start = RetentionFrame(0, 10); var harvest = RetentionFrame(1, 14); var spent = RetentionFrame(2, 12);
        var legacy = Account(start); var reserved = Account(start);
        Observe(legacy, start, harvest, true); Observe(reserved, start, harvest, true);
        Observe(legacy, harvest, spent, false);
        ObserveReserved(reserved, harvest, spent, [new(Variant(harvest), 4)]);
        Assert.Equal(2, Quantity(legacy)); Assert.Equal(4, Quantity(reserved));
        var foreign = RetentionFrame(3, 20);
        ObserveReserved(reserved, spent, foreign, [new(Variant(harvest), 4)]);
        Assert.Equal(4, Quantity(reserved));
    }

    [Fact]
    public void ReservedRetentionStillDebitsUnprotectedOwnedSurplus()
    {
        var start = RetentionFrame(0, 10); var harvest = RetentionFrame(1, 14); var spent = RetentionFrame(2, 10);
        var account = Account(start); Observe(account, start, harvest, true);
        ObserveReserved(account, harvest, spent, [new(Variant(harvest), 2)]);
        Assert.Equal(2, Quantity(account));
    }

    [Theory]
    [InlineData("excess")]
    [InlineData("missing_physical")]
    [InlineData("foreign_variant")]
    [InlineData("duplicate")]
    [InlineData("zero")]
    [InlineData("already_lost")]
    public void ReservedRetentionCannotCreateCreditOrRepairConsumedOutput(string fault)
    {
        var start = RetentionFrame(0, 10); var harvest = RetentionFrame(1, 14); var spent = RetentionFrame(2, 12);
        var account = Account(start); Observe(account, start, harvest, true);
        var key = Variant(harvest);
        MoneyRevenueCropRetainedVariant[] floors = [new(key, 4)];
        if (fault == "excess") floors = [new(key, 5)];
        if (fault == "zero") floors = [new(key, 0)];
        if (fault == "foreign_variant") floors = [new(new string('a', 64), 4)];
        if (fault == "duplicate") floors = [new(key, 2), new(key, 2)];
        if (fault == "missing_physical") spent = RetentionFrame(2, 3);
        if (fault == "already_lost")
        {
            var lost = RetentionFrame(2, 10); Observe(account, harvest, lost, false);
            harvest = lost; spent = RetentionFrame(3, 14);
        }
        Assert.IsType<InvalidDataException>(Assert.Throws<TargetInvocationException>(() =>
            ObserveReserved(account, harvest, spent, floors)).InnerException);
    }

    private static string Variant(SnapshotEnvelope frame) => MoneyRevenueCropRetentionAccount.InventoryIdentity(
        frame.State["player"].GetProperty("inventory").GetProperty("value")[0]);
    private static void ObserveReserved(object account, SnapshotEnvelope before, SnapshotEnvelope after,
        MoneyRevenueCropRetainedVariant[] floors) => account.GetType().GetMethod("ObserveReservedForeign", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(account, [before, after, floors]);
}
