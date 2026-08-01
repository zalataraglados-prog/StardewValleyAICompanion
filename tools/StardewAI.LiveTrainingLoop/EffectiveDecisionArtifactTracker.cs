using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public sealed class EffectiveDecisionArtifactTracker
{
    private string modelPlanPath;
    private string rankingPath;
    private string compiledQueuePath;
    private string sourceStateHash;
    private int revision;

    public EffectiveDecisionArtifactTracker(
        string modelPlanPath,
        string rankingPath,
        string compiledQueuePath,
        string sourceStateHash)
    {
        this.modelPlanPath = modelPlanPath;
        this.rankingPath = rankingPath;
        this.compiledQueuePath = compiledQueuePath;
        this.sourceStateHash = sourceStateHash;
    }

    public void Replace(
        string replacementModelPlanPath,
        string replacementRankingPath,
        string replacementCompiledQueuePath,
        string replacementSourceStateHash)
    {
        modelPlanPath = replacementModelPlanPath;
        rankingPath = replacementRankingPath;
        compiledQueuePath = replacementCompiledQueuePath;
        sourceStateHash = replacementSourceStateHash;
        revision++;
    }

    public void Stamp(JsonObject execution)
    {
        execution["effective_model_plan_path"] = modelPlanPath;
        execution["effective_ranking_path"] = rankingPath;
        execution["effective_compiled_queue_path"] = compiledQueuePath;
        execution["effective_decision_source_state_hash"] = sourceStateHash;
        execution["effective_decision_revision"] = revision;
    }

    public static string ReadCandidateId(JsonObject execution)
    {
        var parameters = execution["effective_queue_item"]?["normalized_command"]?["parameters"] as JsonArray;
        if (parameters is null)
        {
            return string.Empty;
        }

        const string prefix = "candidate_id:";
        foreach (var parameterNode in parameters)
        {
            if (parameterNode is not JsonObject parameter ||
                !string.Equals(ReadString(parameter, "name"), "precondition", StringComparison.Ordinal))
            {
                continue;
            }

            var value = ReadString(parameter, "value");
            if (value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length)
            {
                return value[prefix.Length..];
            }
        }

        return string.Empty;
    }

    private static string ReadString(JsonObject value, string property)
    {
        return value[property] is JsonValue jsonValue &&
            jsonValue.TryGetValue<string>(out var result)
                ? result
                : string.Empty;
    }
}
