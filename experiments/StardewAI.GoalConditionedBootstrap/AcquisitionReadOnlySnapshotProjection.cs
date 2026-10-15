using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionReadOnlySnapshotProjection
{
    public static SnapshotEnvelope Read(string snapshotPath)
    {
        var fullPath = Path.GetFullPath(snapshotPath);
        return InvocationDeterministicBuildSession.GetOrBuildReference(
            "AcquisitionReadOnlySnapshotProjection.Read",
            "v1",
            new[]
            {
                InvocationDeterministicBuildSession.File(
                    "SnapshotPath",
                    fullPath)
            },
            () => CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
                fullPath,
                "Acquisition read-only snapshot projection"));
    }
}
