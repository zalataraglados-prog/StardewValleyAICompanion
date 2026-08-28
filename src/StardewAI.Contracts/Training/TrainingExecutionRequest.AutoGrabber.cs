using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("auto_grabber_safe_slot_kind")]
    public string AutoGrabberSafeSlotKind { get; set; } = string.Empty;

    [JsonPropertyName("auto_grabber_held_container_runtime_type")]
    public string AutoGrabberHeldContainerRuntimeType { get; set; } = string.Empty;

    [JsonPropertyName("auto_grabber_contents_before_json")]
    public string AutoGrabberContentsBeforeJson { get; set; } = string.Empty;

    [JsonPropertyName("auto_grabber_transferable_contents_json")]
    public string AutoGrabberTransferableContentsJson { get; set; } = string.Empty;

    [JsonPropertyName("auto_grabber_remaining_contents_json")]
    public string AutoGrabberRemainingContentsJson { get; set; } = string.Empty;

    [JsonPropertyName("auto_grabber_content_stack_count_before")]
    public int? AutoGrabberContentStackCountBefore { get; set; }

    [JsonPropertyName("auto_grabber_transferable_stack_count")]
    public int? AutoGrabberTransferableStackCount { get; set; }

    [JsonPropertyName("auto_grabber_expected_stack_count_after")]
    public int? AutoGrabberExpectedStackCountAfter { get; set; }

    [JsonPropertyName("auto_grabber_content_quantity_before")]
    public int? AutoGrabberContentQuantityBefore { get; set; }

    [JsonPropertyName("auto_grabber_expected_transfer_quantity")]
    public int? AutoGrabberExpectedTransferQuantity { get; set; }

    [JsonPropertyName("auto_grabber_expected_quantity_after")]
    public int? AutoGrabberExpectedQuantityAfter { get; set; }

    [JsonPropertyName("auto_grabber_expected_location_action_return")]
    public bool? AutoGrabberExpectedLocationActionReturn { get; set; }
}
