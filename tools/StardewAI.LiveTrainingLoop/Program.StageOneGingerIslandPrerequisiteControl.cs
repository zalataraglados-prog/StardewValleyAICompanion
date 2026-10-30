using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

static partial class Program
{
    private const string GingerIslandPrerequisiteControlParameter =
        "master_angler_ginger_island_prerequisite_control_only";

    private static async Task<StageOneCollectionTeacherQueueBuildResult?>
        TryBuildStageOneGingerIslandPrerequisiteControlAsync(
            LiveTrainingOptions options,
            string stateHash,
            SnapshotEnvelope snapshot,
            AvailabilityAwarePolicyPredictionEnvelope ranking,
            JsonObject rankingNode,
            string snapshotPath,
            string selectionPath,
            string planPath,
            string rankingPath,
            string queuePath)
    {
        var candidates = ranking.RankedEventCandidates
            .Where(candidate =>
                candidate.Available &&
                HasGingerIslandPrerequisiteControlMarker(
                    candidate.Parameters))
            .OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return null;
        }

        var selectedSource = candidates[0];
        var selected = JsonSerializer.Deserialize<PolicyEventCandidatePrediction>(
            JsonSerializer.Serialize(selectedSource, JsonOptions),
            JsonOptions) ?? throw new InvalidDataException(
            "Ginger Island prerequisite control candidate could not be cloned.");
        selected.Rank = 1;
        selected.Score = 0;
        selected.ModelScore = null;
        selected.ExpectedReward = 0;
        selected.PolicyModelSource =
            "deterministic_control.ginger_island_prerequisite.v1";

        var plan = new DailyPlanCompiler().Compile(
            new[] { selected },
            stateHash,
            goalId: options.Goal,
            maxCandidates: 1);
        var queue = new ActionQueueCompiler().Compile(plan, snapshot);
        var reasons = plan.CandidateAudit
            .Where(value => value.Decision != "accepted")
            .SelectMany(value => value.Reasons)
            .Concat(queue.CompilerDiagnostics)
            .Concat(queue.Items.SelectMany(value =>
                value.BlockingReasons.Concat(value.MissingStateFactors)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (plan.Steps.Length == 0 ||
            queue.Status != "pending" ||
            queue.Items.Length == 0 ||
            queue.Items.Any(value => value.Status != "pending") ||
            reasons.Length > 0)
        {
            return new StageOneCollectionTeacherQueueBuildResult(
                true,
                null,
                rankingNode,
                snapshotPath,
                string.Empty,
                rankingPath,
                string.Empty,
                string.Empty,
                "blocked_ginger_island_prerequisite_control_not_dispatchable",
                string.Join(",", reasons.DefaultIfEmpty(
                    "ginger_island_prerequisite_control_did_not_compile_to_a_pending_queue")));
        }

        await WriteTypedJsonAsync(
            selectionPath,
            new
            {
                schema_version =
                    "stage_one_ginger_island_prerequisite_control_selection.v1",
                status = "ready",
                source_state_hash = stateHash,
                selected_candidate_id = selected.CandidateId,
                eligible_control_candidate_ids = candidates
                    .Select(candidate => candidate.CandidateId)
                    .ToArray(),
                selection_reason =
                    "stable_controller_prerequisite_order_without_model_score",
                teacher_preference_label_eligible = false,
                formal_training_authorized = false
            });
        await WriteTypedJsonAsync(planPath, plan);
        await WriteTypedJsonAsync(queuePath, queue);
        var queueNode = JsonNode.Parse(JsonSerializer.Serialize(
            queue,
            JsonOptions))?.AsObject() ?? throw new InvalidDataException(
            "Ginger Island prerequisite control queue is empty.");
        return new StageOneCollectionTeacherQueueBuildResult(
            true,
            queueNode,
            rankingNode,
            snapshotPath,
            planPath,
            rankingPath,
            queuePath,
            selected.CandidateId,
            "ready_ginger_island_prerequisite_control",
            "teacher_preference_label_eligible=false" +
            " formal_training_authorized=false" +
            " selection_reason=stable_controller_prerequisite_order_without_model_score");
    }

    private static bool HasGingerIslandPrerequisiteControlMarker(
        IEnumerable<SmallModelActionParameter> parameters) =>
        parameters.Any(parameter =>
            parameter.Name is
                GingerIslandPrerequisiteControlParameter or
                "continuation.master_angler_ginger_island_prerequisite_control_only" &&
            parameter.Value == "true");
}
