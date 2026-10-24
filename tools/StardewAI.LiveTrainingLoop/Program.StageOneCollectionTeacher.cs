using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Training;
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
            string stateHash)
    {
        if (!options.RequireGoldenWalnutSourceScheduleReady ||
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
        var ranking = await PostJsonStringAsync(
            http,
            options.BackendUrl + "/api/v1/planner/baseline/rank-options",
            JsonSerializer.Serialize(rankRequest, JsonOptions));
        await WriteJsonAsync(rankingPath, ranking);

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
