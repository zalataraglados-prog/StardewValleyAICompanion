using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static QueueExecutionReceiptEnvelope TargetDateQueueReceipt(
        ActionQueueEnvelope queue,
        ActionQueueItem item,
        SnapshotEnvelope before,
        string afterStateHash,
        long afterGameTick,
        string selectedCandidateId,
        string runId = "run.target-date-shop")
    {
        var primitiveKind = item.NormalizedCommand.Steps.Single().StepType;
        var changedFacts = JsonSerializer.SerializeToElement(
            new[]
            {
                new
                {
                    fact = "player.inventory",
                    transition = "exact_quantity_increased"
                }
            },
            JsonDefaults.Options);
        return new QueueExecutionReceiptEnvelope
        {
            RunId = runId,
            QueueId = queue.QueueId,
            SourceStateHash = before.StateHash,
            AfterStateHash = afterStateHash,
            BeforeGameTick = before.GameTick,
            AfterGameTick = afterGameTick,
            AfterSnapshotFresh = true,
            QueueExecutionMode = "sequential_queue_items",
            Status = "applied",
            Success = true,
            PlannedItemCount = 1,
            ExecutedItemCount = 1,
            FinalPendingItemCount = 0,
            MaxQueueItemAttempts = 1,
            SelectedCandidateId = selectedCandidateId,
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
                    AfterStateHash = afterStateHash,
                    StateHashChanged = true,
                    BeforeGameTick = before.GameTick,
                    AfterGameTick = afterGameTick,
                    AfterSnapshotFresh = true,
                    Status = "applied",
                    PrimitiveKind = primitiveKind,
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "fixture_native_terminal_verified"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        item,
                        JsonDefaults.Options),
                    ChangedFacts = changedFacts
                }
            }
        };
    }

    private static void WriteTargetDateInventoryAfterSnapshot(
        string beforeSnapshotPath,
        string afterSnapshotPath,
        string afterStateHash,
        string qualifiedItemId,
        int stack,
        int quality)
    {
        Require(stack > 0,
            "Fresh-receipt quantity fixture requires a positive stack.");
        var root = JsonNode.Parse(
            File.ReadAllText(beforeSnapshotPath))!.AsObject();
        root["state_hash"] = afterStateHash;
        root["game_tick"] = root["game_tick"]!.GetValue<long>() + 1;
        var inventory = root["state"]!["player"]!["inventory"]!["value"]!
            .AsArray();
        inventory.Add(JsonSerializer.SerializeToNode(new
        {
            qualified_item_id = qualifiedItemId,
            stack,
            quality
        }, JsonDefaults.Options));
        File.WriteAllText(
            afterSnapshotPath,
            root.ToJsonString(JsonDefaults.Options));
    }

    private static AcquisitionRouteTargetDateUnlock TargetDateRequirementRoute(
        AcquisitionRouteTargetDateOpportunityCost route) =>
        route.UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute.UpstreamRoute.UpstreamRoute
            .UpstreamRoute.UpstreamRoute;
}
