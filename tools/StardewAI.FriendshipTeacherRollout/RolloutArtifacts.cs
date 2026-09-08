using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.FriendshipTeacherRollout;

public static class RolloutJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never
    };

    public static async Task WriteAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(value, Options),
            cancellationToken);
    }
}

public sealed class TeacherObjectiveEvidence
{
    public int DaySequence { get; set; }

    public int ObjectiveSequence { get; set; }

    public string CandidateId { get; set; } = string.Empty;

    public string NpcName { get; set; } = string.Empty;

    public int TotalDays { get; set; }

    public int TimeBefore { get; set; }

    public int TimeAfter { get; set; }

    public int VerifiedPrimitiveCount { get; set; }

    public int ConnectorCount { get; set; }

    public int MovementCount { get; set; }

    public int WaitCount { get; set; }

    public int FriendshipPointsBefore { get; set; }

    public int FriendshipPointsAfter { get; set; }

    public bool TalkedToTodayBefore { get; set; }

    public bool TalkedToTodayAfter { get; set; }

    public int PolicyTrajectoryCount { get; set; }

    public bool DialogueRecoveryApplied { get; set; }

    public string ArtifactDirectory { get; set; } = string.Empty;
}

public sealed class TeacherDayTransitionEvidence
{
    public int Sequence { get; set; }

    public int TotalDaysBefore { get; set; }

    public int TotalDaysAfter { get; set; }

    public int FriendshipPointSumBefore { get; set; }

    public int FriendshipPointSumAfter { get; set; }

    public int DayStartFriendshipPointSum { get; set; }

    public int NetDayProgressPointDelta { get; set; }

    public bool MadeNetDayProgress { get; set; }

    public int VerifiedGoalDirectedObjectiveCount { get; set; }

    public bool MadeGoalDirectedProgress { get; set; }

    public bool MadeProgress { get; set; }

    public int VerifiedNpcCount { get; set; }

    public int VerifiedFriendshipRowCount { get; set; }

    public int MismatchCount { get; set; }

    public string ArtifactDirectory { get; set; } = string.Empty;
}

public sealed class TeacherStoryEventAdvanceEvidence
{
    public int Sequence { get; set; }

    public int TotalDays { get; set; }

    public string EventId { get; set; } = string.Empty;

    public string AssetName { get; set; } = string.Empty;

    public string LocationId { get; set; } = string.Empty;

    public int CommandIndexBefore { get; set; }

    public string BoundaryKindBefore { get; set; } = string.Empty;

    public int VerifiedPrimitiveCount { get; set; }

    public string ObservedEffect { get; set; } = string.Empty;

    public bool EventActiveAfter { get; set; }

    public string ArtifactDirectory { get; set; } = string.Empty;
}

public sealed class FriendshipTeacherRolloutSummary
{
    public string SchemaVersion { get; set; } =
        "stardewai.friendship_teacher_rollout.v1";

    public string Status { get; set; } = "blocked";

    public string RunId { get; set; } = string.Empty;

    public string ExitPhase { get; set; } = string.Empty;

    public string ExitReason { get; set; } = string.Empty;

    public string[] BlockingReasons { get; set; } = Array.Empty<string>();

    public int InitialTotalDays { get; set; }

    public int FinalTotalDays { get; set; }

    public int FinalQualifyingCount { get; set; }

    public int RequiredQualifyingCount { get; set; } = 10;

    public int ObjectiveCount { get; set; }

    public int DayTransitionCount { get; set; }

    public int StoryEventAdvanceCount { get; set; }

    public int ConsecutiveNoProgressDays { get; set; }

    public bool FormalTrainingStarted { get; set; }

    public TeacherObjectiveEvidence[] Objectives { get; set; } =
        Array.Empty<TeacherObjectiveEvidence>();

    public TeacherDayTransitionEvidence[] DayTransitions { get; set; } =
        Array.Empty<TeacherDayTransitionEvidence>();

    public TeacherStoryEventAdvanceEvidence[] StoryEventAdvances { get; set; } =
        Array.Empty<TeacherStoryEventAdvanceEvidence>();
}
