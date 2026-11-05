namespace StardewAI.Contracts.Training;

public static class TrainingExecutionParameterBindings
{
    public const string RouteWaitUntilTime = "route_wait_until_time";
    public const string RouteWaitMaxSlices = "route_wait_max_slices";
    public const string WaitUntilTime = "wait_until_time";
    public const string WaitMaxSlices = "wait_max_slices";

    public static string WireFieldForQueueParameter(string name) => name switch
    {
        RouteWaitUntilTime => WaitUntilTime,
        RouteWaitMaxSlices => WaitMaxSlices,
        _ => name
    };
}
