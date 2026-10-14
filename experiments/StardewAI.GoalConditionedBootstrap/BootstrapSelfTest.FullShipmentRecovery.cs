using System.Text.Json;
using System.Text.Json.Nodes;
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
            parsnipBinCount: 1,
            tileX: 1);
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

        var movementBefore = RecoverySnapshot(
            "full-shipment-recovery-movement-before",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        var movementAfter = RecoverySnapshot(
            "full-shipment-recovery-movement-after",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1,
            tileX: 1);
        AddRecoveryMovementProjectionNoise(movementBefore, afterMove: false);
        AddRecoveryMovementProjectionNoise(movementAfter, afterMove: true);
        movementAfter.GameTick = movementBefore.GameTick + 1;
        var movement = RecoveryFixture(
            movementBefore,
            movementAfter,
            "executor.traverse_connector",
            "traverse_connector");
        var movementReasons = ValidateRecovery(
            movement.Queue,
            movementBefore,
            movement.Receipt,
            movementAfter,
            movementBefore,
            required);
        Require(
            movementReasons.Length == 0,
            "Lawful movement-derived recovery noise was treated as persistent " +
            "state drift: " + string.Join(",", movementReasons));

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

        var moneyAfter = RecoverySnapshot(
            "full-shipment-recovery-money-drift",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1,
            money: 501);
        moneyAfter.GameTick = before.GameTick + 1;
        var money = RecoveryFixture(
            before,
            moneyAfter,
            "executor.traverse_connector",
            "traverse_connector");
        RequireRecoveryRejected(
            ValidateRecovery(
                money.Queue,
                before,
                money.Receipt,
                moneyAfter,
                before,
                required),
            "full_shipment_recovery_money_or_currency_drifted",
            "Money drift crossed the recovery-settlement boundary.");

        var inventoryAfter = RecoverySnapshot(
            "full-shipment-recovery-inventory-drift",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1,
            inventoryParsnipCount: 2);
        inventoryAfter.GameTick = before.GameTick + 1;
        var inventory = RecoveryFixture(
            before,
            inventoryAfter,
            "executor.traverse_connector",
            "traverse_connector");
        RequireRecoveryRejected(
            ValidateRecovery(
                inventory.Queue,
                before,
                inventory.Receipt,
                inventoryAfter,
                before,
                required),
            "full_shipment_recovery_inventory_drifted",
            "Inventory drift crossed the recovery-settlement boundary.");

        var unknownPersistentAfter = RecoverySnapshot(
            "full-shipment-recovery-unknown-persistent-drift",
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        unknownPersistentAfter.GameTick = before.GameTick + 1;
        unknownPersistentAfter.State["future_persistent_domain"] =
            JsonSerializer.SerializeToElement(new
            {
                future_counter = 1
            });
        var unknownPersistent = RecoveryFixture(
            before,
            unknownPersistentAfter,
            "executor.traverse_connector",
            "traverse_connector");
        RequireRecoveryRejected(
            ValidateRecovery(
                unknownPersistent.Queue,
                before,
                unknownPersistent.Receipt,
                unknownPersistentAfter,
                before,
                required),
            "full_shipment_recovery_persistent_state_drifted",
            "An unknown persistent field bypassed the boundary contract.");

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

        var staleTickBefore = RecoverySnapshot(
            before.StateHash,
            totalDay: 40,
            parsnipShipped: false,
            parsnipBinCount: 1);
        staleTickBefore.GameTick = before.GameTick - 1;
        RequireRecoveryRejected(
            ValidateRecovery(
                valid.Queue,
                before,
                valid.Receipt,
                after,
                staleTickBefore,
                required),
            "full_shipment_recovery_game_tick_chain_broken",
            "A repeated state hash at a stale game tick was admitted.");

        var wrongPrimitive = RecoveryFixture(
            before,
            after,
            "executor.traverse_connector",
            "interact");
        RequireRecoveryRejected(
            ValidateRecovery(
                wrongPrimitive.Queue,
                before,
                wrongPrimitive.Receipt,
                after,
                before,
                required),
            "full_shipment_recovery_queue_not_native_stabilization",
            "A recovery label admitted an unrelated compiled primitive.");

        var oversizedWait = RecoveryFixture(
            before,
            after,
            "executor.wait_ticks",
            "wait_ticks",
            waitTicks: 600);
        RequireRecoveryRejected(
            FullShipmentRecurrenceProofBuilder.ValidateRecoveryTransition(
                oversizedWait.Queue,
                before,
                oversizedWait.Receipt,
                after,
                before,
                required,
                "(O)24",
                FullShipmentRecoveryRun,
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                "recovery:refresh_plan_after_stabilization"),
            "full_shipment_recovery_queue_not_native_stabilization",
            "An unbounded recovery refresh wait was admitted.");

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
        int parsnipBinCount,
        int money = 500,
        int inventoryParsnipCount = 1,
        int tileX = 0)
    {
        var snapshot = FullShipmentSettlementSnapshot(
            stateHash,
            totalDay,
            parsnipShipped,
            obsidianShipped: false,
            parsnipBinCount,
            achievement34: false);
        snapshot.State["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = Available("Farm"),
            tile_x = Available(tileX),
            tile_y = Available(10),
            facing_direction = Available(2),
            current_tool_index = Available(0),
            money = Available(money),
            club_coins = Available(10),
            shop_currency_balances = Available(new
            {
                money,
                star_tokens = 20,
                club_coins = 10,
                qi_gems = 5
            }),
            total_money_earned = Available(1000U),
            inventory = Available(new[]
            {
                new
                {
                    slot_index = 0,
                    qualified_item_id = "(O)24",
                    stack = inventoryParsnipCount,
                    quality = 0
                }
            })
        });
        snapshot.State["quests"] = JsonSerializer.SerializeToElement(new
        {
            active = Available(Array.Empty<object>())
        });
        return snapshot;
    }

    private static void AddRecoveryMovementProjectionNoise(
        SnapshotEnvelope snapshot,
        bool afterMove)
    {
        var farm = JsonNode.Parse(snapshot.State["farm"].GetRawText())!
            .AsObject();
        farm["machines"] = JsonSerializer.SerializeToNode(Available(new[]
        {
            new
            {
                qualified_item_id = "(BC)163",
                machine_probe_cache_tick = afterMove ? 11 : 10,
                held_item = (string?)null
            }
        }));
        farm["material_inventory_graph"] = JsonSerializer.SerializeToNode(
            Available(new
            {
                inventory_nodes = new[]
                {
                    new
                    {
                        inventory_kind = "player_inventory",
                        location_id = afterMove ? "FarmHouse" : "Farm"
                    }
                }
            }));
        var bins = farm["shipping_bins"]!["value"]!.AsArray();
        var bin = bins[0]!.AsObject();
        bin["distance_to_player"] = afterMove ? 68.5 : 1.5;
        bin["player_within_shipping_range"] = !afterMove;
        bin["interaction_stand_tile_x"] = afterMove ? 71 : 70;
        bin["interaction_stand_tile_y"] = afterMove ? 13 : 14;
        bin["interaction_stand_tile_blocked_reason"] = null;
        bin["stand_tiles"] = JsonSerializer.SerializeToNode(new[]
        {
            new { x = afterMove ? 71 : 70, y = afterMove ? 13 : 14 }
        });
        snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);

        var player = JsonNode.Parse(snapshot.State["player"].GetRawText())!
            .AsObject();
        player["active_dialogue_events"] = JsonSerializer.SerializeToNode(
            Available(afterMove
                ? new[] { "Introduction", "firstVisit_FarmHouse" }
                : new[] { "Introduction", "firstVisit_Farm" }));
        snapshot.State["player"] = JsonSerializer.SerializeToElement(player);

        var quests = JsonNode.Parse(snapshot.State["quests"].GetRawText())!
            .AsObject();
        quests["adventure_guild_reward"] = JsonSerializer.SerializeToNode(
            Available(new
            {
                current_location_matches = false,
                action_tile_x = afterMove ? 11 : 12,
                action_tile_y = 12
            }));
        snapshot.State["quests"] = JsonSerializer.SerializeToElement(quests);

        var progress = JsonNode.Parse(
            snapshot.State["world_progress"].GetRawText())!.AsObject();
        progress["game_state_query_unlock_state"] =
            JsonSerializer.SerializeToNode(Available(new
            {
                players = new[]
                {
                    new
                    {
                        player_id = "123",
                        stats = new
                        {
                            TreesChopped = 1,
                            stepsTaken = afterMove ? 12 : 9
                        }
                    }
                }
            }));
        snapshot.State["world_progress"] =
            JsonSerializer.SerializeToElement(progress);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }

    private static (
        ActionQueueEnvelope Queue,
        QueueExecutionReceiptEnvelope Receipt) RecoveryFixture(
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string optionId,
        string primitiveKind,
        int waitTicks = 30)
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
                        Target = primitiveKind == "wait_ticks"
                            ? waitTicks.ToString()
                            : string.Empty,
                        EstimatedTicks = primitiveKind == "wait_ticks"
                            ? waitTicks
                            : 1
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
