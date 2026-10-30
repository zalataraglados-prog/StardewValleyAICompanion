using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static async Task<StageOneCollectionTeacherQueueBuildResult?>
        TryBuildStageOneMandatoryMenuRecoveryAsync(
            HttpClient http,
            LiveTrainingOptions options,
            SnapshotEnvelope typedSnapshot,
            string stateHash,
            string plainSnapshotPath,
            string preferencePath,
            string planPath,
            string rankingPath,
            string queuePath)
    {
        if (!StageOneMandatoryMenuRecovery.RequiresPreflight(typedSnapshot))
        {
            return null;
        }

        var availabilityResponse = await PostJsonStringAsync(
            http,
            options.BackendUrl + "/api/v1/planner/options/availability",
            JsonSerializer.Serialize(
                new OptionAvailabilityRequest
                {
                    StateHash = stateHash,
                    CandidateOptionIds =
                    [
                        "recovery.stabilize_day",
                        "skills.choose_profession",
                        "mail.process_letter"
                    ]
                },
                JsonOptions));
        var availability = JsonSerializer.Deserialize<OptionAvailabilityEnvelope>(
            availabilityResponse.ToJsonString(JsonOptions),
            JsonOptions) ?? throw new InvalidDataException(
            "Mandatory-menu recovery availability response is empty.");
        var typedRanking = StageOneMandatoryMenuRecovery.BuildRanking(
            availability,
            options.Goal);
        var mandatoryMenuRecovery =
            StageOneMandatoryMenuRecovery.TrySelect(typedRanking);
        if (mandatoryMenuRecovery is null)
        {
            return null;
        }

        var ranking = JsonNode.Parse(JsonSerializer.Serialize(
            typedRanking,
            JsonOptions))?.AsObject() ?? throw new InvalidDataException(
            "Mandatory-menu recovery ranking response is empty.");
        var recoveryPlan = new DailyPlanCompiler().Compile(
            new[] { mandatoryMenuRecovery },
            stateHash,
            goalId: options.Goal,
            maxCandidates: 1);
        var recoveryQueue = new ActionQueueCompiler().Compile(
            recoveryPlan,
            typedSnapshot);
        if (recoveryPlan.Steps.Length == 0 ||
            recoveryQueue.Status != "pending" ||
            recoveryQueue.Items.Length == 0 ||
            recoveryQueue.Items.Any(item => item.Status != "pending"))
        {
            return new StageOneCollectionTeacherQueueBuildResult(
                true,
                null,
                ranking,
                plainSnapshotPath,
                string.Empty,
                rankingPath,
                string.Empty,
                string.Empty,
                "blocked_mandatory_menu_recovery_not_dispatchable",
                mandatoryMenuRecovery.CandidateId);
        }

        ranking["stage_one_mandatory_menu_recovery_control"] =
            new JsonObject
            {
                ["active"] = true,
                ["selected_candidate_id"] = mandatoryMenuRecovery.CandidateId,
                ["candidate_kind"] = mandatoryMenuRecovery.Kind,
                ["policy"] =
                    "available_mandatory_menu_recovery_preempts_" +
                    "collection_intent_building_membership_and_strategy"
            };
        await WriteJsonAsync(rankingPath, ranking);
        await WriteTypedJsonAsync(
            preferencePath,
            new
            {
                schema_version =
                    "stage_one_mandatory_menu_recovery_control.v2",
                status = "ready",
                source_state_hash = stateHash,
                selected_candidate_id = mandatoryMenuRecovery.CandidateId,
                candidate_kind = mandatoryMenuRecovery.Kind,
                selection_reason =
                    "available_mandatory_menu_recovery_preempts_" +
                    "collection_intent_building_membership_and_strategy",
                teacher_preference_label_eligible = false,
                formal_training_authorized = false
            });
        await WriteTypedJsonAsync(planPath, recoveryPlan);
        await WriteTypedJsonAsync(queuePath, recoveryQueue);
        var queueNode = JsonNode.Parse(JsonSerializer.Serialize(
            recoveryQueue,
            JsonOptions))?.AsObject() ?? throw new InvalidDataException(
                "Stage-one mandatory menu recovery queue is empty.");
        return new StageOneCollectionTeacherQueueBuildResult(
            true,
            queueNode,
            ranking,
            plainSnapshotPath,
            planPath,
            rankingPath,
            queuePath,
            mandatoryMenuRecovery.CandidateId,
            "ready_mandatory_menu_recovery_control",
            "control_recovery=mandatory_menu candidate_kind=" +
            mandatoryMenuRecovery.Kind +
            " teacher_preference_label_eligible=false" +
            " formal_training_authorized=false");
    }
}
