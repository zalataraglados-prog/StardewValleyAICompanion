using System.Text.Json;
using System.Text.Json.Serialization;
using System.Reflection;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Contracts.State;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class NativeExecutionActionSourceReader
{
    private enum NativeProductBoundary { None, Tilling, NativeAction, WateringResources }
    internal static VerifiedNativeExecutionAction Read(string actionQueuePath, string executionReceiptPath, string afterSnapshotPath)
    {
        var frame = ReadFrame(actionQueuePath, executionReceiptPath, afterSnapshotPath, null);
        return new(frame.Queue, frame.Item, frame.Result, frame.Request, frame.Before, frame.After, frame.Endpoint, frame.SourceSha256);
    }

    // Only ReadQueue supplies an origin hash, after validating the complete immutable queue chain.
    private static NativeExecutionActionFrame ReadFrame(string actionQueuePath, string executionReceiptPath,
        string afterSnapshotPath, string? compiledStateHash, NativeProductBoundary productBoundary = NativeProductBoundary.None)
        => ReadFrameCore(actionQueuePath, executionReceiptPath, afterSnapshotPath, compiledStateHash, productBoundary, appliedNativeWork: false);

    private static NativeExecutionActionFrame ReadAppliedNativeWorkFrame(string actionQueuePath, string executionReceiptPath,
        string afterSnapshotPath, string compiledStateHash, NativeProductBoundary productBoundary)
        => ReadFrameCore(actionQueuePath, executionReceiptPath, afterSnapshotPath, compiledStateHash, productBoundary, appliedNativeWork: true);

    private static NativeExecutionActionFrame ReadFrameCore(string actionQueuePath, string executionReceiptPath,
        string afterSnapshotPath, string? compiledStateHash, NativeProductBoundary productBoundary, bool appliedNativeWork)
    {
        var frame = NativeExecutionFrameReader.ReadFrame(actionQueuePath, executionReceiptPath, afterSnapshotPath,
            compiledStateHash, (NativeExecutionFrameReader.NativeProductBoundary)(int)productBoundary, appliedNativeWork,
            QueueExecutionReceiptValidator.ReceiptPrimitiveIdentityMatches);
        return new(frame.Queue, frame.Item, frame.Result, frame.Request, frame.Before, frame.After,
            frame.Endpoint, frame.SourceSha256, frame.BeforeSnapshotPath, frame.AfterSnapshotPath) { SourcePaths = frame.SourcePaths };
    }

    private static string Text(JsonElement row, string name) => CurrentTeacherFrontierSupport.RequiredString(row, name);
    private static bool SamePath(string left, string right) => string.Equals(left, right,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);


}

internal sealed record VerifiedNativeExecutionAction(ActionQueueEnvelope Queue, ActionQueueItem Item,
    TrainingExecutionResult Result, TrainingExecutionRequest Request, SnapshotEnvelope Before, SnapshotEnvelope After,
    string Endpoint, IReadOnlyDictionary<string, string> SourceSha256);

internal sealed record NativeExecutionActionFrame(ActionQueueEnvelope Queue, ActionQueueItem Item,
    TrainingExecutionResult Result, TrainingExecutionRequest Request, SnapshotEnvelope Before, SnapshotEnvelope After,
    string Endpoint, IReadOnlyDictionary<string, string> SourceSha256, string BeforeSnapshotPath, string AfterSnapshotPath)
{
    internal IReadOnlyDictionary<string, string> SourcePaths { get; init; } = new Dictionary<string, string>();
}
