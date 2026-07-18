using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.State
{
    public sealed class MarriageHouseProgressRef
    {
        [JsonPropertyName("location_accessible")]
        public bool LocationAccessible { get; set; }

        [JsonPropertyName("is_current_location")]
        public bool IsCurrentLocation { get; set; }

        [JsonPropertyName("carpenter_action_tile_x")]
        public int? CarpenterActionTileX { get; set; }

        [JsonPropertyName("carpenter_action_tile_y")]
        public int? CarpenterActionTileY { get; set; }

        [JsonPropertyName("carpenter_action_raw")]
        public string CarpenterActionRaw { get; set; } = string.Empty;

        [JsonPropertyName("is_master_game")]
        public bool IsMasterGame { get; set; }

        [JsonPropertyName("robin_present_at_counter")]
        public bool RobinPresentAtCounter { get; set; }

        [JsonPropertyName("building_under_construction")]
        public bool BuildingUnderConstruction { get; set; }

        [JsonPropertyName("married_or_roommate")]
        public bool MarriedOrRoommate { get; set; }

        [JsonPropertyName("engaged")]
        public bool Engaged { get; set; }

        [JsonPropertyName("spouse")]
        public string Spouse { get; set; } = string.Empty;

        [JsonPropertyName("pending_roommate")]
        public bool PendingRoommate { get; set; }

        [JsonPropertyName("farmhouse_upgrade_level")]
        public int FarmhouseUpgradeLevel { get; set; }

        [JsonPropertyName("days_until_farmhouse_upgrade")]
        public int DaysUntilFarmhouseUpgrade { get; set; }

        [JsonPropertyName("money")]
        public int Money { get; set; }

        [JsonPropertyName("grandpa_factor_satisfied")]
        public bool GrandpaFactorSatisfied { get; set; }

        [JsonPropertyName("cellar_unlocked")]
        public bool CellarUnlocked { get; set; }

        [JsonPropertyName("cellar_infrastructure")]
        public CellarInfrastructureProgressRef CellarInfrastructure { get; set; } = new();

        [JsonPropertyName("house_upgrade")]
        public FarmhouseUpgradeProgressRef? HouseUpgrade { get; set; }
    }

    public sealed class FarmhouseUpgradeProgressRef
    {
        [JsonPropertyName("upgrade_id")]
        public string UpgradeId { get; set; } = string.Empty;

        [JsonPropertyName("level_before")]
        public int LevelBefore { get; set; }

        [JsonPropertyName("level_after")]
        public int LevelAfter { get; set; }

        [JsonPropertyName("price")]
        public int Price { get; set; }

        [JsonPropertyName("required_item_id")]
        public string RequiredItemId { get; set; } = string.Empty;

        [JsonPropertyName("required_item_count")]
        public int RequiredItemCount { get; set; }

        [JsonPropertyName("inventory_item_count")]
        public int InventoryItemCount { get; set; }

        [JsonPropertyName("construction_days")]
        public int ConstructionDays { get; set; }

        [JsonPropertyName("meets_grandpa_house_level_after_construction")]
        public bool MeetsGrandpaHouseLevelAfterConstruction { get; set; }

        [JsonPropertyName("grandpa_factor_satisfied_after_construction")]
        public bool GrandpaFactorSatisfiedAfterConstruction { get; set; }

        [JsonPropertyName("direct_grandpa_score_delta_after_construction")]
        public int DirectGrandpaScoreDeltaAfterConstruction { get; set; }

        [JsonPropertyName("unlocks_cellar")]
        public bool UnlocksCellar { get; set; }

        [JsonPropertyName("unlocks_cask_recipe")]
        public bool UnlocksCaskRecipe { get; set; }

        [JsonPropertyName("adds_indoor_machine_placement_location")]
        public bool AddsIndoorMachinePlacementLocation { get; set; }

        [JsonPropertyName("machine_capacity_projection_status")]
        public string MachineCapacityProjectionStatus { get; set; } = string.Empty;

        [JsonPropertyName("action_status")]
        public string ActionStatus { get; set; } = string.Empty;
    }

    public sealed class CellarInfrastructureProgressRef
    {
        [JsonPropertyName("projection_status")]
        public string ProjectionStatus { get; set; } = string.Empty;

        [JsonPropertyName("location_id")]
        public string LocationId { get; set; } = string.Empty;

        [JsonPropertyName("map_width")]
        public int MapWidth { get; set; }

        [JsonPropertyName("map_height")]
        public int MapHeight { get; set; }

        [JsonPropertyName("static_placeable_tile_count")]
        public int StaticPlaceableTileCount { get; set; }

        [JsonPropertyName("occupied_object_count")]
        public int OccupiedObjectCount { get; set; }

        [JsonPropertyName("machine_count")]
        public int MachineCount { get; set; }

        [JsonPropertyName("machine_counts_by_qualified_id")]
        public Dictionary<string, int> MachineCountsByQualifiedId { get; set; } = new();
    }
}
