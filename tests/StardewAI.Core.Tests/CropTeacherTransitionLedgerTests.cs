using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CropTeacherTransitionLedgerTests
{
    [Fact]
    public void PersistsOnlyVerifiedNativeHarvestTilesAndResumesWithoutReplay()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-crop-ledger-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var planPath = Path.Combine(root, "run-plan.json");
        var ledgerPath = Path.Combine(root, "transition-ledger.json");
        try
        {
            File.WriteAllText(planPath, "{\"plan\":1}");
            var ledger = CropTeacherTransitionLedger.Open(
                ledgerPath,
                planPath);
            var step = HarvestStep();
            var execution = new JsonObject
            {
                ["step_results"] = new JsonArray(
                    HarvestResult(10, 12, verified: true),
                    HarvestResult(11, 12, verified: false))
            };

            var appended = ledger.RecordVerifiedHarvestResults(
                execution,
                step,
                "2026-10-24T00:00:00.0000000+00:00");

            Assert.Equal(1, appended);
            Assert.Single(ledger.HarvestReceipts);
            Assert.Contains(
                CropTeacherTransitionLedger.HarvestTileKey(
                    "crop:parsnip:first",
                    4,
                    10,
                    12),
                ledger.CompletedHarvestTileKeys);
            Assert.DoesNotContain(
                CropTeacherTransitionLedger.HarvestTileKey(
                    "crop:parsnip:first",
                    4,
                    11,
                    12),
                ledger.CompletedHarvestTileKeys);
            var boundary = new CropTeacherDayBoundaryReceipt(
                CropTeacherTransitionLedger.DayBoundaryKey(3),
                3,
                4,
                "1:spring:4",
                "1:spring:5",
                "queue:sleep",
                "item:sleep",
                "state:before:sleep",
                "state:after:sleep",
                "save:before:sleep",
                "save:after:sleep",
                "2026-10-24T00:00:30.0000000+00:00");
            Assert.True(ledger.RecordVerifiedDayBoundary(boundary));
            Assert.False(ledger.RecordVerifiedDayBoundary(boundary));
            Assert.Single(ledger.DayBoundaryReceipts);

            var resumed = CropTeacherTransitionLedger.Open(
                ledgerPath,
                planPath);
            Assert.Single(resumed.HarvestReceipts);
            Assert.Single(resumed.DayBoundaryReceipts);
            Assert.Equal(
                0,
                resumed.RecordVerifiedHarvestResults(
                    execution,
                    step,
                    "2026-10-24T00:01:00.0000000+00:00"));
            Assert.Single(resumed.HarvestReceipts);
        }
        finally
        {
            if (File.Exists(ledgerPath))
            {
                File.Delete(ledgerPath);
            }
            if (File.Exists(planPath))
            {
                File.Delete(planPath);
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root);
            }
        }
    }

    [Fact]
    public void RejectsALedgerAfterItsBoundRunPlanChanges()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-crop-ledger-binding-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var planPath = Path.Combine(root, "run-plan.json");
        var ledgerPath = Path.Combine(root, "transition-ledger.json");
        try
        {
            File.WriteAllText(planPath, "{\"plan\":1}");
            var ledger = CropTeacherTransitionLedger.Open(
                ledgerPath,
                planPath);
            ledger.RecordVerifiedHarvestResults(
                new JsonObject
                {
                    ["step_results"] = new JsonArray(
                        HarvestResult(10, 12, verified: true))
                },
                HarvestStep(),
                "2026-10-24T00:00:00.0000000+00:00");
            File.WriteAllText(planPath, "{\"plan\":2}");

            var error = Assert.Throws<InvalidDataException>(() =>
                CropTeacherTransitionLedger.Open(ledgerPath, planPath));

            Assert.Equal(
                "crop_teacher_transition_ledger_binding_invalid",
                error.Message);
        }
        finally
        {
            if (File.Exists(ledgerPath))
            {
                File.Delete(ledgerPath);
            }
            if (File.Exists(planPath))
            {
                File.Delete(planPath);
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root);
            }
        }
    }

    [Fact]
    public void PersistsRealShapedMultiStepHarvestReceiptsFromQueueBindings()
    {
        var root = Path.Combine(
            Path.GetTempPath(),
            "stardewai-crop-ledger-real-receipts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var planPath = Path.Combine(root, "run-plan.json");
        var ledgerPath = Path.Combine(root, "transition-ledger.json");
        try
        {
            File.WriteAllText(planPath, "{\"plan\":1}");
            var ledger = CropTeacherTransitionLedger.Open(ledgerPath, planPath);
            var step = HarvestStep(
                new MoneyRevenueCropPlantingTile(71, 16),
                new MoneyRevenueCropPlantingTile(70, 17),
                new MoneyRevenueCropPlantingTile(71, 17));
            var execution = new JsonObject
            {
                ["feedback_available"] = true,
                ["step_results"] = new JsonArray(
                    QueueBoundHarvestResult(0, 71, 16),
                    QueueBoundHarvestResult(1, 70, 17),
                    QueueBoundHarvestResult(2, 71, 17))
            };

            Assert.Equal(
                3,
                ledger.RecordVerifiedHarvestResults(
                    execution,
                    step,
                    "2026-10-24T01:00:00.0000000+00:00"));
            Assert.Equal(3, ledger.HarvestReceipts.Count);

            var resumed = CropTeacherTransitionLedger.Open(ledgerPath, planPath);
            Assert.Equal(3, resumed.HarvestReceipts.Count);
            Assert.Equal(
                0,
                resumed.RecordVerifiedHarvestResults(
                    execution,
                    step,
                    "2026-10-24T01:01:00.0000000+00:00"));
            Assert.Equal(3, resumed.HarvestReceipts.Count);
        }
        finally
        {
            if (File.Exists(ledgerPath))
            {
                File.Delete(ledgerPath);
            }
            if (File.Exists(planPath))
            {
                File.Delete(planPath);
            }
            if (Directory.Exists(root))
            {
                Directory.Delete(root);
            }
        }
    }

    private static CropTeacherRunStep HarvestStep(
        params MoneyRevenueCropPlantingTile[] requestedTiles)
    {
        var tiles = requestedTiles.Length > 0
            ? requestedTiles
            :
            [
                new MoneyRevenueCropPlantingTile(10, 12),
                new MoneyRevenueCropPlantingTile(11, 12)
            ];
        var batch = new MoneyRevenueCropNativeProductionActionBatch(
            1,
            "crop:parsnip:first",
            "executor.harvest_crop",
            "always",
            "Farm",
            string.Empty,
            tiles);
        return new CropTeacherRunStep(
            CropTeacherRunStageKinds.ExecuteCropBatch,
            4,
            batch.CommitmentId,
            batch.LocationId,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            batch.Tiles.Length,
            batch,
            "test");
    }

    private static JsonObject HarvestResult(
        int tileX,
        int tileY,
        bool verified) => new()
    {
        ["option_id"] = "executor.harvest_crop",
        ["status"] = verified ? "applied" : "blocked",
        ["feedback_available"] = true,
        ["primitive_verification_status"] = verified ? "verified" : "blocked",
        ["target_location"] = "Farm",
        ["target_tile_x"] = tileX,
        ["target_tile_y"] = tileY,
        ["queue_id"] = "queue:test",
        ["queue_item_id"] = $"item:{tileX}:{tileY}",
        ["before_state_hash"] = $"before:{tileX}:{tileY}",
        ["after_state_hash"] = $"after:{tileX}:{tileY}",
        ["observed_effect"] = "native_harvest_verified",
        ["effective_queue_item"] = new JsonObject
        {
            ["normalized_command"] = new JsonObject
            {
                ["parameters"] = new JsonArray(
                    new JsonObject
                    {
                        ["name"] = "action_commitment_id",
                        ["value"] = "crop:parsnip:first"
                    })
            }
        }
    };

    private static JsonObject QueueBoundHarvestResult(
        int index,
        int tileX,
        int tileY) => new()
    {
        ["queue_item_index"] = index,
        ["queue_item_id"] = $"queue_item:{index}",
        ["option_id"] = "executor.harvest_crop",
        ["status"] = "applied",
        ["primitive_verification_status"] = "verified",
        ["feedback_available"] = true,
        ["before_state_hash"] = $"before:{index}",
        ["after_state_hash"] = $"after:{index}",
        ["queue_id"] = "queue:real-shaped",
        ["target_location"] = string.Empty,
        ["observed_effect"] = "has_crop=false;ready_for_harvest=false",
        ["effective_queue_item"] = new JsonObject
        {
            ["normalized_command"] = new JsonObject
            {
                ["parameters"] = new JsonArray(
                    Parameter("target_tile_x", tileX.ToString()),
                    Parameter("target_tile_y", tileY.ToString()),
                    Parameter("target_location", "Farm"),
                    Parameter("action_commitment_id", "crop:parsnip:first"))
            }
        }
    };

    private static JsonObject Parameter(string name, string value) => new()
    {
        ["name"] = name,
        ["value"] = value
    };
}
