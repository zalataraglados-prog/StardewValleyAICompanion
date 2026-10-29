using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Training;

static partial class Program
{
    private static async Task<StageOneCollectionTeacherQueueBuildResult?>
        TryBuildNativeControlSettleQueueAsync(
            LiveTrainingOptions options,
            string snapshotJson,
            string stateHash,
            string snapshotPath,
            string preferencePath,
            string planPath,
            string rankingPath,
            string queuePath)
    {
        var typedSnapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
            snapshotJson,
            JsonOptions) ?? throw new InvalidDataException(
                "Stage-one collection snapshot is empty.");
        var sourceCandidate = NativeControlSettleRecovery.TryCreate(
            typedSnapshot);
        if (sourceCandidate is null)
        {
            return null;
        }

        var availability = new OptionAvailabilityEnvelope
        {
            StateHash = stateHash,
            CurrentTime = typedSnapshot.InGameTime.Value ?? 600,
            Options = new[]
            {
                new OptionAvailability
                {
                    OptionId = "recovery.stabilize_day",
                    Available = true,
                    ReadEligible = true,
                    BindingStatus = "bound",
                    CompileStatus = "ready",
                    ExecutionAuthorization = "authorized",
                    Status = "ready",
                    ExecutorEnabled = true,
                    EventCandidates = new[] { sourceCandidate }
                }
            }
        };
        var candidates = new EventCandidateRanker().Rank(
            new BaselineTrainingReport(),
            availability,
            options.Goal);
        var candidate = candidates.SingleOrDefault(value =>
            string.Equals(
                value.CandidateId,
                NativeControlSettleRecovery.CandidateId,
                StringComparison.Ordinal));
        if (candidate is null)
        {
            return new StageOneCollectionTeacherQueueBuildResult(
                true,
                null,
                null,
                snapshotPath,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "blocked_native_control_settle_candidate_missing",
                "deterministic_native_control_settle_candidate_not_ranked");
        }

        candidate.PolicyModelSource =
            "deterministic_control.native_control_settle.v1";
        candidate.ModelScore = null;
        var typedRanking = new AvailabilityAwarePolicyPredictionEnvelope
        {
            Availability = availability,
            RankedEventCandidates = candidates
        };
        var ranking = JsonNode.Parse(JsonSerializer.Serialize(
            typedRanking,
            JsonOptions))?.AsObject() ?? throw new InvalidDataException(
                "Native-control settle ranking is empty.");
        var plan = new DailyPlanCompiler().Compile(
            new[] { candidate },
            stateHash,
            goalId: options.Goal,
            executionMode: options.TargetExecutionMode,
            maxCandidates: 1);
        var queue = new ActionQueueCompiler().Compile(plan, typedSnapshot);
        var blockingReasons = plan.CandidateAudit
            .Where(value => value.Decision != "accepted")
            .SelectMany(value => value.Reasons)
            .Concat(queue.CompilerDiagnostics)
            .Concat(queue.Items.SelectMany(value =>
                value.BlockingReasons.Concat(value.MissingStateFactors)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (plan.Steps.Length != 1 ||
            queue.Status != "pending" ||
            queue.Items.Length != 1 ||
            queue.Items[0].Status != "pending" ||
            blockingReasons.Length > 0)
        {
            return new StageOneCollectionTeacherQueueBuildResult(
                true,
                null,
                ranking,
                snapshotPath,
                string.Empty,
                rankingPath,
                string.Empty,
                string.Empty,
                "blocked_native_control_settle_not_dispatchable",
                string.Join(",", blockingReasons.DefaultIfEmpty(
                    "native_control_settle_did_not_compile_to_one_pending_item")));
        }

        await WriteJsonAsync(rankingPath, ranking);
        await WriteTypedJsonAsync(
            preferencePath,
            new
            {
                schema_version = "native_control_settle_selection.v1",
                status = "ready_native_control_settle",
                source_state_hash = stateHash,
                selected_candidate_id = candidate.CandidateId,
                wait_ticks = NativeControlSettleRecovery.WaitTicks,
                selection_reason =
                    "exact_compatible_movement_context_waiting_for_native_CanMove",
                teacher_preference_label_eligible = false,
                formal_training_authorized = false
            });
        await WriteTypedJsonAsync(planPath, plan);
        await WriteTypedJsonAsync(queuePath, queue);
        var queueNode = JsonNode.Parse(JsonSerializer.Serialize(
            queue,
            JsonOptions))?.AsObject() ?? throw new InvalidDataException(
                "Native-control settle queue is empty.");
        return new StageOneCollectionTeacherQueueBuildResult(
            true,
            queueNode,
            ranking,
            snapshotPath,
            planPath,
            rankingPath,
            queuePath,
            candidate.CandidateId,
            "ready_native_control_settle",
            "wait_ticks=" + NativeControlSettleRecovery.WaitTicks +
            " teacher_preference_label_eligible=false" +
            " formal_training_authorized=false");
    }
}
