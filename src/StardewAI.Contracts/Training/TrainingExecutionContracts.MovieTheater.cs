using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("movie_stage")]
    public string MovieStage { get; set; } = string.Empty;
    [JsonPropertyName("movie_projection_fingerprint")]
    public string MovieProjectionFingerprint { get; set; } = string.Empty;
    [JsonPropertyName("movie_id")]
    public string MovieId { get; set; } = string.Empty;
    [JsonPropertyName("movie_guest_name")]
    public string MovieGuestName { get; set; } = string.Empty;
    [JsonPropertyName("movie_concession_id")]
    public string MovieConcessionId { get; set; } = string.Empty;
    [JsonPropertyName("movie_objective_key")]
    public string MovieObjectiveKey { get; set; } = string.Empty;
    [JsonPropertyName("movie_friendship_effective")]
    public int? MovieFriendshipEffective { get; set; }
    [JsonPropertyName("movie_concession_friendship_effective")]
    public int? MovieConcessionFriendshipEffective { get; set; }
    [JsonPropertyName("movie_ticket_slot_index")]
    public int? MovieTicketSlotIndex { get; set; }
    [JsonPropertyName("movie_ticket_stack_before")]
    public int? MovieTicketStackBefore { get; set; }
    [JsonPropertyName("movie_action_raw")]
    public string MovieActionRaw { get; set; } = string.Empty;
    [JsonPropertyName("movie_action_token")]
    public string MovieActionToken { get; set; } = string.Empty;
}
