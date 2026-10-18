using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static SnapshotEnvelope AddSeedAcquisitionTransition(
        string root,
        List<MoneyRevenueCropNativeLifecycleTransitionRequest> transitions,
        SnapshotEnvelope before,
        int sequence,
        int purchaseQuantity,
        int unitPrice)
    {
        var prefix = "seed-acquisition-transition-" +
            sequence.ToString("D3");
        var beforePath = Path.Combine(root, prefix + "-before.json");
        var queuePath = Path.Combine(root, prefix + "-queue.json");
        var receiptPath = Path.Combine(root, prefix + "-receipt.json");
        var afterPath = Path.Combine(root, prefix + "-after.json");
        var continuation = SeedAcquisitionContinuationParameters(
            purchaseQuantity > 0 ? purchaseQuantity : 30,
            unitPrice);
        var queue = SeedAcquisitionQueue(
            prefix,
            before,
            continuation,
            purchaseQuantity > 0,
            unitPrice);
        var after = purchaseQuantity > 0
            ? SeedAcquisitionPurchase(
                before,
                purchaseQuantity,
                unitPrice)
            : SeedAcquisitionRoute(before);
        var selectedCandidateId = "candidate." + prefix;
        var receipt = SeedAcquisitionReceipt(
            queue,
            before,
            after,
            "run.seed-acquisition.fixture",
            selectedCandidateId);
        Write(beforePath, before);
        Write(queuePath, queue);
        Write(receiptPath, receipt);
        Write(afterPath, after);
        transitions.Add(new MoneyRevenueCropNativeLifecycleTransitionRequest(
            "seed_acquisition_stage",
            103,
            queuePath,
            beforePath,
            receiptPath,
            afterPath,
            "run.seed-acquisition.fixture",
            PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor,
            selectedCandidateId));
        return after;
    }

    private static SmallModelActionParameter[]
        SeedAcquisitionContinuationParameters(
            int outputStack,
            int unitPrice) =>
        new[]
        {
            SeedAcquisitionParameter(
                "continuation.option_id",
                "economy.buy_supplies"),
            SeedAcquisitionParameter(
                "continuation.shop_id",
                "SeedShop"),
            SeedAcquisitionParameter(
                "continuation.stock_id",
                "seed-shop:745"),
            SeedAcquisitionParameter(
                "continuation.qualified_item_id",
                "(O)745"),
            SeedAcquisitionParameter(
                "continuation.output_stack_per_purchase",
                outputStack.ToString()),
            SeedAcquisitionParameter(
                "continuation.output_quality",
                "0"),
            SeedAcquisitionParameter(
                "continuation.max_unit_price",
                unitPrice.ToString()),
            SeedAcquisitionParameter(
                "continuation.quantity",
                "1")
        };

    private static ActionQueueEnvelope SeedAcquisitionQueue(
        string prefix,
        SnapshotEnvelope before,
        SmallModelActionParameter[] continuation,
        bool purchase,
        int unitPrice)
    {
        var specifications = purchase
            ? new[]
            {
                (
                    OptionId: "executor.buy_shop_item",
                    Parameters: new[]
                    {
                        SeedAcquisitionParameter(
                            "qualified_item_id",
                            "(O)745"),
                        SeedAcquisitionParameter("quantity", "1"),
                        SeedAcquisitionParameter(
                            "expected_shop_id",
                            "SeedShop"),
                        SeedAcquisitionParameter(
                            "expected_stock_id",
                            "seed-shop:745"),
                        SeedAcquisitionParameter(
                            "expected_output_stack",
                            continuation.Single(value => value.Name ==
                                "continuation.output_stack_per_purchase").Value),
                        SeedAcquisitionParameter(
                            "expected_output_quality",
                            "0"),
                        SeedAcquisitionParameter(
                            "max_unit_price",
                            unitPrice.ToString()),
                        SeedAcquisitionParameter(
                            "expected_unit_price",
                            unitPrice.ToString())
                    }.Concat(continuation).ToArray()),
                (
                    OptionId: "executor.close_menu",
                    Parameters: Array.Empty<SmallModelActionParameter>())
            }
            : new[]
            {
                (
                    OptionId: "executor.traverse_connector",
                    Parameters: continuation.Concat(new[]
                    {
                        SeedAcquisitionParameter(
                            "connector_kind",
                            "map_warp"),
                        SeedAcquisitionParameter(
                            "expected_target_location",
                            "Town")
                    }).ToArray())
            };
        var items = specifications.Select((value, index) =>
            new ActionQueueItem
            {
                QueueItemId = prefix + ".item." + index,
                SourceActionId = prefix + ".action." + index,
                OptionId = value.OptionId,
                NormalizedCommand = new NormalizedCommand
                {
                    OptionId = value.OptionId,
                    StateHash = before.StateHash,
                    Parameters = value.Parameters,
                    Steps = new[]
                    {
                        new CompiledActionStep
                        {
                            StepId = prefix + ".step." + index,
                            StepType = value.OptionId["executor.".Length..],
                            Target = "fixture",
                            ExpectedEffect =
                                "fixture_seed_acquisition_transition",
                            EstimatedTicks = 1
                        }
                    }
                }
            }).ToArray();
        return new ActionQueueEnvelope
        {
            QueueId = prefix + ".queue",
            SourceModelOutputId = prefix + ".model-output",
            SourceModel = "seed-acquisition-self-test",
            StateHash = before.StateHash,
            GoalId = "earn_money",
            Status = "pending",
            Items = items
        };
    }

    private static QueueExecutionReceiptEnvelope SeedAcquisitionReceipt(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string runId,
        string selectedCandidateId)
    {
        var results = queue.Items.Select((item, index) =>
        {
            var sourceHash = index == 0
                ? before.StateHash
                : queue.QueueId + ".intermediate." + index;
            var afterHash = index == queue.Items.Length - 1
                ? after.StateHash
                : queue.QueueId + ".intermediate." + (index + 1);
            return new QueueExecutionStepReceipt
            {
                QueueItemIndex = index,
                QueueItemCount = queue.Items.Length,
                OriginalPlannedItemCount = queue.Items.Length,
                QueueId = queue.QueueId,
                QueueItemId = item.QueueItemId,
                OptionId = item.OptionId,
                SourceStateHash = sourceHash,
                CompiledCommandStateHash = before.StateHash,
                SelectedQueueCandidateCompleted =
                    index == queue.Items.Length - 1,
                AfterStateHash = afterHash,
                StateHashChanged = true,
                BeforeGameTick = before.GameTick + index,
                AfterGameTick = before.GameTick + index + 1,
                AfterSnapshotFresh = true,
                Status = "applied",
                PrimitiveKind = item.OptionId["executor.".Length..],
                PrimitiveVerificationStatus = "verified",
                PrimitiveVerificationReasons = new[]
                {
                    "native_seed_acquisition_transition_observed"
                },
                EffectiveQueueItem = JsonSerializer.SerializeToElement(
                    item,
                    JsonDefaults.Options),
                ChangedFacts = JsonSerializer.SerializeToElement(
                    new[] { "seed_acquisition_state_changed" },
                    JsonDefaults.Options)
            };
        }).ToArray();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = runId,
            QueueId = queue.QueueId,
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = queue.Items.Length,
            ExecutedItemCount = queue.Items.Length,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = queue.Items.Length,
            SelectedCandidateId = selectedCandidateId,
            SelectedCandidateCompleted = true,
            StepResults = results
        };
    }

    private static SnapshotEnvelope SeedAcquisitionRoute(
        SnapshotEnvelope before)
    {
        var after = CloneSnapshot(before);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        player["location_id"]!["value"] = "Town";
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);
        after.GameTick = before.GameTick + 1;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static SnapshotEnvelope SeedAcquisitionPurchase(
        SnapshotEnvelope before,
        int quantity,
        int unitPrice)
    {
        var after = CloneSnapshot(before);
        var player = JsonNode.Parse(after.State["player"].GetRawText())!
            .AsObject();
        var inventory = player["inventory"]!["value"]!.AsArray();
        var row = inventory.Single(value =>
            value!["qualified_item_id"]!.GetValue<string>() == "(O)745")!;
        row["stack"] = row["stack"]!.GetValue<int>() + quantity;
        player["money"]!["value"] =
            player["money"]!["value"]!.GetValue<int>() - unitPrice;
        player["shop_currency_balances"]!["value"]![0]!["balance"] =
            player["money"]!["value"]!.GetValue<int>();
        after.State["player"] = JsonSerializer.SerializeToElement(
            player,
            JsonDefaults.Options);

        var farm = JsonNode.Parse(after.State["farm"].GetRawText())!
            .AsObject();
        var graphRow = farm["material_inventory_graph"]!["value"]![
            "inventory_nodes"]![0]!["slots"]![0]!;
        graphRow["stack"] = graphRow["stack"]!.GetValue<int>() + quantity;
        after.State["farm"] = JsonSerializer.SerializeToElement(
            farm,
            JsonDefaults.Options);
        after.GameTick = before.GameTick + 2;
        after.StateHash = SnapshotHash.ComputeStateHash(after.State);
        return after;
    }

    private static SmallModelActionParameter SeedAcquisitionParameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
