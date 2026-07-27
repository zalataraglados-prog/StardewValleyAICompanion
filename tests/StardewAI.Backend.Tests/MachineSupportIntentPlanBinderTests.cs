using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;

namespace StardewAI.Backend.Tests;

public sealed class MachineSupportIntentPlanBinderTests
{
    [Fact]
    public void BindsSupportedCraftAndRebindsLedgerRevision()
    {
        var repository = new CapturingRepository();
        var snapshot = new SnapshotEnvelope
        {
            StateHash = "state:craft"
        };
        var step = new SmallModelPlanStep
        {
            StepId = "step:craft",
            Kind = "craft_machine_item",
            Preconditions =
            [
                "candidate_id:machine-craft:keg"
            ],
            Parameters =
            [
                Parameter(
                    "goal_support_status",
                    "supported_bounded_positive_net_benefit"),
                Parameter(
                    "machine_support_intent_id",
                    "machine-support:money:keg"),
                Parameter(
                    "goal_support_parent_goal_id",
                    "goal.economy.earn_money"),
                Parameter(
                    "output_qualified_item_id",
                    "(BC)12"),
                Parameter("output_item_id", "12"),
                Parameter(
                    "machine_demand_class",
                    "production_capacity_requirement"),
                Parameter(
                    "goal_support_kind",
                    "machine_capacity_current_backlog"),
                Parameter(
                    "goal_support_evidence_status",
                    "complete"),
                Parameter("goal_support_gross_benefit", "400"),
                Parameter("goal_support_opportunity_cost", "60"),
                Parameter("goal_support_net_benefit", "340"),
                Parameter("goal_support_score", "0.034"),
                Parameter(
                    "required_additional_machine_count",
                    "1"),
                Parameter("commitment_ledger_id", "ledger:test"),
                Parameter("commitment_ledger_revision", "7"),
                Parameter(
                    "material_reservation_ledger_id",
                    "ledger:test"),
                Parameter(
                    "material_reservation_ledger_revision",
                    "7"),
                Parameter("machine_support_intent_revision", ""),
                Parameter("machine_support_intent_stage", ""),
                Parameter(
                    "machine_support_intent_source_state_hash",
                    "")
            ]
        };
        var plan = new SmallModelPlanEnvelope
        {
            Steps = [step]
        };

        var result = MachineSupportIntentPlanBinder.Bind(
            plan,
            snapshot,
            repository);

        Assert.NotNull(result);
        Assert.True(result.Accepted);
        var request = Assert.IsType<
            MachineSupportIntentUpsertRequest>(
            repository.Request);
        Assert.Equal(7, request.ExpectedLedgerRevision);
        Assert.Equal(
            MachineSupportIntentStages.CraftSelected,
            request.Stage);
        Assert.Equal(340, request.NetBenefit);
        Assert.Equal(
            "8",
            Value(step, "commitment_ledger_revision"));
        Assert.Equal(
            "1",
            Value(step, "machine_support_intent_revision"));
        Assert.Equal(
            MachineSupportIntentStages.CraftSelected,
            Value(step, "machine_support_intent_stage"));
        Assert.Equal(
            snapshot.StateHash,
            Value(
                step,
                "machine_support_intent_source_state_hash"));
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };

    private static string Value(
        SmallModelPlanStep step,
        string name) =>
        step.Parameters.Single(parameter =>
            parameter.Name == name).Value;

    private sealed class CapturingRepository :
        IStrategyCommitmentRepository
    {
        private StrategyCommitmentLedger ledger = new()
        {
            LedgerId = "ledger:test",
            Revision = 7
        };

        public MachineSupportIntentUpsertRequest? Request
        {
            get;
            private set;
        }

        public StrategyCommitmentLedger Get(
            SnapshotEnvelope snapshot) => ledger;

        public StrategyCommitmentMutationResult
            UpsertMachineSupport(
                SnapshotEnvelope snapshot,
                MachineSupportIntentUpsertRequest request)
        {
            Request = request;
            ledger = new StrategyCommitmentLedger
            {
                LedgerId = "ledger:test",
                Revision = 8,
                SourceStateHash = snapshot.StateHash,
                MachineSupportIntents =
                [
                    new MachineSupportIntent
                    {
                        IntentId = request.IntentId,
                        Revision = 1,
                        Status =
                            StrategyCommitmentStatuses.Active,
                        Stage = request.Stage,
                        SourceStateHash = snapshot.StateHash
                    }
                ]
            };
            return new StrategyCommitmentMutationResult
            {
                Accepted = true,
                Ledger = ledger
            };
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

        public StrategyCommitmentMutationResult
            UpsertMachineRelocation(
                SnapshotEnvelope snapshot,
                MachineRelocationIntentUpsertRequest request) =>
            throw new NotSupportedException();
    }
}
