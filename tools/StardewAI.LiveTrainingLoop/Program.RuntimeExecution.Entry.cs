using System.Text.Json.Nodes;
using StardewAI.LiveTrainingLoop;

static partial class Program
{
    private static Task<JsonObject> ExecuteRuntimeTestHarnessAsync(HttpClient http, HttpClient executorHttp,
        LiveTrainingOptions options, int iteration, string beforeSnapshotPath, JsonObject beforeSnapshot, JsonObject queue,
        string stateHash, string queueId, string decisionModelPlanPath, string decisionRankingPath,
        string decisionCompiledQueuePath, string decisionSnapshotPath, string decisionSourceStateHash,
        string initialSelectedCandidateId, int initialSelectedQueueIndex, string initialMechanicalCompiledQueuePath,
        JsonObject? objectiveContinuation, List<JsonObject> suppressedObjectiveContinuations,
        bool useDailyPlanDecisionLease = true, string? executionSnapshotUrlOverride = null, string? executionSnapshotProfileOverride = null) =>
        ExecuteRuntimeTestHarnessCoreAsync(http, executorHttp, options, iteration, beforeSnapshotPath, beforeSnapshot, queue,
            stateHash, queueId, decisionModelPlanPath, decisionRankingPath, decisionCompiledQueuePath, decisionSnapshotPath,
            decisionSourceStateHash, initialSelectedCandidateId, initialSelectedQueueIndex, initialMechanicalCompiledQueuePath,
            objectiveContinuation, suppressedObjectiveContinuations, useDailyPlanDecisionLease, executionSnapshotUrlOverride, executionSnapshotProfileOverride);
}
