using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Execution;
using StardewAI.Core.Strategy;

namespace StardewAI.Backend.Tests;

public sealed class RainTotemMaterialReservationPlanBinderTests
{
    [Fact]
    public void BindAtomicallyReservesExactRecipeSlotsAndRebindsPlanLedger()
    {
        var snapshot = Snapshot();
        var authorization = RainTotemMaterialReservationContract.Authorization(
            101,
            "Default");
        var plan = Plan(snapshot, authorization);
        var repository = new MemoryRepository();

        var result = RainTotemMaterialReservationPlanBinder.Bind(
            plan,
            snapshot,
            repository);

        Assert.NotNull(result);
        Assert.True(result!.Accepted, string.Join(";", result.Errors));
        Assert.Equal(3, result.MaterialClaimCount);
        Assert.Equal(1, result.CommittedLedgerRevision);
        var ledger = Assert.IsType<StrategyCommitmentLedger>(result.Ledger);
        Assert.Equal(3, ledger.MaterialReservations.Length);
        Assert.All(ledger.MaterialReservations, reservation =>
        {
            Assert.Equal(authorization.GoalId, reservation.GoalId);
            Assert.Equal(
                authorization.SourceDecisionId,
                reservation.SourceDecisionId);
            Assert.Equal(authorization.Purpose, reservation.Purpose);
            Assert.Equal(
                StrategyCommitmentStatuses.Active,
                reservation.Status);
        });

        var step = Assert.Single(plan.Steps);
        Assert.Equal("ready", Parameter(
            step,
            "material_reservation_guard_status"));
        Assert.Equal(ledger.LedgerId, Parameter(
            step,
            "material_reservation_ledger_id"));
        Assert.Equal("1", Parameter(
            step,
            "material_reservation_ledger_revision"));
        var boundIds = JsonSerializer.Deserialize<string[]>(Parameter(
            step,
            "material_reservation_ids_json"));
        Assert.Equal(
            ledger.MaterialReservations
                .Select(row => row.ReservationId)
                .OrderBy(value => value, StringComparer.Ordinal),
            boundIds);
        var consumptions = JsonSerializer.Deserialize<
            ReservationPortfolioMaterialConsumption[]>(Parameter(
                step,
                "material_reservation_consumptions_json"));
        Assert.NotNull(consumptions);
        Assert.Equal(3, consumptions!.Length);
        Assert.Equal(
            boundIds,
            consumptions.Select(row => row.MaterialReservationId)
                .OrderBy(value => value, StringComparer.Ordinal));

        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue:rain-totem",
            Items =
            [
                new ActionQueueItem
                {
                    QueueItemId = "item:rain-totem",
                    OptionId = "executor.craft_rain_totem",
                    Status = "pending",
                    NormalizedCommand = new NormalizedCommand
                    {
                        Parameters = step.Parameters
                    }
                }
            ]
        };
        var readiness = new ActionQueueDispatchReadinessService().Evaluate(
            queue,
            queue.Items[0],
            ledger,
            snapshot.StateHash);
        Assert.True(
            readiness.Ready,
            string.Join(";", readiness.BlockingReasons));

        var afterSnapshot = Snapshot();
        afterSnapshot.StateHash = "after-native-rain-totem-craft";
        afterSnapshot.GameTick = 2;
        var settlementBuild =
            RainTotemMaterialReservationSettlementRequestBuilder.Build(
                queue.Items[0],
                snapshot.StateHash,
                afterSnapshot.StateHash,
                new string('a', 64));
        Assert.True(
            settlementBuild.Valid,
            string.Join(";", settlementBuild.Errors));
        var settlement =
            repository.SettleReservationPortfolioSupportingTransition(
                afterSnapshot,
                settlementBuild.Request!);
        Assert.True(
            settlement.Accepted,
            string.Join(";", settlement.Errors));
        Assert.Equal(3, settlement.CompletedMaterialReservationIds.Length);
        Assert.All(settlement.Ledger!.MaterialReservations, reservation =>
            Assert.Equal(
                StrategyCommitmentStatuses.Completed,
                reservation.Status));
    }

    [Fact]
    public void BindRejectsMissingExactSlotConsumptionPlan()
    {
        var snapshot = Snapshot();
        var authorization = RainTotemMaterialReservationContract.Authorization(
            101,
            "Default");
        var plan = Plan(snapshot, authorization);
        Set(plan.Steps[0], "ingredient_rows_json", "[]");

        var result = RainTotemMaterialReservationPlanBinder.Bind(
            plan,
            snapshot,
            new MemoryRepository());

        Assert.NotNull(result);
        Assert.False(result!.Accepted);
        Assert.Contains(
            "rain_totem_material_consumption_plan_unavailable",
            result.Errors);
    }

    private static SmallModelPlanEnvelope Plan(
        SnapshotEnvelope snapshot,
        MaterialReservationConsumptionAuthorization authorization) => new()
        {
            StateHash = snapshot.StateHash,
            GoalId = authorization.GoalId,
            Steps =
            [
                new SmallModelPlanStep
                {
                    StepId = "step:rain-totem",
                    Kind = "craft_rain_totem",
                    Parameters =
                    [
                        Parameter(
                            "weather_supply_target_total_day",
                            "101"),
                        Parameter(
                            "weather_supply_affected_context_id",
                            "Default"),
                        Parameter(
                            "crafting_source",
                            "native_personal_crafting_menu"),
                        Parameter(
                            "ingredient_rows_json",
                            IngredientRowsJson),
                        Parameter(
                            "material_reservation_goal_id",
                            authorization.GoalId),
                        Parameter(
                            "material_reservation_source_decision_id",
                            authorization.SourceDecisionId),
                        Parameter(
                            "material_reservation_purpose",
                            authorization.Purpose),
                        Parameter(
                            "material_reservation_portfolio_id",
                            RainTotemMaterialReservationContract.PortfolioId(
                                101,
                                "Default")),
                        Parameter("commitment_ledger_id", string.Empty),
                        Parameter("commitment_ledger_revision", "0"),
                        Parameter(
                            "material_reservation_ledger_id",
                            string.Empty),
                        Parameter(
                            "material_reservation_ledger_revision",
                            "0"),
                        Parameter(
                            "material_reservation_guard_status",
                            "ready_no_active_material_reservations"),
                        Parameter("material_reservation_ids_json", "[]"),
                        Parameter(
                            "material_reservation_consumptions_json",
                            "[]")
                    ]
                }
            ]
        };

    private static SnapshotEnvelope Snapshot()
    {
        var state = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(
            """
            {
              "farm":{
                "material_inventory_graph":{"value":{
                  "schema_version":"material_inventory_graph.v1",
                  "status":"available",
                  "player_id":123,
                  "inventory_nodes":[{
                    "node_id":"player:123",
                    "inventory_kind":"player_inventory",
                    "supply_state":"available",
                    "owner_player_id":123,
                    "actor_use_authorized":true,
                    "capacity":12,
                    "slots":[
                      {"slot_index":0,"item_id":"709","qualified_item_id":"(O)709","stack":1,"maximum_stack_size":999,"quality":0},
                      {"slot_index":1,"item_id":"432","qualified_item_id":"(O)432","stack":1,"maximum_stack_size":999,"quality":0},
                      {"slot_index":2,"item_id":"726","qualified_item_id":"(O)726","stack":5,"maximum_stack_size":999,"quality":0}
                    ]
                  }],
                  "access_points":[],
                  "workbench_links":[],
                  "quantity_rows":[]
                },"status":"available"}
              },
              "identity":{"player_id":{"value":"123","status":"available"}}
            }
            """)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            StateHash = SnapshotHash.ComputeStateHash(state),
            SaveId = new FieldEnvelope<string?> { Value = "save:rain-totem" },
            PlayerId = new FieldEnvelope<string?> { Value = "123" },
            GameTick = 1,
            RealTimestamp = "2026-10-23T00:00:00Z",
            Completeness = "complete",
            State = state
        };
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new() { Name = name, Value = value };

    private static string Parameter(
        SmallModelPlanStep step,
        string name) => step.Parameters.Single(row =>
            row.Name == name).Value;

    private static void Set(
        SmallModelPlanStep step,
        string name,
        string value) => step.Parameters.Single(row =>
            row.Name == name).Value = value;

    private const string IngredientRowsJson =
        "[{\"requirement_id_or_category\":\"709\",\"required_count\":1,\"reverse_slot_consumption_plan\":[{\"slot_index\":0,\"qualified_item_id\":\"(O)709\",\"amount\":1}]},{\"requirement_id_or_category\":\"432\",\"required_count\":1,\"reverse_slot_consumption_plan\":[{\"slot_index\":1,\"qualified_item_id\":\"(O)432\",\"amount\":1}]},{\"requirement_id_or_category\":\"726\",\"required_count\":5,\"reverse_slot_consumption_plan\":[{\"slot_index\":2,\"qualified_item_id\":\"(O)726\",\"amount\":5}]}]";

    private sealed class MemoryRepository : IStrategyCommitmentRepository
    {
        private readonly ReservationPortfolioLedgerService service = new();
        private StrategyCommitmentLedger? ledger;

        public StrategyCommitmentLedger Get(SnapshotEnvelope snapshot) =>
            ledger ?? new StrategyCommitmentLedger
            {
                LedgerId = "ledger:rain-totem",
                SaveId = "save:rain-totem",
                PlayerId = "123",
                Revision = 0,
                SourceStateHash = snapshot.StateHash
            };

        public ReservationPortfolioCommitResult CommitReservationPortfolio(
            SnapshotEnvelope snapshot,
            ReservationPortfolioCommitRequest request)
        {
            var result = service.Commit(
                ledger,
                snapshot,
                request,
                "2026-10-23T00:00:00Z");
            if (result.Accepted)
                ledger = result.Ledger;
            return result;
        }

        public StrategyCommitmentMutationResult Upsert(
            SnapshotEnvelope snapshot,
            CropPlantingCommitmentUpsertRequest request) =>
            throw new NotSupportedException();
        public StrategyCommitmentMutationResult Cancel(
            SnapshotEnvelope snapshot,
            string commitmentId,
            StrategyCommitmentCancelRequest request) =>
            throw new NotSupportedException();
        public StrategyCommitmentMutationResult UpsertMaterial(
            SnapshotEnvelope snapshot,
            MaterialReservationUpsertRequest request) =>
            throw new NotSupportedException();
        public StrategyCommitmentMutationResult CancelMaterial(
            SnapshotEnvelope snapshot,
            string reservationId,
            StrategyCommitmentCancelRequest request) =>
            throw new NotSupportedException();
        public StrategyCommitmentMutationResult UpsertCurrency(
            SnapshotEnvelope snapshot,
            CurrencyReservationUpsertRequest request) =>
            throw new NotSupportedException();
        public StrategyCommitmentMutationResult CancelCurrency(
            SnapshotEnvelope snapshot,
            string reservationId,
            StrategyCommitmentCancelRequest request) =>
            throw new NotSupportedException();
        public ReservationPortfolioRouteSettlementResult
            SettleReservationPortfolioRoute(
                SnapshotEnvelope snapshot,
                ReservationPortfolioRouteSettlementRequest request) =>
            throw new NotSupportedException();
        public ReservationPortfolioSupportingTransitionSettlementResult
            SettleReservationPortfolioSupportingTransition(
                SnapshotEnvelope snapshot,
                ReservationPortfolioSupportingTransitionSettlementRequest
                    request)
        {
            var result = service.SettleSupportingTransition(
                ledger,
                snapshot,
                request,
                "2026-10-23T00:01:00Z");
            if (result.Accepted)
                ledger = result.Ledger;
            return result;
        }
        public StrategyCommitmentMutationResult UpsertMachineRelocation(
            SnapshotEnvelope snapshot,
            MachineRelocationIntentUpsertRequest request) =>
            throw new NotSupportedException();
        public StrategyCommitmentMutationResult UpsertMachineSupport(
            SnapshotEnvelope snapshot,
            MachineSupportIntentUpsertRequest request) =>
            throw new NotSupportedException();
    }
}
