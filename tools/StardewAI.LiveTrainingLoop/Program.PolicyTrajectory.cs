using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static PolicyTrajectoryAppendBatchResult AppendPolicyDecisionTrajectories(
        LiveTrainingOptions options,
        int iteration,
        JsonObject aggregateExecution,
        TrainingDatasetAppendResult appendResult)
    {
        var result = new PolicyTrajectoryAppendBatchResult();
        var policyRunId = !string.IsNullOrWhiteSpace(options.RunId)
            ? options.RunId
            : !string.IsNullOrWhiteSpace(options.ArtifactRunId)
                ? options.ArtifactRunId
                : "live-training";
        var steps = aggregateExecution["step_results"] as JsonArray;
        if (steps is null || steps.Count == 0)
        {
            steps = new JsonArray(JsonNode.Parse(aggregateExecution.ToJsonString(JsonOptions)));
        }

        var builder = new PolicyDecisionTrajectoryBuilder();
        var writer = new JsonlPolicyTrajectoryWriter();
        var emittedDecisions = new HashSet<string>(StringComparer.Ordinal);
        var stepOrdinal = 0;
        foreach (var stepNode in steps)
        {
            stepOrdinal++;
            if (stepNode is not JsonObject step)
            {
                result.Skip("step_not_object");
                continue;
            }

            if (!QueueReplanFilter.IsTrainingVerifiedExecution(step) ||
                step["after_snapshot_fresh"]?.GetValue<bool>() != true)
            {
                result.Skip("execution_not_verified_applied_fresh");
                continue;
            }

            var rankingPath = ReadString(step, "effective_ranking_path");
            var decisionSourceHash = ReadString(step, "effective_decision_source_state_hash");
            var executionSourceHash = ReadString(step, "effective_before_state_hash");
            var candidateId = EffectiveDecisionArtifactTracker.ReadCandidateId(step);
            if (string.IsNullOrWhiteSpace(rankingPath) || !File.Exists(rankingPath))
            {
                result.Skip("effective_ranking_missing");
                continue;
            }
            if (!string.Equals(decisionSourceHash, executionSourceHash, StringComparison.Ordinal))
            {
                result.Skip("decision_execution_source_hash_mismatch");
                continue;
            }
            if (string.IsNullOrWhiteSpace(candidateId))
            {
                result.Skip("effective_candidate_id_missing");
                continue;
            }

            var decisionKey = rankingPath + "\n" + decisionSourceHash + "\n" + candidateId;
            if (!emittedDecisions.Add(decisionKey))
            {
                result.Skip("decision_already_emitted");
                continue;
            }

            try
            {
                var decision = JsonSerializer.Deserialize<AvailabilityAwarePolicyPredictionEnvelope>(
                    File.ReadAllText(rankingPath),
                    JsonOptions) ?? throw new InvalidOperationException("ranking response is empty");
                var selectedCandidate = decision.RankedEventCandidates?.SingleOrDefault(candidate =>
                    string.Equals(candidate.CandidateId, candidateId, StringComparison.Ordinal));
                if (selectedCandidate is not null && !selectedCandidate.Available)
                {
                    result.Skip("effective_candidate_unavailable");
                    continue;
                }
                var beforeSnapshotPath = ReadString(step, "effective_before_snapshot_path");
                if (string.IsNullOrWhiteSpace(beforeSnapshotPath) || !File.Exists(beforeSnapshotPath))
                {
                    result.Skip("effective_before_snapshot_missing");
                    continue;
                }

                var beforeSnapshot = JsonNode.Parse(File.ReadAllText(beforeSnapshotPath))?.AsObject()
                    ?? throw new InvalidOperationException("before snapshot is empty");
                var snapshotEnvelope = JsonSerializer.Deserialize<SnapshotEnvelope>(
                    beforeSnapshot.ToJsonString(JsonOptions),
                    JsonOptions) ?? throw new InvalidOperationException("before snapshot contract is empty");
                var stateFeatures = new PolicyStateFeatureProjector().Project(
                    new WorldModelProjector().Project(
                        snapshotEnvelope,
                        options.Goal,
                        options.TargetExecutionMode));
                var episodeId = appendResult.EpisodeId + ".policy." + iteration.ToString("D4") + "." + stepOrdinal.ToString("D4");
                var executionEpisode = BuildPolicyExecutionEpisode(
                    appendResult,
                    step,
                    episodeId,
                    policyRunId,
                    decisionSourceHash);
                var trajectory = builder.Build(
                    "trajectory." + policyRunId + "." + iteration.ToString("D4") + "." + stepOrdinal.ToString("D4"),
                    policyRunId,
                    BuildPolicyTrajectoryContext(beforeSnapshot),
                    stateFeatures,
                    new PolicyTrajectoryVersions
                    {
                        FeatureSchema = PolicyTrajectoryVersionPins.FeatureSchema,
                        CandidateVocabulary = OptionCapabilityRegistrySource.SchemaVersion,
                        CapabilityRegistry = OptionCapabilityRegistrySource.SchemaVersion,
                        KnowledgeDictionary = options.KnowledgeDictionaryVersion,
                        Compiler = PolicyTrajectoryVersionPins.Compiler,
                        Executor = options.PolicyTrajectoryExecutorVersion
                    },
                    decisionSourceHash,
                    decision,
                    candidateId,
                    executionEpisode);
                writer.Append(options.PolicyTrajectoryDatasetPath, trajectory);
                result.AppendedCount++;
            }
            catch (Exception ex) when (ex is IOException or JsonException or InvalidOperationException or ArgumentException)
            {
                result.Skip("trajectory_rejected:" + ex.GetType().Name);
            }
        }

        return result;
    }

    private static PlanExecutionEpisodeEnvelope BuildPolicyExecutionEpisode(
        TrainingDatasetAppendResult appendResult,
        JsonObject execution,
        string episodeId,
        string policyRunId,
        string sourceStateHash)
    {
        var optionId = ReadString(execution, "option_id");
        var applied = string.Equals(ReadString(execution, "status"), "applied", StringComparison.Ordinal);
        return new PlanExecutionEpisodeEnvelope
        {
            EpisodeId = episodeId,
            RunId = policyRunId,
            SourceStateHash = sourceStateHash,
            AfterStateHash = ReadString(execution, "after_state_hash"),
            StateHashChanged = execution["state_hash_changed"]?.GetValue<bool>() == true,
            BeforeGameTick = ReadLong(execution, "before_game_tick"),
            AfterGameTick = ReadLong(execution, "after_game_tick"),
            AfterSnapshotFresh = execution["after_snapshot_fresh"]?.GetValue<bool>() == true,
            ModelPlanPath = ReadString(execution, "effective_model_plan_path"),
            CompiledQueuePath = ReadString(execution, "effective_compiled_queue_path"),
            ExecutionResultPath = ReadString(execution, "execution_path"),
            BeforeSnapshotPath = ReadString(execution, "effective_before_snapshot_path"),
            AfterSnapshotPath = ReadString(execution, "after_snapshot_path"),
            DatasetPath = appendResult.DatasetPath,
            RowId = appendResult.RowId,
            QueueId = ReadString(execution, "effective_queue_id"),
            OptionId = optionId,
            Status = ReadString(execution, "status"),
            Success = applied,
            Reward = CalculateExecutionReward(execution, optionId, applied),
            TrainingRole = TrainingRoles.StrategyValue,
            FailureAttribution = applied ? string.Empty : "executor_calibration",
            BlockReasons = ReadArrayStrings(execution, "block_reasons"),
            PrimitiveKind = ReadString(execution, "primitive_kind"),
            PrimitiveVerificationStatus = ReadString(execution, "primitive_verification_status"),
            PrimitiveVerificationReasons = ReadArrayStrings(execution, "primitive_verification_reasons"),
            ChangedFacts = execution["changed_facts"] is null
                ? JsonDocument.Parse("[]").RootElement.Clone()
                : JsonSerializer.Deserialize<JsonElement>(execution["changed_facts"]!.ToJsonString())
        };
    }

    private static PolicyTrajectoryContext BuildPolicyTrajectoryContext(JsonObject snapshot)
    {
        return new PolicyTrajectoryContext
        {
            SaveId = ReadEnvelopeString(snapshot, "save_id"),
            Year = (int)ReadFieldDouble(snapshot, "time", "year"),
            Season = ReadFieldString(snapshot, "time", "season"),
            Day = (int)ReadFieldDouble(snapshot, "time", "day"),
            Time = (int)ReadFieldDouble(snapshot, "time", "time"),
            DatasetPartition = "live"
        };
    }

    private static string ReadEnvelopeString(JsonObject value, string property)
    {
        var node = value[property]?["value"];
        return node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var result)
            ? result
            : string.Empty;
    }
}
