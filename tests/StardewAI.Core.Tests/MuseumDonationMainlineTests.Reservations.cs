using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumDonationMainlineTests
{
    [Theory]
    [InlineData("valid", true)]
    [InlineData("missing", false)]
    [InlineData("revision", false)]
    [InlineData("reserved", false)]
    [InlineData("duplicate", false)]
    public void TeacherRechecksManagedDonationCounterfactualsAgainstTheRetainedLedger(string kind, bool expected)
    {
        var snapshot = ReservationSnapshot();
        var sourceLedger = DonationLedger(snapshot);
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { "museum.donate_items" }, true, sourceLedger);
        var candidates = new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability);
        if (kind == "duplicate") candidates[0].Parameters = candidates[0].Parameters.Append(
            new SmallModelActionParameter { Name = "material_reservation_ledger_id", Value = "wrong-extra-ledger" }).ToArray();
        var current = kind == "reserved" ? DonationLedger(snapshot, 2) : DonationLedger(snapshot);
        if (kind == "revision") current.Revision++;
        var method = typeof(CurrentStageOneCollectionTeacherPreferenceBuilder).GetMethod("ValidateManagedMuseumLedger",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        var label = new CurrentStageOneCollectionTeacherPreferenceLabel();
        var ready = (bool)method.Invoke(null, new object?[] { candidates, snapshot, kind == "missing" ? null : current, label })!;
        Assert.Equal(expected, ready);
        if (!ready) Assert.NotEmpty(label.BlockingReasons);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OtherGoalReservedMuseumItemIsExcludedBeforeRanking(bool remote)
    {
        var snapshot = ReservationSnapshot(remote);
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(
            snapshot, new[] { "museum.donate_items" }, true, DonationLedger(snapshot, 2)).Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("museum_donation_item_reserved_or_material_projection_drifted", candidate.BlockReasons);
    }

    [Fact]
    public void AReservedStackCanDonateOneSpareWithoutChangingTheReservation()
    {
        var snapshot = ReservationSnapshot();
        var ledger = DonationLedger(snapshot, 1);
        var before = JsonSerializer.Serialize(ledger);
        var plan = DonationPlan(snapshot, ledger);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", queue.Status);
        AssertParameter(item.NormalizedCommand.Parameters, "expected_stack_after", "1");
        AssertParameter(item.NormalizedCommand.Parameters, "material_reservation_ids_json", "[\"protected-quest-item\"]");
        Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, snapshot.StateHash).Ready);
        Assert.Equal(before, JsonSerializer.Serialize(ledger));
    }

    [Fact]
    public void CompilerRejectsAProtectedDonationEvenWhenThePlanWasBuiltWithoutTheReservation()
    {
        var snapshot = ReservationSnapshot();
        var plan = DonationPlan(snapshot, DonationLedger(snapshot));
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, DonationLedger(snapshot, 2));
        Assert.Equal("blocked", queue.Status);
        Assert.Contains("museum_donation_item_reserved_or_material_projection_drifted", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void CompilerRebuildsLedgerBindingRatherThanTrustingPlanParameters()
    {
        var snapshot = ReservationSnapshot();
        var ledger = DonationLedger(snapshot);
        var plan = DonationPlan(snapshot, ledger);
        foreach (var parameter in Assert.Single(plan.Steps).Parameters)
        {
            if (parameter.Name == "material_reservation_ledger_id") parameter.Value = "forged-ledger";
            if (parameter.Name == "material_reservation_ledger_revision") parameter.Value = "999";
        }
        var queue = new ActionQueueCompiler().Compile(plan, snapshot, ledger);
        Assert.Equal("pending", queue.Status);
        AssertParameter(Assert.Single(queue.Items).NormalizedCommand.Parameters, "material_reservation_ledger_id", ledger.LedgerId);
        AssertParameter(queue.Items[0].NormalizedCommand.Parameters, "material_reservation_ledger_revision", "0");
    }

    [Theory]
    [InlineData("revision", "dispatch_strategy_ledger_revision_drifted")]
    [InlineData("actor", "dispatch_museum_donation_actor_identity_drifted")]
    [InlineData("state", "dispatch_museum_donation_state_hash_drifted")]
    [InlineData("binding", "dispatch_strategy_ledger_binding_incomplete")]
    public void DonationDispatchRechecksTheCurrentLedgerAndSnapshot(string mutation, string reason)
    {
        var snapshot = ReservationSnapshot();
        var ledger = DonationLedger(snapshot);
        var queue = new ActionQueueCompiler().Compile(DonationPlan(snapshot, ledger), snapshot, ledger);
        var item = Assert.Single(queue.Items);
        var currentState = snapshot.StateHash;
        if (mutation == "revision") ledger.Revision++;
        if (mutation == "actor") ledger.PlayerId = "another-player";
        if (mutation == "state") currentState = "new-state";
        if (mutation == "binding") item.NormalizedCommand.Parameters = item.NormalizedCommand.Parameters
            .Where(parameter => parameter.Name != "material_reservation_ledger_id").ToArray();
        var result = new ActionQueueDispatchReadinessService().Evaluate(queue, item, ledger, currentState);
        Assert.False(result.Ready);
        Assert.Contains(reason, result.BlockingReasons);
    }

    [Fact]
    public void ManagedPlanCannotBeCompiledWithoutItsLedger()
    {
        var snapshot = ReservationSnapshot();
        var plan = DonationPlan(snapshot, DonationLedger(snapshot));
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        Assert.Equal("blocked", queue.Status);
        Assert.Contains("museum_donation_strategy_ledger_unavailable", Assert.Single(queue.Items).BlockingReasons);
    }

    [Fact]
    public void LegacyCalibrationQueueDoesNotMasqueradeAsManagedDispatch()
    {
        var snapshot = ReservationSnapshot();
        var queue = new ActionQueueCompiler().Compile(DonationPlan(snapshot, null), snapshot);
        Assert.Equal("pending", queue.Status);
        var readiness = new ActionQueueDispatchReadinessService().Evaluate(queue, Assert.Single(queue.Items),
            DonationLedger(snapshot), snapshot.StateHash);
        Assert.False(readiness.Ready);
        Assert.Contains("dispatch_strategy_ledger_binding_incomplete", readiness.BlockingReasons);
    }

    [Fact]
    public void ActiveReservationsRequireAnAvailableMaterialGraph()
    {
        var snapshot = ReservationSnapshot();
        snapshot.State.Remove("farm");
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { "museum.donate_items" }, true, DonationLedger(snapshot, 1)).Options).EventCandidates);
        Assert.False(candidate.Available);
        Assert.Contains("museum_donation_material_inventory_graph_unavailable_or_wrong_actor", candidate.BlockReasons);
    }

    private static SmallModelPlanEnvelope DonationPlan(SnapshotEnvelope snapshot, StrategyCommitmentLedger? ledger)
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot,
            new[] { "museum.donate_items" }, true, ledger);
        return new DailyPlanCompiler().Compile(new EventCandidateRanker().Rank(new BaselineTrainingReport(), availability), snapshot.StateHash);
    }

    private static StrategyCommitmentLedger DonationLedger(SnapshotEnvelope snapshot, int reserved = 0) => new()
    {
        LedgerId = "museum-test-ledger", SaveId = "MuseumFarm", PlayerId = "123",
        SourceStateHash = snapshot.StateHash,
        MaterialReservations = reserved == 0 ? Array.Empty<MaterialReservation>() : new[] { new MaterialReservation
        {
            ReservationId = "protected-quest-item", Revision = 1, OwnerPlayerId = 123,
            SourceStateHash = snapshot.StateHash, SourceDecisionId = "quest-protection",
            GoalId = "quest.collect", NodeId = "player:123", SlotIndex = 0,
            QualifiedItemId = "(O)96", Quantity = reserved, Purpose = "quest"
        } }
    };

    private static SnapshotEnvelope ReservationSnapshot(bool remote = false)
    {
        var snapshot = remote ? RemoteSnapshot(Snapshot(59, 95)) : Snapshot(59, 95);
        snapshot.SaveId = new() { Value = "MuseumFarm", Status = FieldStatus.Available };
        snapshot.PlayerId = new() { Value = "123", Status = FieldStatus.Available };
        snapshot.State["identity"] = JsonSerializer.SerializeToElement(new
        {
            save_id = new { value = "MuseumFarm", status = "available" },
            player_id = new { value = "123", status = "available" }
        });
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new
        {
            material_inventory_graph = new { status = "available", value = new
            {
                schema_version = "material_inventory_graph.v1", status = "available", player_id = 123,
                inventory_nodes = new[] { new
                {
                    node_id = "player:123", inventory_kind = "player_inventory", supply_state = "available",
                    owner_player_id = 123, ownership_class = "actor_owned", actor_use_authorized = true,
                    slots = new[] { new { slot_index = 0, item_id = "96", qualified_item_id = "(O)96", stack = 2 } }
                } }
            } }
        });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
