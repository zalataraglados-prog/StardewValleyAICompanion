using System.Linq;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewValley;
using StardewValley.Locations;

namespace StardewAI.TransparentBridge.Adapters;

public sealed partial class WorldProgressReadAdapter
{
    private static MarriageHouseProgressRef? ReadMarriageHouse(Farmer? actor, GameLocation? scienceHouse)
    {
        if (actor is null || scienceHouse is null)
        {
            return null;
        }

        var actionTile = FindActionTile(scienceHouse, "Carpenter");
        var current = ReferenceEquals(Game1.currentLocation, scienceHouse);
        var robin = scienceHouse.characters.FirstOrDefault(npc => string.Equals(npc.Name, "Robin", StringComparison.Ordinal));
        var robinAtCounter = actionTile is not null && robin is not null &&
            Vector2.Distance(robin.Tile, new Vector2(actionTile.X, actionTile.Y)) <= 3f;
        var level = actor.HouseUpgradeLevel;
        var days = actor.daysUntilHouseUpgrade.Value;
        var married = actor.isMarriedOrRoommates();
        var currentGrandpaFactor = married && level >= 2;
        var cellarInfrastructure = ReadCellarInfrastructure(actor);
        var buildingUnderConstruction = Game1.IsThereABuildingUnderConstruction();
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var spec = level switch
        {
            0 => Upgrade("farmhouse_level_1", 0, 1, 10000, "(O)388", 450),
            1 => Upgrade("farmhouse_level_2", 1, 2, 65000, "(O)709", 100),
            2 => Upgrade("farmhouse_level_3", 2, 3, 100000, string.Empty, 0),
            _ => null
        };

        if (spec is not null)
        {
            spec.InventoryItemCount = string.IsNullOrEmpty(spec.RequiredItemId) ? 0 : actor.Items.CountId(spec.RequiredItemId);
            spec.MeetsGrandpaHouseLevelAfterConstruction = spec.LevelAfter >= 2;
            spec.GrandpaFactorSatisfiedAfterConstruction = married && spec.MeetsGrandpaHouseLevelAfterConstruction;
            spec.DirectGrandpaScoreDeltaAfterConstruction = !currentGrandpaFactor && spec.GrandpaFactorSatisfiedAfterConstruction ? 1 : 0;
            spec.UnlocksCellar = spec.LevelBefore == 2 && spec.LevelAfter == 3;
            spec.UnlocksCaskRecipe = spec.UnlocksCellar;
            spec.AddsIndoorMachinePlacementLocation = spec.UnlocksCellar;
            spec.MachineCapacityProjectionStatus = spec.UnlocksCellar
                ? cellarInfrastructure.ProjectionStatus
                : "no_new_machine_location_from_this_upgrade";
            spec.ActionStatus = days >= 0
                ? "farmhouse_upgrade_already_in_progress"
                : buildingUnderConstruction
                    ? "another_building_under_construction"
                    : actor.Money < spec.Price
                        ? "insufficient_money"
                        : spec.InventoryItemCount < spec.RequiredItemCount
                            ? "insufficient_required_material"
                            : !current
                                ? "science_house_not_current_location"
                                : actionTile is null
                                    ? "carpenter_action_tile_unavailable"
                                    : !robinAtCounter
                                        ? "robin_not_present_at_counter"
                                        : !menuClear
                                            ? "carpenter_menu_or_dialogue_not_clear"
                                            : "ready";
        }

        return new MarriageHouseProgressRef
        {
            LocationAccessible = Game1.isLocationAccessible("ScienceHouse"),
            IsCurrentLocation = current,
            CarpenterActionTileX = actionTile?.X,
            CarpenterActionTileY = actionTile?.Y,
            CarpenterActionRaw = actionTile?.Action ?? string.Empty,
            IsMasterGame = Game1.IsMasterGame,
            RobinPresentAtCounter = robinAtCounter,
            BuildingUnderConstruction = buildingUnderConstruction,
            MarriedOrRoommate = married,
            Engaged = actor.isEngaged(),
            Spouse = actor.spouse ?? string.Empty,
            PendingRoommate = actor.hasCurrentOrPendingRoommate(),
            FarmhouseUpgradeLevel = level,
            DaysUntilFarmhouseUpgrade = days,
            Money = actor.Money,
            GrandpaFactorSatisfied = currentGrandpaFactor,
            CellarUnlocked = level >= 3,
            CellarInfrastructure = cellarInfrastructure,
            HouseUpgrade = spec,
            HomeRenovations = ReadHomeRenovations(actor, scienceHouse, actionTile, robinAtCounter, buildingUnderConstruction, menuClear)
        };
    }

    private static CellarInfrastructureProgressRef ReadCellarInfrastructure(Farmer actor)
    {
        var home = Utility.getHomeOfFarmer(actor) as FarmHouse;
        var cellarName = home?.GetCellarName();
        if (string.IsNullOrWhiteSpace(cellarName) || Game1.getLocationFromName(cellarName) is not Cellar cellar)
        {
            return new CellarInfrastructureProgressRef
            {
                ProjectionStatus = "cellar_location_unavailable"
            };
        }

        var back = cellar.Map?.GetLayer("Back");
        if (back is null)
        {
            return new CellarInfrastructureProgressRef
            {
                ProjectionStatus = "cellar_map_unavailable",
                LocationId = cellar.NameOrUniqueName
            };
        }

        var staticCollisionMask = CollisionMask.All & ~CollisionMask.Characters & ~CollisionMask.Farmers;
        var placeableTiles = 0;
        for (var y = 0; y < back.LayerHeight; y++)
        {
            for (var x = 0; x < back.LayerWidth; x++)
            {
                var tile = new Vector2(x, y);
                if (cellar.isTilePlaceable(tile) && !cellar.IsTileBlockedBy(tile, staticCollisionMask))
                {
                    placeableTiles++;
                }
            }
        }

        var machines = cellar.objects.Pairs
            .Where(pair => pair.Value.bigCraftable.Value && pair.Value.GetMachineData() is not null)
            .ToArray();
        return new CellarInfrastructureProgressRef
        {
            ProjectionStatus = "cellar_static_map_capacity_available",
            LocationId = cellar.NameOrUniqueName,
            MapWidth = back.LayerWidth,
            MapHeight = back.LayerHeight,
            StaticPlaceableTileCount = placeableTiles,
            OccupiedObjectCount = cellar.objects.Pairs.Count(),
            MachineCount = machines.Length,
            MachineCountsByQualifiedId = machines
                .GroupBy(pair => pair.Value.QualifiedItemId, StringComparer.Ordinal)
                .Where(group => !string.IsNullOrWhiteSpace(group.Key))
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal)
        };
    }

    private static FarmhouseUpgradeProgressRef Upgrade(
        string id,
        int levelBefore,
        int levelAfter,
        int price,
        string requiredItemId,
        int requiredItemCount) =>
        new()
        {
            UpgradeId = id,
            LevelBefore = levelBefore,
            LevelAfter = levelAfter,
            Price = price,
            RequiredItemId = requiredItemId,
            RequiredItemCount = requiredItemCount,
            ConstructionDays = 3
        };
}
