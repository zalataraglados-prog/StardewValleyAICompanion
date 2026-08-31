using StardewAI.Contracts.Training;

static partial class Program
{
    private static void ApplyTailoringRequestFields(
        TrainingExecutionRequest request,
        System.Text.Json.Nodes.JsonObject? item)
    {
        if (request.OptionId != "executor.tailor_item")
            return;
        request.TailoringCandidateId = ReadQueueParameterString(item, "tailoring_candidate_id");
        request.TailoringOperation = ReadQueueParameterString(item, "tailoring_operation");
        request.TailoringPurpose = ReadQueueParameterString(item, "tailoring_purpose");
        request.TailoringRecipeId = ReadQueueParameterString(item, "tailoring_recipe_id");
        request.TailoringSourceId = ReadQueueParameterString(item, "tailoring_source_id");
        request.TailoringSourceKind = ReadQueueParameterString(item, "tailoring_source_kind");
        request.TailoringSpendLeftCount = ReadQueueParameterInt(item, "tailoring_spend_left_count");
        request.TailoringSpendRightCount = ReadQueueParameterInt(item, "tailoring_spend_right_count");
        request.TailoringOutputContractKind = ReadQueueParameterString(item, "tailoring_output_contract_kind");
        request.TailoringTailoredCountsBeforeJson = ReadQueueParameterString(item, "tailoring_tailored_counts_before_json");
        request.TailoringMarksTailoredItem = ReadQueueParameterBool(item, "tailoring_marks_tailored_item");
        request.TailoringNativeContract = ReadQueueParameterString(item, "tailoring_native_contract");
    }
}
