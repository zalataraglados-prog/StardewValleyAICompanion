using System.Globalization;
using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static string VerifyMoneyRevenueShopRecurrenceArtifact(
        SnapshotEnvelope before,
        SnapshotEnvelope after,
        string fixtureId = "default",
        string qualifiedItemId = "(O)24",
        int slotIndex = 0,
        int quantity = 1,
        string shopId = "SeedShop")
    {
        var root = TempArtifactRoot(
            "money-revenue-shop-artifact",
            fixtureId);
        Directory.CreateDirectory(root);
        var beforePath = Path.Combine(root, "before.json");
        var afterPath = Path.Combine(root, "after.json");
        var resultPath = Path.Combine(root, "execution-result.json");
        var episodePath = Path.Combine(root, "execution-episode.json");
        Write(beforePath, before);
        Write(afterPath, after);
        var beforeMoney = before.State["player"].GetProperty("money")
            .GetProperty("value").GetInt32();
        var afterMoney = after.State["player"].GetProperty("money")
            .GetProperty("value").GetInt32();
        var saleValue = checked(afterMoney - beforeMoney);
        Require(saleValue > 0 && quantity > 0 && saleValue % quantity == 0,
            "Shop-sale recurrence fixture requires positive revenue.");
        var unitPrice = saleValue / quantity;
        var saleValueText = saleValue.ToString(
            CultureInfo.InvariantCulture);
        var unitPriceText = unitPrice.ToString(
            CultureInfo.InvariantCulture);
        var changes = new[]
        {
            new SimulatedFactChange
            {
                Path = "player.money",
                Before = beforeMoney.ToString(CultureInfo.InvariantCulture),
                After = afterMoney.ToString(CultureInfo.InvariantCulture)
            }
        };
        var reasons = new[]
        {
            "native_shop_menu_click_applied",
            "money_increased_by_exact_sale_value",
            "inventory_count_decreased_by_exact_stack"
        };
        var result = new TrainingExecutionResult
        {
            RunId = "run.revenue-shop-artifact",
            QueueId = "queue.revenue-shop-artifact",
            QueueItemId = "item.revenue-shop-artifact",
            BeforeStateHash = before.StateHash,
            OptionId = "executor.sell_shop_item",
            Status = "applied",
            FeedbackAvailable = true,
            PrimitiveKind = "sell_shop_item",
            PrimitiveVerificationStatus = "verified",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect =
                "shop_id=" + shopId + ";qualified_item_id=" +
                qualifiedItemId + ";quantity=" + quantity +
                ";expected_unit_price=" + unitPriceText,
            ObservedEffect = "money_increased_by_exact_sale_value",
            BlockReasons = Array.Empty<string>(),
            ChangedFacts = changes
        };
        Write(resultPath, result);
        var queueItem = new ActionQueueItem
        {
            QueueItemId = result.QueueItemId,
            OptionId = result.OptionId,
            Status = "pending",
            BlockingReasons = Array.Empty<string>(),
            NormalizedCommand = new NormalizedCommand
            {
                OptionId = result.OptionId,
                StateHash = before.StateHash,
                Parameters = new[]
                {
                    RevenueParameter(
                        "slot_index",
                        slotIndex.ToString(CultureInfo.InvariantCulture)),
                    RevenueParameter("qualified_item_id", qualifiedItemId),
                    RevenueParameter(
                        "quantity",
                        quantity.ToString(CultureInfo.InvariantCulture)),
                    RevenueParameter("expected_unit_price", unitPriceText),
                    RevenueParameter("expected_total_value", saleValueText),
                    RevenueParameter("expected_shop_id", shopId)
                },
                Steps = new[]
                {
                    new CompiledActionStep
                    {
                        StepId = result.QueueItemId + ":sell",
                        StepType = "sell_shop_item",
                        Target = shopId + ":" + qualifiedItemId,
                        ExpectedEffect =
                            "money_increased_by_exact_sale_value",
                        EstimatedTicks = 30
                    }
                }
            }
        };
        Write(episodePath, new PlanExecutionEpisodeEnvelope
        {
            EpisodeId = "episode.revenue-shop-artifact",
            RunId = result.RunId,
            SourceStateHash = before.StateHash,
            AfterStateHash = after.StateHash,
            StateHashChanged = true,
            BeforeGameTick = before.GameTick,
            AfterGameTick = after.GameTick,
            AfterSnapshotFresh = true,
            AfterSnapshotNote = "fresh",
            ExecutionResultPath = resultPath,
            BeforeSnapshotPath = beforePath,
            AfterSnapshotPath = afterPath,
            QueueId = result.QueueId,
            OptionId = result.OptionId,
            Status = result.Status,
            Success = true,
            TrainingRole = TrainingRoles.ExecutorCalibration,
            BlockReasons = Array.Empty<string>(),
            EffectiveQueueItem = JsonSerializer.SerializeToElement(
                queueItem,
                JsonDefaults.Options),
            PrimitiveKind = result.PrimitiveKind,
            PrimitiveVerificationStatus =
                result.PrimitiveVerificationStatus,
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = result.RequestedEffect,
            ObservedEffect = result.ObservedEffect,
            ChangedFacts = JsonSerializer.SerializeToElement(changes)
        });
        VerifyMoneyRevenueShopRecurrenceArtifact(
            root,
            episodePath,
            saleValue);
        return episodePath;
    }

    private static SmallModelActionParameter RevenueParameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
