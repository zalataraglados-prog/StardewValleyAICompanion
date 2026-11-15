using System.Text.Json;
using System.Reflection;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProducerTerminalRejectsForeignVariantEvenWhenAggregateInventoryIsSufficient(bool foreignSilver)
    {
        using var f = new NativeVariantTerminalFixture();
        var initial = f.Shared.Source.Before;
        var stock = initial.State["player"].GetProperty("inventory").GetProperty("value").EnumerateArray().ToArray();
        string Key(int index) => MoneyRevenueCropRetentionAccount.InventoryIdentity(stock.Single(row => row.GetProperty("slot_index").GetInt32() == index));
        var proof = new MoneyRevenueCropOutputRetentionProof
        {
            SchemaVersion = "money_revenue_crop_output_retention_proof.v2", MinimumUnits = 6, MinimumGrossRevenue = 1050,
            HarvestItemQualifiedId = f.Target.QualifiedItemId,
            ProducerRetention = new(6, foreignSilver ? [new(Key(f.NormalSlot), 6)] : [new(Key(f.NormalSlot), 5), new(Key(f.SilverSlot), 1)], [])
        };
        if (!foreignSilver) MoneyRevenueCropOutputRetentionProofBuilder.VerifyProducerTerminalTarget(proof, f.Target, initial);
        else Assert.Contains("foreign", Assert.Throws<InvalidDataException>(() =>
            MoneyRevenueCropOutputRetentionProofBuilder.VerifyProducerTerminalTarget(proof, f.Target, initial)).Message);
        Assert.Throws<InvalidDataException>(() => MoneyRevenueCropOutputRetentionProofBuilder.VerifyProducerTerminalTarget(
            proof, f.Target with { NativeVariants = null }, initial));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ProducerRetentionConsumesSealedNativeFramesBetweenDeclaredHarvestBatches(bool product)
    {
        var f = new SiteTillNativeFixture(productTransport: product);
        using var host = f.Files.F.Runtime;
        var shared = await SharedLifecycleTransition(f);
        var before = LifecycleRead<SnapshotEnvelope>(shared.BeforeSnapshotPath);
        var after = LifecycleRead<SnapshotEnvelope>(shared.AfterSnapshotPath);
        var first = NativeVariantTerminalFixture.Copy(before);
        var last = NativeVariantTerminalFixture.Copy(after);
        void ChangeCount(SnapshotEnvelope frame, int delta)
        {
            ChangeState(frame, "player", node =>
            {
                var slot = node["inventory"]!["value"]!.AsArray().Single(row => row!["qualified_item_id"]?.GetValue<string>() == "(O)745")!;
                slot["stack"] = slot["stack"]!.GetValue<int>() + delta;
            });
        }
        first.GameTick -= 2; last.GameTick += 2;
        ChangeCount(first, -2); ChangeCount(last, 2);
        var firstPath = Path.Combine(f.Files.F.Source.RuntimePath, "producer-declared-first.json");
        var lastPath = Path.Combine(f.Files.F.Source.RuntimePath, "producer-declared-last.json");
        Fixture.Save(firstPath, first); Fixture.Save(lastPath, last);
        var requests = new[] { shared with { TransitionKind = "crop_actions", BeforeSnapshotPath = firstPath,
                AfterSnapshotPath = shared.BeforeSnapshotPath, NativeSharedSource = null }, shared,
            shared with { TransitionKind = "crop_actions", BeforeSnapshotPath = shared.AfterSnapshotPath,
                AfterSnapshotPath = lastPath, NativeSharedSource = null } };
        static string Hash(string path) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
        MoneyRevenueCropNativeLifecycleTransitionProof Row(int index, SnapshotEnvelope start, SnapshotEnvelope end, bool owned) =>
            new(owned ? "crop_actions" : "shared_source_return", shared.TotalDay, shared.QueuePath, Hash(shared.QueuePath),
                requests[index].BeforeSnapshotPath, Hash(requests[index].BeforeSnapshotPath), shared.ExecutionReceiptPath, Hash(shared.ExecutionReceiptPath),
                requests[index].AfterSnapshotPath, Hash(requests[index].AfterSnapshotPath), "declared-row-" + index, start.StateHash, end.StateHash,
                owned ? 2 : 1, owned ? "executor.harvest_crop" : "executor.till_soil", owned ? Id : "", true);
        var rows = new[] { Row(0, first, before, true), Row(1, before, after, false), Row(2, after, last, true) };
        var requestPath = Path.Combine(f.Files.F.Source.RuntimePath, "producer-declared-lifecycle-request.json");
        Fixture.Save(requestPath, new MoneyRevenueCropNativeLifecycleProofRequest { Transitions = requests });
        var lifecycle = new MoneyRevenueCropNativeLifecycleProof { RequestPath = requestPath, Transitions = rows };
        // Tests the attribution consumer with declared harvest boundaries and an original sealed native middle.
        // It deliberately does NOT claim these synthetic outer rows are a complete native lifecycle proof.
        var method = typeof(MoneyRevenueCropOutputRetentionProofBuilder).GetMethod("BuildProducerRetention", BindingFlags.NonPublic | BindingFlags.Static)!;
        var result = (MoneyRevenueCropProducerRetention)method.Invoke(null, [lifecycle, new[] { rows[0], rows[2] }, 0, 2, "(O)745", 4, 4])!;
        Assert.Equal(4, result.RetainedQuantity);
        Assert.Equal(new[] { true, false, true }, result.Segments.Select(row => row.OwnedHarvest));
        Assert.Equal(1, result.Segments[1].ObservedBoundaryCount);
        Assert.Equal(0, result.Segments[1].InventoryIncrease);
        File.AppendAllText(f.JournalPath, "\n");
        var error = Assert.Throws<TargetInvocationException>(() => method.Invoke(null,
            [lifecycle, new[] { rows[0], rows[2] }, 0, 2, "(O)745", 4, 4]));
        Assert.IsType<InvalidDataException>(error.InnerException);
    }
}
