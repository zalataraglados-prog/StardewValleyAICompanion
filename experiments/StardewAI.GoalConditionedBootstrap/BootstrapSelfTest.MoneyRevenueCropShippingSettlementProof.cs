using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static string VerifyMoneyCropShippingTerminalSettlementProof(
        string root,
        string planPath,
        string retentionProofPath)
    {
        var retention = CurrentTeacherFrontierSupport.Read<
            MoneyRevenueCropOutputRetentionProof>(
                retentionProofPath,
                "Money crop shipping retention fixture");
        var current = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            retention.AfterSnapshotPath,
            "Money crop shipping start fixture");
        var unitPrice = checked((int)(
            retention.MinimumGrossRevenue / retention.MinimumUnits));
        var transitions = new List<MoneyRevenueTerminalQueueTransitionProof>();
        for (var index = 0; index < retention.MinimumUnits; index++)
        {
            var transitionId = "crop-shipping-deposit-" + (index + 1);
            var transitionRoot = Path.Combine(root, transitionId);
            Directory.CreateDirectory(transitionRoot);
            var before = current;
            var after = MoneyCropShippingDepositAfter(
                before,
                retention.HarvestItemQualifiedId,
                unitPrice);
            var candidateId = "candidate." + transitionId;
            var queue = MoneyCropShippingDepositQueue(
                before,
                retention.HarvestItemQualifiedId,
                unitPrice,
                transitionId);
            var receipt = MoneyCropQueueReceipt(
                queue,
                before,
                after,
                candidateId,
                "ship_inventory_item_to_bin",
                new[]
                {
                    new
                    {
                        path = "player.inventory." +
                            retention.HarvestItemQualifiedId + ".count",
                        before = MoneyCropInventoryCount(
                            before,
                            retention.HarvestItemQualifiedId),
                        after = MoneyCropInventoryCount(
                            after,
                            retention.HarvestItemQualifiedId)
                    },
                    new
                    {
                        path = "farm.shipping_bin." +
                            retention.HarvestItemQualifiedId + ".count",
                        before = index,
                        after = index + 1
                    }
                });
            var queuePath = Path.Combine(transitionRoot, "queue.json");
            var beforePath = Path.Combine(transitionRoot, "before.json");
            var receiptPath = Path.Combine(transitionRoot, "receipt.json");
            var afterPath = Path.Combine(transitionRoot, "after.json");
            Write(queuePath, queue);
            Write(beforePath, before);
            Write(receiptPath, receipt);
            Write(afterPath, after);
            transitions.Add(new MoneyRevenueTerminalQueueTransitionProof
            {
                TransitionId = transitionId,
                QueuePath = queuePath,
                BeforeSnapshotPath = beforePath,
                ExecutionReceiptPath = receiptPath,
                AfterSnapshotPath = afterPath,
                RunId = receipt.RunId,
                ExecutorVersion =
                    PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
                SelectedCandidateId = candidateId
            });
            current = after;
        }

        var settlementRoot = Path.Combine(root, "crop-shipping-settlement");
        Directory.CreateDirectory(settlementRoot);
        var settlementAfter = MoneyCropShippingSettlementAfter(
            current,
            checked(retention.MinimumUnits * unitPrice));
        const string settlementCandidateId =
            "candidate.crop-shipping-settlement";
        var settlementQueue = MoneyCropShippingSettlementQueue(current);
        var settlementReceipt = MoneyCropQueueReceipt(
            settlementQueue,
            current,
            settlementAfter,
            settlementCandidateId,
            "sleep",
            new[]
            {
                new
                {
                    path = "player.money",
                    before = MoneyCropPlayerInt(current, "money"),
                    after = MoneyCropPlayerInt(settlementAfter, "money")
                }
            });
        var settlementQueuePath = Path.Combine(settlementRoot, "queue.json");
        var settlementBeforePath = transitions[^1].AfterSnapshotPath;
        var settlementReceiptPath = Path.Combine(settlementRoot, "receipt.json");
        var settlementAfterPath = Path.Combine(settlementRoot, "after.json");
        Write(settlementQueuePath, settlementQueue);
        Write(settlementReceiptPath, settlementReceipt);
        Write(settlementAfterPath, settlementAfter);
        var recurrenceManifestPath = Path.Combine(
            settlementRoot,
            "recurrence-manifest.json");
        var recurrenceReceiptPath = Path.Combine(
            settlementRoot,
            "recurrence-receipt.json");
        Write(recurrenceManifestPath,
            new MoneyRevenueRecurrenceProofManifest
            {
                Transitions = new[]
                {
                    new MoneyRevenueRecurrenceTransitionProof
                    {
                        TransitionId = "crop-shipping-day-settlement",
                        ProofKind = MoneyRevenueRecurrenceProofKinds
                            .NativeShippingSettlement,
                        QueuePath = settlementQueuePath,
                        BeforeSnapshotPath = settlementBeforePath,
                        ExecutionReceiptPath = settlementReceiptPath,
                        AfterSnapshotPath = settlementAfterPath,
                        RunId = settlementReceipt.RunId,
                        ExecutorVersion = PolicyTrajectoryVersionPins
                            .RuntimeTestHarnessExecutor,
                        SelectedCandidateId = settlementCandidateId
                    }
                }
            });
        Write(recurrenceReceiptPath,
            MoneyRevenueRecurrenceProofBuilder.Build(recurrenceManifestPath));

        var requestPath = Path.Combine(
            root,
            "crop-shipping-settlement-request.json");
        var proofPath = Path.Combine(
            root,
            "crop-shipping-settlement-proof.json");
        Write(requestPath,
            new MoneyRevenueCropTerminalSettlementProofRequest
            {
                CropProductionPlanPath = planPath,
                OutputRetentionProofPath = retentionProofPath,
                ShippingDepositTransitions = transitions.ToArray(),
                RevenueRecurrenceManifestPath = recurrenceManifestPath,
                RevenueRecurrenceReceiptPath = recurrenceReceiptPath
            });
        var proof = MoneyRevenueCropTerminalSettlementProofBuilder.Build(
            requestPath);
        Require(proof.TerminalKind ==
                    MoneyRevenueRecurrenceProofKinds.NativeShippingSettlement &&
                proof.ShippingDeposits.Length == retention.MinimumUnits &&
                proof.ShippingDeposits.Select(row => row.PendingValueBefore)
                    .SequenceEqual(new long[] { 0, unitPrice }) &&
                proof.ShippingDeposits.Select(row => row.PendingValueAfter)
                    .SequenceEqual(new long[] { unitPrice, unitPrice * 2L }) &&
                proof.SettledQuantity == retention.MinimumUnits &&
                proof.SettledRevenue == retention.MinimumGrossRevenue &&
                proof.RouteTransitions.Length == 0 &&
                proof.NativeTerminalSettlementVerified &&
                !proof.FormalProductTrainingAuthorized,
            "Money crop native shipping settlement lost its exact wave chain.");
        Write(proofPath, proof);

        var missingDepositRequestPath = Path.Combine(
            root,
            "crop-shipping-missing-deposit-request.json");
        Write(missingDepositRequestPath,
            new MoneyRevenueCropTerminalSettlementProofRequest
            {
                CropProductionPlanPath = planPath,
                OutputRetentionProofPath = retentionProofPath,
                ShippingDepositTransitions = transitions.Take(1).ToArray(),
                RevenueRecurrenceManifestPath = recurrenceManifestPath,
                RevenueRecurrenceReceiptPath = recurrenceReceiptPath
            });
        RequireRejects(
            () => MoneyRevenueCropTerminalSettlementProofBuilder.Build(
                missingDepositRequestPath),
            "A crop shipping settlement with a missing native deposit was admitted.");
        var mixedModeRequestPath = Path.Combine(
            root,
            "crop-shipping-mixed-mode-request.json");
        Write(mixedModeRequestPath,
            new MoneyRevenueCropTerminalSettlementProofRequest
            {
                CropProductionPlanPath = planPath,
                OutputRetentionProofPath = retentionProofPath,
                ShopSaleEpisodePath = "must-not-be-read.json",
                ShippingDepositTransitions = transitions.ToArray(),
                RevenueRecurrenceManifestPath = recurrenceManifestPath,
                RevenueRecurrenceReceiptPath = recurrenceReceiptPath
            });
        RequireRejects(
            () => MoneyRevenueCropTerminalSettlementProofBuilder.Build(
                mixedModeRequestPath),
            "A crop settlement mixing ShopMenu and shipping modes was admitted.");
        return proofPath;
    }

    private static ActionQueueEnvelope MoneyCropShippingDepositQueue(
        SnapshotEnvelope before,
        string qualifiedItemId,
        int unitPrice,
        string transitionId)
    {
        var item = new ActionQueueItem
        {
            QueueItemId = "queue-item." + transitionId,
            OptionId = "executor.ship_inventory_item_to_bin",
            Status = "pending",
            NormalizedCommand = new NormalizedCommand
            {
                OptionId = "executor.ship_inventory_item_to_bin",
                StateHash = before.StateHash,
                Parameters = new[]
                {
                    ShippingParameter("slot_index", "1"),
                    ShippingParameter("qualified_item_id", qualifiedItemId),
                    ShippingParameter("quantity", "1"),
                    ShippingParameter("expected_unit_price", unitPrice.ToString()),
                    ShippingParameter("target_location", "Farm"),
                    ShippingParameter("target_tile_x", "71"),
                    ShippingParameter("target_tile_y", "14")
                },
                Steps = new[]
                {
                    new CompiledActionStep
                    {
                        StepId = "step." + transitionId,
                        StepType = "ship_inventory_item_to_bin",
                        Target = "Farm:71,14",
                        ExpectedEffect = "deposit one retained crop",
                        EstimatedTicks = 1
                    }
                }
            }
        };
        return new ActionQueueEnvelope
        {
            QueueId = "queue." + transitionId,
            StateHash = before.StateHash,
            Status = "pending",
            Items = new[] { item }
        };
    }

    private static ActionQueueEnvelope MoneyCropShippingSettlementQueue(
        SnapshotEnvelope before)
    {
        var item = new ActionQueueItem
        {
            QueueItemId = "queue-item.crop-shipping-settlement",
            OptionId = "executor.sleep",
            Status = "pending",
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
        return new ActionQueueEnvelope
        {
            QueueId = "queue.crop-shipping-settlement",
            StateHash = before.StateHash,
            Status = "pending",
            Items = new[] { item }
        };
    }

    private static QueueExecutionReceiptEnvelope MoneyCropQueueReceipt(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string candidateId,
        string primitiveKind,
        object changedFacts)
    {
        var item = queue.Items.Single();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run." + queue.QueueId,
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
                    SelectedQueueCandidateCompleted = true,
                    AfterStateHash = after.StateHash,
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick,
                    AfterGameTick = after.GameTick,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = primitiveKind,
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_transition_observed"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        item,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(
                        changedFacts,
                        JsonDefaults.Options)
                }
            }
        };
    }

    private static SnapshotEnvelope MoneyCropShippingDepositAfter(
        SnapshotEnvelope before,
        string qualifiedItemId,
        int unitPrice)
    {
        var after = CloneSnapshot(before);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        var inventory = player["inventory"]!["value"]!.AsArray();
        var row = inventory.Single(value =>
            value?["qualified_item_id"]?.GetValue<string>() == qualifiedItemId);
        var remaining = row!["stack"]!.GetValue<int>() - 1;
        Require(remaining >= 0,
            "Money crop shipping fixture exhausted retained output.");
        if (remaining == 0)
        {
            row.ReplaceWith(new JsonObject
            {
                ["slot_index"] = 1,
                ["item_id"] = null,
                ["qualified_item_id"] = null,
                ["stack"] = null,
                ["quality"] = null,
                ["sell_to_store_price"] = null,
                ["is_empty"] = true
            });
        }
        else
        {
            row["stack"] = remaining;
        }
        player["inventory"]!["value"] = inventory;
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);

        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!
            .AsObject();
        var bin = farm["shipping_bins"]!["value"]!.AsArray().Single()!
            .AsObject();
        var contents = bin["contents"]!.AsArray();
        var existing = contents.SingleOrDefault(value =>
            value?["qualified_item_id"]?.GetValue<string>() == qualifiedItemId);
        var quantity = 1;
        if (existing is null)
        {
            contents.Add(new JsonObject
            {
                ["item_id"] = qualifiedItemId[3..],
                ["qualified_item_id"] = qualifiedItemId,
                ["quality"] = 0,
                ["count"] = 1,
                ["unit_sell_to_store_price"] = unitPrice,
                ["total_sell_to_store_price"] = unitPrice
            });
        }
        else
        {
            quantity = existing["count"]!.GetValue<int>() + 1;
            existing["count"] = quantity;
            existing["total_sell_to_store_price"] =
                checked(quantity * unitPrice);
        }
        bin["contents"] = contents;
        bin["pending_settlement_value"] = checked(quantity * unitPrice);
        bin["contents_signature"] = "crop-shipping:" + quantity;
        after.State["farm"] = JsonSerializer.SerializeToElement(
            farm,
            JsonDefaults.Options);
        after.GameTick = before.GameTick + 1;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static SnapshotEnvelope MoneyCropShippingSettlementAfter(
        SnapshotEnvelope before,
        int revenue)
    {
        var after = CloneSnapshot(before);
        var time = JsonNode.Parse(after.State["time"].GetRawText())!.AsObject();
        time["total_days"]!["value"] =
            time["total_days"]!["value"]!.GetValue<int>() + 1;
        after.State["time"] = JsonSerializer.SerializeToElement(
            time,
            JsonDefaults.Options);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        player["money"]!["value"] =
            checked(player["money"]!["value"]!.GetValue<int>() + revenue);
        player["total_money_earned"]!["value"] = checked(
            player["total_money_earned"]!["value"]!.GetValue<uint>() +
            (uint)revenue);
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!
            .AsObject();
        var bin = farm["shipping_bins"]!["value"]!.AsArray().Single()!
            .AsObject();
        bin["contents"] = new JsonArray();
        bin["pending_settlement_value"] = 0;
        bin["contents_signature"] = "empty-shipping-scope";
        after.State["farm"] = JsonSerializer.SerializeToElement(
            farm,
            JsonDefaults.Options);
        after.GameTick = before.GameTick + 1;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static int MoneyCropInventoryCount(
        SnapshotEnvelope snapshot,
        string qualifiedItemId) => JsonNode.Parse(
            snapshot.State["player"].GetRawText())!["inventory"]!["value"]!
            .AsArray()
            .Where(row => row?["qualified_item_id"]?.GetValue<string>() ==
                qualifiedItemId)
            .Sum(row => row!["stack"]!.GetValue<int>());

    private static int MoneyCropPlayerInt(
        SnapshotEnvelope snapshot,
        string field) => JsonNode.Parse(
            snapshot.State["player"].GetRawText())![field]!["value"]!
            .GetValue<int>();
}
