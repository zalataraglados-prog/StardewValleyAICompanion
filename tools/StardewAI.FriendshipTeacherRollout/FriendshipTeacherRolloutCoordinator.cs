using System.Text.Json;
using System.Text.RegularExpressions;
using StardewAI.Core.Training;

namespace StardewAI.FriendshipTeacherRollout;

public sealed class FriendshipTeacherRolloutCoordinator
{
    private const int RequiredQualifyingCount = 10;
    private readonly RolloutOptions options;
    private readonly HttpClient http;
    private readonly FriendshipTeacherRolloutPolicy policy = new();
    private readonly FriendshipTeacherDayProgressEvaluator progressEvaluator =
        new();
    private readonly string calibrationJson;
    private readonly List<TeacherObjectiveEvidence> objectives = new();
    private readonly List<TeacherDayTransitionEvidence> transitions = new();
    private readonly List<TeacherStoryEventAdvanceEvidence> storyEventAdvances =
        new();
    private int completedObjectivesToday;
    private int consecutiveNoProgressDays;
    private FriendshipTeacherLabelDisposition labelDisposition;
    private CurrentSocialDayTeacherLabel? currentLabel;
    private string pendingCandidateId = string.Empty;
    private bool pendingReceiptVerified;
    private FriendshipTeacherDayTransitionState transitionState;
    private string transitionBeforeRaw = string.Empty;
    private int dayStartFriendshipPointSum;

    public FriendshipTeacherRolloutCoordinator(RolloutOptions options)
    {
        this.options = options;
        http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        calibrationJson = File.ReadAllText(options.CalibrationPath);
    }

    public async Task<FriendshipTeacherRolloutSummary> RunAsync(
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.OutputRoot);
        var summary = new FriendshipTeacherRolloutSummary
        {
            RunId = options.RunId
        };
        try
        {
            var snapshot = await CaptureSnapshotAsync(
                "initial-full-snapshot.json",
                cancellationToken);
            var initialTotalDays = SnapshotReader.IntField(
                snapshot.Root,
                "time",
                "total_days");
            summary.InitialTotalDays = initialTotalDays;
            dayStartFriendshipPointSum = SnapshotReader.FriendshipPointSum(
                snapshot.Root);

            try
            {
                for (var decisionIndex = 1; decisionIndex <= 1024; decisionIndex++)
                {
                    var observation = Observation(snapshot.Root);
                    var decision = policy.Decide(observation);
                    await RolloutJson.WriteAsync(
                        Path.Combine(
                            options.OutputRoot,
                            "checkpoints",
                            "decision-" + decisionIndex.ToString("D4") + ".json"),
                        new
                        {
                            decisionIndex,
                            totalDays = observation.CurrentTotalDays,
                            time = SnapshotReader.IntField(snapshot.Root, "time", "time"),
                            qualifyingCount = observation.QualifyingCount,
                            observation.CompletedObjectivesToday,
                            observation.CompletedDayTransitions,
                            observation.ConsecutiveNoProgressDays,
                            activeStoryEvent = observation.ActiveStoryEvent,
                            labelDisposition = observation.LabelDisposition.ToString(),
                            transitionState = observation.DayTransitionState.ToString(),
                            phase = decision.Phase.ToString(),
                            decision.Reason,
                            decision.CandidateId,
                            decision.BlockingReasons
                        },
                        cancellationToken);

                    switch (decision.Phase)
                    {
                        case FriendshipTeacherRolloutPhase.BuildFreshTeacherLabel:
                            await BuildFreshLabelAsync(
                                snapshot,
                                decisionIndex,
                                cancellationToken);
                            break;
                        case FriendshipTeacherRolloutPhase.ExecuteSelectedObjective:
                            pendingCandidateId = decision.CandidateId;
                            pendingReceiptVerified = false;
                            var execution = await ExecuteObjectiveAsync(
                                snapshot,
                                decision.CandidateId,
                                cancellationToken);
                            objectives.Add(execution.Evidence);
                            completedObjectivesToday++;
                            pendingReceiptVerified = true;
                            labelDisposition = FriendshipTeacherLabelDisposition.NotBuilt;
                            currentLabel = null;
                            snapshot.Dispose();
                            snapshot = execution.AfterSnapshot;
                            break;
                        case FriendshipTeacherRolloutPhase.AdvanceBlockingStoryEvent:
                            var advancedStorySnapshot =
                                await AdvanceStoryEventAsync(
                                    snapshot,
                                    cancellationToken);
                            snapshot.Dispose();
                            snapshot = advancedStorySnapshot;
                            break;
                        case FriendshipTeacherRolloutPhase.RecoverBlockingMenu:
                            var recoveredSnapshot = await RecoverMenuAsync(
                                cancellationToken);
                            snapshot.Dispose();
                            snapshot = recoveredSnapshot;
                            break;
                        case FriendshipTeacherRolloutPhase.CloseDayAtNativeSaveBoundary:
                            transitionBeforeRaw = snapshot.Raw;
                            var nextDaySnapshot = await CloseDayAsync(
                                snapshot,
                                cancellationToken);
                            transitionState =
                                FriendshipTeacherDayTransitionState.NativeSaveVerified;
                            labelDisposition = FriendshipTeacherLabelDisposition.NotBuilt;
                            currentLabel = null;
                            pendingCandidateId = string.Empty;
                            pendingReceiptVerified = false;
                            snapshot.Dispose();
                            snapshot = nextDaySnapshot;
                            break;
                        case FriendshipTeacherRolloutPhase.AuditNativeDayTransition:
                            await AuditDayTransitionAsync(
                                snapshot,
                                cancellationToken);
                            transitionState =
                                FriendshipTeacherDayTransitionState.AuditVerified;
                            completedObjectivesToday = 0;
                            break;
                        case FriendshipTeacherRolloutPhase.BoundedRunComplete:
                        case FriendshipTeacherRolloutPhase.Complete:
                        case FriendshipTeacherRolloutPhase.Blocked:
                            return await FinishAsync(
                                summary,
                                decision,
                                snapshot.Root,
                                cancellationToken);
                        default:
                            throw new InvalidOperationException(
                                "Unsupported rollout phase: " + decision.Phase + ".");
                    }
                }
                throw new InvalidOperationException(
                    "Friendship teacher rollout exceeded 1024 control decisions.");
            }
            finally
            {
                snapshot.Dispose();
            }
        }
        catch (Exception error) when (error is not OperationCanceledException)
        {
            summary.Status = "blocked";
            summary.ExitPhase = FriendshipTeacherRolloutPhase.Blocked.ToString();
            summary.ExitReason = "runtime_coordinator_exception";
            summary.BlockingReasons = new[]
            {
                error.GetType().Name + ":" + error.Message
            };
            summary.ObjectiveCount = objectives.Count;
            summary.DayTransitionCount = transitions.Count;
            summary.StoryEventAdvanceCount = storyEventAdvances.Count;
            summary.ConsecutiveNoProgressDays = consecutiveNoProgressDays;
            summary.Objectives = objectives.ToArray();
            summary.DayTransitions = transitions.ToArray();
            summary.StoryEventAdvances = storyEventAdvances.ToArray();
            summary.FormalTrainingStarted = false;
            await RolloutJson.WriteAsync(
                Path.Combine(options.OutputRoot, "summary.json"),
                summary,
                cancellationToken);
            return summary;
        }
    }

    private FriendshipTeacherRolloutObservation Observation(JsonElement snapshot) =>
        new()
        {
            CurrentTotalDays = SnapshotReader.IntField(
                snapshot,
                "time",
                "total_days"),
            DeadlineTotalDaysExclusive = options.DeadlineTotalDaysExclusive,
            QualifyingCount = SnapshotReader.QualifyingCount(snapshot),
            RequiredQualifyingCount = RequiredQualifyingCount,
            CompletedObjectivesToday = completedObjectivesToday,
            MaxObjectivesPerDay = options.MaxObjectivesPerDay,
            ConsecutiveNoProgressDays = consecutiveNoProgressDays,
            MaxConsecutiveNoProgressDays = options.MaxConsecutiveNoProgressDays,
            CompletedDayTransitions = transitions.Count,
            MaxDayTransitions = options.MaxDayTransitions,
            CompletedStoryEventAdvances = storyEventAdvances.Count,
            StopAfterStoryEventAdvances = options.StopAfterStoryEventAdvances,
            ActiveMenuOpen = SnapshotReader.ActiveMenuOpen(snapshot),
            ActiveStoryEvent = SnapshotReader.ActiveStoryEvent(snapshot),
            PendingCandidateId = pendingCandidateId,
            PendingObjectiveReceiptVerified = pendingReceiptVerified,
            LabelDisposition = labelDisposition,
            LabelCandidateId = currentLabel?.SelectedCandidate?.CandidateId ??
                string.Empty,
            DayTransitionState = transitionState
        };

    private async Task BuildFreshLabelAsync(
        SnapshotCapture snapshot,
        int sequence,
        CancellationToken cancellationToken)
    {
        currentLabel = new CurrentSocialDayTeacherLabelBuilder().Build(
            snapshot.Root,
            calibrationJson);
        labelDisposition = policy.Classify(currentLabel);
        pendingCandidateId = string.Empty;
        pendingReceiptVerified = false;
        if (transitionState ==
            FriendshipTeacherDayTransitionState.AuditVerified)
        {
            transitionState = FriendshipTeacherDayTransitionState.None;
        }
        await RolloutJson.WriteAsync(
            Path.Combine(
                options.OutputRoot,
                "teacher-labels",
                "teacher-label-" + sequence.ToString("D4") + ".json"),
            currentLabel,
            cancellationToken);
    }

    private async Task<ObjectiveExecutionResult> ExecuteObjectiveAsync(
        SnapshotCapture before,
        string candidateId,
        CancellationToken cancellationToken)
    {
        if (currentLabel?.SelectedCandidate is null ||
            !string.Equals(
                currentLabel.SelectedCandidate.CandidateId,
                candidateId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Rollout candidate differs from the fresh teacher label.");
        }
        var daySequence = transitions.Count + 1;
        var objectiveSequence = completedObjectivesToday + 1;
        var directory = Path.Combine(
            options.OutputRoot,
            "days",
            "day-" + daySequence.ToString("D2"),
            "objective-" + objectiveSequence.ToString("D2"));
        Directory.CreateDirectory(directory);
        var loopRoot = Path.Combine(directory, "live-loop");
        await RunLoopAsync(
            loopRoot,
            new[]
            {
                "--max-attempts", options.ObjectiveMaxAttempts.ToString(),
                "--daily-plan-candidate-options", "social.talk_npc",
                "--daily-plan-candidate-id", candidateId,
                "--stop-after-social-objective-complete"
            },
            directory,
            options.ObjectiveTimeoutSeconds,
            cancellationToken);

        var evidence = VerifyObjectiveArtifacts(
            loopRoot,
            directory,
            candidateId,
            before.Root);
        var after = await CaptureSnapshotAsync(
            Path.Combine(
                "days",
                "day-" + daySequence.ToString("D2"),
                "objective-" + objectiveSequence.ToString("D2"),
                "after-full-snapshot.json"),
            cancellationToken);
        try
        {
            evidence.TimeAfter = SnapshotReader.IntField(
                after.Root,
                "time",
                "time");
            await RolloutJson.WriteAsync(
                Path.Combine(directory, "verification.json"),
                evidence,
                cancellationToken);
            return new ObjectiveExecutionResult(evidence, after);
        }
        catch
        {
            after.Dispose();
            throw;
        }
    }

    private async Task<SnapshotCapture> RecoverMenuAsync(
        CancellationToken cancellationToken)
    {
        var daySequence = transitions.Count + 1;
        var recoverySequence = objectives.Count(value =>
            value.DaySequence == daySequence &&
            value.DialogueRecoveryApplied) + 1;
        var directory = Path.Combine(
            options.OutputRoot,
            "days",
            "day-" + daySequence.ToString("D2"),
            "recovery-" + recoverySequence.ToString("D2"));
        Directory.CreateDirectory(directory);
        var loopRoot = Path.Combine(directory, "live-loop");
        await RunLoopAsync(
            loopRoot,
            new[]
            {
                "--iterations", "1",
                "--daily-plan-candidate-options", "recovery.stabilize_day"
            },
            directory,
            options.RecoveryTimeoutSeconds,
            cancellationToken);
        VerifyRecoveryArtifacts(loopRoot);
        var after = await CaptureSnapshotAsync(
            Path.Combine(
                "days",
                "day-" + daySequence.ToString("D2"),
                "recovery-" + recoverySequence.ToString("D2"),
                "after-full-snapshot.json"),
            cancellationToken);
        try
        {
            if (SnapshotReader.ActiveMenuOpen(after.Root))
            {
                throw new InvalidOperationException(
                    "Mechanical menu recovery left a menu open.");
            }
            var latest = objectives.LastOrDefault(value =>
                value.DaySequence == daySequence &&
                !value.DialogueRecoveryApplied);
            if (latest is not null)
                latest.DialogueRecoveryApplied = true;
            return after;
        }
        catch
        {
            after.Dispose();
            throw;
        }
    }

    private async Task<SnapshotCapture> AdvanceStoryEventAsync(
        SnapshotCapture before,
        CancellationToken cancellationToken)
    {
        if (storyEventAdvances.Count >= options.MaxStoryEventAdvances)
        {
            throw new InvalidOperationException(
                "Friendship teacher story-event advance limit reached.");
        }
        if (!SnapshotReader.ActiveStoryEvent(before.Root))
            throw new InvalidOperationException("No active story event to advance.");

        var boundaryKind = SnapshotReader.StoryEventString(
            before.Root,
            "boundary_kind");
        var eventId = SnapshotReader.StoryEventString(
            before.Root,
            "event_id");
        if (!string.IsNullOrWhiteSpace(options.RequiredStoryEventId) &&
            !string.Equals(
                eventId,
                options.RequiredStoryEventId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Friendship teacher story-event admission expected event " +
                options.RequiredStoryEventId + " but observed " + eventId + ".");
        }
        if (!string.Equals(
                boundaryKind,
                "automatic_progress",
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Friendship teacher cannot select an unbound story-event boundary: " +
                boundaryKind + ".");
        }

        var sequence = storyEventAdvances.Count + 1;
        var daySequence = transitions.Count + 1;
        var directory = Path.Combine(
            options.OutputRoot,
            "days",
            "day-" + daySequence.ToString("D2"),
            "story-event-" + sequence.ToString("D2"));
        Directory.CreateDirectory(directory);
        var loopRoot = Path.Combine(directory, "live-loop");
        await RunLoopAsync(
            loopRoot,
            new[]
            {
                "--iterations", options.StoryEventExecutionMaxAttempts.ToString(),
                "--required-verified-actions", "1",
                "--daily-plan-candidate-options", "story.advance_event"
            },
            directory,
            options.StoryEventTimeoutSeconds,
            cancellationToken);

        var verifiedSteps = ReadVerifiedSteps(
            LoopSnapshotDirectory(loopRoot));
        var storySteps = verifiedSteps.Where(step =>
            ReadString(step, "option_id") ==
                "executor.advance_story_event").ToArray();
        if (storySteps.Length != 1)
        {
            throw new InvalidOperationException(
                "Expected exactly one verified native story-event receipt.");
        }

        var after = await CaptureSnapshotAsync(
            Path.Combine(
                "days",
                "day-" + daySequence.ToString("D2"),
                "story-event-" + sequence.ToString("D2"),
                "after-full-snapshot.json"),
            cancellationToken);
        try
        {
            var evidence = new TeacherStoryEventAdvanceEvidence
            {
                Sequence = sequence,
                TotalDays = SnapshotReader.IntField(
                    before.Root,
                    "time",
                    "total_days"),
                EventId = eventId,
                AssetName = SnapshotReader.StoryEventString(
                    before.Root,
                    "from_asset_name"),
                LocationId = SnapshotReader.StoryEventString(
                    before.Root,
                    "location_id"),
                CommandIndexBefore = SnapshotReader.StoryEventInt(
                    before.Root,
                    "current_command_index"),
                BoundaryKindBefore = boundaryKind,
                VerifiedPrimitiveCount = storySteps.Length,
                ObservedEffect = ReadString(
                    storySteps[0],
                    "observed_effect"),
                EventActiveAfter = SnapshotReader.ActiveStoryEvent(after.Root),
                ArtifactDirectory = directory
            };
            storyEventAdvances.Add(evidence);
            await RolloutJson.WriteAsync(
                Path.Combine(directory, "verification.json"),
                evidence,
                cancellationToken);
            if (evidence.EventActiveAfter)
            {
                throw new InvalidOperationException(
                    "Native story-event action stopped at another boundary; " +
                    "a fresh explicit binding is required.");
            }
            if (SnapshotReader.ActiveMenuOpen(after.Root))
            {
                throw new InvalidOperationException(
                    "Native story-event completion left a blocking menu open.");
            }
            return after;
        }
        catch
        {
            after.Dispose();
            throw;
        }
    }

    private async Task<SnapshotCapture> CloseDayAsync(
        SnapshotCapture before,
        CancellationToken cancellationToken)
    {
        var sequence = transitions.Count + 1;
        var directory = Path.Combine(
            options.OutputRoot,
            "days",
            "day-" + sequence.ToString("D2"),
            "native-save-boundary");
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(
            Path.Combine(directory, "before-full-snapshot.json"),
            before.Raw,
            cancellationToken);
        var loopRoot = Path.Combine(directory, "live-loop");
        await RunLoopAsync(
            loopRoot,
            new[]
            {
                "--max-attempts", "1",
                "--daily-plan-candidate-options", "recovery.stabilize_day",
                "--require-native-save-boundary",
                "--save-boundary-max-attempts",
                options.SaveBoundaryMaxAttempts.ToString(),
                "--save-slot", options.SaveSlot
            },
            directory,
            options.SaveBoundaryTimeoutSeconds,
            cancellationToken);
        var report = ReadLoopReport(loopRoot);
        if (!ReadBool(report, "native_save_boundary_verified"))
            throw new InvalidOperationException("Native save boundary was not verified.");
        var beforeDays = SnapshotReader.IntField(before.Root, "time", "total_days");
        var after = await CaptureSnapshotAsync(
            Path.Combine(
                "days",
                "day-" + sequence.ToString("D2"),
                "native-save-boundary",
                "after-full-snapshot.json"),
            cancellationToken,
            beforeDays + 1,
            TimeSpan.FromSeconds(120));
        if (SnapshotReader.IntField(after.Root, "time", "total_days") ==
            beforeDays + 1)
        {
            return after;
        }
        after.Dispose();
        throw new InvalidOperationException(
            "Native save boundary did not advance exactly one day.");
    }

    private async Task AuditDayTransitionAsync(
        SnapshotCapture after,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transitionBeforeRaw))
            throw new InvalidOperationException("Day transition before-snapshot is missing.");
        using var beforeDocument = JsonDocument.Parse(transitionBeforeRaw);
        var audit = new FriendshipDayTransitionSnapshotAuditor().Audit(
            beforeDocument.RootElement,
            after.Root);
        var sequence = transitions.Count + 1;
        var directory = Path.Combine(
            options.OutputRoot,
            "days",
            "day-" + sequence.ToString("D2"),
            "native-save-boundary");
        await RolloutJson.WriteAsync(
            Path.Combine(directory, "friendship-day-transition-audit.json"),
            audit,
            cancellationToken);
        if (!string.Equals(audit.Status, "pass", StringComparison.Ordinal) ||
            audit.MismatchCount != 0 ||
            audit.VerifiedNpcCount <= 0)
        {
            transitionState = FriendshipTeacherDayTransitionState.Failed;
            throw new InvalidOperationException(
                "Friendship day-transition audit failed.");
        }
        var pointsBefore = SnapshotReader.FriendshipPointSum(
            beforeDocument.RootElement);
        var pointsAfter = SnapshotReader.FriendshipPointSum(after.Root);
        var dayProgress = progressEvaluator.Evaluate(
            dayStartFriendshipPointSum,
            pointsAfter,
            consecutiveNoProgressDays,
            completedObjectivesToday);
        consecutiveNoProgressDays = dayProgress.ConsecutiveNoProgressDays;
        transitions.Add(new TeacherDayTransitionEvidence
        {
            Sequence = sequence,
            TotalDaysBefore = audit.BeforeTotalDays,
            TotalDaysAfter = audit.AfterTotalDays,
            FriendshipPointSumBefore = pointsBefore,
            FriendshipPointSumAfter = pointsAfter,
            DayStartFriendshipPointSum =
                dayProgress.DayStartFriendshipPointSum,
            NetDayProgressPointDelta = dayProgress.NetPointDelta,
            MadeNetDayProgress = dayProgress.MadeNetProgress,
            VerifiedGoalDirectedObjectiveCount =
                dayProgress.VerifiedGoalDirectedObjectiveCount,
            MadeGoalDirectedProgress =
                dayProgress.MadeGoalDirectedProgress,
            MadeProgress = dayProgress.MadeProgress,
            VerifiedNpcCount = audit.VerifiedNpcCount,
            VerifiedFriendshipRowCount = audit.VerifiedFriendshipRowCount,
            MismatchCount = audit.MismatchCount,
            ArtifactDirectory = directory
        });
        dayStartFriendshipPointSum = pointsAfter;
        transitionBeforeRaw = string.Empty;
    }

    private async Task RunLoopAsync(
        string root,
        IReadOnlyList<string> phaseArguments,
        string logDirectory,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(root);
        var arguments = new List<string>
        {
            "--root", root,
            "--backend-url", options.BackendUrl,
            "--bridge-snapshot-url", options.LoopSnapshotUrl,
            "--execution-snapshot-profile", "social",
            "--executor-url", options.ExecutorUrl,
            "--no-manifest",
            "--run-id", options.RunId,
            "--save-isolation-path", options.SaveIsolationPath,
            "--sleep-ms", "0",
            "--skip-training",
            "--use-daily-plan",
            "--daily-plan-max-candidates", "1",
            "--after-snapshot-wait-ms", "1000",
            "--snapshot-artifact-mode", options.SnapshotArtifactMode,
            "--min-free-space-mb", options.MinFreeSpaceMb.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            "--continue-after-blocked-queue-items"
        };
        arguments.AddRange(phaseArguments);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeoutSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        try
        {
            await ChildProcessRunner.RunDotNetAsync(
                options.ProjectRoot,
                options.LiveTrainingLoopDll,
                arguments,
                Path.Combine(logDirectory, "loop.stdout.log"),
                Path.Combine(logDirectory, "loop.stderr.log"),
                timeoutSource.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                "LiveTrainingLoop phase exceeded " + timeoutSeconds + " seconds.");
        }
    }

    private TeacherObjectiveEvidence VerifyObjectiveArtifacts(
        string loopRoot,
        string directory,
        string candidateId,
        JsonElement beforeSnapshot)
    {
        var report = ReadLoopReport(loopRoot);
        if (!ReadBool(report, "social_objective_completed"))
            throw new InvalidOperationException("Social objective did not complete.");
        var snapshotDirectory = LoopSnapshotDirectory(loopRoot);
        var verifiedSteps = ReadVerifiedSteps(snapshotDirectory);
        var socialSteps = verifiedSteps.Where(step =>
            ReadString(step, "option_id") == "executor.social_interact").ToArray();
        if (socialSteps.Length != 1)
            throw new InvalidOperationException("Expected one verified social interaction.");
        var social = socialSteps[0];
        if (ReadString(social, "effective_selected_candidate_id") != candidateId)
            throw new InvalidOperationException("Social receipt candidate identity drifted.");

        var trajectoryPath = Path.Combine(
            loopRoot,
            "datasets",
            "policy-decision-trajectories.jsonl");
        var trajectoryLines = File.Exists(trajectoryPath)
            ? File.ReadAllLines(trajectoryPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray()
            : Array.Empty<string>();
        if (trajectoryLines.Length != 1)
            throw new InvalidOperationException("Expected exactly one policy trajectory.");
        using var trajectory = JsonDocument.Parse(trajectoryLines[0]);
        if (ReadString(
                trajectory.RootElement.GetProperty("selection"),
                "candidate_id") != candidateId ||
            ReadString(
                trajectory.RootElement.GetProperty("outcome"),
                "primitive_option_id") != "executor.social_interact" ||
            !ReadBool(trajectory.RootElement.GetProperty("outcome"), "success"))
        {
            throw new InvalidOperationException("Policy trajectory identity or outcome drifted.");
        }
        return new TeacherObjectiveEvidence
        {
            DaySequence = transitions.Count + 1,
            ObjectiveSequence = completedObjectivesToday + 1,
            CandidateId = candidateId,
            NpcName = ReadString(social, "social_npc_name"),
            TotalDays = SnapshotReader.IntField(
                beforeSnapshot,
                "time",
                "total_days"),
            TimeBefore = SnapshotReader.IntField(beforeSnapshot, "time", "time"),
            VerifiedPrimitiveCount = verifiedSteps.Length,
            ConnectorCount = verifiedSteps.Count(step =>
                ReadString(step, "option_id") == "executor.traverse_connector"),
            MovementCount = verifiedSteps.Count(step =>
                ReadString(step, "option_id") == "executor.move_to_tile"),
            WaitCount = verifiedSteps.Count(step =>
                ReadString(step, "option_id") == "executor.wait_ticks"),
            FriendshipPointsBefore = ReadInt(
                social,
                "social_friendship_points_before"),
            FriendshipPointsAfter = ReadInt(
                social,
                "social_friendship_points_after"),
            TalkedToTodayBefore = ReadBool(
                social,
                "social_talked_to_today_before"),
            TalkedToTodayAfter = ReadBool(
                social,
                "social_talked_to_today_after"),
            PolicyTrajectoryCount = trajectoryLines.Length,
            ArtifactDirectory = directory
        };
    }

    private void VerifyRecoveryArtifacts(string loopRoot)
    {
        var steps = ReadVerifiedSteps(LoopSnapshotDirectory(loopRoot));
        var close = steps.Where(step =>
            ReadString(step, "option_id") == "executor.close_menu").ToArray();
        if (close.Length != 1 || !ReadBool(close[0], "dialogue_native_handled"))
            throw new InvalidOperationException("Dialogue recovery receipt was not verified.");
    }

    private JsonElement ReadLoopReport(string loopRoot)
    {
        var path = Path.Combine(
            loopRoot,
            "runs",
            options.RunId,
            "live-training-loop-report.json");
        if (!File.Exists(path))
            throw new FileNotFoundException("LiveTrainingLoop report is missing.", path);
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.Clone();
    }

    private string LoopSnapshotDirectory(string loopRoot) => Path.Combine(
        loopRoot,
        "runs",
        options.RunId,
        "live-snapshots");

    private static JsonElement[] ReadVerifiedSteps(string snapshotDirectory)
    {
        if (!Directory.Exists(snapshotDirectory))
            throw new DirectoryNotFoundException("Loop snapshot directory is missing.");
        var steps = new List<JsonElement>();
        foreach (var path in Directory.EnumerateFiles(
                     snapshotDirectory,
                     "execution-*.json")
                 .Where(path => Regex.IsMatch(
                     Path.GetFileNameWithoutExtension(path),
                     "^execution-[0-9]{4}$",
                     RegexOptions.CultureInvariant))
                 .OrderBy(path => path, StringComparer.Ordinal))
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty(
                    "step_results",
                    out var results) ||
                results.ValueKind != JsonValueKind.Array)
            {
                continue;
            }
            steps.AddRange(results.EnumerateArray()
                .Where(step =>
                    ReadString(step, "status") == "applied" &&
                    ReadString(step, "primitive_verification_status") ==
                        "verified")
                .Select(step => step.Clone()));
        }
        return steps.ToArray();
    }

    private async Task<SnapshotCapture> CaptureSnapshotAsync(
        string relativePath,
        CancellationToken cancellationToken,
        int? expectedTotalDays = null,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));
        Exception? lastError = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            try
            {
                var raw = await http.GetStringAsync(
                    options.SnapshotUrl,
                    cancellationToken);
                var document = JsonDocument.Parse(raw);
                var totalDays = SnapshotReader.IntField(
                    document.RootElement,
                    "time",
                    "total_days");
                if (!expectedTotalDays.HasValue ||
                    totalDays == expectedTotalDays.Value)
                {
                    var path = Path.Combine(options.OutputRoot, relativePath);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    await File.WriteAllTextAsync(path, raw, cancellationToken);
                    return new SnapshotCapture(raw, document);
                }
                document.Dispose();
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                lastError = error;
            }
            await Task.Delay(500, cancellationToken);
        }
        throw new TimeoutException(
            "Timed out waiting for a fresh world snapshot." +
            (lastError is null ? string.Empty : " Last error: " + lastError.Message));
    }

    private async Task<FriendshipTeacherRolloutSummary> FinishAsync(
        FriendshipTeacherRolloutSummary summary,
        FriendshipTeacherRolloutDecision decision,
        JsonElement snapshot,
        CancellationToken cancellationToken)
    {
        summary.Status = decision.Phase switch
        {
            FriendshipTeacherRolloutPhase.Complete => "goal_satisfied",
            FriendshipTeacherRolloutPhase.BoundedRunComplete =>
                "bounded_evidence_complete",
            _ => "blocked"
        };
        summary.ExitPhase = decision.Phase.ToString();
        summary.ExitReason = decision.Reason;
        summary.BlockingReasons = decision.BlockingReasons;
        summary.FinalTotalDays = SnapshotReader.IntField(
            snapshot,
            "time",
            "total_days");
        summary.FinalQualifyingCount = SnapshotReader.QualifyingCount(snapshot);
        summary.ObjectiveCount = objectives.Count;
        summary.DayTransitionCount = transitions.Count;
        summary.StoryEventAdvanceCount = storyEventAdvances.Count;
        summary.ConsecutiveNoProgressDays = consecutiveNoProgressDays;
        summary.Objectives = objectives.ToArray();
        summary.DayTransitions = transitions.ToArray();
        summary.StoryEventAdvances = storyEventAdvances.ToArray();
        summary.FormalTrainingStarted = false;
        await RolloutJson.WriteAsync(
            Path.Combine(options.OutputRoot, "summary.json"),
            summary,
            cancellationToken);
        return summary;
    }

    private static string ReadString(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static int ReadInt(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(name, out var value) &&
        value.ValueKind == JsonValueKind.Number &&
        value.TryGetInt32(out var result)
            ? result
            : throw new InvalidDataException("Required integer is missing: " + name + ".");

    private static bool ReadBool(JsonElement source, string name) =>
        source.ValueKind == JsonValueKind.Object &&
        source.TryGetProperty(name, out var value) &&
        value.ValueKind is JsonValueKind.True or JsonValueKind.False &&
        value.GetBoolean();

    private sealed class SnapshotCapture : IDisposable
    {
        public SnapshotCapture(string raw, JsonDocument document)
        {
            Raw = raw;
            Document = document;
        }

        public string Raw { get; }

        public JsonDocument Document { get; }

        public JsonElement Root => Document.RootElement;

        public void Dispose() => Document.Dispose();
    }

    private sealed class ObjectiveExecutionResult
    {
        public ObjectiveExecutionResult(
            TeacherObjectiveEvidence evidence,
            SnapshotCapture afterSnapshot)
        {
            Evidence = evidence;
            AfterSnapshot = afterSnapshot;
        }

        public TeacherObjectiveEvidence Evidence { get; }

        public SnapshotCapture AfterSnapshot { get; }
    }
}
