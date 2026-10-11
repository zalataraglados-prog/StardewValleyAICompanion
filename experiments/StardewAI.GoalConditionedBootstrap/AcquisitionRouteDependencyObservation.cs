namespace StardewAI.GoalConditionedBootstrap;

internal static class AcquisitionRouteDependencyObservation
{
    public static T ObserveAxis<T>(
        string producerId,
        Func<T> factory,
        params object?[] identities) =>
        InvocationDeterministicBuildSession.GetOrBuild(
            producerId,
            "v1",
            identities.Select((value, index) => value is string path
                ? InvocationDeterministicBuildSession.File(
                    "InputPath" + index.ToString("D2"),
                    path)
                : InvocationDeterministicBuildSession.Value(
                    "InputValue" + index.ToString("D2"),
                    value)),
            factory,
            "candidate_denominator_certificate");
}
