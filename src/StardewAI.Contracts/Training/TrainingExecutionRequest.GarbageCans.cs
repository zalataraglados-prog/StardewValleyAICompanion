using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("fixture_garbage_can_profile")]
    public string FixtureGarbageCanProfile { get; set; } = string.Empty;

    [JsonPropertyName("garbage_can_action")]
    public string GarbageCanAction { get; set; } = string.Empty;

    [JsonPropertyName("garbage_can_id")]
    public string GarbageCanId { get; set; } = string.Empty;

    [JsonPropertyName("expected_checked_today_before")]
    public bool? ExpectedCheckedTodayBefore { get; set; }

    [JsonPropertyName("expected_checked_today_after")]
    public bool? ExpectedCheckedTodayAfter { get; set; }

    [JsonPropertyName("expected_trash_cans_checked_before")]
    public int? ExpectedTrashCansCheckedBefore { get; set; }

    [JsonPropertyName("expected_trash_cans_checked_delta")]
    public int? ExpectedTrashCansCheckedDelta { get; set; }

    [JsonPropertyName("expected_daily_luck")]
    public double? ExpectedDailyLuck { get; set; }

    [JsonPropertyName("expected_alleyway_buffet_read")]
    public bool? ExpectedAlleywayBuffetRead { get; set; }

    [JsonPropertyName("predicted_item_produced")]
    public bool? PredictedItemProduced { get; set; }

    [JsonPropertyName("selected_entry_id")]
    public string SelectedEntryId { get; set; } = string.Empty;

    [JsonPropertyName("selected_ignore_base_chance")]
    public bool? SelectedIgnoreBaseChance { get; set; }

    [JsonPropertyName("selected_mega_success")]
    public bool? SelectedMegaSuccess { get; set; }

    [JsonPropertyName("selected_double_mega_success")]
    public bool? SelectedDoubleMegaSuccess { get; set; }

    [JsonPropertyName("output_delivery")]
    public string OutputDelivery { get; set; } = string.Empty;

    [JsonPropertyName("expected_output_json")]
    public string ExpectedOutputJson { get; set; } = string.Empty;

    [JsonPropertyName("reacting_npc_json")]
    public string ReactingNpcJson { get; set; } = string.Empty;

    [JsonPropertyName("garbage_can_data_payload_sha256")]
    public string GarbageCanDataPayloadSha256 { get; set; } = string.Empty;

    [JsonPropertyName("garbage_can_data_contract_status")]
    public string GarbageCanDataContractStatus { get; set; } = string.Empty;

    [JsonPropertyName("garbage_can_prediction_status")]
    public string GarbageCanPredictionStatus { get; set; } = string.Empty;

    [JsonPropertyName("garbage_can_projection_fingerprint")]
    public string GarbageCanProjectionFingerprint { get; set; } = string.Empty;

    [JsonPropertyName("garbage_can_native_contract")]
    public string GarbageCanNativeContract { get; set; } = string.Empty;
}
