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
        TryBuildStageOneMandatoryMenuRecoveryQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string snapshotJson,
            JsonObject snapshot,
            string stateHash)
    {
        if (!HasOpenMenuForMandatoryRecovery(snapshot))
        {
            return null;
        }

        var typedSnapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
            snapshotJson,
            JsonOptions) ?? throw new InvalidDataException(
            "Mandatory-menu recovery snapshot is empty.");
        if (!StageOneMandatoryMenuRecovery.RequiresPreflight(typedSnapshot))
        {
            return null;
        }

        var prefix = Path.Combine(
            options.SnapshotDir,
            $"stage-one-collection-{{0}}-{iteration:D4}.json");
        var plainSnapshotPath = string.Format(prefix, "snapshot");
        var preferencePath = string.Format(prefix, "preference");
        var planPath = string.Format(prefix, "compiled-plan");
        var rankingPath = string.Format(prefix, "ranking");
        var queuePath = string.Format(prefix, "compiled-queue");
        await File.WriteAllTextAsync(
            plainSnapshotPath,
            snapshotJson,
            new System.Text.UTF8Encoding(false));
        return await TryBuildStageOneMandatoryMenuRecoveryAsync(
            http,
            options,
            typedSnapshot,
            stateHash,
            plainSnapshotPath,
            preferencePath,
            planPath,
            rankingPath,
            queuePath);
    }

    private static bool HasOpenMenuForMandatoryRecovery(JsonObject snapshot)
    {
        var state = snapshot["state"] as JsonObject;
        var menus = state?["menus"] as JsonObject;
        var activeMenuField = menus?["active_menu"] as JsonObject;
        var activeMenu = activeMenuField?["value"];
        if (activeMenu is JsonValue scalar &&
            scalar.TryGetValue<string>(out var menuType))
        {
            return !string.Equals(
                menuType,
                "none",
                StringComparison.OrdinalIgnoreCase);
        }

        return activeMenu is JsonObject menu &&
            menu["is_open"] is JsonValue openValue &&
            openValue.TryGetValue<bool>(out var isOpen) &&
            isOpen;
    }

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
        var ranking = JsonNode.Parse(JsonSerializer.Serialize(
            typedRanking,
            JsonOptions))?.AsObject() ?? throw new InvalidDataException(
            "Mandatory-menu recovery ranking response is empty.");
        var professionChoiceRequired =
            StageOneMandatoryMenuRecovery.RequiresExplicitProfessionChoice(
                typedSnapshot);
        var preferredProfessionCandidateId =
            options.DailyPlanCandidateOptionIds.Contains(
                "skills.choose_profession",
                StringComparer.Ordinal) &&
            string.Equals(
                options.DailyPlanCandidateKind,
                StageOneMandatoryMenuRecovery.ProfessionChoiceKind,
                StringComparison.Ordinal)
                ? options.DailyPlanCandidateId
                : string.Empty;
        var mandatoryMenuRecovery =
            StageOneMandatoryMenuRecovery.TrySelect(
                typedRanking,
                professionChoiceRequired,
                preferredProfessionCandidateId);
        if (mandatoryMenuRecovery is null)
        {
            const string blockedStatus =
                "blocked_mandatory_menu_recovery_unavailable";
            var blockedReason = professionChoiceRequired
                ? "explicit_profession_choice_unavailable"
                : "mandatory_menu_recovery_unavailable";
            ranking["stage_one_mandatory_menu_recovery_control"] =
                new JsonObject
                {
                    ["active"] = true,
                    ["blocked"] = true,
                    ["profession_choice_required"] = professionChoiceRequired,
                    ["preferred_profession_candidate_id"] =
                        preferredProfessionCandidateId,
                    ["reason"] = blockedReason
                };
            await WriteJsonAsync(rankingPath, ranking);
            await WriteTypedJsonAsync(
                preferencePath,
                new
                {
                    schema_version =
                        "stage_one_mandatory_menu_recovery_control.v2",
                    status = "blocked",
                    source_state_hash = stateHash,
                    block_reason = blockedReason,
                    teacher_preference_label_eligible = false,
                    formal_training_authorized = false
                });
            return new StageOneCollectionTeacherQueueBuildResult(
                true,
                null,
                ranking,
                plainSnapshotPath,
                string.Empty,
                rankingPath,
                string.Empty,
                string.Empty,
                blockedStatus,
                "control_recovery=mandatory_menu block_reason=" +
                blockedReason +
                " teacher_preference_label_eligible=false" +
                " formal_training_authorized=false");
        }

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
                ["preferred_profession_candidate_id"] =
                    preferredProfessionCandidateId,
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
