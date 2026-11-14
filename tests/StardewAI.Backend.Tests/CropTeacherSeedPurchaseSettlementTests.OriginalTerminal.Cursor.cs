using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.Testing;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void TerminalOutcome_LastUnitUsesOriginalNullItemAndGraphRemovalBoundary()
    {
        using var f = new TerminalOutcomeFixture(quantity: 1);
        var outcome = f.Prepare().VerifyTransition(f.Transition);
        Assert.Equal(1, outcome.AcceptedDepositCountAfter); Assert.Equal(50L, outcome.PendingValueAfter);
        Assert.False(outcome.TerminalSettlementVerified);
    }

    [Fact]
    public async Task OriginalTerminalOwnerApproachAndDepositContinueOneAcceptedHeadWithoutCountingMovement()
    {
        using var f = new TerminalOutcomeFixture(approach: true);
        await using var owner = await f.Shared.Files.Open();
        var approach = await AdmitTerminal(owner, f);
        await owner.ExecuteOriginalTerminalAsync(approach, f.Shared.Runtime.Client, () => Task.FromResult(EmitTerminal(f)));
        await RebindTerminal(owner, approach, f);
        Assert.Equal(0, owner.AcceptedTerminalDepositCount);
        var before = JsonSerializer.Deserialize<SnapshotEnvelope>(File.ReadAllText(owner.CurrentSnapshotPath), JsonOptions)!;
        var ledger = JsonSerializer.Deserialize<StrategyCommitmentLedger>(File.ReadAllText(owner.CurrentLedgerPath), JsonOptions)!;
        var model = CropTeacherTerminalShipping.Preparation.BuildOriginalAction(before, f.Target);
        var queue = new ActionQueueCompiler().Compile(model, before, ledger);
        var modelPath = Path.Combine(f.Root, "next-terminal-model.json");
        var queuePath = Path.Combine(f.Root, "next-terminal-queue.json");
        var receiptPath = Path.Combine(f.Root, "execution-0002.json");
        var afterPath = Path.Combine(f.Root, "after-snapshot-0002.json");
        var deposit = await owner.CompileAndAdmitOriginalTerminalAsync(f.Target, () =>
        {
            Fixture.Save(modelPath, model); Fixture.Save(queuePath, queue);
            return Task.FromResult(new StardewAI.LiveTrainingLoop.CropTeacherOriginalTerminalCompilation(modelPath, queuePath,
                receiptPath, afterPath, f.Shared.Files.Native + "-next"));
        });
        var after = f.DepositAfter(before);
        var candidate = model.Actions.Single().Parameters.Single(row => row.Name == "precondition").Value["candidate_id:".Length..];
        await owner.ExecuteOriginalTerminalAsync(deposit, f.Shared.Runtime.Client, () =>
        {
            var receipt = MoneyCropTeacherFixture.Create<QueueExecutionReceiptEnvelope>("MoneyCropQueueReceipt", queue, before, after,
                candidate, "ship_inventory_item_to_bin", new[] {
                    new { path = "player.inventory." + f.Target.QualifiedItemId + ".count", before = 2, after = 1 },
                    new { path = "farm.shipping_bin." + f.Target.QualifiedItemId + ".count", before = 0, after = 1 } });
            receipt.RunId = f.Shared.Files.Options.RunId;
            Fixture.Save(receiptPath, receipt); Fixture.Save(afterPath, after);
            f.Shared.Runtime.Store.Snapshots[after.StateHash] = after;
            return Task.FromResult(new JsonObject { ["execution_path"] = receiptPath, ["after_snapshot_path"] = afterPath });
        });
        Assert.Equal(0, owner.AcceptedTerminalDepositCount);
        await RebindTerminal(owner, deposit, f);
        Assert.Equal(1, owner.AcceptedTerminalDepositCount); Assert.Equal(2, f.Shared.Runtime.Handler.Commits);
        var accepted = OwnedControlAccepted(f.Shared, deposit);
        Assert.Equal(50L, accepted["terminal_pending_value_after"]!.GetValue<long>());
        await Assert.ThrowsAsync<InvalidDataException>(() => owner.CompileAndAdmitOriginalTerminalAsync(f.Target,
            () => throw new InvalidOperationException("Stale cursor cannot compile.")));
    }
}
