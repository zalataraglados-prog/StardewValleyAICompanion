using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("farm_computer_safe_slot_kind")]
    public string FarmComputerSafeSlotKind { get; set; } = string.Empty;

    [JsonPropertyName("farm_computer_root_location_id")]
    public string FarmComputerRootLocationId { get; set; } = string.Empty;

    [JsonPropertyName("farm_computer_includes_hay")]
    public bool? FarmComputerIncludesHay { get; set; }

    [JsonPropertyName("farm_computer_pieces_of_hay")]
    public int? FarmComputerPiecesOfHay { get; set; }

    [JsonPropertyName("farm_computer_hay_capacity")]
    public int? FarmComputerHayCapacity { get; set; }

    [JsonPropertyName("farm_computer_total_crops")]
    public int? FarmComputerTotalCrops { get; set; }

    [JsonPropertyName("farm_computer_crops_ready")]
    public int? FarmComputerCropsReady { get; set; }

    [JsonPropertyName("farm_computer_unwatered_crops")]
    public int? FarmComputerUnwateredCrops { get; set; }

    [JsonPropertyName("farm_computer_greenhouse_crops_ready")]
    public int? FarmComputerGreenhouseCropsReady { get; set; }

    [JsonPropertyName("farm_computer_open_hoe_dirt")]
    public int? FarmComputerOpenHoeDirt { get; set; }

    [JsonPropertyName("farm_computer_total_forage")]
    public int? FarmComputerTotalForage { get; set; }

    [JsonPropertyName("farm_computer_machines_ready")]
    public int? FarmComputerMachinesReady { get; set; }

    [JsonPropertyName("farm_computer_farm_cave_ready")]
    public bool? FarmComputerFarmCaveReady { get; set; }

    [JsonPropertyName("farm_computer_report_sha256")]
    public string FarmComputerReportSha256 { get; set; } = string.Empty;

    [JsonPropertyName("farm_computer_expected_delay_ms")]
    public int? FarmComputerExpectedDelayMs { get; set; }

    [JsonPropertyName("farm_computer_expected_shake_timer")]
    public int? FarmComputerExpectedShakeTimer { get; set; }

    [JsonPropertyName("farm_computer_expected_freeze_ms")]
    public int? FarmComputerExpectedFreezeMs { get; set; }

    [JsonPropertyName("farm_computer_expected_location_action_return")]
    public bool? FarmComputerExpectedLocationActionReturn { get; set; }
}
