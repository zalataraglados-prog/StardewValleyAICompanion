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
            bool allowObjectiveSafeSingletonCapacityRecovery = false,
            bool requireFishingAttemptBlockedForCapacityRecovery = true)
    {
        if ((!capacityRecoveryOnly &&
             !options.RequireGoldenWalnutSourceScheduleReady) ||
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
        await WriteJsonAsync(rankingPath, ranking);
        var capacityRecovery = StageOneInventoryCapacityRecovery.TrySelect(
            typedRanking,
            snapshot,
            allowObjectiveSafeSingletonCapacityRecovery,
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

        var preference =
            CurrentStageOneCollectionTeacherPreferenceBuilder.Build(
                options.StageOneCollectionRequirementInventoryPath,
                options.StageOneCollectionAcquisitionLoweringPath,
                rankingPath,
                plainSnapshotPath,
                intentsPath);
        await WriteTypedJsonAsync(preferencePath, preference);
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
