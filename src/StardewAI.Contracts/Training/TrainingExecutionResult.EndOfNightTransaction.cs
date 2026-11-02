using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionResult
{
    [JsonPropertyName("end_of_night_transaction_id")]
    public string EndOfNightTransactionId { get; set; } = string.Empty;

    [JsonPropertyName("end_of_night_transaction_status")]
    public string EndOfNightTransactionStatus { get; set; } = string.Empty;

    [JsonPropertyName("end_of_night_transaction_evidence_path")]
    public string EndOfNightTransactionEvidencePath { get; set; } = string.Empty;
}
