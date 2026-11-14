using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_tilling_resources")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginTillingResources { get; set; }
}

// Admission of the requested witness tiers, not verification of their eventual payloads.
public static class NativeTillingObservationContract
{
    public static bool IsCompleteRequest(TrainingExecutionRequest request) => request.ActionOriginTillingResources &&
        request.ExecutionMode == "training_singleplayer" && request.OptionId == "executor.till_soil" &&
        request.SourcePlanningOptionId == "executor.till_soil" && !string.IsNullOrWhiteSpace(request.ActionOriginObservationScopeId) &&
        request.ActionOriginSnapshotAnchor && request.ActionOriginToolInputs && request.ActionOriginInventoryPayload &&
        request.ActionOriginInventoryTransients && request.ActionOriginInventoryGraph && request.ActionOriginDebrisState;
}
