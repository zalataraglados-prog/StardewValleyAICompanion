using System.Text.Json;
using StardewAI.FriendshipTeacherRollout;

namespace StardewAI.Core.Tests;

public sealed class FriendshipTeacherRolloutCoordinatorTests
{
    [Fact]
    public void SnapshotReaderConsumesOnlyReadableTransparentFields()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "state": {
                "time": {
                  "time": {"value": 910, "status": "available"},
                  "total_days": {"value": 223, "status": "derived"}
                },
                "menus": {
                  "active_menu": {
                    "value": {"is_open": false},
                    "status": "available"
                  }
                },
                "npcs": {
                  "grandpa_friendship_progress": {
                    "value": {
                      "qualifying_count": 1,
                      "eligible_villager_rows": [
                        {"friendship_points": 2407},
                        {"friendship_points": 1436},
                        {"friendship_points": null}
                      ]
                    },
                    "status": "available"
                  }
                }
              }
            }
            """);

        Assert.Equal(910, SnapshotReader.IntField(
            document.RootElement, "time", "time"));
        Assert.Equal(223, SnapshotReader.IntField(
            document.RootElement, "time", "total_days"));
        Assert.False(SnapshotReader.ActiveMenuOpen(document.RootElement));
        Assert.Equal(1, SnapshotReader.QualifyingCount(document.RootElement));
        Assert.Equal(3843, SnapshotReader.FriendshipPointSum(
            document.RootElement));
    }

    [Fact]
    public void SnapshotReaderRejectsUnavailableField()
    {
        using var document = JsonDocument.Parse(
            """
            {"state":{"time":{"time":{"value":910,"status":"unavailable"}}}}
            """);

        Assert.Throws<InvalidDataException>(() => SnapshotReader.IntField(
            document.RootElement, "time", "time"));
    }

    [Fact]
    public void SnapshotReaderRecognizesOnlyTransparentActiveStoryEvent()
    {
        using var document = JsonDocument.Parse(
            """
            {
              "state": {
                "player": {
                  "story_event": {
                    "value": {
                      "active": true,
                      "event_up": true,
                      "event_id": "558291",
                      "boundary_kind": "automatic_progress",
                      "current_command_index": 8
                    },
                    "status": "available"
                  }
                }
              }
            }
            """);

        Assert.True(SnapshotReader.ActiveStoryEvent(document.RootElement));
        Assert.Equal("558291", SnapshotReader.StoryEventString(
            document.RootElement, "event_id"));
        Assert.Equal(8, SnapshotReader.StoryEventInt(
            document.RootElement, "current_command_index"));
    }

    [Fact]
    public void CoordinatorDelegatesActionsAndKeepsTrainingDisabled()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.FriendshipTeacherRollout",
            "FriendshipTeacherRolloutCoordinator.cs"));
        var optionSource = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.FriendshipTeacherRollout",
            "RolloutOptions.cs"));

        Assert.Contains("--daily-plan-candidate-id", source, StringComparison.Ordinal);
        Assert.Contains("--stop-after-social-objective-complete", source, StringComparison.Ordinal);
        Assert.Contains("--skip-training", source, StringComparison.Ordinal);
        Assert.Contains("--execution-snapshot-profile", source, StringComparison.Ordinal);
        Assert.Contains("--min-free-space-mb", source, StringComparison.Ordinal);
        Assert.Contains("--snapshot-artifact-mode", source, StringComparison.Ordinal);
        Assert.Contains("content_addressed_gzip", optionSource, StringComparison.Ordinal);
        Assert.Contains("\"social\"", source, StringComparison.Ordinal);
        Assert.Contains("--require-native-save-boundary", source, StringComparison.Ordinal);
        Assert.Contains("\"--required-verified-actions\", \"1\"", source, StringComparison.Ordinal);
        Assert.Contains("FriendshipDayTransitionSnapshotAuditor", source, StringComparison.Ordinal);
        Assert.Contains("CurrentSocialDayTeacherLabelBuilder", source, StringComparison.Ordinal);
        Assert.Contains("story.advance_event", source, StringComparison.Ordinal);
        Assert.Contains("executor.advance_story_event", source, StringComparison.Ordinal);
        Assert.Contains("automatic_progress", source, StringComparison.Ordinal);
        Assert.Contains("RequiredStoryEventId", source, StringComparison.Ordinal);
        Assert.Contains("StopAfterStoryEventAdvances", source, StringComparison.Ordinal);
        Assert.Contains("StoryEventExecutionMaxAttempts", source, StringComparison.Ordinal);

        Assert.DoesNotContain("training_execution_request", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/v1/training/execute", source, StringComparison.Ordinal);
        Assert.DoesNotContain("PostAsync(", source, StringComparison.Ordinal);
        Assert.DoesNotContain("changeFriendship", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("talkedToToday =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("teleport", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CoordinatorReusesExactlyOneFreshSnapshotPerStateChangingPhase()
    {
        var source = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.FriendshipTeacherRollout",
            "FriendshipTeacherRolloutCoordinator.cs"));

        Assert.Contains("snapshot.Dispose();", source, StringComparison.Ordinal);
        Assert.Contains("snapshot = execution.AfterSnapshot;", source, StringComparison.Ordinal);
        Assert.Contains("snapshot = recoveredSnapshot;", source, StringComparison.Ordinal);
        Assert.Contains("snapshot = nextDaySnapshot;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("snapshot = null;", source, StringComparison.Ordinal);
        Assert.DoesNotContain("profile=full", source, StringComparison.Ordinal);
    }

    [Fact]
    public void CoordinatorBoundsAndKillsHungChildLoops()
    {
        var coordinator = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.FriendshipTeacherRollout",
            "FriendshipTeacherRolloutCoordinator.cs"));
        var childRunner = File.ReadAllText(FindRepositoryFile(
            "tools",
            "StardewAI.FriendshipTeacherRollout",
            "ChildProcessRunner.cs"));

        Assert.Contains("CancelAfter", coordinator, StringComparison.Ordinal);
        Assert.Contains("Kill(entireProcessTree: true)", childRunner, StringComparison.Ordinal);
        Assert.Contains("Path.GetExtension(assemblyPath)", childRunner, StringComparison.Ordinal);
        Assert.Contains("isManagedAssembly ? \"dotnet\" : assemblyPath", childRunner, StringComparison.Ordinal);
        Assert.Contains("loop.stdout.log", coordinator, StringComparison.Ordinal);
        Assert.Contains("loop.stderr.log", coordinator, StringComparison.Ordinal);
    }

    private static string FindRepositoryFile(params string[] segments)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var path = Path.Combine(
                new[] { directory.FullName }.Concat(segments).ToArray());
            if (File.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new FileNotFoundException(
            "Repository file was not found: " + Path.Combine(segments));
    }
}
