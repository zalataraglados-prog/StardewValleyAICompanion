using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplySignDisplayItemRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.set_sign_display_item", StringComparison.Ordinal))
        {
            return;
        }
        request.SignDisplaySourceRuntimeType = ReadQueueParameterString(item, "source_runtime_type");
        request.SignDisplaySourceQuality = ReadQueueParameterInt(item, "source_quality");
        request.SignDisplaySourceStateSha256 = ReadQueueParameterString(item, "source_state_sha256");
        request.SignExpectedDisplayType = ReadQueueParameterInt(item, "expected_display_type");
        request.SignDisplayTargetProjectionFingerprint = ReadQueueParameterString(item, "target_projection_fingerprint");
        request.SignDisplayTargetQualifiedItemId = ReadQueueParameterString(item, "target_qualified_item_id");
        request.SignDisplayTargetStateSha256 = ReadQueueParameterString(item, "target_state_sha256");
        request.SignPreviousDisplayItemQualifiedItemId = ReadQueueParameterString(item, "previous_display_item_qualified_item_id");
        request.SignPreviousDisplayItemRuntimeType = ReadQueueParameterString(item, "previous_display_item_runtime_type");
        request.SignPreviousDisplayItemStateSha256 = ReadQueueParameterString(item, "previous_display_item_state_sha256");
        request.SignPreviousDisplayType = ReadQueueParameterInt(item, "previous_display_type");
        request.SignReplaceExistingDisplay = ReadNullableBoolQueueParameter(item, "replace_existing_display");
        request.SignAllowReplaceExistingDisplay = ReadNullableBoolQueueParameter(item, "allow_replace_existing_display");
    }
}
