namespace StardewAI.DemonstrationRecorder;

public sealed class RecorderConfig
{
    public string DefaultGoalId { get; set; } = "grandpa.maximum_21";

    public string OutputRoot { get; set; } = "recordings";

    public string TransparentBridgeSnapshotEndpoint { get; set; } =
        "http://127.0.0.1:8765/api/v1/snapshot";

    public int MinimumSnapshotIntervalTicks { get; set; } = 60;

    public int MaximumSnapshotsPerSession { get; set; } = 512;

    public bool CaptureHourlyAnchor { get; set; } = true;
}
