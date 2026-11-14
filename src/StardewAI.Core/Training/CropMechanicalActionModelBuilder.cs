using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Training;

public static class CropMechanicalActionModelBuilder
{
    public static JsonObject Build(string stateHash, string sourceModel, string candidateId, string optionId,
        SmallModelActionParameter[] parameters, string executionMode, JsonNode actor)
    {
        if (actor is null) throw new ArgumentNullException(nameof(actor));
        return JsonSerializer.SerializeToNode(new
        {
            schema_version = "small_model_action.v1", model_output_id = "model-output:" + candidateId,
            source_model = sourceModel, state_hash = stateHash, goal_id = "daily.closed_loop",
            execution_mode = executionMode, actor = actor.DeepClone(),
            actions = new[] { new { action_id = candidateId + ":action", option_id = optionId, rationale = sourceModel, parameters } }
        })!.AsObject();
    }
}
