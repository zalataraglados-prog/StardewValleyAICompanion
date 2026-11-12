using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed partial class CropTeacherSeedPurchaseSettlementTests
{
    [Fact]
    public void TerminalOutcome_OriginalApproachCompilerNeutralBoundaryAndRebindDoNotIncreaseCursor()
    {
        using var f = new TerminalOutcomeFixture(approach: true);
        var preparation = f.Prepare();
        Assert.Same(f.Target, preparation.Target);
        Assert.Equal("shipping_approach", preparation.Kind);
        Assert.Equal("executor.move_to_tile", Assert.Single(f.Queue.Items).OptionId);
        var outcome = preparation.VerifyTransition(f.Transition);
        Assert.Same(preparation.Target, outcome.Target);
        Assert.Equal(0, outcome.AcceptedDepositCountAfter); Assert.Equal(0L, outcome.PendingValueAfter);
        Assert.False(outcome.TerminalSettlementVerified); Assert.False(outcome.FormalProductTrainingAuthorized);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Shared.Source.Ledger, f.After, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        AssertOtherRootsUnchanged(f.Shared.Source.Ledger, outcome.VerifyRebindReply(reply));
        Assert.Contains(Path.GetFullPath(f.InitialPath), outcome.InputHashes.Keys);
        outcome.ValidateInputsUnchanged();
    }

    [Fact]
    public void TerminalOutcome_OriginalSingleDepositIncreasesOnlyVerifiedCursorAndActualPendingValue()
    {
        using var f = new TerminalOutcomeFixture();
        var preparation = f.Prepare();
        Assert.Equal("shipping_deposit", preparation.Kind);
        Assert.Equal("executor.ship_inventory_item_to_bin", Assert.Single(f.Queue.Items).OptionId);
        var outcome = preparation.VerifyTransition(f.Transition);
        Assert.Same(f.Target, outcome.Target);
        Assert.Equal(1, outcome.AcceptedDepositCountAfter); Assert.Equal(50L, outcome.PendingValueAfter);
        Assert.False(outcome.TerminalSettlementVerified); Assert.False(outcome.FormalProductTrainingAuthorized);
        var reply = new ReservationPortfolioLedgerService().Commit(f.Shared.Source.Ledger, f.After, outcome.RebindRequest, RecordedAt);
        Assert.True(reply.Accepted, string.Join(",", reply.Errors));
        AssertOtherRootsUnchanged(f.Shared.Source.Ledger, outcome.VerifyRebindReply(reply));
        reply.Ledger!.CurrencyReservations.Single(row => row.ReservationId == "other-money").Amount--;
        Assert.Throws<InvalidDataException>(() => outcome.VerifyRebindReply(reply));
    }

    [Fact]
    public void TerminalOutcome_SecondDepositConsumesOriginalInitialCertificateWithoutRebuildingLifecycle()
    {
        using var f = new TerminalOutcomeFixture(cursor: 1);
        var preparation = f.Prepare();
        Assert.EndsWith(":deposit:2", preparation.CandidateId);
        var outcome = preparation.VerifyTransition(f.Transition);
        Assert.Equal(2, outcome.AcceptedDepositCountAfter); Assert.Equal(100L, outcome.PendingValueAfter);
        Assert.Equal(3, outcome.Target.InitialQuantity);
        Assert.False(outcome.TerminalSettlementVerified);
    }

    [Fact]
    public void TerminalOutcome_RejectsUnpinnedTargetHashQuantityActorAndVariant()
    {
        using var f = new TerminalOutcomeFixture();
        var t = f.Target;
        foreach (var bad in new[] { t with { RetentionProofSha256 = new string('0', 64) }, t with { RequiredQuantity = 1 },
            t with { InitialQuantity = 3 }, t with { PlayerId = "foreign" }, t with { SaveId = "foreign" },
            t with { AcceptedDepositCount = 1 }, t with { AcceptedDepositCount = -1 }, t with { Quality = 4 },
            t with { UnitPrice = 51 }, t with { StandTileX = 3 } })
            Assert.Throws<InvalidDataException>(() => f.Prepare(bad));
    }

    [Fact]
    public void TerminalOutcome_RejectsEntireModelDriftAndQueueNotReplayedByOriginalCompiler()
    {
        using var f = new TerminalOutcomeFixture();
        var altered = JsonSerializer.Deserialize<SmallModelActionEnvelope>(JsonSerializer.Serialize(f.Model, JsonOptions), JsonOptions)!;
        altered.Actions = altered.Actions.Concat(altered.Actions).ToArray(); Fixture.Save(f.ModelPath, altered);
        Assert.Throws<InvalidDataException>(() => f.Prepare());
        altered.Actions = f.Model.Actions; altered.SourceModel = "foreign"; Fixture.Save(f.ModelPath, altered);
        Assert.Throws<InvalidDataException>(() => f.Prepare());
        Fixture.Save(f.ModelPath, f.Model);
        f.Queue.Items[0].NormalizedCommand.Parameters.Single(row => row.Name == "quantity").Value = "2";
        Fixture.Save(f.QueuePath, f.Queue);
        Assert.Throws<InvalidDataException>(() => f.Prepare());
    }

    [Fact]
    public void TerminalOutcome_RejectsForeignKindDayCandidateSourceQueueAndUnknownNativeReceipt()
    {
        using var f = new TerminalOutcomeFixture(); var preparation = f.Prepare(); var t = f.Transition;
        foreach (var bad in new[] { t with { TransitionKind = "shipping_approach" }, t with { TotalDay = t.TotalDay + 1 },
            t with { SelectedCandidateId = "foreign" }, t with { QueuePath = f.ModelPath }, t with { BeforeSnapshotPath = f.AfterPath },
            t with { RunId = "foreign" }, t with { ExecutorVersion = "foreign" } })
            Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(bad));
        var receipt = JsonNode.Parse(File.ReadAllText(f.ReceiptPath))!;
        receipt["step_results"]![0]!["status"] = "unknown"; Fixture.Save(f.ReceiptPath, receipt);
        Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(t));
    }

    [Fact]
    public void TerminalOutcome_RejectsCurrentPendingAndSourceActorDrift()
    {
        using var f = new TerminalOutcomeFixture();
        ChangeState(f.Before, "farm", node => node["shipping_bins"]!["value"]![0]!["pending_settlement_value"] = 50);
        Fixture.Save(f.Shared.Source.SourcePath, f.Before);
        Assert.Throws<InvalidDataException>(() => f.Prepare());
        f.Before.PlayerId.Value = "foreign"; Fixture.Save(f.Shared.Source.SourcePath, f.Before);
        Assert.Throws<InvalidDataException>(() => f.Prepare());
    }

    [Fact]
    public void TerminalOutcome_RejectsUnrelatedResourceChangeAndRetainedInputTampering()
    {
        using var f = new TerminalOutcomeFixture(); var preparation = f.Prepare();
        ChangeState(f.After, "player", node => node["money"]!["value"] = node["money"]!["value"]!.GetValue<int>() + 1);
        f.SaveNative();
        Assert.Throws<InvalidDataException>(() => preparation.VerifyTransition(f.Transition));
        File.AppendAllText(f.InitialPath, " ");
        Assert.Throws<InvalidDataException>(() => preparation.ValidateInputsUnchanged());
    }
}
