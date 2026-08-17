using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("management_intent")]
    public string AnimalManagementIntent { get; set; } = string.Empty;

    [JsonPropertyName("management_reason")]
    public string AnimalManagementReason { get; set; } = string.Empty;

    [JsonPropertyName("animal_id")]
    public long? ManagedAnimalId { get; set; }

    [JsonPropertyName("expected_name_before")]
    public string ExpectedAnimalNameBefore { get; set; } = string.Empty;

    [JsonPropertyName("requires_initial_pet")]
    public bool? AnimalManagementRequiresInitialPet { get; set; }

    [JsonPropertyName("expected_allow_reproduction_before")]
    public bool? ExpectedAllowReproductionBefore { get; set; }

    [JsonPropertyName("target_allow_reproduction")]
    public bool? TargetAllowReproduction { get; set; }

    [JsonPropertyName("expected_sell_price")]
    public int? ExpectedAnimalSellPrice { get; set; }

    [JsonPropertyName("confirm_irreversible_sale")]
    public bool ConfirmIrreversibleAnimalSale { get; set; }

    [JsonPropertyName("expected_home_building_type_before")]
    public string ExpectedAnimalHomeBuildingTypeBefore { get; set; } = string.Empty;

    [JsonPropertyName("expected_home_building_tile_x_before")]
    public int? ExpectedAnimalHomeBuildingTileXBefore { get; set; }

    [JsonPropertyName("expected_home_building_tile_y_before")]
    public int? ExpectedAnimalHomeBuildingTileYBefore { get; set; }

    [JsonPropertyName("target_home_building_type")]
    public string TargetAnimalHomeBuildingType { get; set; } = string.Empty;

    [JsonPropertyName("target_home_building_tile_x")]
    public int? TargetAnimalHomeBuildingTileX { get; set; }

    [JsonPropertyName("target_home_building_tile_y")]
    public int? TargetAnimalHomeBuildingTileY { get; set; }

    [JsonPropertyName("target_home_indoor_location_id")]
    public string TargetAnimalHomeIndoorLocationId { get; set; } = string.Empty;

    [JsonPropertyName("expected_target_home_occupant_count_before")]
    public int? ExpectedTargetAnimalHomeOccupantCountBefore { get; set; }

    [JsonPropertyName("expected_target_home_capacity")]
    public int? ExpectedTargetAnimalHomeCapacity { get; set; }
}
