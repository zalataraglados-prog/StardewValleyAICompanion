using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_watering_resources")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginWateringResources { get; set; }
}

public static class NativeWateringObservationContract
{
    public const string MetadataSchemaVersion = "native_watering_tool_metadata.v1";

    public static bool IsCompleteRequest(TrainingExecutionRequest request) => request.ActionOriginWateringResources &&
        !request.ActionOriginTillingResources && request.ExecutionMode == "training_singleplayer" &&
        request.OptionId == "executor.water_crop" && request.SourcePlanningOptionId == "executor.water_crop" &&
        !string.IsNullOrWhiteSpace(request.ActionOriginObservationScopeId) && request.ActionOriginSnapshotAnchor &&
        request.ActionOriginToolInputs && request.ActionOriginInventoryPayload && request.ActionOriginInventoryTransients &&
        request.ActionOriginInventoryGraph && request.ActionOriginDebrisState;
}
