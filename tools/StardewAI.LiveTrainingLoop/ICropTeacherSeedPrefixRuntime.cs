using System.Text.Json.Nodes;

namespace StardewAI.LiveTrainingLoop;

public interface ICropTeacherSeedPrefixRuntime
{
    Task ValidateBeforeDispatchAsync(int index, JsonObject item, JsonObject current, string currentSnapshotPath);
    Task ObserveNativeStepAsync(JsonObject execution, string afterSnapshotPath);
    Task RecordControllerStopAsync(int index, string currentSnapshotPath, string reason);
}
