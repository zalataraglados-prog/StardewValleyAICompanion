using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionReceiptBuilder
{
    private static AcquisitionCropPlantingTransitionEvidence VerifyCropPlanting(
        ActionQueueEnvelope queue,
        SnapshotEnvelope before,
        SnapshotEnvelope after)
    {
        var item = queue.Items.Single();
        var command = item.NormalizedCommand;
        var parameters = command?.Parameters;
        var reasons = new List<string>();
        if (item.OptionId != "executor.plant_seed" ||
            command is null ||
            command.Steps is not { Length: 1 } ||
            command.Steps[0].StepType != "plant_seed")
        {
            reasons.Add("supporting_transition_not_crop_planting");
        }
        var location = UniqueParameter(parameters, "target_location");
        var seedId = UniqueParameter(parameters, "seed_id");
        var sourceId = UniqueParameter(parameters, "acquisition_source_id");
        var harvestQualifiedId = UniqueParameter(
            parameters,
            "acquisition_qualified_item_id");
        var x = UniqueIntParameter(parameters, "target_tile_x");
        var y = UniqueIntParameter(parameters, "target_tile_y");
        if (string.IsNullOrWhiteSpace(location) ||
            string.IsNullOrWhiteSpace(seedId) ||
            sourceId != "crop:" + seedId ||
            string.IsNullOrWhiteSpace(harvestQualifiedId) ||
            !x.HasValue ||
            !y.HasValue)
        {
            reasons.Add("supporting_transition_crop_lineage_incomplete");
        }

        var beforeSeeds = SeedQuantity(before, seedId, out var beforeSeedResolved);
        var afterSeeds = SeedQuantity(after, seedId, out var afterSeedResolved);
        if (!beforeSeedResolved || !afterSeedResolved ||
            beforeSeeds < 1 || afterSeeds != beforeSeeds - 1)
        {
            reasons.Add("supporting_transition_seed_delta_not_exactly_one");
        }
        var beforeCrop = CropAt(before, location, x, y, out var beforeCropResolved);
        var afterCrop = CropAt(after, location, x, y, out var afterCropResolved);
        if (!beforeCropResolved || !afterCropResolved)
            reasons.Add("supporting_transition_crop_state_unavailable");
        if (beforeCrop.HasValue)
            reasons.Add("supporting_transition_target_crop_already_present");
        var afterDead = false;
        var afterReady = false;
        var afterDeadResolved = afterCrop.HasValue &&
            TryReadBool(afterCrop.Value, "dead", out afterDead);
        var afterReadyResolved = afterCrop.HasValue &&
            TryReadBool(
                afterCrop.Value,
                "ready_for_harvest",
                out afterReady);
        if (!afterCrop.HasValue ||
            ReadString(afterCrop.Value, "harvest_source_seed_id") != seedId ||
            ReadString(afterCrop.Value, "harvest_item_qualified_id") !=
                harvestQualifiedId ||
            !afterDeadResolved ||
            !afterReadyResolved ||
            afterDead ||
            afterReady)
        {
            reasons.Add("supporting_transition_after_crop_identity_mismatch");
        }

        return new AcquisitionCropPlantingTransitionEvidence
        {
            TargetLocationId = location,
            TargetTileX = x,
            TargetTileY = y,
            SeedId = seedId,
            HarvestItemQualifiedId = harvestQualifiedId,
            BeforeSeedQuantity = beforeSeedResolved ? beforeSeeds : null,
            AfterSeedQuantity = afterSeedResolved ? afterSeeds : null,
            SeedQuantityDecrease = beforeSeedResolved && afterSeedResolved
                ? beforeSeeds - afterSeeds
                : null,
            BeforeTargetCropPresent = beforeCropResolved
                ? beforeCrop.HasValue
                : null,
            AfterTargetCropPresent = afterCropResolved
                ? afterCrop.HasValue
                : null,
            AfterCropDead = afterDeadResolved
                ? afterDead
                : null,
            AfterCropReadyForHarvest = afterReadyResolved
                ? afterReady
                : null,
            Resolved = beforeSeedResolved && afterSeedResolved &&
                beforeCropResolved && afterCropResolved,
            Verified = reasons.Count == 0,
            BlockingReasons = reasons.Distinct(StringComparer.Ordinal).ToArray()
        };
    }

    private static AcquisitionCropPlantingTransitionEvidence BlockedTransition(
        string reason) => new()
        {
            BlockingReasons = new[] { reason }
        };

    private static int SeedQuantity(
        SnapshotEnvelope snapshot,
        string seedId,
        out bool resolved)
    {
        resolved = TryStateFieldValue(
            snapshot,
            "player",
            "seed_inventory",
            out var value) && value.ValueKind == JsonValueKind.Array;
        if (!resolved)
            return 0;
        return value.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                (ReadString(row, "seed_id") == seedId ||
                 ReadString(row, "item_id") == seedId))
            .Sum(row => Math.Max(0, ReadInt(row, "stack")));
    }

    private static JsonElement? CropAt(
        SnapshotEnvelope snapshot,
        string location,
        int? x,
        int? y,
        out bool resolved)
    {
        resolved = TryStateFieldValue(
            snapshot,
            "current_location",
            "crops",
            out var value) && value.ValueKind == JsonValueKind.Array &&
            TryStateString(snapshot, "player", "location_id", out var current) &&
            current == location && x.HasValue && y.HasValue;
        if (!resolved)
            return null;
        var matches = value.EnumerateArray()
            .Where(row => row.ValueKind == JsonValueKind.Object &&
                ReadInt(row, "tile_x") == x &&
                ReadInt(row, "tile_y") == y)
            .ToArray();
        resolved = matches.Length <= 1;
        return matches.Length == 1 ? matches[0] : null;
    }
}
