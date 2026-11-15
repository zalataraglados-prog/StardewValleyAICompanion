using System.Reflection;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void ReservedNativeFrameProjectionPreservesFreeSpendButRejectsTheFirstReservedShortfall()
    {
        using var f = new NativeVariantTerminalFixture();
        var (source, ledger) = SaleStockCandidateSource(f, 3);
        var beforeHarvest = NativeVariantTerminalFixture.Copy(source); beforeHarvest.GameTick--;
        ChangeState(beforeHarvest, "player", node => Normal(node)["stack"] = 2);
        var account = Activator.CreateInstance(typeof(MoneyRevenueCropRetentionAccount), BindingFlags.Instance | BindingFlags.NonPublic,
            null, [beforeHarvest, f.Target.QualifiedItemId], null)!;
        typeof(MoneyRevenueCropRetentionAccount).GetMethod("Observe", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(account, [beforeHarvest, source, true]);
        var bind = typeof(CropTeacherSaleStockInventory).GetMethod("BindPreserved", BindingFlags.Static | BindingFlags.NonPublic)!;
        var preserve = (Func<SnapshotEnvelope, MoneyRevenueCropRetainedVariant[]>)bind.Invoke(null,
            [source, ledger, Id, "declared.candidate-wave", f.Target.QualifiedItemId])!;
        // Declared gain/frame projections only, not native authorization or a whole production claim.
        var first = f.Deposit(source, 0); var second = f.Deposit(first, 1); var missing = f.Deposit(second, 2);
        var observe = typeof(MoneyRevenueCropRetentionAccount).GetMethod("ObserveReservedForeign", BindingFlags.Instance | BindingFlags.NonPublic)!;
        observe.Invoke(account, [source, first, preserve(first)]);
        observe.Invoke(account, [first, second, preserve(second)]);
        Assert.Equal(3, typeof(MoneyRevenueCropRetentionAccount).GetProperty("Quantity", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(account));
        Assert.Contains("unavailable material projection", Assert.Throws<InvalidDataException>(() => preserve(missing)).Message);
        var changed = NativeVariantTerminalFixture.Copy(first);
        ChangeState(changed, "player", node => Normal(node)["quality"] = 4);
        Assert.Contains("original slot variant", Assert.Throws<InvalidDataException>(() => preserve(changed)).Message);
        var unrelated = (Func<SnapshotEnvelope, MoneyRevenueCropRetainedVariant[]>)bind.Invoke(null,
            [source, ledger, Id, "different-wave", f.Target.QualifiedItemId])!;
        Assert.Empty(unrelated(first));

        JsonObject Normal(JsonNode node) => node["inventory"]!["value"]!.AsArray().OfType<JsonObject>()
            .Single(row => row["slot_index"]!.GetValue<int>() == f.NormalSlot);
    }
}
