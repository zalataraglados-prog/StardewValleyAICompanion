using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private const string FullShipmentRecoveryCandidate =
        "recovery:native_save_boundary";
    private const string FullShipmentRecoveryRun =
        "run.full-shipment-recovery";

    private static void VerifyFullShipmentRecoveryTransitionEvidence()
    {
        var required = new[] { "(O)24", "(O)60", "(O)80" };
        var before = RecoverySnapshot(
            "full-shipment-recovery-before",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        var after = RecoverySnapshot(
            "full-shipment-recovery-after",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        after.GameTick = before.GameTick + 1;
        var valid = RecoveryFixture(
            before,
            after,
            "executor.traverse_connector",
            "traverse_connector");
        var reasons = ValidateRecovery(
            valid.Queue,
            before,
            valid.Receipt,
            after,
            before,
            required);
        Require(
            reasons.Length == 0,
            "A native Full Shipment recovery transition was rejected: " +
            string.Join(",", reasons));

        var legacyExpected = RecoverySnapshot(
            string.Empty,
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        legacyExpected.State["machine_probe_fixture"] =
            JsonSerializer.SerializeToElement(new
            {
                machine_probe_cache_tick = 1179,
                ready_for_harvest = false
            });
        legacyExpected.StateHash = SnapshotHash.ComputeLegacyStateHash(
            legacyExpected.State);
        var legacyBefore = RecoverySnapshot(
            string.Empty,
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        legacyBefore.State["machine_probe_fixture"] =
            JsonSerializer.SerializeToElement(new
            {
                machine_probe_cache_tick = 1181,
                ready_for_harvest = false
            });
        legacyBefore.StateHash = SnapshotHash.ComputeLegacyStateHash(
            legacyBefore.State);
        Require(
            legacyExpected.StateHash != legacyBefore.StateHash,
            "Legacy machine-probe cache metadata did not perturb the fixture hash.");
        var legacyEquivalent = RecoveryFixture(
            legacyBefore,
            after,
            "executor.traverse_connector",
            "traverse_connector");
        var legacyEquivalentReasons = ValidateRecovery(
            legacyEquivalent.Queue,
            legacyBefore,
            legacyEquivalent.Receipt,
            after,
            legacyExpected,
            required);
        Require(
            !legacyEquivalentReasons.Contains(
                "full_shipment_recovery_state_hash_chain_broken",
                StringComparer.Ordinal),
            "Equivalent legacy snapshots were disconnected by cache metadata: " +
            string.Join(",", legacyEquivalentReasons));

        var sleep = RecoveryFixture(
            before,
            after,
            "executor.sleep",
            "sleep");
        RequireRecoveryRejected(
            ValidateRecovery(
                sleep.Queue,
                before,
                sleep.Receipt,
                after,
                before,
                required),
            "full_shipment_recovery_queue_not_native_stabilization",
            "A terminal sleep entered the intermediate recovery chain.");

        var progressAfter = RecoverySnapshot(
            "full-shipment-recovery-progress-drift",
            totalDay: 40,
            parsnipShipped: true,
            parsnipBinCount: 1);
        progressAfter.GameTick = before.GameTick + 1;
        var progress = RecoveryFixture(
            before,
            progressAfter,
            "executor.traverse_connector",
            "traverse_connector");
        RequireRecoveryRejected(
            ValidateRecovery(
                progress.Queue,
                before,
                progress.Receipt,
                progressAfter,
                before,
                required),
            "full_shipment_recovery_progress_or_day_drifted",
            "Full Shipment progress changed inside the recovery chain.");

        var dayAfter = RecoverySnapshot(
            "full-shipment-recovery-day-drift",
            totalDay: 41,
            parsnipShipped: false,
            parsnipBinCount: 1);
        dayAfter.GameTick = before.GameTick + 1;
        var day = RecoveryFixture(
            before,
            dayAfter,
            "executor.traverse_connector",
            "traverse_connector");
        RequireRecoveryRejected(
            ValidateRecovery(
                day.Queue,
                before,
                day.Receipt,
                dayAfter,
                before,
                required),
            "full_shipment_recovery_progress_or_day_drifted",
            "A day transition entered the intermediate recovery chain.");

        var emptyBinAfter = RecoverySnapshot(
            "full-shipment-recovery-bin-drift",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 0);
        emptyBinAfter.GameTick = before.GameTick + 1;
        var emptyBin = RecoveryFixture(
            before,
            emptyBinAfter,
            "executor.traverse_connector",
            "traverse_connector");
        RequireRecoveryRejected(
            ValidateRecovery(
                emptyBin.Queue,
                before,
                emptyBin.Receipt,
                emptyBinAfter,
                before,
                required),
            "full_shipment_recovery_pending_bin_item_drifted",
            "A pending shipping-bin item disappeared during recovery.");

        var wrongExpected = RecoverySnapshot(
            "full-shipment-recovery-wrong-prior",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        RequireRecoveryRejected(
            ValidateRecovery(
                valid.Queue,
                before,
                valid.Receipt,
                after,
                wrongExpected,
                required),
            "full_shipment_recovery_state_hash_chain_broken",
            "A reordered or disconnected recovery transition was admitted.");

        after.PlayerId = new FieldEnvelope<string?>
        {
            Value = "other-player",
            Status = FieldStatus.Available
        };
        RequireRecoveryRejected(
            ValidateRecovery(
                valid.Queue,
                before,
                valid.Receipt,
                after,
                before,
                required),
            "full_shipment_recovery_actor_or_game_identity_drifted",
            "A recovery transition crossed player identity.");
    }

    private static SnapshotEnvelope RecoverySnapshot(
        string stateHash,
        int totalDay,
        bool parsnipShipped,
        int parsnipBinCount) => FullShipmentSettlementSnapshot(
            stateHash,
            totalDay,
            parsnipShipped,
            obsidianShipped: false,
            parsnipBinCount,
            achievement34: false);

    private static (
        ActionQueueEnvelope Queue,
        QueueExecutionReceiptEnvelope Receipt) RecoveryFixture(
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string optionId,
        string primitiveKind)
    {
        var item = new ActionQueueItem
        {
            QueueItemId = "queue-item.full-shipment-recovery.0",
            SourceActionId = "action.full-shipment-recovery.0",
            OptionId = optionId,
            NormalizedCommand = new NormalizedCommand
            {
                OptionId = optionId,
                StateHash = before.StateHash,
                Steps = new[]
                {
                    new CompiledActionStep
                    {
                        StepId = "primitive.full-shipment-recovery.0",
                        StepType = primitiveKind,
                        EstimatedTicks = 1
                    }
                }
            }
        };
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.full-shipment-recovery",
            StateHash = before.StateHash,
            Items = new[] { item }
        };
        var receipt = TargetDateQueueReceipt(
            queue,
            item,
            before,
            after.StateHash,
            after.GameTick,
            FullShipmentRecoveryCandidate,
            FullShipmentRecoveryRun);
        return (queue, receipt);
    }

    private static string[] ValidateRecovery(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        QueueExecutionReceiptEnvelope receipt,
        SnapshotEnvelope after,
        SnapshotEnvelope expectedBefore,
        IReadOnlyCollection<string> required) =>
        FullShipmentRecurrenceProofBuilder.ValidateRecoveryTransition(
            queue,
            before,
            receipt,
            after,
            expectedBefore,
            required,
            "(O)24",
            FullShipmentRecoveryRun,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            FullShipmentRecoveryCandidate);

    private static void RequireRecoveryRejected(
        IReadOnlyCollection<string> reasons,
        string expectedReason,
        string message) => Require(
            reasons.Contains(expectedReason, StringComparer.Ordinal),
            message + " Reasons: " + string.Join(",", reasons));
}
