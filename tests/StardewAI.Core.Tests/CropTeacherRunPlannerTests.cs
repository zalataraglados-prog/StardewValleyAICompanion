using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

namespace StardewAI.Core.Tests;

public sealed class CropTeacherRunPlannerTests
{
    private const string CommitmentId = "crop:parsnip:first";
    private const string SeedId = "(O)472";

    [Theory]
    [InlineData("crop_teacher_water_target_crop_missing", true)]
    [InlineData("crop_teacher_harvest_target_not_ready_on_planned_day", true)]
    [InlineData("crop_teacher_run_day_advanced_with_unsettled_harvest", true)]
    [InlineData("crop_teacher_run_day_advanced_without_verified_save_boundary", false)]
    [InlineData("unexpected_failure", false)]
    public void ClassifiesOnlyStrategicDriftForTeacherRedispatch(
        string reason,
        bool expected)
    {
        Assert.Equal(
            expected,
            CropTeacherRunPlanner.IsTeacherRedispatchReason(reason));
    }

    [Fact]
    public void RejectsAResumeSnapshotBeforeThePlansFirstDay()
    {
        var (plan, request) = BuildPlan();
        var snapshot = Snapshot("FarmHouse", 0, [], totalDay: 0);

        Assert.Equal(
            "crop_teacher_snapshot_precedes_first_run_day",
            CropTeacherRunPlanner.GetResumeSnapshotBlockReason(
                plan,
                snapshot));
        var error = Assert.Throws<InvalidDataException>(() =>
            Evaluate(plan, request, snapshot));
        Assert.Equal(
            "crop_teacher_snapshot_precedes_first_run_day",
            error.Message);
    }

    [Fact]
    public void AdvancesThroughTheAuthoritativeFirstDayWithoutReplayingCompletedWork()
    {
        var (plan, request) = BuildPlan();

        var acquire = Evaluate(plan, request, Snapshot("FarmHouse", 0, []));
        Assert.Equal(CropTeacherRunStageKinds.AcquireSeed, acquire.Stage);
        Assert.Equal(2, acquire.RemainingQuantity);
        Assert.Equal(SeedId, acquire.SeedQualifiedItemId);
        Assert.Equal("SeedShop", acquire.ShopId);

        var routeFarm = Evaluate(plan, request, Snapshot("FarmHouse", 2, []));
        Assert.Equal(CropTeacherRunStageKinds.RouteToFarm, routeFarm.Stage);
        Assert.Equal("Farm", routeFarm.TargetLocationId);

        var prepare = Evaluate(plan, request, Snapshot("Farm", 2, []));
        Assert.Equal(CropTeacherRunStageKinds.PrepareSite, prepare.Stage);
        Assert.Equal(CommitmentId, prepare.CommitmentId);

        var prepared = new HashSet<string>(StringComparer.Ordinal) { CommitmentId };
        var plant = Evaluate(plan, request, Snapshot("Farm", 2, []), prepared);
        Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, plant.Stage);
        Assert.Equal("executor.plant_seed", plant.ActionBatch!.OptionId);
        Assert.Equal(2, plant.ActionBatch.Tiles.Length);

        var oneCrop = new[] { Crop(10, 12, needsWatering: true) };
        var remainingPlant = Evaluate(
            plan,
            request,
            Snapshot("Farm", 1, oneCrop),
            prepared);
        Assert.Equal("executor.plant_seed", remainingPlant.ActionBatch!.OptionId);
        Assert.Single(remainingPlant.ActionBatch.Tiles);
        Assert.Equal(11, remainingPlant.ActionBatch.Tiles[0].TileX);

        var cropsNeedWater = new[]
        {
            Crop(10, 12, needsWatering: true),
            Crop(11, 12, needsWatering: true)
        };
        var water = Evaluate(
            plan,
            request,
            Snapshot("Farm", 0, cropsNeedWater),
            prepared);
        Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, water.Stage);
        Assert.Equal("executor.water_crop", water.ActionBatch!.OptionId);
        Assert.Equal(2, water.ActionBatch.Tiles.Length);

        var wateredCrops = new[]
        {
            Crop(10, 12, needsWatering: false),
            Crop(11, 12, needsWatering: false)
        };
        var routeHome = Evaluate(
            plan,
            request,
            Snapshot("Farm", 0, wateredCrops),
            prepared);
        Assert.Equal(CropTeacherRunStageKinds.RouteHome, routeHome.Stage);
        Assert.Equal("FarmHouse", routeHome.TargetLocationId);

        var sleep = Evaluate(
            plan,
            request,
            Snapshot("FarmHouse", 0, wateredCrops),
            prepared);
        Assert.Equal(CropTeacherRunStageKinds.NativeSleep, sleep.Stage);

        var nextDayWater = Evaluate(
            plan,
            request,
            Snapshot("Farm", 0, cropsNeedWater, totalDay: 2),
            prepared,
            completedDayBoundaries: DayBoundariesThrough(2));
        Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, nextDayWater.Stage);
        Assert.Equal("executor.water_crop", nextDayWater.ActionBatch!.OptionId);

        var harvestCrops = new[]
        {
            Crop(10, 12, needsWatering: false, readyForHarvest: true),
            Crop(11, 12, needsWatering: false, readyForHarvest: true)
        };
        var harvest = Evaluate(
            plan,
            request,
            Snapshot("Farm", 0, harvestCrops, totalDay: 4),
            prepared,
            completedDayBoundaries: DayBoundariesThrough(4));
        Assert.Equal(CropTeacherRunStageKinds.ExecuteCropBatch, harvest.Stage);
        Assert.Equal("executor.harvest_crop", harvest.ActionBatch!.OptionId);

        var receipts = harvest.ActionBatch.Tiles.Select(tile =>
            CropTeacherTransitionLedger.HarvestTileKey(
                CommitmentId,
                4,
                tile.TileX,
                tile.TileY)).ToHashSet(StringComparer.Ordinal);
        var complete = Evaluate(
            plan,
            request,
            Snapshot("Farm", 0, [], totalDay: 4),
            prepared,
            receipts,
            DayBoundariesThrough(4));
        Assert.Equal(CropTeacherRunStageKinds.Complete, complete.Stage);
    }

    [Fact]
    public void FailsClosedWhenAnExistingCropHasTheWrongSeedIdentity()
    {
        var (plan, request) = BuildPlan();
        var wrongCrop = new[]
        {
            new
            {
                location_id = "Farm",
                tile_x = 10,
                tile_y = 12,
                harvest_source_seed_id = "473",
                needs_watering = true
            }
        };

        var error = Assert.Throws<InvalidDataException>(() => Evaluate(
            plan,
            request,
            Snapshot("Farm", 2, wrongCrop),
            new HashSet<string>(StringComparer.Ordinal) { CommitmentId }));

        Assert.Equal(
            "crop_teacher_existing_crop_seed_identity_mismatch",
            error.Message);
    }

    [Fact]
    public void RequiresStrategicReplanWhenAPlannedCropDisappearsOvernight()
    {
        var (plan, request) = BuildPlan();
        var step = Evaluate(
            plan,
            request,
            Snapshot(
                "Farm",
                0,
                [Crop(10, 12, needsWatering: true)],
                totalDay: 2),
            new HashSet<string>(StringComparer.Ordinal) { CommitmentId },
            completedDayBoundaries: DayBoundariesThrough(2));

        Assert.Equal(CropTeacherRunStageKinds.ReplanRequired, step.Stage);
        Assert.Equal(
            "crop_teacher_water_target_crop_missing",
            step.Reason);
    }

    [Fact]
    public void RefusesToStartTheNextDayWithoutADurableSaveReceipt()
    {
        var (plan, request) = BuildPlan();
        var crops = new[]
        {
            Crop(10, 12, needsWatering: true),
            Crop(11, 12, needsWatering: true)
        };

        var step = Evaluate(
            plan,
            request,
            Snapshot("Farm", 0, crops, totalDay: 2));

        Assert.Equal(CropTeacherRunStageKinds.ReplanRequired, step.Stage);
        Assert.Equal(
            "crop_teacher_run_day_advanced_without_verified_save_boundary",
            step.Reason);
    }

    [Fact]
    public void DoesNotTreatAMissingHarvestCropAsCompleteWithoutAReceipt()
    {
        var (plan, request) = BuildPlan();
        var step = Evaluate(
            plan,
            request,
            Snapshot("Farm", 0, [], totalDay: 4),
            completedDayBoundaries: DayBoundariesThrough(4));

        Assert.Equal(CropTeacherRunStageKinds.ReplanRequired, step.Stage);
        Assert.Equal(
            "crop_teacher_harvest_target_crop_missing_without_receipt",
            step.Reason);
    }

    [Fact]
    public void RequiresReplanAfterAdvancingPastAnUnsettledHarvest()
    {
        var (plan, request) = BuildPlan();
        var step = Evaluate(
            plan,
            request,
            Snapshot("FarmHouse", 0, [], totalDay: 5),
            completedDayBoundaries: DayBoundariesThrough(5));

        Assert.Equal(CropTeacherRunStageKinds.ReplanRequired, step.Stage);
        Assert.Equal(
            "crop_teacher_run_day_advanced_with_unsettled_harvest",
            step.Reason);
    }

    private static CropTeacherRunStep Evaluate(
        MoneyRevenueCropNativeProductionRunPlan plan,
        MoneyRevenueCropNativeProductionRunPlanRequest request,
        SnapshotEnvelope snapshot,
        IReadOnlySet<string>? prepared = null,
        IReadOnlySet<string>? harvestReceipts = null,
        IReadOnlySet<string>? completedDayBoundaries = null) =>
        CropTeacherRunPlanner.Evaluate(
            plan,
            request,
            snapshot,
            prepared,
            harvestReceipts,
            completedDayBoundaries);

    private static IReadOnlySet<string> DayBoundariesThrough(
        int currentTotalDay) => Enumerable.Range(
            1,
            Math.Max(0, currentTotalDay - 1))
        .Select(CropTeacherTransitionLedger.DayBoundaryKey)
        .ToHashSet(StringComparer.Ordinal);

    private static (MoneyRevenueCropNativeProductionRunPlan Plan,
        MoneyRevenueCropNativeProductionRunPlanRequest Request) BuildPlan()
    {
        var tiles = new[]
        {
            new MoneyRevenueCropPlantingTile(10, 12),
            new MoneyRevenueCropPlantingTile(11, 12)
        };
        var request = new MoneyRevenueCropNativeProductionRunPlanRequest
        {
            MaxQueueItemCount = 8,
            LayoutIntents =
            [
                new MoneyRevenueCropNativeProductionLayoutIntent(
                    CommitmentId,
                    "Farm",
                    tiles,
                    null,
                    new MoneyRevenueCropPlantingTile(9, 14))
            ],
            SeedAcquisitionIntents =
            [
                new MoneyRevenueCropNativeSeedAcquisitionIntent(
                    CommitmentId,
                    SeedId,
                    "SeedShop",
                    20)
            ]
        };
        var plant = new MoneyRevenueCropNativeProductionActionBatch(
            1,
            CommitmentId,
            "executor.plant_seed",
            "always",
            "Farm",
            SeedId,
            tiles);
        var water = new MoneyRevenueCropNativeProductionActionBatch(
            2,
            CommitmentId,
            "executor.water_crop",
            "crop_needs_watering",
            "Farm",
            string.Empty,
            tiles);
        var harvest = new MoneyRevenueCropNativeProductionActionBatch(
            1,
            CommitmentId,
            "executor.harvest_crop",
            "always",
            "Farm",
            string.Empty,
            tiles);
        var plan = new MoneyRevenueCropNativeProductionRunPlan
        {
            Status = "native_crop_production_run_ready",
            SourceTotalDay = 1,
            LocationId = "Farm",
            CoveredCommitmentIds = [CommitmentId],
            FirstTotalDay = 1,
            LastTotalDay = 4,
            DayCount = 4,
            ActionBatchCount = 5,
            ActionCount = 10,
            MaxQueueItemCount = 8,
            SeedAcquisitionCount = 1,
            SeedAcquisitions =
            [
                new MoneyRevenueCropNativeSeedAcquisition(
                    1,
                    CommitmentId,
                    SeedId,
                    "SeedShop",
                    20,
                    2,
                    1,
                    "economy.buy_supplies_one_unit_fresh_snapshot_slices")
            ],
            Days =
            [
                new MoneyRevenueCropNativeProductionRunDay(
                    1,
                    [CommitmentId],
                    true,
                    [plant, water],
                    true),
                new MoneyRevenueCropNativeProductionRunDay(
                    2,
                    [],
                    true,
                    [water with { BatchIndex = 1 }],
                    true),
                new MoneyRevenueCropNativeProductionRunDay(
                    3,
                    [],
                    true,
                    [water with { BatchIndex = 1 }],
                    true),
                new MoneyRevenueCropNativeProductionRunDay(
                    4,
                    [],
                    true,
                    [harvest],
                    false)
            ],
            NativeProductionRunReady = true
        };
        return (plan, request);
    }

    private static SnapshotEnvelope Snapshot(
        string locationId,
        int seedQuantity,
        object[] crops,
        int totalDay = 1)
    {
        var inventory = seedQuantity > 0
            ? new object[]
            {
                new
                {
                    slot_index = 0,
                    qualified_item_id = SeedId,
                    stack = seedQuantity,
                    is_empty = false
                }
            }
            : Array.Empty<object>();
        return new SnapshotEnvelope
        {
            StateHash = $"state:{totalDay}:{locationId}:{seedQuantity}:{crops.Length}",
            State = new Dictionary<string, JsonElement>
            {
                ["time"] = Domain(new { total_days = Field(totalDay) }),
                ["player"] = Domain(new
                {
                    location_id = Field(locationId),
                    inventory = Field(inventory)
                }),
                ["farm"] = Domain(new { crops = Field(crops) }),
                ["current_location"] = Domain(new
                {
                    home_context = Field(new
                    {
                        home_location_id = "FarmHouse",
                        current_location_is_home = locationId == "FarmHouse"
                    })
                })
            }
        };
    }

    private static object Crop(
        int x,
        int y,
        bool needsWatering,
        bool readyForHarvest = false) => new
    {
        location_id = "Farm",
        tile_x = x,
        tile_y = y,
        harvest_source_seed_id = "472",
        needs_watering = needsWatering,
        ready_for_harvest = readyForHarvest
    };

    private static object Field(object value) => new
    {
        value,
        status = "available",
        source = new { kind = "game_object", path = "test" },
        adapter = "test",
        read_at_tick = 1,
        confidence = 1
    };

    private static JsonElement Domain(object value) =>
        JsonSerializer.SerializeToElement(value);
}
