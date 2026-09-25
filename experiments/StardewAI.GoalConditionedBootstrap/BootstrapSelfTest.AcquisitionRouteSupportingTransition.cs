using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyCropPlantingSupportingReceipt(
        AcquisitionRouteDispatchCompilation compilation,
        SnapshotEnvelope before)
    {
        var after = PlantedCropSnapshot(before, "(O)24");
        var execution = SupportingExecutionReceipt(
            compilation,
            before,
            after);
        var verified = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                execution,
                after,
                "run.crop-support.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(verified.Status ==
                    "verified_supporting_transition_fresh_replan_required" &&
                verified.QueueExecutionVerified &&
                verified.SupportingTransitionVerified &&
                verified.FreshReplanRequired &&
                !verified.TerminalReceiptEligible &&
                !verified.FormalTrainingAuthorized &&
                verified.CropPlantingTransition is
                {
                    Verified: true,
                    BeforeSeedQuantity: 3,
                    AfterSeedQuantity: 2,
                    SeedQuantityDecrease: 1,
                    BeforeTargetCropPresent: false,
                    AfterTargetCropPresent: true,
                    AfterCropDead: false,
                    AfterCropReadyForHarvest: false
                },
            "Exact crop planting did not produce a verified nonterminal receipt.");

        var wrongAfter = PlantedCropSnapshot(before, "(O)188");
        var wrongExecution = SupportingExecutionReceipt(
            compilation,
            before,
            wrongAfter);
        var rejected = AcquisitionRouteSupportingTransitionReceiptBuilder
            .BuildCore(
                compilation,
                before,
                wrongExecution,
                wrongAfter,
                "run.crop-support.self-test",
                PolicyTrajectoryVersionPins.RuntimeTestHarnessExecutor);
        Require(!rejected.SupportingTransitionVerified &&
                rejected.BlockingReasons.Contains(
                    "supporting_transition_after_crop_identity_mismatch",
                    StringComparer.Ordinal),
            "A planted crop with the wrong harvest identity was admitted.");
    }

    private static SnapshotEnvelope PlantedCropSnapshot(
        SnapshotEnvelope before,
        string harvestQualifiedItemId)
    {
        var state = before.State.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
        state["player"] = JsonSerializer.SerializeToElement(new
        {
            location_id = Field("Farm"),
            tile_x = Field(4),
            tile_y = Field(6),
            energy = Field(270),
            inventory = Field(Array.Empty<object>()),
            seed_inventory = Field(new[]
            {
                new
                {
                    slot_index = 0,
                    item_id = "472",
                    qualified_item_id = "(O)472",
                    seed_id = "472",
                    stack = 2
                }
            })
        }, JsonDefaults.Options);
        state["current_location"] = JsonSerializer.SerializeToElement(new
        {
            crops = Field(new[]
            {
                new
                {
                    location_id = "Farm",
                    tile_x = 5,
                    tile_y = 6,
                    harvest_item_id = harvestQualifiedItemId == "(O)24"
                        ? "24"
                        : "188",
                    harvest_item_qualified_id = harvestQualifiedItemId,
                    harvest_source_seed_id = "472",
                    dead = false,
                    ready_for_harvest = false
                }
            }),
            planting_context = Field(new
            {
                location_id = "Farm",
                hoe_dirt_tiles = new[]
                {
                    new
                    {
                        tile_x = 5,
                        tile_y = 6,
                        has_crop = true,
                        seed_results = Array.Empty<object>()
                    }
                }
            })
        }, JsonDefaults.Options);
        return new SnapshotEnvelope
        {
            GameVersion = before.GameVersion,
            SaveId = before.SaveId,
            PlayerId = before.PlayerId,
            GameTick = before.GameTick + 1,
            RealTimestamp = "2026-09-26T00:00:01Z",
            Completeness = "complete",
            State = state,
            StateHash = SnapshotHash.ComputeStateHash(state)
        };
    }

    private static QueueExecutionReceiptEnvelope SupportingExecutionReceipt(
        AcquisitionRouteDispatchCompilation compilation,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var queue = compilation.ActionQueue ?? throw new InvalidDataException(
            "Supporting-transition self-test queue is null.");
        var item = queue.Items.Single();
        return new QueueExecutionReceiptEnvelope
        {
            RunId = "run.crop-support.self-test",
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
            SelectedCandidateId = compilation.SelectedCandidateId,
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
                    PrimitiveKind = "plant_seed",
                    PrimitiveVerificationStatus = "verified",
                    PrimitiveVerificationReasons = new[]
                    {
                        "native_planting_state_transition_observed"
                    },
                    EffectiveQueueItem = JsonSerializer.SerializeToElement(
                        item,
                        JsonDefaults.Options),
                    ChangedFacts = JsonSerializer.SerializeToElement(new[]
                    {
                        "player.seed_inventory[472].stack=2",
                        "current_location.crops[5,6].harvest_source_seed_id=472"
                    })
                }
            }
        };
    }

    private static object Field<T>(T value) => new
    {
        value,
        status = "available"
    };
}
