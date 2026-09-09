using System.Security.Cryptography;
using System.Text.Json;

namespace StardewAI.DemonstrationRecorder;

internal sealed class RecordingSession
{
    private readonly object taskLock = new();
    private readonly List<Task> snapshotTasks = new();
    private long sequence;
    private int snapshotCount;

    public RecordingSession(string root, string goalId, string label)
    {
        SessionId = "human-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssZ") + "-" +
            Guid.NewGuid().ToString("N")[..8];
        GoalId = goalId;
        Label = label;
        Root = Path.Combine(root, SessionId);
        SnapshotRoot = Path.Combine(Root, "snapshots");
        Directory.CreateDirectory(SnapshotRoot);
        EventPath = Path.Combine(Root, "events.jsonl");
        Sink = new AsyncJsonlSink(EventPath);
    }

    public string SessionId { get; }
    public string GoalId { get; }
    public string Label { get; }
    public string Root { get; }
    public string SnapshotRoot { get; }
    public string EventPath { get; }
    public AsyncJsonlSink Sink { get; }
    public long NextSequence() => Interlocked.Increment(ref sequence);
    public int SnapshotCount => Volatile.Read(ref snapshotCount);

    public int NextSnapshotNumber() => Interlocked.Increment(ref snapshotCount);

    public void Track(Task task)
    {
        lock (taskLock)
            snapshotTasks.Add(task);
    }

    public async Task FinalizeAsync(string stopReason)
    {
        Task[] pending;
        lock (taskLock)
            pending = snapshotTasks.ToArray();
        await Task.WhenAll(pending).ConfigureAwait(false);
        Enqueue("session_completed", new
        {
            stop_reason = stopReason,
            snapshot_count = SnapshotCount,
            expert_admitted = false,
            admission_state = "pending_semanticization_and_day_outcome_verification"
        });
        await Sink.CompleteAsync().ConfigureAwait(false);
        var eventHash = HashFile(EventPath);
        var snapshots = Directory.GetFiles(SnapshotRoot, "*.json", SearchOption.TopDirectoryOnly)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(path => new
            {
                path = Path.GetRelativePath(Root, path).Replace('\\', '/'),
                sha256 = HashFile(path),
                bytes = new FileInfo(path).Length
            }).ToArray();
        var manifestPath = Path.Combine(Root, "session-manifest.json");
        var manifest = JsonSerializer.Serialize(new
        {
            schema_version = "human_demonstration_recording_manifest.v1",
            session_id = SessionId,
            source_kind = "human_player",
            goal_id = GoalId,
            label = Label,
            event_log = new
            {
                path = "events.jsonl",
                sha256 = eventHash,
                bytes = new FileInfo(EventPath).Length
            },
            snapshots,
            expert_admitted = false,
            admission_policy = "Recording remains raw evidence until semantic segments, fresh boundaries, and completed-day outcome are verified."
        }, JsonOptions.Indented);
        var temporary = manifestPath + ".tmp";
        await File.WriteAllTextAsync(temporary, manifest + Environment.NewLine).ConfigureAwait(false);
        File.Move(temporary, manifestPath, true);
    }

    public void Enqueue(string eventType, object payload)
    {
        Sink.Enqueue(JsonSerializer.Serialize(new
        {
            schema_version = "human_demonstration_event.v1",
            sequence = NextSequence(),
            session_id = SessionId,
            goal_id = GoalId,
            recorded_at_utc = DateTime.UtcNow,
            event_type = eventType,
            payload
        }, JsonOptions.Compact));
    }

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}

internal static class JsonOptions
{
    public static readonly JsonSerializerOptions Compact = new(JsonSerializerDefaults.Web);
    public static readonly JsonSerializerOptions Indented = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };
}
