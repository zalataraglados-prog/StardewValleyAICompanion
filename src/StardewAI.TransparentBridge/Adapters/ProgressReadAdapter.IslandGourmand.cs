using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;
using StardewValley.TerrainFeatures;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private const string IslandGourmandSchemaVersion = "island_gourmand_progress.v1";
    private const string IslandGourmandRequiredMail = "Island_UpgradeHouse";

    private static IslandGourmandProgressRef? ReadIslandGourmand(
        Farmer? master,
        IslandFarmCave? cave,
        IslandWest? west)
    {
        if (master is null || cave is null)
            return null;

        var fulfilled = Math.Clamp(
            cave.gourmandRequestsFulfilled.Value,
            0,
            IslandFarmCave.TOTAL_GOURMAND_REQUESTS);
        var complete = fulfilled >= IslandFarmCave.TOTAL_GOURMAND_REQUESTS;
        var houseUnlocked = master.hasOrWillReceiveMail(IslandGourmandRequiredMail);
        var requestItemId = complete ? string.Empty : GourmandRequestItemId(fulfilled);
        var matchingCrops = houseUnlocked && !complete && west is not null
            ? ReadGourmandMatchingCrops(west, requestItemId)
            : Array.Empty<IslandGourmandCropRef>();
        var readyCount = matchingCrops.Count(crop => crop.ReadyForRequest);

        return new IslandGourmandProgressRef
        {
            SchemaVersion = IslandGourmandSchemaVersion,
            ProjectionStatus = complete
                ? "complete_native_three_requests"
                : !houseUnlocked
                    ? "blocked_island_house_upgrade_missing"
                    : west is null
                        ? "blocked_island_west_unavailable"
                        : "exact_native_active_request_projected",
            RequiredMailSatisfied = houseUnlocked,
            TalkedToGourmandReceivedOrPending = master.hasOrWillReceiveMail("talkedToGourmand"),
            RequestsFulfilled = fulfilled,
            TotalRequests = IslandFarmCave.TOTAL_GOURMAND_REQUESTS,
            Complete = complete,
            ActiveRequestIndex = complete ? null : fulfilled,
            RequiredCropItemId = requestItemId,
            RequiredCropQualifiedItemId = string.IsNullOrWhiteSpace(requestItemId)
                ? string.Empty
                : "(O)" + requestItemId,
            RequiredCropDisplayName = string.IsNullOrWhiteSpace(requestItemId)
                ? string.Empty
                : ItemRegistry.GetDataOrErrorItem("(O)" + requestItemId).DisplayName,
            RequestState = complete
                ? "complete"
                : !houseUnlocked
                    ? "locked"
                    : readyCount > 0
                        ? "ready_matching_crop_present"
                        : matchingCrops.Length > 0
                            ? "matching_crop_growing"
                            : "matching_crop_absent",
            MatchingCropCount = matchingCrops.Length,
            ReadyMatchingCropCount = readyCount,
            MatchingCrops = matchingCrops,
            ActionTiles = ReadGourmandActionTiles(cave),
            NativeContract = "IslandFarmCave.performAction_Gourmand_then_Gourmand_Yes_then_OnRequestGourmandCheck_then_native_event_then_GiveReward"
        };
    }

    private static string GourmandRequestItemId(int requestIndex) => requestIndex switch
    {
        0 => "254",
        1 => "262",
        2 => "248",
        _ => string.Empty
    };

    private static IslandGourmandCropRef[] ReadGourmandMatchingCrops(
        IslandWest west,
        string requestItemId)
    {
        if (string.IsNullOrWhiteSpace(requestItemId))
            return Array.Empty<IslandGourmandCropRef>();

        return west.terrainFeatures.Pairs
            .Where(pair => pair.Value is HoeDirt { crop: not null } dirt &&
                dirt.crop.indexOfHarvest.Value == requestItemId)
            .Select(pair =>
            {
                var crop = ((HoeDirt)pair.Value).crop;
                var finalPhaseIndex = crop.phaseDays.Count - 1;
                var ready = crop.currentPhase.Value >= finalPhaseIndex &&
                    (!crop.fullyGrown.Value || crop.dayOfCurrentPhase.Value <= 0);
                return new IslandGourmandCropRef
                {
                    TileX = (int)pair.Key.X,
                    TileY = (int)pair.Key.Y,
                    HarvestItemId = crop.indexOfHarvest.Value,
                    CurrentPhase = crop.currentPhase.Value,
                    FinalPhaseIndex = finalPhaseIndex,
                    FullyGrown = crop.fullyGrown.Value,
                    DayOfCurrentPhase = crop.dayOfCurrentPhase.Value,
                    ReadyForRequest = ready
                };
            })
            .OrderByDescending(crop => crop.ReadyForRequest)
            .ThenBy(crop => crop.TileY)
            .ThenBy(crop => crop.TileX)
            .ToArray();
    }

    private static IslandGourmandActionTileRef[] ReadGourmandActionTiles(
        IslandFarmCave cave)
    {
        var buildings = cave.map?.GetLayer("Buildings");
        if (buildings is null)
            return Array.Empty<IslandGourmandActionTileRef>();

        var rows = new List<IslandGourmandActionTileRef>();
        for (var x = 0; x < buildings.LayerWidth; x++)
        for (var y = 0; y < buildings.LayerHeight; y++)
        {
            var action = cave.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (!string.Equals(action, "Gourmand", StringComparison.Ordinal))
                continue;
            rows.Add(new IslandGourmandActionTileRef
            {
                TileX = x,
                TileY = y,
                Action = action
            });
        }
        return rows
            .OrderBy(row => row.TileY)
            .ThenBy(row => row.TileX)
            .ToArray();
    }
}
