using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueProgression()
    {
        var saleBefore = MoneyProgressionSnapshot(
            totalDay: 10,
            gameTick: 100,
            money: 100,
            totalMoneyEarned: 49_990,
            pendingShippingValue: 120,
            shippingSignature: "pending-120",
            duplicateEndpointCount: 2);
        var saleAfter = MoneyProgressionSnapshot(
            totalDay: 10,
            gameTick: 110,
            money: 120,
            totalMoneyEarned: 50_010,
            pendingShippingValue: 120,
            shippingSignature: "pending-120",
            duplicateEndpointCount: 2);
        var sale = MoneyRevenueProgressionVerifier.VerifyImmediateShopSale(
            saleBefore,
            saleAfter);
        Require(
            sale.TransitionKind ==
                MoneyRevenueTransitionKinds.ImmediateShopSale &&
            sale.MoneyDelta == 20 &&
            sale.TotalMoneyEarnedDelta == 20 &&
            sale.Before.PendingShippingSettlementValue == 120,
            "Immediate Money progression did not preserve an isolated sale.");

        var settlementBefore = MoneyProgressionSnapshot(
            totalDay: 10,
            gameTick: 120,
            money: 120,
            totalMoneyEarned: 50_010,
            pendingShippingValue: 300,
            shippingSignature: "pending-300",
            duplicateEndpointCount: 3);
        var settlementAfter = MoneyProgressionSnapshot(
            totalDay: 11,
            gameTick: 200,
            money: 420,
            totalMoneyEarned: 50_310,
            pendingShippingValue: 0,
            shippingSignature: "empty",
            duplicateEndpointCount: 3);
        var settlement =
            MoneyRevenueProgressionVerifier.VerifyShippingDaySettlement(
                settlementBefore,
                settlementAfter);
        Require(
            settlement.TransitionKind ==
                MoneyRevenueTransitionKinds.NativeShippingDaySettlement &&
            settlement.MoneyDelta == 300 &&
            settlement.TotalMoneyEarnedDelta == 300 &&
            settlement.ConsumedPendingShippingValue == 300,
            "Shipping Money progression double-counted duplicate endpoints.");
        VerifyNativeShippingSettlementReceipt(
            settlementBefore,
            settlementAfter);

        RequireMoneyProgressionRejected(
            saleBefore,
            MoneyProgressionSnapshot(
                totalDay: 10,
                gameTick: 110,
                money: 120,
                totalMoneyEarned: 50_010,
                pendingShippingValue: 0,
                shippingSignature: "empty",
                duplicateEndpointCount: 2),
            MoneyRevenueProgressionVerifier.VerifyImmediateShopSale,
            "A shop sale that consumed pending shipping was admitted.");
        RequireMoneyProgressionRejected(
            settlementBefore,
            MoneyProgressionSnapshot(
                totalDay: 11,
                gameTick: 200,
                money: 720,
                totalMoneyEarned: 50_610,
                pendingShippingValue: 0,
                shippingSignature: "empty",
                duplicateEndpointCount: 3),
            MoneyRevenueProgressionVerifier.VerifyShippingDaySettlement,
            "A shipping settlement that double-counted scope endpoints was admitted.");
    }

    private static void VerifyNativeShippingSettlementReceipt(
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        const string candidateId = "candidate.native-shipping-settlement";
        var item = new ActionQueueItem
        {
            QueueItemId = "queue-item.native-shipping-settlement",
            SourceActionId = "action.native-shipping-settlement",
            OptionId = "executor.sleep",
            Status = "pending",
            BlockingReasons = Array.Empty<string>(),
            NormalizedCommand = new NormalizedCommand
            {
                OptionId = "executor.sleep",
                StateHash = before.StateHash,
                Steps = new[]
                {
                    MoneyProgressionStep("move_to_bed_adjacent"),
                    MoneyProgressionStep("step_onto_sleep_touch_tile"),
                    MoneyProgressionStep("confirm_sleep_yes")
                }
            }
        };
        var queue = new ActionQueueEnvelope
        {
            QueueId = "queue.native-shipping-settlement",
            StateHash = before.StateHash,
            Status = "pending",
            Items = new[] { item }
        };
        var changedFacts = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                path = "player.money",
                before = "120",
                after = "420"
            }
        });
        var receipt = new QueueExecutionReceiptEnvelope
        {
            RunId = "run.native-shipping-settlement",
            QueueId = queue.QueueId,
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = 1,
            ExecutedItemCount = 1,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = 1,
            SelectedCandidateId = candidateId,
            SelectedCandidateCompleted = true,
            StepResults = new[]
            {
                new QueueExecutionStepReceipt
                {
                    QueueItemIndex = 0,
                    QueueItemCount = 1,
                    OriginalPlannedItemCount = 1,
                    QueueId = queue.QueueId,
                    QueueItemId = item.QueueItemId,
                    OptionId = item.OptionId,
                    SourceStateHash = before.StateHash,
                    CompiledCommandStateHash = before.StateHash,
                    TeacherPreferenceStateRebound = false,
                    SelectedQueueCandidateCompleted = true,
                    AfterStateHash = after.StateHash,
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick,
                    AfterGameTick = after.GameTick,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = "sleep",
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_day_transition_applied"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        item,
                        JsonDefaults.Options),
                    ChangedFacts = changedFacts
                }
            }
        };
        var evidence = NativeShippingSettlementEvidenceVerifier.Verify(
            queue,
            before,
            receipt,
            after,
            receipt.RunId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            candidateId);
        Require(
            evidence.Verified &&
            evidence.QueueReceiptVerified &&
            evidence.NativeSleepQueueVerified &&
            evidence.Progression?.ConsumedPendingShippingValue == 300,
            "Native shipping settlement evidence was rejected: " +
            string.Join(",", evidence.BlockingReasons));

        item.OptionId = "executor.move";
        item.NormalizedCommand.OptionId = item.OptionId;
        var nonSleep = NativeShippingSettlementEvidenceVerifier.Verify(
            queue,
            before,
            receipt,
            after,
            receipt.RunId,
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            candidateId);
        Require(
            !nonSleep.Verified &&
            !nonSleep.NativeSleepQueueVerified &&
            nonSleep.BlockingReasons.Contains(
                "native_shipping_settlement_queue_not_single_sleep"),
            "A non-sleep queue was admitted as shipping settlement.");
    }

    private static CompiledActionStep MoneyProgressionStep(string kind) => new()
    {
        StepId = "step." + kind,
        StepType = kind,
        Target = "fixture",
        ExpectedEffect = "fixture",
        EstimatedTicks = 1
    };

    private static void RequireMoneyProgressionRejected(
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        Func<SnapshotEnvelope, SnapshotEnvelope,
            MoneyRevenueTransitionEvidence> verify,
        string message)
    {
        var rejected = false;
        try
        {
            verify(before, after);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }

    private static SnapshotEnvelope MoneyProgressionSnapshot(
        int totalDay,
        long gameTick,
        int money,
        uint totalMoneyEarned,
        int pendingShippingValue,
        string shippingSignature,
        int duplicateEndpointCount)
    {
        var endpoints = Enumerable.Range(0, duplicateEndpointCount)
            .Select(index => new
            {
                completed = true,
                tile_x = 10 + index,
                tile_y = 12,
                bin_scope = "shared",
                player_id = 42L,
                pending_settlement_value = pendingShippingValue,
                pending_settlement_value_complete = true,
                pending_settlement_timing = "next_native_day_transition",
                contents_signature = shippingSignature,
                contents_truncated = false
            })
            .ToArray();
        var state = JsonSerializer.Deserialize<
            Dictionary<string, JsonElement>>(
            JsonSerializer.Serialize(new
            {
                time = new
                {
                    total_days = MoneyProgressionField(totalDay)
                },
                player = new
                {
                    money = MoneyProgressionField(money),
                    total_money_earned =
                        MoneyProgressionField(totalMoneyEarned)
                },
                farm = new
                {
                    shipping_bins = MoneyProgressionField(endpoints)
                }
            }),
            JsonDefaults.Options)!;
        return new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = "0.1.0",
            GameVersion = "1.6.15",
            SaveId = MoneyProgressionIdentity("money-progression-save"),
            PlayerId = MoneyProgressionIdentity("42"),
            GameTick = gameTick,
            RealTimestamp = "2026-10-14T00:00:00Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static object MoneyProgressionField(object value) => new
    {
        value,
        status = FieldStatus.Available,
        source = new { kind = "self_test", path = "money_progression" },
        adapter = "self_test",
        read_at_tick = 1,
        confidence = 1.0
    };

    private static FieldEnvelope<string?> MoneyProgressionIdentity(
        string value) => new()
    {
        Value = value,
        Status = FieldStatus.Available
    };
}
