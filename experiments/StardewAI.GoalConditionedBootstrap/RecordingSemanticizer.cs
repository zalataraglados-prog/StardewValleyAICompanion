using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class RecordingSemanticizer
{
    public static GoalConditionedDemonstration Build(
        string recordingRoot,
        string annotationPath,
        GoalKnowledge knowledge)
    {
        var root = Path.GetFullPath(recordingRoot);
        var manifestPath = Path.Combine(root, "session-manifest.json");
        using var manifestDocument = JsonDocument.Parse(File.ReadAllText(manifestPath));
        var manifest = manifestDocument.RootElement;
        Require(manifest.GetProperty("schema_version").GetString() == "human_demonstration_recording_manifest.v1",
            "Unsupported recording manifest schema.");
        Require(manifest.GetProperty("source_kind").GetString() == DemonstrationSourceKinds.HumanPlayer,
            "Recording is not a human-player source.");
        var sessionId = RequiredString(manifest, "session_id");
        var goalId = RequiredString(manifest, "goal_id");
        Require(goalId == knowledge.GoalId, "Recording goal is not authoritative.");

        var annotationFullPath = Path.GetFullPath(annotationPath);
        var annotations = JsonSerializer.Deserialize<RecordingAdmissionAnnotations>(
            File.ReadAllText(annotationFullPath), JsonOptions)
            ?? throw new InvalidDataException("Recording annotations are null.");
        Require(annotations.SchemaVersion == "recording_admission_annotations.v1",
            "Unsupported annotation schema.");
        Require(annotations.GoalId == knowledge.GoalId && annotations.TargetScore == knowledge.TargetScore,
            "Annotation goal does not match authoritative knowledge.");
        Require(annotations.Segments.Length > 0, "No annotated segments were supplied.");

        var eventDescriptor = manifest.GetProperty("event_log");
        var eventPath = ResolveDescendant(root, RequiredString(eventDescriptor, "path"));
        VerifyHash(eventPath, RequiredString(eventDescriptor, "sha256"));
        var events = File.ReadLines(eventPath)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonDocument.Parse(line).RootElement.Clone())
            .ToArray();
        Require(events.Any(value => EventType(value) == "session_completed"),
            "Recording did not seal normally.");
        var observedDayBoundary = events.Any(value =>
            EventType(value) == "semantic_boundary" &&
            value.GetProperty("payload").TryGetProperty("reason", out var reason) &&
            reason.GetString() is "saving" or "day_started");
        Require(annotations.DayBoundaryVerified && observedDayBoundary,
            "A verified day boundary is required.");

        var markedMethods = events
            .Where(value => EventType(value) == "intent_marked")
            .Select(value => RequiredString(value.GetProperty("payload"), "method_id"))
            .ToHashSet(StringComparer.Ordinal);
        var snapshotFiles = manifest.GetProperty("snapshots").EnumerateArray()
            .ToDictionary(
                value => NormalizeRelative(RequiredString(value, "path")),
                value => new SnapshotDescriptor(
                    ResolveDescendant(root, RequiredString(value, "path")),
                    RequiredString(value, "sha256")),
                StringComparer.Ordinal);

        var usedSnapshots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var segments = annotations.Segments
            .OrderBy(value => value.Sequence)
            .Select(value =>
            {
                Require(markedMethods.Contains(value.MethodId),
                    $"Method '{value.MethodId}' has no intent mark in the raw recording.");
                var start = ResolveSnapshot(value.StartSnapshot, snapshotFiles);
                var end = ResolveSnapshot(value.EndSnapshot, snapshotFiles);
                VerifyHash(start.Path, start.Sha256);
                VerifyHash(end.Path, end.Sha256);
                usedSnapshots.Add(start.Path);
                usedSnapshots.Add(end.Path);
                return new DemonstrationSegment
                {
                    Sequence = value.Sequence,
                    BundleId = value.BundleId,
                    MethodId = value.MethodId,
                    OptionId = value.OptionId,
                    CandidateKind = value.CandidateKind,
                    CandidateId = value.CandidateId,
                    LocationId = value.LocationId,
                    StartStateHash = ReadStateHash(start.Path),
                    EndStateHash = ReadStateHash(end.Path),
                    Verified = value.Verified,
                    SemanticConfidence = value.SemanticConfidence,
                    ObservedEffects = value.ObservedEffects.Clone()
                };
            })
            .ToArray();

        var sourcePaths = new[] { manifestPath, eventPath, annotationFullPath }
            .Concat(usedSnapshots.OrderBy(path => path, StringComparer.OrdinalIgnoreCase))
            .ToArray();
        var result = new GoalConditionedDemonstration
        {
            DemonstrationId = "human:" + sessionId,
            Source = new DemonstrationSource
            {
                Kind = DemonstrationSourceKinds.HumanPlayer,
                SessionId = sessionId,
                SourcePaths = sourcePaths,
                SourceSha256 = sourcePaths.Select(HashFile).ToArray()
            },
            Goal = new DemonstrationGoal
            {
                GoalId = knowledge.GoalId,
                TargetScore = knowledge.TargetScore,
                ActiveCriterionIds = annotations.ActiveCriterionIds,
                MethodIds = segments.Select(value => value.MethodId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()
            },
            Context = new DemonstrationContext
            {
                SaveId = annotations.SaveId,
                Year = annotations.Year,
                Season = annotations.Season,
                Day = annotations.Day,
                StartTime = annotations.StartTime,
                EndTime = annotations.EndTime,
                StartStateHash = segments[0].StartStateHash,
                EndStateHash = segments[^1].EndStateHash
            },
            StateFeatures = annotations.StateFeatures,
            Segments = segments,
            Outcome = new DemonstrationOutcome
            {
                Status = annotations.Status,
                DayBoundaryObserved = true,
                AllSegmentsVerified = segments.All(value => value.Verified),
                GoalProgressBefore = annotations.GoalProgressBefore,
                GoalProgressAfter = annotations.GoalProgressAfter,
                TerminalGoalComplete = annotations.TerminalGoalComplete
            }
        };
        var validation = new DemonstrationValidator().Validate(result, knowledge);
        Require(validation.Admitted,
            "Recording was rejected: " + string.Join(",", validation.Reasons));
        return result;
    }

    private static SnapshotDescriptor ResolveSnapshot(
        string relativePath,
        IReadOnlyDictionary<string, SnapshotDescriptor> snapshots)
    {
        var normalized = NormalizeRelative(relativePath);
        return snapshots.TryGetValue(normalized, out var value)
            ? value
            : throw new InvalidDataException("Annotated snapshot is absent from the sealed manifest: " + normalized);
    }

    private static string ReadStateHash(string path)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return RequiredString(document.RootElement, "state_hash");
    }

    private static string ResolveDescendant(string root, string relativePath)
    {
        Require(!Path.IsPathRooted(relativePath), "Manifest paths must be relative.");
        var fullPath = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        Require(fullPath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
            "Manifest path escapes the recording root.");
        Require(File.Exists(fullPath), "Recording source file is missing: " + fullPath);
        return fullPath;
    }

    private static void VerifyHash(string path, string expected)
    {
        Require(string.Equals(HashFile(path), expected, StringComparison.OrdinalIgnoreCase),
            "Recording source hash mismatch: " + path);
    }

    private static string EventType(JsonElement value) => RequiredString(value, "event_type");

    private static string RequiredString(JsonElement value, string property)
    {
        var result = value.GetProperty(property).GetString();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidDataException("Required string is empty: " + property)
            : result;
    }

    private static string NormalizeRelative(string value) => value.Replace('\\', '/');

    private static string HashFile(string path) =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }

    private sealed record SnapshotDescriptor(string Path, string Sha256);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public sealed class RecordingAdmissionAnnotations
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "recording_admission_annotations.v1";

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("target_score")]
    public int TargetScore { get; set; }

    [JsonPropertyName("save_id")]
    public string SaveId { get; set; } = string.Empty;

    [JsonPropertyName("year")]
    public int Year { get; set; }

    [JsonPropertyName("season")]
    public string Season { get; set; } = string.Empty;

    [JsonPropertyName("day")]
    public int Day { get; set; }

    [JsonPropertyName("start_time")]
    public int StartTime { get; set; }

    [JsonPropertyName("end_time")]
    public int EndTime { get; set; }

    [JsonPropertyName("active_criterion_ids")]
    public string[] ActiveCriterionIds { get; set; } = Array.Empty<string>();

    [JsonPropertyName("state_features")]
    public FeatureVector StateFeatures { get; set; } = new();

    [JsonPropertyName("segments")]
    public RecordingSegmentAnnotation[] Segments { get; set; } = Array.Empty<RecordingSegmentAnnotation>();

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("day_boundary_verified")]
    public bool DayBoundaryVerified { get; set; }

    [JsonPropertyName("goal_progress_before")]
    public double? GoalProgressBefore { get; set; }

    [JsonPropertyName("goal_progress_after")]
    public double? GoalProgressAfter { get; set; }

    [JsonPropertyName("terminal_goal_complete")]
    public bool TerminalGoalComplete { get; set; }
}

public sealed class RecordingSegmentAnnotation
{
    [JsonPropertyName("sequence")]
    public int Sequence { get; set; }

    [JsonPropertyName("bundle_id")]
    public string BundleId { get; set; } = string.Empty;

    [JsonPropertyName("method_id")]
    public string MethodId { get; set; } = string.Empty;

    [JsonPropertyName("option_id")]
    public string OptionId { get; set; } = string.Empty;

    [JsonPropertyName("candidate_kind")]
    public string CandidateKind { get; set; } = string.Empty;

    [JsonPropertyName("candidate_id")]
    public string CandidateId { get; set; } = string.Empty;

    [JsonPropertyName("location_id")]
    public string LocationId { get; set; } = string.Empty;

    [JsonPropertyName("start_snapshot")]
    public string StartSnapshot { get; set; } = string.Empty;

    [JsonPropertyName("end_snapshot")]
    public string EndSnapshot { get; set; } = string.Empty;

    [JsonPropertyName("verified")]
    public bool Verified { get; set; }

    [JsonPropertyName("semantic_confidence")]
    public double SemanticConfidence { get; set; }

    [JsonPropertyName("observed_effects")]
    public JsonElement ObservedEffects { get; set; }
}
