using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("secret_note_runtime_type")]
    public string SecretNoteRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_stack_before")]
    public int? SecretNoteStackBefore { get; set; }

    [JsonPropertyName("secret_note_stack_after")]
    public int? SecretNoteStackAfter { get; set; }

    [JsonPropertyName("secret_note_is_journal")]
    public bool? SecretNoteIsJournal { get; set; }

    [JsonPropertyName("secret_note_journal_index")]
    public int? SecretNoteJournalIndex { get; set; }

    [JsonPropertyName("secret_note_total_count")]
    public int? SecretNoteTotalCount { get; set; }

    [JsonPropertyName("secret_note_unseen_ids_native_order_json")]
    public string SecretNoteUnseenIdsNativeOrderJson { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_unseen_count")]
    public int? SecretNoteUnseenCount { get; set; }

    [JsonPropertyName("secret_note_selection_kind")]
    public string SecretNoteSelectionKind { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_selected_id")]
    public int? SecretNoteSelectedId { get; set; }

    [JsonPropertyName("secret_note_content_sha256")]
    public string SecretNoteContentSha256 { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_display_kind")]
    public string SecretNoteDisplayKind { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_expected_image")]
    public int? SecretNoteExpectedImage { get; set; }

    [JsonPropertyName("secret_note_expected_which_bg")]
    public int? SecretNoteExpectedWhichBackground { get; set; }

    [JsonPropertyName("secret_note_expected_quest_id")]
    public string SecretNoteExpectedQuestId { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_expected_quest_present_before")]
    public bool? SecretNoteExpectedQuestPresentBefore { get; set; }

    [JsonPropertyName("secret_note_expected_quest_present_after")]
    public bool? SecretNoteExpectedQuestPresentAfter { get; set; }

    [JsonPropertyName("secret_note_projection_fingerprint")]
    public string SecretNoteProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_native_contract")]
    public string SecretNoteNativeContract { get; set; } = string.Empty;

    [JsonPropertyName("secret_note_fixture_target_id")]
    public int? SecretNoteFixtureTargetId { get; set; }
}
