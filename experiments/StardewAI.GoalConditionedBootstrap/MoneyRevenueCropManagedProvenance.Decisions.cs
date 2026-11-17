using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

public sealed partial class MoneyRevenueCropManagedProvenance
{
    private JsonArray RetainDecisionArtifacts(EpisodeRow[] records)
    {
        var rows = new JsonArray();
        foreach (var row in records.OrderBy(value => value.Path, StringComparer.Ordinal))
        {
            var artifacts = new JsonArray();
            var output = new JsonObject { ["episode_path"] = row.Path, ["episode_id"] = row.Episode.EpisodeId, ["artifacts"] = artifacts };
            Add(row.Path, row.Episode.ModelPlanPath, "episode_model_plan");
            Add(row.Path, row.Episode.CompiledQueuePath, "episode_compiled_queue");
            var executionPath = Add(row.Path, row.Episode.ExecutionResultPath, "execution_result");
            if (executionPath is not null)
            {
                var execution = Read<JsonObject>(executionPath);
                foreach (var field in new[] { "effective_model_plan_path", "effective_ranking_path", "effective_compiled_queue_path", "effective_decision_snapshot_path" })
                {
                    var path = Add(executionPath, execution[field]?.GetValue<string>(), field[..^5]);
                    if (path is not null && field == "effective_decision_snapshot_path")
                        output["effective_decision_snapshot_state_hash"] = ReadSnapshot(path).StateHash;
                }
                // The old tracker retains its original snapshot when Replace records a fresh control rebind.
                // Keep both source identities; do not invent a literal-equality or native-proof condition.
                foreach (var field in new[] { "effective_decision_source_state_hash", "effective_decision_revision", "effective_selected_candidate_id", "effective_selected_queue_index" })
                    if (execution.TryGetPropertyValue(field, out var value)) output[field] = value?.DeepClone();
            }
            rows.Add(output);

            string? Add(string owner, string? reference, string role)
            {
                if (string.IsNullOrWhiteSpace(reference)) return null;
                var path = Pin(Resolve(owner, reference));
                artifacts.Add(JsonSerializer.SerializeToNode(new MoneyRevenueArtifactBinding(role, path, direct[path]), JsonDefaults.Options));
                return path;
            }
        }
        return rows;
    }
}
