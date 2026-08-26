using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyTextSignEditingRequestFields(TrainingExecutionRequest request, JsonObject? item)
    {
        if (!string.Equals(request.OptionId, "executor.edit_text_sign", StringComparison.Ordinal))
        {
            return;
        }
        request.TextSignTargetProjectionFingerprint = ReadQueueParameterString(item, "target_projection_fingerprint");
        request.TextSignTargetQualifiedItemId = ReadQueueParameterString(item, "target_qualified_item_id");
        request.TextSignTargetStateSha256 = ReadQueueParameterString(item, "target_state_sha256");
        request.TextSignRawBefore = ReadQueueParameterString(item, "raw_sign_text_before");
        request.TextSignDisplayBefore = ReadQueueParameterString(item, "display_sign_text_before");
        request.TextSignShowNextIndexBefore = ReadNullableBoolQueueParameter(item, "expected_show_next_index_before");
        request.TextSignReplacesExistingText = ReadNullableBoolQueueParameter(item, "replaces_existing_text");
        request.TextSignAllowReplaceExistingText = ReadNullableBoolQueueParameter(item, "allow_replace_existing_text");
        request.TextSignRequestedText = ReadQueueParameterString(item, "requested_sign_text");
    }
}
