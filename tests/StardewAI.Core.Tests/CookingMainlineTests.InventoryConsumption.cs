using System.Text.Json;
using System.Reflection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CookingMainlineTests
{
    [Theory]
    [InlineData("built", 1)]
    [InlineData("mini", 1)]
    [InlineData("built", 2)]
    public void PreparedCookingSettlesFreeUnitsAndPassiveForeignPositionsThroughOriginalLedger(string storage, int eggs)
    {
        var (before, ledger, request) = ConsumptionSource(storage, eggs);
        var original = JsonSerializer.Serialize(ledger);
        var service = new ReservationPortfolioLedgerService();
        var commit = service.Commit(ledger, before, request, "declared-before");
        Assert.True(commit.Accepted, string.Join(",", commit.Errors));
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
        var preparedLedger = Assert.IsType<StrategyCommitmentLedger>(commit.Ledger);
        var prepared = Assert.IsType<PreparedInventoryConsumption>(preparedLedger.PreparedInventoryConsumption);
        Assert.Equal(2, prepared.BeforeInventoryGraph.InventoryNodes.Length);
        Assert.Null(preparedLedger.PreparedMaterialTransfer);
        Assert.True(PreparedInventoryConsumptionContract.IsBound(preparedLedger, prepared.BindingSha256, request.PortfolioId,
            request.GoalId, request.InventoryConsumptionPreparation!.RouteSourceDecisionId));
        var queue = CompilePreparedCooking(before, preparedLedger);
        var item = Assert.Single(queue.Items);
        Assert.Equal("pending", item.Status);
        Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, item, preparedLedger, before.StateHash).Ready);

        // Declared physical after graph, not a claim that a live game or native receipt was produced.
        var after = ConsumptionAfter(before, eggs);
        var settle = service.SettleSupportingTransition(preparedLedger, after, ConsumptionSettlement(after, preparedLedger), "declared-after");
        Assert.True(settle.Accepted, string.Join(",", settle.Errors));
        Assert.Equal(preparedLedger.Revision + 1, settle.Ledger!.Revision);
        Assert.Null(settle.Ledger.PreparedInventoryConsumption);
        Assert.Null(settle.MaterialTransferReservations);
        Assert.Equal(0, settle.ConsumedQuantity); // No other goal's claim was consumed.
        Assert.Empty(settle.MaterialRelocations);
        Assert.Equal(eggs == 1 ? 2 : 0, settle.ReboundActiveReservationIds.Length);
        foreach (var prior in ledger.MaterialReservations)
        {
            var current = Assert.Single(settle.Ledger.MaterialReservations, row => row.ReservationId == prior.ReservationId);
            Assert.Equal(prior.GoalId, current.GoalId); Assert.Equal(prior.SourceDecisionId, current.SourceDecisionId);
            Assert.Equal(prior.Purpose, current.Purpose); Assert.Equal(prior.Quantity, current.Quantity);
            Assert.Equal(prior.OwnerPlayerId, current.OwnerPlayerId); Assert.Equal(prior.NodeId, current.NodeId);
            Assert.Equal(eggs == 1 && prior.NodeId.StartsWith("chest:FarmHouse:") ? prior.SlotIndex == 2 ? 0 : 1 : prior.SlotIndex, current.SlotIndex);
            Assert.Equal(prior.Revision + (eggs == 1 && prior.NodeId.StartsWith("chest:FarmHouse:") ? 1 : 0), current.Revision);
        }
        Assert.NotNull(preparedLedger.PreparedInventoryConsumption);
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(queue, item, settle.Ledger, after.StateHash).Ready);
        Assert.False(service.SettleSupportingTransition(settle.Ledger, after, ConsumptionSettlement(after, preparedLedger), "duplicate").Accepted);
    }

    [Theory]
    [InlineData("consumer")]
    [InlineData("schema")]
    [InlineData("reserved_ingredient")]
    [InlineData("duplicate_slot")]
    [InlineData("foreign_owner")]
    public void CookingPreparationCannotInventOrConsumeAnotherGoalsMaterials(string fault)
    {
        var (snapshot, ledger, request) = ConsumptionSource("built", 1);
        if (fault == "consumer") request.InventoryConsumptionPreparation!.ConsumerOptionId = "executor.transfer_material";
        if (fault == "schema") request.InventoryConsumptionPreparation!.SchemaVersion = "unknown";
        if (fault == "reserved_ingredient") { ledger.MaterialReservations[0].SlotIndex = 0; ledger.MaterialReservations[0].QualifiedItemId = "(O)176"; }
        if (fault == "foreign_owner") ledger.MaterialReservations[1].OwnerPlayerId = 2;
        if (fault == "duplicate_slot")
        {
            Assert.True(MaterialInventoryGraphReader.TryRead(snapshot, out var graph));
            graph!.InventoryNodes[1].Slots = graph.InventoryNodes[1].Slots.Append(CookingClone(graph.InventoryNodes[1].Slots[0])).ToArray();
            SetConsumptionGraph(snapshot, graph); ReanchorCooking(snapshot, ledger); request.StateHash = snapshot.StateHash;
        }
        var original = JsonSerializer.Serialize(ledger);
        var result = new ReservationPortfolioLedgerService().Commit(ledger, snapshot, request, "invalid");
        Assert.False(result.Accepted); Assert.NotEmpty(result.Errors);
        Assert.Equal(original, JsonSerializer.Serialize(ledger));
    }

    [Theory]
    [InlineData("missing_binding")]
    [InlineData("wrong_goal")]
    [InlineData("wrong_route")]
    [InlineData("wrong_consumer")]
    [InlineData("receipt")]
    [InlineData("revision")]
    [InlineData("mixed_claim")]
    [InlineData("legacy_claim")]
    [InlineData("extra_output")]
    [InlineData("old_slots")]
    [InlineData("quality")]
    [InlineData("locked")]
    [InlineData("tampered_preparation")]
    [InlineData("rehashed_projection")]
    [InlineData("rehashed_consumption")]
    [InlineData("missing_history")]
    public void CookingSettlementRejectsDriftWithoutPublishingPassiveChanges(string fault)
    {
        var (before, ledger, preparation) = ConsumptionSource("built", 1);
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(ledger, before, preparation, "before");
        Assert.True(committed.Accepted, string.Join(",", committed.Errors));
        var current = committed.Ledger!;
        var after = ConsumptionAfter(before, 1);
        var request = ConsumptionSettlement(after, current);
        if (fault == "missing_binding") request.PreparedInventoryConsumptionSha256 = null;
        if (fault == "wrong_goal") request.GoalId = "foreign";
        if (fault == "wrong_route") request.RouteSourceDecisionId = "foreign";
        if (fault == "wrong_consumer") request.ConsumerOptionId = "executor.transfer_material";
        if (fault == "receipt") request.SupportingTransitionReceiptSha256 = "self-reported-success";
        if (fault == "revision") request.ExpectedLedgerRevision++;
        if (fault == "mixed_claim") request.MaterialConsumptions = [new() { MaterialReservationId = ledger.MaterialReservations[0].ReservationId, ConsumedQuantity = 1 }];
        if (fault == "legacy_claim") request.ConsumedQuantity = 1;
        if (fault == "tampered_preparation") current.PreparedInventoryConsumption!.Projection.PassiveReservationTransitions[0].DestinationSlotIndex = 9;
        if (fault == "missing_history") current.History = current.History.Where(row => row.Operation != "inventory_consumption_prepared").ToArray();
        if (fault is "rehashed_projection" or "rehashed_consumption")
        {
            if (fault == "rehashed_projection") current.PreparedInventoryConsumption!.Projection.PassiveReservationTransitions[0].DestinationSlotIndex = 9;
            else current.PreparedInventoryConsumption!.Consumptions = current.PreparedInventoryConsumption.Consumptions.Skip(1).ToArray();
            var hash = PreparedInventoryConsumptionContract.ComputeBindingSha256(current.PreparedInventoryConsumption!);
            current.PreparedInventoryConsumption!.BindingSha256 = hash;
            current.History.Single(row => row.Operation == "inventory_consumption_prepared").Reason = hash;
            request.PreparedInventoryConsumptionSha256 = hash;
        }
        if (fault is "extra_output" or "old_slots" or "quality" or "locked")
        {
            Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
            if (fault == "extra_output") graph!.InventoryNodes[0].Slots[0].Stack++;
            if (fault == "old_slots") graph!.InventoryNodes[1].Slots[0].SlotIndex = 2;
            if (fault == "quality") graph!.InventoryNodes[1].Slots[0].Quality++;
            if (fault == "locked") graph!.AccessPoints[0].LockedByOtherPlayer = true;
            SetConsumptionGraph(after, graph!); after.StateHash = SnapshotHash.ComputeStateHash(after.State); request.StateHash = after.StateHash;
        }
        var original = JsonSerializer.Serialize(current);
        var result = service.SettleSupportingTransition(current, after, request, "invalid");
        Assert.False(result.Accepted); Assert.NotEmpty(result.Errors);
        Assert.Equal(original, JsonSerializer.Serialize(current));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("duplicate")]
    [InlineData("unknown")]
    [InlineData("foreign_control")]
    [InlineData("changed_source")]
    public void PreparedCookingCannotLoseItsBindingDuringFreshCompileOrDispatch(string fault)
    {
        var (snapshot, ledger, request) = ConsumptionSource("built", 1);
        var result = new ReservationPortfolioLedgerService().Commit(ledger, snapshot, request, "before");
        Assert.True(result.Accepted, string.Join(",", result.Errors));
        var prepared = result.Ledger!;
        var parameters = PreparedCookingConsumptionBinding.ToParameters(prepared);
        if (fault == "missing") parameters = [];
        if (fault == "duplicate") parameters = parameters.Concat(parameters).ToArray();
        if (fault == "unknown") parameters = [P("prepared_inventory_consumption_unknown", "1")];
        if (fault == "foreign_control") parameters = parameters.Append(P(PreparedMaterialTransferContract.BindingParameter, new string('b', 64))).ToArray();
        if (fault == "changed_source")
        {
            Assert.True(MaterialInventoryGraphReader.TryRead(snapshot, out var graph)); graph!.InventoryNodes[1].Slots[1].Quality++;
            SetConsumptionGraph(snapshot, graph); snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        var queue = CompilePreparedCooking(snapshot, prepared, parameters);
        var item = Assert.Single(queue.Items);
        Assert.Equal("blocked", item.Status);
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(queue, item, prepared, snapshot.StateHash).Ready);
    }

    [Fact]
    public void InventoryConsumptionProtocolKeepsLegacyNullJsonAndRejectsPendingOrMixedCommit()
    {
        Assert.DoesNotContain("inventory_consumption", JsonSerializer.Serialize(new StrategyCommitmentLedger()));
        Assert.DoesNotContain("inventory_consumption", JsonSerializer.Serialize(new ReservationPortfolioCommitRequest()));
        Assert.DoesNotContain("inventory_consumption", JsonSerializer.Serialize(new ReservationPortfolioSupportingTransitionSettlementRequest()));
        var (snapshot, ledger, request) = ConsumptionSource("built", 1);
        var service = new ReservationPortfolioLedgerService();
        var committed = service.Commit(ledger, snapshot, request, "before"); Assert.True(committed.Accepted);
        var repeat = CookingClone(request); repeat.ExpectedLedgerRevision = committed.Ledger!.Revision;
        Assert.Contains("inventory_consumption_preparation_already_pending", service.Commit(committed.Ledger, snapshot, repeat, "repeat").Errors);
        var mixed = CookingClone(request); mixed.ReleaseReservationIds = [ledger.MaterialReservations[0].ReservationId];
        Assert.Contains("inventory_consumption_preparation_mixed_mutation", service.Commit(ledger, snapshot, mixed, "mixed").Errors);
    }

    private static T CookingClone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;

    [Theory]
    [InlineData("executor.close_menu")]
    [InlineData("executor.move_to")]
    public void PreparedCookingDoesNotTakeOverUnrelatedCompilerOrDispatchDefaults(string option)
    {
        var (snapshot, ledger, request) = ConsumptionSource("built", 1);
        var prepared = new ReservationPortfolioLedgerService().Commit(ledger, snapshot, request, "before"); Assert.True(prepared.Accepted);
        var model = new SmallModelActionEnvelope
        {
            ModelOutputId = "declared-control-default", SourceModel = "test", StateHash = snapshot.StateHash, GoalId = "test",
            ExecutionMode = "training_singleplayer", Actor = new() { ActorId = "training_farmer.test", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
            Actions = [new() { ActionId = "control", OptionId = option, Parameters = [] }]
        };
        var original = new ActionQueueCompiler().Compile(model, snapshot);
        var actual = new ActionQueueCompiler().Compile(model, snapshot, prepared.Ledger);
        var comparer = typeof(CropTeacherSharedSourceReturn).Assembly.GetType("StardewAI.GoalConditionedBootstrap.CropTeacherOriginalQueueCompilation", true)!;
        comparer.GetMethod("Verify", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [original, actual, "Unrelated control default"]);
        var item = new ActionQueueItem { OptionId = option, Status = "pending", NormalizedCommand = new() { OptionId = option, StateHash = snapshot.StateHash } };
        var readiness = new ActionQueueDispatchReadinessService().Evaluate(new(), item, prepared.Ledger!, snapshot.StateHash);
        Assert.True(readiness.Ready); Assert.Equal("not_applicable", readiness.Status);
        item.NormalizedCommand.Parameters = PreparedCookingConsumptionBinding.ToParameters(prepared.Ledger!);
        Assert.False(new ActionQueueDispatchReadinessService().Evaluate(new(), item, prepared.Ledger!, snapshot.StateHash).Ready);
    }

    [Fact]
    public void PreparedCookingRetainsPlayerHolesAndAcceptsNativeOutputStackingWithoutPredictingItsSlot()
    {
        var (before, ledger, request) = ConsumptionSource("built", 1);
        Assert.True(MaterialInventoryGraphReader.TryRead(before, out var graph));
        graph!.InventoryNodes[0].Slots = graph.InventoryNodes[0].Slots.Concat(new MaterialInventorySlot[]
        {
            new() { SlotIndex = 7, QualifiedItemId = "(O)388", Stack = 3, RuntimeType = "StardewValley.Object" },
            new() { SlotIndex = 10, QualifiedItemId = "(O)194", Stack = 2, Quality = 2, RuntimeType = "StardewValley.Object" }
        }).ToArray();
        SetConsumptionGraph(before, graph); ReanchorCooking(before, ledger); request.StateHash = before.StateHash;
        var service = new ReservationPortfolioLedgerService();
        var prepared = service.Commit(ledger, before, request, "before"); Assert.True(prepared.Accepted, string.Join(",", prepared.Errors));
        var after = ConsumptionAfter(before, 1);
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out graph));
        graph!.InventoryNodes[0].Slots = [new() { SlotIndex = 7, QualifiedItemId = "(O)388", Stack = 3, RuntimeType = "StardewValley.Object" },
            new() { SlotIndex = 10, QualifiedItemId = "(O)194", Stack = 3, Quality = 2, RuntimeType = "StardewValley.Object" }];
        SetConsumptionGraph(after, graph); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        var settled = service.SettleSupportingTransition(prepared.Ledger, after, ConsumptionSettlement(after, prepared.Ledger), "after");
        Assert.True(settled.Accepted, string.Join(",", settled.Errors));
    }

    [Fact]
    public void PreparedCookingFreshSourceMayAdvanceWithoutChangingThePreparedRecipeOrInventories()
    {
        var (before, ledger, request) = ConsumptionSource("built", 1);
        var prepared = new ReservationPortfolioLedgerService().Commit(ledger, before, request, "before"); Assert.True(prepared.Accepted);
        var fresh = CookingClone(before); fresh.GameTick++;
        fresh.State["time"] = JsonSerializer.SerializeToElement(new { time_of_day = new { value = 650, status = "available" } });
        fresh.StateHash = SnapshotHash.ComputeStateHash(fresh.State);
        Assert.NotEqual(before.StateHash, fresh.StateHash);
        var queue = CompilePreparedCooking(fresh, prepared.Ledger!); var item = Assert.Single(queue.Items);
        Assert.Equal("pending", item.Status);
        Assert.True(new ActionQueueDispatchReadinessService().Evaluate(queue, item, prepared.Ledger!, fresh.StateHash).Ready);
        Assert.Equal(before.StateHash, prepared.Ledger!.SourceStateHash);
    }

    private static ActionQueueEnvelope CompilePreparedCooking(SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger,
        SmallModelActionParameter[]? binding = null)
    {
        var availability = new CandidateOptionAvailabilityEvaluator().Evaluate(snapshot, [Intent()], true);
        var candidate = Assert.Single(Assert.Single(availability.Options).EventCandidates);
        return new ActionQueueCompiler().Compile(new SmallModelActionEnvelope
        {
            ModelOutputId = "declared-prepared-cooking", SourceModel = "test", StateHash = snapshot.StateHash, GoalId = "test",
            ExecutionMode = "training_singleplayer", Actor = new() { ActorId = "training_farmer.test", ActorType = "training_farmer", ControlSurface = "training_sandbox" },
            Actions = [new() { ActionId = "cook", OptionId = "executor.cook_recipe", Rationale = "declared protocol test",
                Parameters = candidate.Parameters.Concat(binding ?? PreparedCookingConsumptionBinding.ToParameters(ledger)).ToArray() }]
        }, snapshot, ledger);
    }

    private static (SnapshotEnvelope Snapshot, StrategyCommitmentLedger Ledger, ReservationPortfolioCommitRequest Request)
        ConsumptionSource(string storage, int eggs)
    {
        var (snapshot, ledger) = CookingSaleStockSource(storage, eggs);
        Assert.True(MaterialInventoryGraphReader.TryRead(snapshot, out var graph));
        MaterialInventorySlot Slot(int index, string item, int quantity) => new()
        { SlotIndex = index, QualifiedItemId = item, Stack = quantity, RuntimeType = "StardewValley.Object", MaximumStackSize = 999 };
        var fridge = graph!.InventoryNodes[1];
        fridge.Slots = fridge.Slots.Concat([Slot(2, "(O)388", 2), Slot(4, "(O)390", 2)]).ToArray();
        graph.InventoryNodes = graph.InventoryNodes.Append(new MaterialInventoryNode
        { NodeId = "chest:Farm:1,1", InventoryKind = "chest", ActorUseAuthorized = true, SupplyState = "available", OwnerPlayerId = 1, Slots = [Slot(0, "(O)382", 3)] }).ToArray();
        var stock = ledger.MaterialReservations[0]; stock.SlotIndex = 2; stock.QualifiedItemId = "(O)388";
        stock.Purpose = CropSaleStockReservationContract.TransferablePurpose("declared-crop", "declared-wave",
            MoneyRevenueCropRetentionAccount.InventoryIdentity(JsonSerializer.SerializeToElement(new { qualified_item_id = "(O)388", quality = 0 })), 0);
        ledger.MaterialReservations = [stock, new() { ReservationId = "foreign-stone", SourceDecisionId = "foreign-root", GoalId = "skull_key",
            OwnerPlayerId = 1, NodeId = fridge.NodeId, SlotIndex = 4, QualifiedItemId = "(O)390", Quantity = 1, Status = "active" },
            new() { ReservationId = "remote-coal", SourceDecisionId = "remote-root", GoalId = "craft", OwnerPlayerId = 1,
                NodeId = "chest:Farm:1,1", SlotIndex = 0, QualifiedItemId = "(O)382", Quantity = 1, Status = "active" }];
        ledger.Revision = 4;
        SetConsumptionGraph(snapshot, graph); ReanchorCooking(snapshot, ledger);
        foreach (var claim in ledger.MaterialReservations) { claim.Revision = 1; claim.SourceStateHash = snapshot.StateHash; }
        return (snapshot, ledger, new()
        {
            StateHash = snapshot.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = "declared-cooking-operation", SourceDecisionId = "declared-cooking-operation", GoalId = "test",
            InventoryConsumptionPreparation = new() { SchemaVersion = PreparedInventoryConsumptionContract.RequestSchema,
                ConsumerOptionId = PreparedInventoryConsumptionContract.Consumer, RouteSourceDecisionId = "declared-cooking-selection",
                RecipeName = "Fried Egg", CookingSourceId = "kitchen:FarmHouse:5,5" }
        });
    }

    private static void SetConsumptionGraph(SnapshotEnvelope snapshot, MaterialInventoryGraph graph) =>
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(new { material_inventory_graph = new { status = "available", value = graph } });

    private static SnapshotEnvelope ConsumptionAfter(SnapshotEnvelope before, int eggs)
    {
        var after = CookingClone(before); after.GameTick++;
        Assert.True(MaterialInventoryGraphReader.TryRead(after, out var graph));
        graph!.InventoryNodes[0].Slots = [new() { SlotIndex = 0, QualifiedItemId = "(O)194", Stack = 1, Quality = 2, RuntimeType = "StardewValley.Object" }];
        var fridge = graph.InventoryNodes[1];
        if (eggs == 1) { fridge.Slots = fridge.Slots.Skip(1).ToArray(); fridge.Slots[0].SlotIndex = 0; fridge.Slots[1].SlotIndex = 1; }
        else fridge.Slots[0].Stack--;
        SetConsumptionGraph(after, graph); after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static ReservationPortfolioSupportingTransitionSettlementRequest ConsumptionSettlement(SnapshotEnvelope after, StrategyCommitmentLedger ledger)
    {
        var prepared = ledger.PreparedInventoryConsumption!;
        return new() { StateHash = after.StateHash, ExpectedLedgerRevision = ledger.Revision, PortfolioId = prepared.PortfolioId,
            GoalId = prepared.GoalId, RouteSourceDecisionId = prepared.RouteSourceDecisionId, ConsumerOptionId = PreparedInventoryConsumptionContract.Consumer,
            SupportingTransitionReceiptSha256 = new string('a', 64), PreparedInventoryConsumptionSha256 = prepared.BindingSha256, Reason = "declared-native-consumption" };
    }
}
