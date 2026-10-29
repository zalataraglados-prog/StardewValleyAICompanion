using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private sealed record StageOneCollectionTeacherQueueBuildResult(
        bool Attempted,
        JsonObject? Queue,
        JsonObject? Ranking,
        string SnapshotPath,
        string PlanPath,
        string RankingPath,
        string QueuePath,
        string SelectedCandidateId,
        string Status,
        string Detail);

    private static async Task<StageOneCollectionTeacherQueueBuildResult>
        TryBuildStageOneCollectionTeacherQueueAsync(
            HttpClient http,
            LiveTrainingOptions options,
            int iteration,
            string snapshotJson,
            JsonObject snapshot,
            string stateHash,
            bool capacityRecoveryOnly = false,
            bool allowObjectiveSafeCapacityRecovery = false,
            bool requireFishingAttemptBlockedForCapacityRecovery = true,
            JsonObject? activeObjectiveContinuation = null,
            IReadOnlyCollection<JsonObject>? suppressedObjectiveContinuations = null)
    {
        if ((!capacityRecoveryOnly &&
             !options.UseStageOneCollectionTeacher) ||
            HasPriorityStoryEventInterrupt(snapshot))
        {
            return NotAttemptedStageOneCollectionTeacher();
        }

        var prefix = Path.Combine(
            options.SnapshotDir,
            $"stage-one-collection-{{0}}-{iteration:D4}.json");
        var plainSnapshotPath = string.Format(prefix, "snapshot");
        var intentsPath = string.Format(prefix, "intents");
        var rankingPath = string.Format(prefix, "ranking");
        var preferencePath = string.Format(prefix, "preference");
        var planPath = string.Format(prefix, "compiled-plan");
        var queuePath = string.Format(prefix, "compiled-queue");

        await File.WriteAllTextAsync(
            plainSnapshotPath,
            snapshotJson,
            new UTF8Encoding(false));
        JsonObject ranking;
        AvailabilityAwarePolicyPredictionEnvelope typedRanking;
        if (capacityRecoveryOnly)
        {
            var availabilityResponse = await PostJsonStringAsync(
                http,
                options.BackendUrl + "/api/v1/planner/options/availability",
                JsonSerializer.Serialize(
                    new OptionAvailabilityRequest
                    {
                        StateHash = stateHash
                    },
                    JsonOptions));
            var availability = JsonSerializer.Deserialize<
                OptionAvailabilityEnvelope>(
                availabilityResponse.ToJsonString(JsonOptions),
                JsonOptions) ?? throw new InvalidDataException(
                    "Inventory-capacity availability response is empty.");
            typedRanking = new AvailabilityAwarePolicyPredictionEnvelope
            {
                Availability = availability,
                RankedEventCandidates = new EventCandidateRanker().Rank(
                    new BaselineTrainingReport(),
                    availability,
                    options.Goal)
            };
            ranking = JsonNode.Parse(JsonSerializer.Serialize(
                typedRanking,
                JsonOptions))?.AsObject() ?? throw new InvalidDataException(
                    "Inventory-capacity ranking response is empty.");
        }
        else
        {
            var intents = MasterAnglerTargetDateIntentBuilder.Build(
                options.MasterAnglerStageOneWindowIndexPath,
                plainSnapshotPath,
                options.RouteTimingCalibrationPath);
            await WriteTypedJsonAsync(intentsPath, intents);

            var additionalCandidates = intents.Candidates
                .Select(value => new OptionAvailabilityCandidate
                {
                    OptionId = value.OptionId,
                    Parameters = value.Parameters
                })
                .Concat(intents.WeatherSupplyCandidates.Select(value =>
                    new OptionAvailabilityCandidate
                    {
                        OptionId = value.OptionId,
                        Parameters = value.Parameters
                    }))
                .ToArray();
            var rankRequest = new BaselinePredictionRequest
            {
                GoalId = options.Goal,
                ExecutionMode = options.TargetExecutionMode,
                PolicyCheckpointPath = string.IsNullOrWhiteSpace(
                    options.EffectivePolicyCheckpointPath)
                    ? null
                    : Path.GetFullPath(options.EffectivePolicyCheckpointPath),
                RequireStructuredPolicy = options.RequireStructuredPolicy,
                DatasetPath = Path.GetFullPath(options.DatasetPath),
                StateHash = stateHash,
                IncludeBlockedOptions = false,
                AdditionalCandidates = additionalCandidates
            };
            ranking = await PostJsonStringAsync(
                http,
                options.BackendUrl + "/api/v1/planner/baseline/rank-options",
                JsonSerializer.Serialize(rankRequest, JsonOptions));
            typedRanking = JsonSerializer.Deserialize<
                AvailabilityAwarePolicyPredictionEnvelope>(
                ranking.ToJsonString(JsonOptions),
                JsonOptions) ?? throw new InvalidDataException(
                    "Stage-one collection ranking response is empty.");
        }
        var mandatoryMenuRecovery = capacityRecoveryOnly
            ? null
            : StageOneMandatoryMenuRecovery.TrySelect(typedRanking);
        if (mandatoryMenuRecovery is not null)
        {
            var typedSnapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
                snapshotJson,
                JsonOptions) ?? throw new InvalidDataException(
                    "Stage-one collection snapshot is empty.");
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
                    ["selected_candidate_id"] =
                        mandatoryMenuRecovery.CandidateId,
                    ["candidate_kind"] = mandatoryMenuRecovery.Kind,
                    ["policy"] =
                        "available_mandatory_menu_recovery_preempts_" +
                        "collection_membership_and_strategy"
                };
            await WriteJsonAsync(rankingPath, ranking);
            await WriteTypedJsonAsync(
                preferencePath,
                new
                {
                    schema_version =
                        "stage_one_mandatory_menu_recovery_control.v1",
                    status = "ready",
                    source_state_hash = stateHash,
                    selected_candidate_id =
                        mandatoryMenuRecovery.CandidateId,
                    candidate_kind = mandatoryMenuRecovery.Kind,
                    selection_reason =
                        "available_mandatory_menu_recovery_preempts_" +
                        "collection_membership_and_strategy",
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
        if (!capacityRecoveryOnly && activeObjectiveContinuation is not null)
        {
            var rankedCandidates = ranking["ranked_event_candidates"]?
                .AsArray() ?? new JsonArray();
            var filteredCandidates = QueueReplanFilter.FilterRankedCandidates(
                rankedCandidates,
                activeObjectiveContinuation);
            ranking["ranked_event_candidates"] = filteredCandidates;
            ranking["objective_continuation_filter"] = new JsonObject
            {
                ["active"] = true,
                ["exclusive_recovery_override"] = false,
                ["objective"] = JsonNode.Parse(
                    activeObjectiveContinuation.ToJsonString()),
                ["input_candidate_count"] = rankedCandidates.Count,
                ["selected_candidate_count"] = filteredCandidates.Count,
                ["policy"] =
                    "typed_objective_identity_match;fail_closed_no_objective_switch"
            };
            typedRanking = JsonSerializer.Deserialize<
                AvailabilityAwarePolicyPredictionEnvelope>(
                ranking.ToJsonString(JsonOptions),
                JsonOptions) ?? throw new InvalidDataException(
                    "Filtered stage-one collection ranking response is empty.");
        }
        await WriteJsonAsync(rankingPath, ranking);
        var capacityRecovery = StageOneInventoryCapacityRecovery.TrySelect(
            typedRanking,
            snapshot,
            allowObjectiveSafeCapacityRecovery,
            requireFishingAttemptBlockedForCapacityRecovery);
        if (capacityRecovery is not null)
        {
            var typedSnapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
                snapshotJson,
                JsonOptions) ?? throw new InvalidDataException(
                    "Stage-one collection snapshot is empty.");
            var recoveryPlan = new DailyPlanCompiler().Compile(
                new[] { capacityRecovery.Candidate },
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
                    "blocked_inventory_capacity_recovery_not_dispatchable",
                    capacityRecovery.Candidate.CandidateId);
            }

            await WriteTypedJsonAsync(
                preferencePath,
                new
                {
                    schema_version =
                        "stage_one_inventory_capacity_recovery.v1",
                    status = "ready",
                    formal_training_authorized = false,
                    source_state_hash = stateHash,
                    trigger_reason = capacityRecovery.TriggerReason,
                    caught_qualified_item_id =
                        capacityRecovery.CaughtQualifiedItemId,
                    released_qualified_item_id =
                        capacityRecovery.ReleasedQualifiedItemId,
                    target_stack_quantity_before =
                        capacityRecovery.TargetStackQuantityBefore,
                    selection_reason = capacityRecovery.SelectionReason,
                    selected_candidate_id =
                        capacityRecovery.Candidate.CandidateId
                });
            await WriteTypedJsonAsync(planPath, recoveryPlan);
            await WriteTypedJsonAsync(queuePath, recoveryQueue);
            var queueNode = JsonNode.Parse(JsonSerializer.Serialize(
                recoveryQueue,
                JsonOptions))?.AsObject() ?? throw new InvalidDataException(
                    "Stage-one inventory-capacity recovery queue is empty.");
            return new StageOneCollectionTeacherQueueBuildResult(
                true,
                queueNode,
                ranking,
                plainSnapshotPath,
                planPath,
                rankingPath,
                queuePath,
                capacityRecovery.Candidate.CandidateId,
                "ready_inventory_capacity_recovery",
                "control_recovery=inventory_capacity" +
                " released_qualified_item_id=" +
                capacityRecovery.ReleasedQualifiedItemId +
                " target_stack_quantity_before=" +
                capacityRecovery.TargetStackQuantityBefore +
                " selection_reason=" +
                capacityRecovery.SelectionReason +
                " formal_training_authorized=false");
        }

        if (capacityRecoveryOnly)
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
                "not_applicable_inventory_capacity_recovery",
                "no_dispatchable_inventory_release_candidate");
        }

        if (suppressedObjectiveContinuations is { Count: > 0 })
        {
            var rankedCandidates = ranking["ranked_event_candidates"]?
                .AsArray() ?? new JsonArray();
            var unsuppressedCandidates =
                QueueReplanFilter.FilterSuppressedContinuations(
                    rankedCandidates,
                    suppressedObjectiveContinuations);
            ranking["ranked_event_candidates"] = unsuppressedCandidates;
            ranking["suppressed_objective_filter"] = new JsonObject
            {
                ["active"] = true,
                ["input_candidate_count"] = rankedCandidates.Count,
                ["suppressed_objective_count"] =
                    suppressedObjectiveContinuations.Count,
                ["selected_candidate_count"] =
                    unsuppressedCandidates.Count,
                ["policy"] =
                    "same_typed_objective_suppressed_until_game_day_changes"
            };
            typedRanking = JsonSerializer.Deserialize<
                AvailabilityAwarePolicyPredictionEnvelope>(
                ranking.ToJsonString(JsonOptions),
                JsonOptions) ?? throw new InvalidDataException(
                "Suppression-filtered stage-one collection ranking is empty.");
            await WriteJsonAsync(rankingPath, ranking);
        }

        var preference =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                options.StageOneCollectionRequirementInventoryPath,
                options.StageOneCollectionAcquisitionLoweringPath,
                rankingPath,
                plainSnapshotPath,
                intentsPath);
        await WriteTypedJsonAsync(preferencePath, preference);
        if (string.Equals(
                preference.Status,
                "blocked_authoritatively_tied_top_candidates",
                StringComparison.Ordinal) &&
            preference.OrderedCandidateEvaluations.Length > 0)
        {
            var selectedCandidateId =
                preference.OrderedCandidateEvaluations[0].CandidateId;
            var selectedSource = typedRanking.RankedEventCandidates
                .SingleOrDefault(value => string.Equals(
                    value.CandidateId,
                    selectedCandidateId,
                    StringComparison.Ordinal));
            if (selectedSource is null)
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
                    "blocked_authoritative_tie_control_candidate_missing",
                    selectedCandidateId);
            }

            var controlCandidate = JsonSerializer.Deserialize<
                PolicyEventCandidatePrediction>(
                JsonSerializer.Serialize(selectedSource, JsonOptions),
                JsonOptions) ?? throw new InvalidDataException(
                    "Authoritative-tie control candidate could not be cloned.");
            controlCandidate.Rank = 1;
            controlCandidate.Score = 0;
            controlCandidate.ModelScore = null;
            controlCandidate.ExpectedReward = 0;
            controlCandidate.PolicyModelSource =
                "deterministic_control.current_stage_one_collection_tie.v1";
            var controlPlan = new DailyPlanCompiler().Compile(
                new[] { controlCandidate },
                stateHash,
                goalId: options.Goal,
                maxCandidates: 1);
            var controlQueue = new ActionQueueCompiler().Compile(
                controlPlan,
                JsonSerializer.Deserialize<SnapshotEnvelope>(
                    snapshotJson,
                    JsonOptions) ?? throw new InvalidDataException(
                    "Stage-one collection snapshot is empty."));
            var controlReasons = controlPlan.CandidateAudit
                .Where(value => value.Decision != "accepted")
                .SelectMany(value => value.Reasons)
                .Concat(controlQueue.CompilerDiagnostics)
                .Concat(controlQueue.Items.SelectMany(value =>
                    value.BlockingReasons.Concat(value.MissingStateFactors)))
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (controlPlan.Steps.Length == 0 ||
                controlQueue.Status != "pending" ||
                controlQueue.Items.Length == 0 ||
                controlQueue.Items.Any(value => value.Status != "pending") ||
                controlReasons.Length > 0)
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
                    "blocked_authoritative_tie_control_not_dispatchable",
                    string.Join(",", controlReasons.DefaultIfEmpty(
                        "authoritative_tie_control_did_not_compile_to_a_pending_queue")));
            }

            var controlSelectionPath = string.Format(prefix, "control-selection");
            await WriteTypedJsonAsync(
                controlSelectionPath,
                new
                {
                    schema_version =
                        "stage_one_collection_control_selection.v1",
                    status = "ready_authoritative_tie_control",
                    source_state_hash = stateHash,
                    selected_candidate_id = selectedCandidateId,
                    authoritatively_tied_candidate_ids = preference
                        .BlockingReasons
                        .Where(value => value.StartsWith(
                            "authoritatively_tied_with_top_candidate:",
                            StringComparison.Ordinal))
                        .Select(value => value[
                            "authoritatively_tied_with_top_candidate:"
                                .Length..])
                        .ToArray(),
                    selection_reason =
                        "stable_controller_tiebreak_only",
                    teacher_preference_label_eligible = false,
                    formal_training_authorized = false
                });
            await WriteTypedJsonAsync(planPath, controlPlan);
            await WriteTypedJsonAsync(queuePath, controlQueue);
            var queueNode = JsonNode.Parse(JsonSerializer.Serialize(
                controlQueue,
                JsonOptions))?.AsObject() ?? throw new InvalidDataException(
                "Authoritative-tie control queue is empty.");
            return new StageOneCollectionTeacherQueueBuildResult(
                true,
                queueNode,
                ranking,
                plainSnapshotPath,
                planPath,
                rankingPath,
                queuePath,
                selectedCandidateId,
                "ready_authoritative_tie_control",
                "teacher_preference_label_eligible=false" +
                " formal_training_authorized=false" +
                " control_selection=" + controlSelectionPath);
        }

        if (!string.Equals(preference.Status, "ready", StringComparison.Ordinal) ||
            !preference.TeacherPreferenceLabelEligible ||
            preference.CompiledPlan is null ||
            preference.CompiledQueue is null ||
            preference.SelectedCandidate is null)
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
                preference.Status,
                string.Join(",", preference.BlockingReasons));
        }

        await WriteTypedJsonAsync(planPath, preference.CompiledPlan);
        await WriteTypedJsonAsync(queuePath, preference.CompiledQueue);
        var queue = await TeacherPreferenceQueueLoader.LoadAsync(
            preferencePath,
            stateHash,
            options.TargetExecutionMode);
        return new StageOneCollectionTeacherQueueBuildResult(
            true,
            queue,
            ranking,
            plainSnapshotPath,
            planPath,
            rankingPath,
            queuePath,
            preference.SelectedCandidate.CandidateId,
            preference.Status,
            "formal_training_authorized=false");
    }

    private static StageOneCollectionTeacherQueueBuildResult
        NotAttemptedStageOneCollectionTeacher() => new(
            false,
            null,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            string.Empty,
            "not_attempted",
            string.Empty);
}
