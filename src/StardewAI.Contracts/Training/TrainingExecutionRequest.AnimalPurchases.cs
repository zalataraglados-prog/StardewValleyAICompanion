using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("expected_menu_type_after")]
    public string ExpectedMenuTypeAfter { get; set; } = string.Empty;

    [JsonPropertyName("animal_type_id")]
    public string AnimalTypeId { get; set; } = string.Empty;

    [JsonPropertyName("possible_actual_type_ids_json")]
    public string PossibleActualTypeIdsJson { get; set; } = string.Empty;

    [JsonPropertyName("target_location_id")]
    public string AnimalPurchaseTargetLocationId { get; set; } = string.Empty;

    [JsonPropertyName("home_building_type")]
    public string AnimalHomeBuildingType { get; set; } = string.Empty;

    [JsonPropertyName("home_building_tile_x")]
    public int? AnimalHomeBuildingTileX { get; set; }

    [JsonPropertyName("home_building_tile_y")]
    public int? AnimalHomeBuildingTileY { get; set; }

    [JsonPropertyName("home_indoor_location_id")]
    public string AnimalHomeIndoorLocationId { get; set; } = string.Empty;

    [JsonPropertyName("generated_animal_name")]
    public string GeneratedAnimalName { get; set; } = string.Empty;

    [JsonPropertyName("expected_home_occupant_count_before")]
    public int? ExpectedAnimalHomeOccupantCountBefore { get; set; }

    [JsonPropertyName("expected_home_capacity")]
    public int? ExpectedAnimalHomeCapacity { get; set; }

    [JsonPropertyName("candidate_identity_sha256")]
    public string AnimalPurchaseCandidateIdentitySha256 { get; set; } = string.Empty;
}
