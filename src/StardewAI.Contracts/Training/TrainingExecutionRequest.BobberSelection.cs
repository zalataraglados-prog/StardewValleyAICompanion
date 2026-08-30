using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("bobber_style_id")]
    public int? BobberStyleId { get; set; }

    [JsonPropertyName("bobber_reason")]
    public string BobberReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_bobber_style")]
    public bool? ConfirmBobberStyle { get; set; }

    [JsonPropertyName("bobber_projection_fingerprint")]
    public string BobberProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("bobber_style_before")]
    public int? BobberStyleBefore { get; set; }

    [JsonPropertyName("bobber_random_before")]
    public bool? BobberRandomBefore { get; set; }

    [JsonPropertyName("bobber_random_after")]
    public bool? BobberRandomAfter { get; set; }

    [JsonPropertyName("bobber_fish_caught_species_count")]
    public int? BobberFishCaughtSpeciesCount { get; set; }

    [JsonPropertyName("bobber_native_unlock_quotient")]
    public int? BobberNativeUnlockQuotient { get; set; }

    [JsonPropertyName("bobber_action_raw")]
    public string BobberActionRaw { get; set; } = string.Empty;

    [JsonPropertyName("expected_menu_kind")]
    public string ExpectedMenuKind { get; set; } = string.Empty;
}
