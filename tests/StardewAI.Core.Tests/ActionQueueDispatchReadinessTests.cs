using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;

namespace StardewAI.Core.Tests;

public sealed class ActionQueueDispatchReadinessTests
{
    [Fact]
    public void MatchingMaterialLedgerAllowsMachineCraftDispatch()
    {
        var ledger = Ledger(3, StrategyCommitmentStatuses.Active);
        var result = new ActionQueueDispatchReadinessService().Evaluate(
            Queue(Item(3, "[\"keg-wood\"]")),
            Item(3, "[\"keg-wood\"]"),
            ledger,
            "state-current");

        Assert.True(result.Ready, string.Join(";", result.BlockingReasons));
        Assert.Equal("ready", result.Status);
        Assert.Equal(3, result.RequiredLedgerRevision);
        Assert.Equal(3, result.CurrentLedgerRevision);
    }

    [Fact]
    public void ChangedLedgerRejectsMachineCraftBeforeDispatch()
    {
        var item = Item(3, "[\"keg-wood\"]");
        var result = new ActionQueueDispatchReadinessService().Evaluate(
            Queue(item),
            item,
            Ledger(4, StrategyCommitmentStatuses.Cancelled),
            "state-current");

        Assert.False(result.Ready);
        Assert.Contains(
            "dispatch_strategy_ledger_revision_drifted",
            result.BlockingReasons);
        Assert.Contains(
            "dispatch_material_reservation_not_active:keg-wood",
            result.BlockingReasons);
    }

    [Fact]
    public void MissingLedgerBindingFailsClosedForMachineCraft()
    {
        var item = new ActionQueueItem
        {
            QueueItemId = "item-1",
            OptionId = "executor.craft_machine_item",
            Status = "pending"
        };
        var result = new ActionQueueDispatchReadinessService().Evaluate(
            Queue(item),
            item,
            Ledger(0, StrategyCommitmentStatuses.Cancelled),
            "state-current");

        Assert.False(result.Ready);
        Assert.Contains(
            "dispatch_strategy_ledger_binding_incomplete",
            result.BlockingReasons);
        Assert.Contains(
            "dispatch_material_reservation_ids_missing",
            result.BlockingReasons);
    }

    [Fact]
    public void UnrelatedPrimitiveDoesNotRequireStrategyLedgerBinding()
    {
        var item = new ActionQueueItem
        {
            QueueItemId = "item-1",
            OptionId = "executor.move_to_tile",
            Status = "pending"
        };
        var result = new ActionQueueDispatchReadinessService().Evaluate(
            Queue(item),
            item,
            Ledger(8, StrategyCommitmentStatuses.Active),
            "state-current");

        Assert.True(result.Ready);
        Assert.Equal("not_applicable", result.Status);
    }

    private static ActionQueueEnvelope Queue(ActionQueueItem item) => new()
    {
        QueueId = "queue-1",
        StateHash = "state-compiled",
        Items = new[] { item }
    };

    private static ActionQueueItem Item(int revision, string reservationIdsJson) => new()
    {
        QueueItemId = "item-1",
        OptionId = "executor.craft_machine_item",
        Status = "pending",
        NormalizedCommand = new NormalizedCommand
        {
            Parameters = new[]
            {
                Parameter("material_reservation_guard_status", "ready"),
                Parameter("material_reservation_ledger_id", "strategy-ledger:test"),
                Parameter("material_reservation_ledger_revision", revision.ToString()),
                Parameter("material_reservation_ids_json", reservationIdsJson),
                Parameter("commitment_ledger_id", "strategy-ledger:test"),
                Parameter("commitment_ledger_revision", revision.ToString())
            }
        }
    };

    private static StrategyCommitmentLedger Ledger(int revision, string status) => new()
    {
        LedgerId = "strategy-ledger:test",
        Revision = revision,
        MaterialReservations = new[]
        {
            new MaterialReservation
            {
                ReservationId = "keg-wood",
                Status = status
            }
        }
    };

    private static SmallModelActionParameter Parameter(string name, string value) => new()
    {
        Name = name,
        Value = value
    };
}
