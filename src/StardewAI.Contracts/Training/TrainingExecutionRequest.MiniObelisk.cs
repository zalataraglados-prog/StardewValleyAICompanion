using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("mini_obelisk_safe_slot_kind")]
    public string MiniObeliskSafeSlotKind { get; set; } = string.Empty;

    [JsonPropertyName("mini_obelisk_pair_member_index")]
    public int? MiniObeliskPairMemberIndex { get; set; }

    [JsonPropertyName("mini_obelisk_pair_first_tile_x")]
    public int? MiniObeliskPairFirstTileX { get; set; }

    [JsonPropertyName("mini_obelisk_pair_first_tile_y")]
    public int? MiniObeliskPairFirstTileY { get; set; }

    [JsonPropertyName("mini_obelisk_pair_second_tile_x")]
    public int? MiniObeliskPairSecondTileX { get; set; }

    [JsonPropertyName("mini_obelisk_pair_second_tile_y")]
    public int? MiniObeliskPairSecondTileY { get; set; }

    [JsonPropertyName("mini_obelisk_destination_tile_x")]
    public int? MiniObeliskDestinationTileX { get; set; }

    [JsonPropertyName("mini_obelisk_destination_tile_y")]
    public int? MiniObeliskDestinationTileY { get; set; }

    [JsonPropertyName("mini_obelisk_landing_tile_x")]
    public int? MiniObeliskLandingTileX { get; set; }

    [JsonPropertyName("mini_obelisk_landing_tile_y")]
    public int? MiniObeliskLandingTileY { get; set; }

    [JsonPropertyName("mini_obelisk_expected_delay_milliseconds")]
    public int? MiniObeliskExpectedDelayMilliseconds { get; set; }

    [JsonPropertyName("mini_obelisk_expected_location_action_return")]
    public bool? MiniObeliskExpectedLocationActionReturn { get; set; }
}
