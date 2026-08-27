using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("required_fragility")]
    public int? RequiredFragility { get; set; }

    [JsonPropertyName("slime_ball_seed_days_played")]
    public int? SlimeBallSeedDaysPlayed { get; set; }

    [JsonPropertyName("slime_ball_seed_unique_game_id")]
    public long? SlimeBallSeedUniqueGameId { get; set; }

    [JsonPropertyName("slime_ball_expected_slime_quantity")]
    public int? SlimeBallExpectedSlimeQuantity { get; set; }

    [JsonPropertyName("slime_ball_expected_petrified_slime_quantity")]
    public int? SlimeBallExpectedPetrifiedSlimeQuantity { get; set; }

    [JsonPropertyName("slime_ball_expected_location_action_return")]
    public bool? SlimeBallExpectedLocationActionReturn { get; set; }
}
