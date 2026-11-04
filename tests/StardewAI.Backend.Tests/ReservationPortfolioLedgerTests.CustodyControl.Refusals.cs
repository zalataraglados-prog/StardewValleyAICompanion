using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class ReservationPortfolioLedgerTests
{
    [Theory]
    [InlineData("pending")]
    [InlineData("legacy")]
    [InlineData("destination")]
    [InlineData("clock-missing")]
    [InlineData("clock-conflict")]
    [InlineData("clock-malformed")]
    [InlineData("late")]
    [InlineData("actor")]
    [InlineData("menu")]
    public void CustodyControlExcludesUnreadySourcesBeforeCommittingAnything(string failure)
    {
        var fixture = new CustodyTransferFixture();
        var destination = CustodyTransferFixture.Chest;
        switch (failure)
        {
            case "pending": fixture.Prepare(false); break;
            case "legacy":
                fixture.Ledger.MaterialReservations.Single(row => row.ReservationId == fixture.CustodyId).OutputCustody!.InitialPosition = null;
                foreach (var history in fixture.Ledger.History) history.OutputCustodyInitialPositionSha256 = null;
                break;
            case "destination": destination = "chest:Farm:missing"; break;
            case "clock-missing": fixture.Snapshot.State.Remove("time"); break;
            case "clock-conflict": fixture.Snapshot.State["time"] = JsonSerializer.SerializeToElement(new
                { time = new { value = 600, status = "available" }, time_of_day = new { value = 700, status = "available" } }); break;
            case "clock-malformed": fixture.Snapshot.State["time"] = JsonSerializer.SerializeToElement(new
                { time = new { value = "invalid", status = "available" }, time_of_day = new { value = 700, status = "available" } }); break;
            case "late": fixture.Snapshot.State["time"] = JsonSerializer.SerializeToElement(new { time_of_day = new { value = 2559, status = "available" } }); break;
            case "actor": fixture.Snapshot.PlayerId.Value = "foreign-player"; break;
            case "menu": fixture.Snapshot.State["menus"] = JsonSerializer.SerializeToElement(new { active_menu = new { value = new { is_open = true, type = "test" }, status = "available" } }); break;
        }
        fixture.Snapshot.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(fixture.Snapshot.State);
        var original = JsonSerializer.Serialize(fixture.Ledger);
        var result = MuseumOutputCustodyTransferRequestBuilder.Build(fixture.Snapshot, fixture.Ledger, fixture.CustodyId, destination);
        Assert.Null(result.Request);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(original, JsonSerializer.Serialize(fixture.Ledger));
    }

    [Theory]
    [InlineData("position")]
    [InlineData("reservation")]
    [InlineData("row-revision")]
    [InlineData("portfolio")]
    [InlineData("ledger-revision")]
    [InlineData("kind")]
    [InlineData("goal")]
    [InlineData("duplicate")]
    [InlineData("stripped")]
    [InlineData("legacy-downgrade")]
    [InlineData("mixed-protocol")]
    [InlineData("scalar-intent")]
    [InlineData("execution-intent")]
    [InlineData("execution-projection")]
    public void CustodyControlTamperingCannotBecomeDispatchAuthority(string mutation)
    {
        var attempt = PrepareCustodyControl();
        var item = JsonSerializer.Deserialize<ActionQueueItem>(JsonSerializer.Serialize(attempt.Transfer))!;
        var parameters = item.NormalizedCommand.Parameters.ToList();
        void Change(string name, string value) => parameters.Single(row => row.Name == name).Value = value;
        switch (mutation)
        {
            case "position": Change(MuseumOutputCustodyTransferBinding.PositionParameter, new string('0', 64)); break;
            case "reservation": Change(MuseumOutputCustodyTransferBinding.ReservationParameter, "other-goal-spare"); break;
            case "row-revision": Change(MuseumOutputCustodyTransferBinding.ReservationRevisionParameter, "999"); break;
            case "portfolio": Change(MuseumOutputCustodyTransferBinding.PortfolioParameter, "wrong-portfolio"); break;
            case "ledger-revision": Change(MuseumOutputCustodyTransferBinding.LedgerRevisionParameter, "999"); break;
            case "kind": Change(MuseumOutputCustodyTransferBinding.KindParameter, "machine_input_material_transfer"); break;
            case "goal": item.NormalizedCommand.GoalId = "other-goal"; break;
            case "duplicate": parameters.Add(new() { Name = MuseumOutputCustodyTransferBinding.KindParameter, Value = MuseumOutputCustodyTransferBinding.Kind }); break;
            case "stripped": parameters.RemoveAll(row => MuseumOutputCustodyTransferBinding.IsControlParameter(row.Name)); break;
            case "legacy-downgrade":
                parameters.RemoveAll(row => MuseumOutputCustodyTransferBinding.IsControlParameter(row.Name));
                parameters.Add(new() { Name = "acquisition_reservation_portfolio_id", Value = attempt.Commit.PortfolioId });
                parameters.Add(new() { Name = "acquisition_reservation_ledger_revision", Value = attempt.Commit.CommittedLedgerRevision!.Value.ToString() });
                parameters.Add(new() { Name = "acquisition_support_transition_kind", Value = "machine_input_material_transfer" });
                break;
            case "mixed-protocol": parameters.Add(new() { Name = "acquisition_support_transition_kind", Value = "machine_input_material_transfer" }); break;
            case "scalar-intent": Change("quantity", "2"); break;
            case "execution-intent": Change("material_transfer_intent_json", "null"); break;
            case "execution-projection": Change("material_transfer_projection_json", "null"); break;
        }
        item.NormalizedCommand.Parameters = parameters.ToArray();
        var original = JsonSerializer.Serialize(attempt.Fixture.Ledger);
        var result = new ActionQueueDispatchReadinessService().Evaluate(attempt.Compilation.Queue!, item,
            attempt.Fixture.Ledger, attempt.Before.StateHash);
        Assert.False(result.Ready);
        Assert.NotEmpty(result.BlockingReasons);
        Assert.Equal(original, JsonSerializer.Serialize(attempt.Fixture.Ledger));
    }

    [Theory]
    [InlineData("row")]
    [InlineData("preparation")]
    [InlineData("result-count")]
    [InlineData("history")]
    public void CustodyControlCompilerIndependentlyRejectsChangedCommitResults(string mutation)
    {
        var attempt = PrepareCustodyControl();
        var committed = JsonSerializer.Deserialize<ReservationPortfolioCommitResult>(JsonSerializer.Serialize(attempt.Commit))!;
        switch (mutation)
        {
            case "row": committed.Ledger!.MaterialReservations.Single(row => row.ReservationId == "other-goal-stone").GoalId = "stolen-goal"; break;
            case "preparation": committed.Ledger!.PreparedMaterialTransfer!.BindingSha256 = new string('0', 64); break;
            case "result-count": committed.MaterialClaimCount++; break;
            case "history": committed.Ledger!.History = committed.Ledger.History[..^1]; break;
        }
        var result = MuseumOutputCustodyTransferRequestBuilder.Compile(attempt.Before, attempt.SourceLedger,
            attempt.Fixture.CustodyId, CustodyTransferFixture.Chest, committed);
        Assert.Null(result.Queue);
        Assert.Contains("museum_custody_transfer_commit_replay_drifted", result.Errors);
    }

    [Theory]
    [InlineData("goal")]
    [InlineData("passive-slot")]
    [InlineData("map")]
    [InlineData("proof")]
    public void CustodyControlIndependentlyRejectsChangedSettlementResults(string mutation)
    {
        var attempt = PrepareCustodyControl();
        var queue = attempt.Compilation.Queue!;
        var receipt = attempt.Receipt();
        var request = MuseumOutputCustodyTransferSettlementRequestBuilder.Build(queue, attempt.Transfer, receipt,
            attempt.Before, attempt.After, attempt.Fixture.Ledger).Request!;
        var actual = attempt.Fixture.Service.SettleSupportingTransition(attempt.Fixture.Ledger, attempt.After, request, "settled");
        Assert.True(actual.Accepted);
        switch (mutation)
        {
            case "goal": actual.Ledger!.MaterialReservations.Single(row => row.ReservationId == attempt.Fixture.CustodyId).GoalId = "wrong-goal"; break;
            case "passive-slot": actual.Ledger!.MaterialReservations.Single(row => row.ReservationId == "other-goal-stone").SlotIndex = 9; break;
            case "map": actual.MaterialTransferReservations = null; break;
            case "proof": actual.Ledger!.History.Single(row => row.OutputCustodyPositionTransition is not null)
                .OutputCustodyPositionTransition!.BindingSha256 = new string('0', 64); break;
        }
        Assert.Contains("museum_custody_transfer_settlement_replay_drifted",
            MuseumOutputCustodyTransferSettlementRequestBuilder.Verify(queue, attempt.Transfer, receipt,
                attempt.Before, attempt.After, attempt.Fixture.Ledger, actual));
    }

    [Theory]
    [InlineData("receipt-queue")]
    [InlineData("receipt-fresh")]
    [InlineData("receipt-option")]
    [InlineData("receipt-after")]
    [InlineData("after-slots")]
    [InlineData("before-slots")]
    [InlineData("before-passive-quality")]
    [InlineData("queue-authority")]
    [InlineData("recorded-missing")]
    [InlineData("recorded-shape")]
    [InlineData("recorded-command")]
    public void CustodyControlSettlementRejectsUnverifiedNativeOrQueueAuthority(string mutation)
    {
        var attempt = PrepareCustodyControl();
        var item = JsonSerializer.Deserialize<ActionQueueItem>(JsonSerializer.Serialize(attempt.Transfer))!;
        var receipt = attempt.Receipt();
        switch (mutation)
        {
            case "receipt-queue": receipt.QueueId = "foreign-queue"; break;
            case "receipt-fresh": receipt.AfterSnapshotFresh = false; break;
            case "receipt-option": receipt.OptionId = "executor.donate_museum_item"; break;
            case "receipt-after": receipt.AfterStateHash = "wrong-after"; break;
            case "recorded-missing": receipt.EffectiveQueueItem = null; break;
            case "recorded-shape": receipt.EffectiveQueueItem = JsonSerializer.SerializeToElement("not-an-item"); break;
            case "recorded-command":
                var recorded = JsonSerializer.Deserialize<ActionQueueItem>(JsonSerializer.Serialize(item))!;
                recorded.NormalizedCommand.Actor.ActorId = "foreign-actor";
                receipt.EffectiveQueueItem = JsonSerializer.SerializeToElement(recorded);
                break;
            case "after-slots":
                var farm = System.Text.Json.Nodes.JsonNode.Parse(attempt.After.State["farm"].GetRawText())!;
                farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![1]!["slots"]![0]!["stack"] = 4;
                attempt.After.State["farm"] = JsonSerializer.SerializeToElement(farm);
                attempt.After.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(attempt.After.State);
                receipt.AfterStateHash = attempt.After.StateHash;
                break;
            case "queue-authority":
                item.NormalizedCommand.Actor.ActorId = "foreign-actor";
                break;
            case "before-slots":
            case "before-passive-quality":
                var beforeFarm = System.Text.Json.Nodes.JsonNode.Parse(attempt.Before.State["farm"].GetRawText())!;
                var nodes = beforeFarm["material_inventory_graph"]!["value"]!["inventory_nodes"]!;
                if (mutation == "before-slots") nodes[0]!["slots"]![1]!["stack"] = 3;
                else nodes[1]!["slots"]![0]!["quality"] = 1;
                attempt.Before.State["farm"] = JsonSerializer.SerializeToElement(beforeFarm);
                attempt.Before.StateHash = StardewAI.Contracts.State.SnapshotHash.ComputeStateHash(attempt.Before.State);
                item.NormalizedCommand.StateHash = attempt.Before.StateHash;
                receipt.SourceStateHash = attempt.Before.StateHash;
                receipt.EffectiveQueueItem = JsonSerializer.SerializeToElement(item);
                break;
        }
        var original = JsonSerializer.Serialize(attempt.Fixture.Ledger);
        var result = MuseumOutputCustodyTransferSettlementRequestBuilder.Build(attempt.Compilation.Queue!, item,
            receipt, attempt.Before, attempt.After, attempt.Fixture.Ledger);
        Assert.Null(result.Request);
        Assert.NotEmpty(result.Errors);
        Assert.Equal(original, JsonSerializer.Serialize(attempt.Fixture.Ledger));
    }
}
