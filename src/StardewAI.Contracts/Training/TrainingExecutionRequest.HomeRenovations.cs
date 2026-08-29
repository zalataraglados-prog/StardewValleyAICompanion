using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("renovation_id")]
    public string RenovationId { get; set; } = string.Empty;

    [JsonPropertyName("renovation_selected_index")]
    public int? RenovationSelectedIndex { get; set; }

    [JsonPropertyName("renovation_reason")]
    public string RenovationReason { get; set; } = string.Empty;

    [JsonPropertyName("confirm_renovation")]
    public bool? ConfirmRenovation { get; set; }

    [JsonPropertyName("confirm_destructive_renovation")]
    public bool? ConfirmDestructiveRenovation { get; set; }

    [JsonPropertyName("renovation_is_destructive")]
    public bool? RenovationIsDestructive { get; set; }

    [JsonPropertyName("home_location_id")]
    public string HomeLocationId { get; set; } = string.Empty;

    [JsonPropertyName("home_runtime_type")]
    public string HomeRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("expected_home_house_upgrade_level")]
    public int? ExpectedHomeHouseUpgradeLevel { get; set; }

    [JsonPropertyName("home_renovation_data_payload_sha256")]
    public string HomeRenovationDataPayloadSha256 { get; set; } = string.Empty;

    [JsonPropertyName("home_renovation_data_contract_status")]
    public string HomeRenovationDataContractStatus { get; set; } = string.Empty;

    [JsonPropertyName("native_available_renovation_ids_json")]
    public string NativeAvailableRenovationIdsJson { get; set; } = string.Empty;

    [JsonPropertyName("native_renovation_shop_index")]
    public int? NativeRenovationShopIndex { get; set; }

    [JsonPropertyName("renovation_room_id")]
    public string RenovationRoomId { get; set; } = string.Empty;

    [JsonPropertyName("renovation_animation_type")]
    public string RenovationAnimationType { get; set; } = string.Empty;

    [JsonPropertyName("renovation_check_for_obstructions")]
    public bool? RenovationCheckForObstructions { get; set; }

    [JsonPropertyName("renovation_first_purchase_mail_id")]
    public string RenovationFirstPurchaseMailId { get; set; } = string.Empty;

    [JsonPropertyName("renovation_first_purchase_mail_before")]
    public bool? RenovationFirstPurchaseMailBefore { get; set; }

    [JsonPropertyName("expected_renovation_first_purchase_mail_after")]
    public bool? ExpectedRenovationFirstPurchaseMailAfter { get; set; }

    [JsonPropertyName("renovation_refund_eligible")]
    public bool? RenovationRefundEligible { get; set; }

    [JsonPropertyName("renovation_requirements_json")]
    public string RenovationRequirementsJson { get; set; } = string.Empty;

    [JsonPropertyName("renovate_actions_json")]
    public string RenovateActionsJson { get; set; } = string.Empty;

    [JsonPropertyName("selected_region_rectangles_json")]
    public string SelectedRegionRectanglesJson { get; set; } = string.Empty;

    [JsonPropertyName("selected_region_obstruction_status")]
    public string SelectedRegionObstructionStatus { get; set; } = string.Empty;

    [JsonPropertyName("renovation_projection_fingerprint")]
    public string RenovationProjectionFingerprint { get; set; } = string.Empty;
}
