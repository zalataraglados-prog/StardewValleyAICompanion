using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("statue_blessing_id")]
    public int? StatueBlessingId { get; set; }

    [JsonPropertyName("statue_blessing_buff_id")]
    public string StatueBlessingBuffId { get; set; } = string.Empty;

    [JsonPropertyName("statue_blessing_effect_kind")]
    public string StatueBlessingEffectKind { get; set; } = string.Empty;

    [JsonPropertyName("statue_blessing_exact_effect")]
    public string StatueBlessingExactEffect { get; set; } = string.Empty;

    [JsonPropertyName("statue_blessing_days_played")]
    public int? StatueBlessingDaysPlayed { get; set; }

    [JsonPropertyName("statue_blessing_random_upper_bound_exclusive")]
    public int? StatueBlessingRandomUpperBoundExclusive { get; set; }
}
