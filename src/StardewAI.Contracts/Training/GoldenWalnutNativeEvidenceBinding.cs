using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed record GoldenWalnutNativeEvidenceBinding(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("source_run_id")] string SourceRunId,
    [property: JsonPropertyName("source_save_id")] string SourceSaveId,
    [property: JsonPropertyName("source_player_id")] string SourcePlayerId,
    [property: JsonPropertyName("game_version")] string GameVersion,
    [property: JsonPropertyName("source_schedule_path")] string SourceSchedulePath,
    [property: JsonPropertyName("source_schedule_sha256")] string SourceScheduleSha256,
    [property: JsonPropertyName("verification_receipt_path")] string VerificationReceiptPath,
    [property: JsonPropertyName("verification_receipt_sha256")] string VerificationReceiptSha256,
    [property: JsonPropertyName("gourmand_continuation_receipt_path")] string GourmandContinuationReceiptPath,
    [property: JsonPropertyName("gourmand_continuation_receipt_sha256")] string GourmandContinuationReceiptSha256);
