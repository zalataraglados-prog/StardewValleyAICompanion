using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;
using StardewAI.LiveTrainingLoop;

if (args.Length > 0 && args[0] == "lookup-product-receipt")
{
    try { Environment.ExitCode = await ProductReceiptRecoveryCommand.RunAsync(args.Skip(1).ToArray()); }
    catch (Exception exception) { Console.Error.WriteLine(exception.Message); Environment.ExitCode = 2; }
    return;
}

if (args.Length > 0 && args[0] == "install-crop-reservations")
{
    using var installationHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    try { Environment.ExitCode = await InstallCropReservationsAsync(args.Skip(1).ToArray(), installationHttp); }
    catch (Exception exception) { Console.Error.WriteLine(exception.Message); Environment.ExitCode = 2; }
    return;
}

if (args.Length > 0 && args[0] == "close-crop-seed-reservations")
{
    using var lifecycleHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
    try { Environment.ExitCode = await CloseCropSeedReservationsAsync(args.Skip(1).ToArray(), lifecycleHttp); }
    catch (Exception exception) { Console.Error.WriteLine(exception.Message); Environment.ExitCode = 2; }
    return;
}

if (args.Length > 0 && args[0] == "resume-crop-managed-owner")
{
    using var resumeHttp = new HttpClient { Timeout = TimeSpan.FromSeconds(180) };
    try
    {
        var resumeOptions = LiveTrainingOptions.Parse(args.Skip(1).ToArray());
        Console.WriteLine(await ResumeCropTeacherManagedOwnerAsync(resumeHttp, resumeOptions, string.Empty));
    }
    catch (Exception exception) { Console.Error.WriteLine(exception.Message); Environment.ExitCode = 2; }
    return;
}

var options = LiveTrainingOptions.Parse(args);
if (!string.IsNullOrWhiteSpace(options.ResumeManagedCropOwnerDirectory))
    throw new ArgumentException("Use the explicit resume-crop-managed-owner command to reopen retained managed evidence.");
options.ValidateFormalExecutionBoundary();
Directory.CreateDirectory(options.Root);
Directory.CreateDirectory(options.RunDir);
Directory.CreateDirectory(options.SnapshotDir);
Directory.CreateDirectory(Path.GetDirectoryName(options.DatasetPath)!);
Directory.CreateDirectory(Path.GetDirectoryName(options.ProgressLogPath)!);
var trainingDataTransaction = FormalTrainingDataTransaction.Begin(options);

using var http = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(Math.Max(180, options.ExecutorTimeoutSeconds))
};
using var executorHttp = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(options.ExecutorTimeoutSeconds)
};

AppendProgress(options, "start", 0, string.Empty, string.Empty, "concurrency=1 target=" + options.TargetExecutionMode + " feedback=" + options.FeedbackMode);

var rowsAppended = 0;
var verifiedActions = 0;
var attemptsStarted = 0;
var lastQueueId = string.Empty;
var lastStateHash = string.Empty;
JsonObject? lastTrainingReport = null;
JsonObject? lastPrediction = null;
var lastPlanningHandoffCaptureManifestPath = string.Empty;
var lastPlanningHandoffTeacherDispatchManifestPath = string.Empty;
var lastPlanningHandoffNativeRunPlanPath = string.Empty;
MoneyRevenueCropNativeProductionRunPlan? activeCropTeacherRunPlan = null;
MoneyRevenueCropNativeProductionRunPlanRequest?
    activeCropTeacherRunPlanRequest = null;
CropTeacherTransitionLedger? activeCropTeacherTransitionLedger = null;
await using var cropTeacherManagedOwner = new CropTeacherOwnerLifetime();
PendingCropTeacherDayBoundary? pendingCropTeacherDayBoundary = null;
CropTeacherTransitionLedger? pendingCropTeacherDayBoundaryLedger = null;
CropTeacherDayBoundaryStart? completedCropTeacherBoundaryStart = null;
CropTeacherTransitionLedger? completedCropTeacherBoundaryLedger = null;
var cropTeacherPreparedCommitmentIds = new HashSet<string>(
    StringComparer.Ordinal);
var cropTeacherControlAttemptsStarted = 0;
var cropTeacherFirstDayCompleted = false;
var cropTeacherRunCompleted = false;
var cropTeacherReplanRequired = false;
var cropTeacherReplanReason = string.Empty;
var cropTeacherReplanCount = 0;
var cropTeacherTransitionLedgerPath = string.Empty;
var cropTeacherHarvestReceiptCount = 0;
var cropTeacherDayBoundaryReceiptCount = 0;
if (!string.IsNullOrWhiteSpace(
        options.ResumeCropTeacherTransitionLedgerPath))
{
    var resumed = CropTeacherResumeLoader.Load(
        options.ResumeCropTeacherTransitionLedgerPath,
        Path.Combine(
            options.SnapshotDir,
            "crop-teacher-resume",
            "crop-teacher-transition-ledger.json"));
    activeCropTeacherRunPlanRequest = resumed.RunPlanRequest;
    activeCropTeacherRunPlan = resumed.RunPlan;
    activeCropTeacherTransitionLedger = resumed.TransitionLedger;
    cropTeacherTransitionLedgerPath = resumed.TransitionLedgerPath;
    lastPlanningHandoffNativeRunPlanPath = resumed.NativeRunPlanPath;
    cropTeacherHarvestReceiptCount =
        resumed.TransitionLedger.HarvestReceipts.Count;
    cropTeacherDayBoundaryReceiptCount =
        resumed.TransitionLedger.DayBoundaryReceipts.Count;
    cropTeacherFirstDayCompleted =
        resumed.TransitionLedger.CompletedDayBoundaryKeys.Contains(
            CropTeacherTransitionLedger.DayBoundaryKey(
                resumed.RunPlan.FirstTotalDay));
    AppendProgress(
        options,
        "crop_teacher_run_resumed",
        0,
        resumed.RunPlan.SourceStateHash,
        string.Empty,
        "source_ledger=" + resumed.SourceTransitionLedgerPath +
        " active_ledger=" + resumed.TransitionLedgerPath +
        " discarded_volatile_harvest_receipts=" +
        resumed.DiscardedVolatileHarvestReceiptCount +
        " run_plan=" + resumed.NativeRunPlanPath +
        " run_plan_sha256=" +
        resumed.TransitionLedger.RunPlanSha256 +
        " harvest_receipts=" + cropTeacherHarvestReceiptCount +
        " day_boundary_receipts=" +
        cropTeacherDayBoundaryReceiptCount);
}
var stageOneCollectionTeacherQueueCount = 0;
var stageOneCollectionControlQueueCount = 0;
var lastStageOneCollectionTeacherFallbackSignature = string.Empty;
JsonObject? activeObjectiveContinuation = null;
JsonObject? deferredObjectiveContinuationForCapacityRecovery = null;
var suppressedObjectiveContinuations = new List<JsonObject>();
var suppressionDayKey = string.Empty;
var objectiveCompleted = false;
var socialObjectiveCompleted = false;
var leasedDecisionModelPlanPath = string.Empty;
var leasedDecisionRankingPath = string.Empty;
var leasedDecisionCompiledQueuePath = string.Empty;
var leasedDecisionSnapshotPath = string.Empty;
var leasedDecisionSourceStateHash = string.Empty;
var leasedDecisionGoalId = string.Empty;
var leasedSelectedCandidateId = string.Empty;
var leasedSelectedQueueIndex = -1;
var leasedDecisionIsInventoryCapacityRecovery = false;
var persistedIterationCount = CountPersistedIterations(options.SnapshotDir);
var nextArtifactIteration = NextArtifactIteration(options.SnapshotDir);
var consecutiveErrors = 0;
var lastErrorIteration = 0;
var stopReason = string.Empty;
var primaryAttemptsStarted = 0;
var nativeSaveBoundaryAttemptsStarted = 0;
var nativeSaveBoundaryPhaseStarted = false;
var nativeSaveBoundaryVerified = false;
var nativeSaveBoundaryInitialDayKey = string.Empty;
var nativeSaveBoundaryCurrentDayKey = string.Empty;
var goldenWalnutSourceMilestone =
    new GoldenWalnutFreshSaveMilestoneAssessment(
        false,
        -1,
        0,
        false,
        false,
        false,
        false,
        new[] { "not_evaluated" });
var goldenWalnutSourceMilestoneSignature = string.Empty;
var gourmandContinuationMilestone =
    new GoldenWalnutGourmandContinuationMilestoneAssessment(
        false,
        -1,
        0,
        new[] { "not_evaluated" });
var gourmandContinuationMilestoneSignature = string.Empty;
SaveDirectoryFingerprint? initialSaveFingerprint = null;
SaveDirectoryFingerprint? currentSaveFingerprint = null;
var hasExplicitPrimaryTarget =
    options.RequiredVerifiedActions > 0 ||
    options.StopAfterObjectiveComplete ||
    options.StopAfterSocialObjectiveComplete ||
    options.RequireGoldenWalnutSourceScheduleReady ||
    options.RequireGourmandContinuationReady;
var noProgressBackoff = new NoProgressBackoffPolicy(
    options.NoProgressBackoffMs,
    options.NoProgressMaxBackoffMs);
var unverifiedExecutionBackoff = new NoProgressBackoffPolicy(
    options.NoProgressBackoffMs,
    options.NoProgressMaxBackoffMs);
var routeOscillationGuard = new RouteOscillationGuard();

for (var attemptOrdinal = 1; ; attemptOrdinal++)
{
    var explicitPrimaryTargetMet =
        (options.RequiredVerifiedActions <= 0 ||
         verifiedActions >= options.RequiredVerifiedActions) &&
        (!options.StopAfterObjectiveComplete || objectiveCompleted) &&
        (!options.StopAfterSocialObjectiveComplete || socialObjectiveCompleted) &&
        (!options.RequireGoldenWalnutSourceScheduleReady ||
         goldenWalnutSourceMilestone.Ready) &&
        (!options.RequireGourmandContinuationReady ||
         gourmandContinuationMilestone.Ready);
    var effectivePrimaryAttempts = cropTeacherFirstDayCompleted &&
        !options.RequireGoldenWalnutSourceScheduleReady &&
        !options.ContinueAfterCropTeacherRun
        ? Math.Max(primaryAttemptsStarted, options.MaxAttempts)
        : primaryAttemptsStarted;
    var phaseDecision = activeCropTeacherRunPlan is null
        ? TrainingCompletionPolicy.Decide(
            effectivePrimaryAttempts,
            options.MaxAttempts,
            nativeSaveBoundaryAttemptsStarted,
            options.SaveBoundaryMaxAttempts,
            hasExplicitPrimaryTarget,
            explicitPrimaryTargetMet,
            options.RequireNativeSaveBoundary,
            nativeSaveBoundaryVerified)
        : cropTeacherControlAttemptsStarted >= 256
            ? new LiveTrainingPhaseDecision(
                LiveTrainingPhase.Incomplete,
                "crop_teacher_run_attempt_limit_reached")
            : new LiveTrainingPhaseDecision(LiveTrainingPhase.Primary);
    if (phaseDecision.Phase is LiveTrainingPhase.Complete or LiveTrainingPhase.Incomplete)
    {
        if (phaseDecision.Phase == LiveTrainingPhase.Incomplete &&
            string.IsNullOrWhiteSpace(stopReason))
        {
            stopReason = phaseDecision.StopReason;
        }
        break;
    }

    var closingNativeSaveBoundary =
        phaseDecision.Phase == LiveTrainingPhase.NativeSaveBoundary;
    if (closingNativeSaveBoundary && !nativeSaveBoundaryPhaseStarted)
    {
        nativeSaveBoundaryPhaseStarted = true;
        nativeSaveBoundaryInitialDayKey = string.Empty;
        nativeSaveBoundaryCurrentDayKey = string.Empty;
        initialSaveFingerprint = null;
        currentSaveFingerprint = null;
        activeObjectiveContinuation = null;
        deferredObjectiveContinuationForCapacityRecovery = null;
        leasedDecisionModelPlanPath = string.Empty;
        leasedDecisionRankingPath = string.Empty;
        leasedDecisionCompiledQueuePath = string.Empty;
        leasedDecisionSnapshotPath = string.Empty;
        leasedDecisionSourceStateHash = string.Empty;
        leasedDecisionGoalId = string.Empty;
        leasedSelectedCandidateId = string.Empty;
        leasedSelectedQueueIndex = -1;
        leasedDecisionIsInventoryCapacityRecovery = false;
        AppendProgress(
            options,
            "native_save_boundary_start",
            attemptOrdinal,
            lastStateHash,
            lastQueueId,
            "primary_attempts=" + primaryAttemptsStarted +
            " verified_actions=" + verifiedActions);
    }

    var iteration = nextArtifactIteration + attemptOrdinal - 1;
    try
    {
        persistedIterationCount = ApplyRollingArtifactRetention(options, iteration);
        var artifactBudgetBlock = GetArtifactBudgetBlock(options, persistedIterationCount);
        if (!string.IsNullOrWhiteSpace(artifactBudgetBlock))
        {
            stopReason = artifactBudgetBlock;
            AppendProgress(options, "stopped", iteration, lastStateHash, lastQueueId, stopReason);
            break;
        }
        attemptsStarted++;
        if (closingNativeSaveBoundary)
        {
            nativeSaveBoundaryAttemptsStarted++;
        }
        else
        {
            primaryAttemptsStarted++;
            if (activeCropTeacherRunPlan is not null)
            {
                cropTeacherControlAttemptsStarted++;
            }
        }
        var rawSnapshotJson = iteration == 1 && !string.IsNullOrWhiteSpace(options.SnapshotFile)
            ? await ContentAddressedJsonArtifactStore.ReadAllTextAsync(
                options.SnapshotFile)
            : await http.GetStringAsync(options.BridgeSnapshotUrl);
        var beforeSnapshot = JsonNode.Parse(rawSnapshotJson)?.AsObject() ?? new JsonObject();
        if (attemptOrdinal == 1 && activeCropTeacherRunPlan is not null)
        {
            var resumeSnapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
                rawSnapshotJson,
                JsonOptions) ?? throw new InvalidDataException(
                    "crop_teacher_resume_snapshot_invalid");
            var resumeBlockReason =
                CropTeacherRunPlanner.GetResumeSnapshotBlockReason(
                    activeCropTeacherRunPlan,
                    resumeSnapshot);
            if (!string.IsNullOrWhiteSpace(resumeBlockReason))
            {
                stopReason = "crop_teacher_resume_snapshot_incompatible reason=" +
                    resumeBlockReason;
                AppendProgress(
                    options,
                    "stopped",
                    iteration,
                    resumeSnapshot.StateHash,
                    string.Empty,
                    stopReason);
                break;
            }
        }
        var currentDayKey = QueueReplanFilter.SnapshotDayKey(beforeSnapshot);
        if (pendingCropTeacherDayBoundary is not null)
        {
            var boundaryCheck = await CheckCropTeacherDayBoundaryAsync(
                options,
                pendingCropTeacherDayBoundary,
                beforeSnapshot);
            if (!boundaryCheck.Verified)
            {
                AppendProgress(
                    options,
                    "crop_teacher_day_boundary_waiting",
                    iteration,
                    lastStateHash,
                    pendingCropTeacherDayBoundary.QueueId,
                    boundaryCheck.Detail);
                await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                continue;
            }
            var ledger = pendingCropTeacherDayBoundaryLedger ??
                throw new InvalidOperationException(
                    "crop_teacher_transition_ledger_missing");
            ledger.RecordVerifiedDayBoundary(
                boundaryCheck.Receipt ?? throw new InvalidOperationException(
                    "crop_teacher_day_boundary_receipt_missing"));
            cropTeacherDayBoundaryReceiptCount =
                ledger.DayBoundaryReceipts.Count;
            AppendProgress(
                options,
                "crop_teacher_day_boundary_verified",
                iteration,
                lastStateHash,
                pendingCropTeacherDayBoundary.QueueId,
                "from_total_day=" +
                pendingCropTeacherDayBoundary.FromTotalDay +
                " receipt_count=" + cropTeacherDayBoundaryReceiptCount);
            pendingCropTeacherDayBoundary = null;
            pendingCropTeacherDayBoundaryLedger = null;
            if (ReferenceEquals(ledger, completedCropTeacherBoundaryLedger))
            {
                completedCropTeacherBoundaryStart = null;
                completedCropTeacherBoundaryLedger = null;
            }
        }
        if (options.RequireGoldenWalnutSourceScheduleReady &&
            !closingNativeSaveBoundary)
        {
            goldenWalnutSourceMilestone =
                EvaluateGoldenWalnutSourceMilestone(rawSnapshotJson);
            var milestoneSignature =
                GoldenWalnutSourceMilestoneSignature(
                    goldenWalnutSourceMilestone);
            if (!string.Equals(
                    milestoneSignature,
                    goldenWalnutSourceMilestoneSignature,
                    StringComparison.Ordinal))
            {
                goldenWalnutSourceMilestoneSignature = milestoneSignature;
                AppendGoldenWalnutSourceMilestoneProgress(
                    options,
                    goldenWalnutSourceMilestone,
                    iteration,
                    lastStateHash,
                    lastQueueId);
            }
            if (goldenWalnutSourceMilestone.CurrentTotalDay >=
                GoldenWalnutUnlockScheduleProofBuilder
                    .DeadlineTotalDayExclusive)
            {
                stopReason =
                    "golden_walnut_deadline_total_day_reached current=" +
                    goldenWalnutSourceMilestone.CurrentTotalDay +
                    " deadline_exclusive=" +
                    GoldenWalnutUnlockScheduleProofBuilder
                        .DeadlineTotalDayExclusive;
                AppendProgress(
                    options,
                    "stopped",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    stopReason);
                break;
            }
            if (goldenWalnutSourceMilestone.Ready)
            {
                activeCropTeacherRunPlan = null;
                activeCropTeacherRunPlanRequest = null;
                activeCropTeacherTransitionLedger = null;
                pendingCropTeacherDayBoundary = null;
                pendingCropTeacherDayBoundaryLedger = null;
                if (!options.RequireGourmandContinuationReady)
                {
                    await DelayBeforeNextAttemptAsync(
                        options,
                        attemptOrdinal);
                    continue;
                }
            }
        }
        if (options.RequireGourmandContinuationReady &&
            !closingNativeSaveBoundary)
        {
            gourmandContinuationMilestone =
                EvaluateGourmandContinuationMilestone(rawSnapshotJson);
            var gourmandSignature =
                GourmandContinuationMilestoneSignature(
                    gourmandContinuationMilestone);
            if (!string.Equals(
                    gourmandSignature,
                    gourmandContinuationMilestoneSignature,
                    StringComparison.Ordinal))
            {
                gourmandContinuationMilestoneSignature = gourmandSignature;
                AppendGourmandContinuationMilestoneProgress(
                    options,
                    gourmandContinuationMilestone,
                    iteration,
                    lastStateHash,
                    lastQueueId);
            }
            if (gourmandContinuationMilestone.Ready)
            {
                await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                continue;
            }
        }
        if (options.RequireNativeSaveBoundary && closingNativeSaveBoundary)
        {
            if (string.IsNullOrWhiteSpace(nativeSaveBoundaryInitialDayKey))
            {
                if (string.IsNullOrWhiteSpace(currentDayKey))
                {
                    throw new InvalidOperationException(
                        "native_save_boundary_initial_day_key_unavailable");
                }
                nativeSaveBoundaryInitialDayKey = currentDayKey;
                initialSaveFingerprint =
                    await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
                        options.SaveIsolationPath,
                        options.SaveSlot);
                currentSaveFingerprint = initialSaveFingerprint;
                AppendProgress(
                    options,
                    "native_save_boundary_baseline_captured",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "initial_day=" + nativeSaveBoundaryInitialDayKey);
            }
            nativeSaveBoundaryCurrentDayKey = currentDayKey;
        }
        if (closingNativeSaveBoundary &&
            !string.Equals(
                nativeSaveBoundaryInitialDayKey,
                nativeSaveBoundaryCurrentDayKey,
                StringComparison.Ordinal))
        {
            currentSaveFingerprint = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
                options.SaveIsolationPath,
                options.SaveSlot);
            var observedBoundary = NativeSaveBoundaryVerifier.Evaluate(
                nativeSaveBoundaryInitialDayKey,
                nativeSaveBoundaryCurrentDayKey,
                initialSaveFingerprint!,
                currentSaveFingerprint);
            nativeSaveBoundaryVerified = observedBoundary.Verified;
            AppendProgress(
                options,
                nativeSaveBoundaryVerified
                    ? "native_save_boundary_verified"
                    : "native_save_boundary_waiting_for_durable_save",
                iteration,
                lastStateHash,
                lastQueueId,
                "day_advanced=" + observedBoundary.DayAdvanced +
                " save_changed=" + observedBoundary.SaveChanged +
                " initial_day=" + nativeSaveBoundaryInitialDayKey +
                " current_day=" + nativeSaveBoundaryCurrentDayKey);
            await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
            continue;
        }
        if (!string.IsNullOrWhiteSpace(currentDayKey) &&
            !string.Equals(currentDayKey, suppressionDayKey, StringComparison.Ordinal))
        {
            unverifiedExecutionBackoff.Reset();
            var clearedCount = suppressedObjectiveContinuations.Count;
            suppressedObjectiveContinuations.Clear();
            suppressionDayKey = currentDayKey;
            if (clearedCount > 0)
            {
                AppendProgress(
                    options,
                    "objective_suppression_reset",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "reason=game_day_changed cleared_count=" + clearedCount +
                    " day_key=" + currentDayKey);
            }
        }
        var snapshotJson = beforeSnapshot.ToJsonString(JsonlOptions);
        var snapshotPath = Path.Combine(options.SnapshotDir, "before-snapshot-" + iteration.ToString("D4") + ".json");
        await ContentAddressedJsonArtifactStore.WriteAsync(
            snapshotPath,
            snapshotJson,
            options.SnapshotArtifactMode);
        persistedIterationCount++;

    var ingest = await PostJsonStringAsync(
        http,
        SnapshotIngestUrl(
            options,
            iteration == 1 &&
                !string.IsNullOrWhiteSpace(options.SnapshotFile)
                ? options.SnapshotFileProfile
                : null),
        snapshotJson);
    lastStateHash = ReadString(ingest, "state_hash");

    var ready = await http.GetFromJsonAsync<JsonObject>(options.ReadyProbeUrl);
    if (ready is null || ready["ready"]?.GetValue<bool>() != true)
    {
        AppendProgress(options, "blocked", iteration, lastStateHash, string.Empty, "ready_probe_failed");
        await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
        continue;
    }

    var modelPlanPath = string.Empty;
    var rankingPath = string.Empty;
    var decisionModelPlanPath = string.Empty;
    var decisionRankingPath = string.Empty;
    var decisionCompiledQueuePath = string.Empty;
    var decisionSnapshotPath = snapshotPath;
    var decisionSourceStateHash = lastStateHash;
    var selectedCandidateId = string.Empty;
    var selectedQueueIndex = -1;
    var resumedSelectedQueueDecision = false;
    var useDailyPlanDecisionLease = options.UseDailyPlan;
    var executionSnapshotProfile = options.ExecutionSnapshotProfile;
    var executionSnapshotUrl = options.BridgeSnapshotUrl;
    CropTeacherRunStep? cropTeacherStepForExecution = null;
    ICropTeacherSeedRuntime? cropSeedSupportForExecution = null;
    CropTeacherManagedSeedOperation? cropManagedOperationForExecution = null;
    JsonObject? dailyPlanRanking = null;
    StageOneCollectionTeacherQueueBuildResult? nativeControlSettle = null;
    JsonObject? deferredObjectiveContinuationForDailyMaintenance = null;
    var inventoryCapacityRecoveryControlActive = false;
    if (options.UseDailyPlan)
    {
        if (cropTeacherManagedOwner.Current is { } controlOwner)
        {
            var managedSettle = await PrepareCropTeacherOwnedNativeControlSettleAsync(options, iteration, controlOwner);
            nativeControlSettle = managedSettle?.Build;
            cropManagedOperationForExecution = managedSettle?.Operation;
        }
        else
        {
            var settlePrefix = Path.Combine(
                options.SnapshotDir,
                $"native-control-settle-{{0}}-{iteration:D4}.json");
            nativeControlSettle = await TryBuildNativeControlSettleQueueAsync(
                options,
                snapshotJson,
                lastStateHash,
                snapshotPath,
                string.Format(settlePrefix, "preference"),
                string.Format(settlePrefix, "compiled-plan"),
                string.Format(settlePrefix, "ranking"),
                string.Format(settlePrefix, "compiled-queue"));
        }
    }
    JsonObject queue;
    if (nativeControlSettle?.Queue is not null)
    {
        queue = nativeControlSettle.Queue;
        dailyPlanRanking = nativeControlSettle.Ranking;
        modelPlanPath = nativeControlSettle.PlanPath;
        rankingPath = nativeControlSettle.RankingPath;
        decisionModelPlanPath = nativeControlSettle.PlanPath;
        decisionRankingPath = nativeControlSettle.RankingPath;
        decisionCompiledQueuePath = nativeControlSettle.QueuePath;
        decisionSnapshotPath = nativeControlSettle.SnapshotPath;
        decisionSourceStateHash = ReadString(queue, "state_hash");
        selectedCandidateId = nativeControlSettle.SelectedCandidateId;
        selectedQueueIndex = 0;
        useDailyPlanDecisionLease = cropManagedOperationForExecution is null;
        stageOneCollectionControlQueueCount++;
        AppendProgress(
            options,
            "stage_one_collection_control_queue_compiled",
            iteration,
            lastStateHash,
            ReadString(queue, "queue_id"),
            "candidate_id=" + selectedCandidateId +
            " teacher_queue_count=" +
            stageOneCollectionTeacherQueueCount +
            " control_queue_count=" +
            stageOneCollectionControlQueueCount +
            " " + nativeControlSettle.Detail);
    }
    else if (options.UsePrecompiledQueue)
    {
        queue = await TeacherPreferenceQueueLoader.LoadQueueAsync(
            options.PrecompiledQueuePath,
            lastStateHash,
            options.TargetExecutionMode);
        selectedCandidateId =
            EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(
                ExecutableQueueItems(queue).FirstOrDefault());
    }
    else if (options.UseTeacherPreferenceQueue)
    {
        queue = await TeacherPreferenceQueueLoader.LoadAsync(
            options.TeacherPreferencePath,
            lastStateHash,
            options.TargetExecutionMode);
    }
    else if (options.UseDailyPlan && activeCropTeacherRunPlan is not null)
    {
        var managedOwner = cropTeacherManagedOwner.Current;
        var typedSnapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(
            managedOwner is null ? snapshotJson : ContentAddressedJsonArtifactReader.ReadAllText(managedOwner.CurrentSnapshotPath),
            JsonOptions) ?? throw new InvalidDataException(
                "crop_teacher_full_snapshot_invalid");
        var cropStep = CropTeacherRunPlanner.Evaluate(
            activeCropTeacherRunPlan,
            activeCropTeacherRunPlanRequest ??
                throw new InvalidOperationException(
                    "crop_teacher_run_plan_request_missing"),
            typedSnapshot,
            cropTeacherPreparedCommitmentIds,
            activeCropTeacherTransitionLedger?.CompletedHarvestTileKeys,
            activeCropTeacherTransitionLedger?.CompletedDayBoundaryKeys,
            managedOwner is null ? null : JsonSerializer.Deserialize<StardewAI.Contracts.Strategy.StrategyCommitmentLedger>(
                ContentAddressedJsonArtifactReader.ReadAllText(managedOwner.CurrentLedgerPath), JsonOptions));
        if (!cropTeacherFirstDayCompleted &&
            (cropStep.TotalDay > activeCropTeacherRunPlan.FirstTotalDay ||
             cropStep.Stage == CropTeacherRunStageKinds.Complete))
        {
            cropTeacherFirstDayCompleted = true;
            AppendProgress(
                options,
                "crop_teacher_first_day_complete",
                iteration,
                lastStateHash,
                string.Empty,
                "reason=" + cropStep.Reason +
                " control_attempts=" +
                cropTeacherControlAttemptsStarted);
            if (options.StopAfterCropTeacherFirstDay &&
                cropStep.Stage != CropTeacherRunStageKinds.Complete)
            {
                if (managedOwner is not null)
                {
                    stopReason = "crop_teacher_managed_first_day_saved_owner_retained";
                    break;
                }
                activeCropTeacherRunPlan = null;
                activeCropTeacherRunPlanRequest = null;
                activeCropTeacherTransitionLedger = null;
                await DelayBeforeNextAttemptAsync(
                    options,
                    attemptOrdinal);
                continue;
            }
        }
        if (cropStep.Stage == CropTeacherRunStageKinds.ReplanRequired)
        {
            if (managedOwner is not null)
            {
                stopReason = "crop_teacher_managed_replan_requires_original_lifecycle reason=" + cropStep.Reason;
                break;
            }
            if (CropTeacherRunPlanner.IsTeacherRedispatchReason(
                    cropStep.Reason) &&
                cropTeacherReplanCount < 8)
            {
                var supersededLedger =
                    activeCropTeacherTransitionLedger ??
                    throw new InvalidOperationException(
                        "crop_teacher_transition_ledger_missing");
                var supersededRunPlanPath =
                    supersededLedger.RunPlanPath;
                var replanHandoff = BuildCropTeacherReplanHandoff(
                    cropStep.Reason,
                    supersededRunPlanPath,
                    supersededLedger.RunPlanSha256);
                var dispatch = await DispatchCropTeacherAsync(
                    http,
                    options,
                    iteration,
                    rawSnapshotJson,
                    replanHandoff,
                    "replan");
                cropTeacherReplanCount++;
                cropTeacherReplanReason = cropStep.Reason;
                lastPlanningHandoffCaptureManifestPath =
                    dispatch.Capture.ManifestPath;
                lastPlanningHandoffTeacherDispatchManifestPath =
                    dispatch.TeacherDispatchManifestPath;
                lastPlanningHandoffNativeRunPlanPath =
                    dispatch.NativeRunPlanPath;
                activeCropTeacherRunPlanRequest = dispatch.RunPlanRequest;
                activeCropTeacherRunPlan = dispatch.RunPlan;
                cropTeacherTransitionLedgerPath =
                    dispatch.TransitionLedgerPath;
                activeCropTeacherTransitionLedger =
                    dispatch.TransitionLedger;
                cropTeacherHarvestReceiptCount =
                    dispatch.TransitionLedger.HarvestReceipts.Count;
                cropTeacherDayBoundaryReceiptCount =
                    dispatch.TransitionLedger.DayBoundaryReceipts.Count;
                pendingCropTeacherDayBoundary = null;
                pendingCropTeacherDayBoundaryLedger = null;
                cropTeacherPreparedCommitmentIds.Clear();
                activeObjectiveContinuation = null;
                deferredObjectiveContinuationForCapacityRecovery = null;
                leasedDecisionModelPlanPath = string.Empty;
                leasedDecisionRankingPath = string.Empty;
                leasedDecisionCompiledQueuePath = string.Empty;
                leasedDecisionSnapshotPath = string.Empty;
                leasedDecisionSourceStateHash = string.Empty;
                leasedDecisionGoalId = string.Empty;
                leasedSelectedCandidateId = string.Empty;
                leasedSelectedQueueIndex = -1;
                leasedDecisionIsInventoryCapacityRecovery = false;
                AppendProgress(
                    options,
                    "crop_teacher_replanned",
                    iteration,
                    lastStateHash,
                    string.Empty,
                    "reason=" + cropStep.Reason +
                    " replan_count=" + cropTeacherReplanCount +
                    " superseded_run_plan=" + supersededRunPlanPath +
                    " native_run_plan=" + dispatch.NativeRunPlanPath +
                    " actions=" + dispatch.ActionCount);
                await DelayBeforeNextAttemptAsync(
                    options,
                    attemptOrdinal);
                continue;
            }
            cropTeacherReplanRequired = true;
            cropTeacherReplanReason = cropStep.Reason;
            stopReason = "crop_teacher_replan_required reason=" +
                cropStep.Reason + " commitment_id=" +
                cropStep.CommitmentId;
            AppendProgress(
                options,
                "crop_teacher_replan_required",
                iteration,
                lastStateHash,
                string.Empty,
                "reason=" + cropStep.Reason +
                " commitment_id=" + cropStep.CommitmentId +
                " total_day=" + cropStep.TotalDay);
            break;
        }
        ManagedCropControl? managedTerminalControl = null;
        if (cropStep.Stage == CropTeacherRunStageKinds.Complete && managedOwner is not null)
        {
            if (cropTeacherManagedOwner.TerminalSelection is null)
            {
                var productionEvidence = await managedOwner.BuildProductionEvidenceAsync(
                    Path.Combine(managedOwner.EvidenceDirectory, "production-terminal")).ConfigureAwait(false);
                AppendProgress(options, "crop_teacher_managed_production_evidence", iteration, lastStateHash, string.Empty,
                    "manifest=" + productionEvidence.ManifestPath + " retention_proofs=" + productionEvidence.OutputRetentionProofPaths.Length);
                var terminalSelection = await PrepareCropTeacherManagedTerminalAsync(managedOwner, productionEvidence).ConfigureAwait(false);
                cropTeacherManagedOwner.AttachTerminal(productionEvidence, terminalSelection.Selection);
                AppendProgress(options, "crop_teacher_managed_terminal_selected", iteration, lastStateHash, string.Empty,
                    "selection=" + terminalSelection.Path + " native_dispatch_performed=false");
            }
            managedTerminalControl = await BuildCropTeacherOwnedTerminalContinuationAsync(http, options, iteration,
                activeCropTeacherRunPlan, activeCropTeacherRunPlanRequest!, cropTeacherManagedOwner).ConfigureAwait(false);
            if (managedTerminalControl is null)
            {
                stopReason = "crop_teacher_managed_terminal_settlement_requires_semantic_save_fresh_head_closeout";
                break;
            }
        }
        if (cropStep.Stage == CropTeacherRunStageKinds.Complete && managedOwner is null)
        {
            completedCropTeacherBoundaryStart =
                await CaptureCropTeacherTerminalBoundaryStartAsync(
                    options,
                    cropStep,
                    activeCropTeacherRunPlan,
                    beforeSnapshot,
                    lastStateHash);
            completedCropTeacherBoundaryLedger =
                completedCropTeacherBoundaryStart is null
                    ? null
                    : activeCropTeacherTransitionLedger;
            cropTeacherRunCompleted = true;
            activeCropTeacherRunPlan = null;
            activeCropTeacherRunPlanRequest = null;
            activeCropTeacherTransitionLedger = null;
            AppendProgress(
                options,
                "crop_teacher_run_complete",
                iteration,
                lastStateHash,
                string.Empty,
                "reason=" + cropStep.Reason +
                " control_attempts=" +
                cropTeacherControlAttemptsStarted);
            await DelayBeforeNextAttemptAsync(
                options,
                attemptOrdinal);
            continue;
        }
        var managedControl = managedTerminalControl ?? (managedOwner is null ? null : await BuildCropTeacherOwnedControlQueueAsync(http, options, iteration,
            cropStep, activeCropTeacherRunPlan, activeCropTeacherRunPlanRequest!, managedOwner));
        var cropQueue = managedControl?.Build ?? await BuildCropTeacherControlQueueAsync(
            http,
            options,
            iteration,
            cropStep,
            activeCropTeacherRunPlan,
            activeCropTeacherRunPlanRequest,
            snapshotPath,
            beforeSnapshot,
            lastStateHash);
        cropManagedOperationForExecution = managedControl?.Operation;
        cropTeacherStepForExecution =
            cropQueue.ExecutionStepOverride ??
            (!string.IsNullOrWhiteSpace(cropQueue.InterruptKind)
                ? null
                : cropStep);
        cropSeedSupportForExecution = (ICropTeacherSeedRuntime?)cropQueue.SeedPlantingSupport ?? cropQueue.SeedTransferSupport ?? (ICropTeacherSeedRuntime?)cropQueue.SeedPurchaseSupport;
        if (cropQueue.SitePreparationComplete)
        {
            if (!cropTeacherPreparedCommitmentIds.Add(
                    cropStep.CommitmentId))
            {
                throw new InvalidOperationException(
                    "crop_teacher_site_preparation_completion_replayed");
            }
            AppendProgress(
                options,
                "crop_teacher_site_preparation_complete",
                iteration,
                cropQueue.StateHash,
                string.Empty,
                "commitment_id=" + cropStep.CommitmentId);
            await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
            continue;
        }
        queue = cropQueue.Queue ?? throw new InvalidOperationException(
            "crop_teacher_control_queue_missing");
        modelPlanPath = cropQueue.ModelPlanPath;
        rankingPath = cropQueue.RankingPath;
        decisionModelPlanPath = modelPlanPath;
        decisionRankingPath = rankingPath;
        snapshotPath = cropQueue.BeforeSnapshotPath;
        beforeSnapshot = cropQueue.BeforeSnapshot;
        decisionSnapshotPath = snapshotPath;
        lastStateHash = cropQueue.StateHash;
        decisionSourceStateHash = lastStateHash;
        executionSnapshotProfile = cropQueue.SnapshotProfile;
        executionSnapshotUrl = cropQueue.SnapshotUrl;
        useDailyPlanDecisionLease = false;
        activeObjectiveContinuation = null;
        inventoryCapacityRecoveryControlActive = string.Equals(
            cropQueue.InterruptKind,
            "crop_teacher_harvest_inventory_capacity_recovery",
            StringComparison.Ordinal);
        AppendProgress(
            options,
            "crop_teacher_control_queue_compiled",
            iteration,
            lastStateHash,
            ReadString(queue, "queue_id"),
            "stage=" + (!string.IsNullOrWhiteSpace(
                    cropQueue.InterruptKind)
                ? cropQueue.InterruptKind
                : cropStep.Stage) +
            " interrupted_stage=" + (!string.IsNullOrWhiteSpace(
                    cropQueue.InterruptKind)
                ? cropStep.Stage
                : string.Empty) +
            " reason=" + cropStep.Reason +
            " commitment_id=" + cropStep.CommitmentId +
            " remaining=" + cropStep.RemainingQuantity);
    }
    else if (options.UseDailyPlan)
    {
        var hasDecisionLease = File.Exists(leasedDecisionModelPlanPath) &&
            File.Exists(leasedDecisionRankingPath) &&
            File.Exists(leasedDecisionCompiledQueuePath) &&
            File.Exists(leasedDecisionSnapshotPath) &&
            !string.IsNullOrWhiteSpace(leasedDecisionSourceStateHash) &&
            !string.IsNullOrWhiteSpace(leasedDecisionGoalId) &&
            !string.IsNullOrWhiteSpace(leasedSelectedCandidateId) &&
            leasedSelectedQueueIndex >= 0;
        if (hasDecisionLease)
        {
            inventoryCapacityRecoveryControlActive =
                leasedDecisionIsInventoryCapacityRecovery;
            var decisionLease = SelectedQueueDecisionLease.Load(
                leasedDecisionCompiledQueuePath,
                leasedDecisionRankingPath);
            var selectedCandidate = decisionLease.CandidateAt(
                leasedSelectedQueueIndex);
            var refresh = await BuildQueueFromSelectedCandidateAsync(
                http,
                options,
                lastStateHash,
                leasedDecisionGoalId,
                selectedCandidate,
                activeObjectiveContinuation);
            modelPlanPath = Path.Combine(options.SnapshotDir, "candidate-compiled-plan-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(modelPlanPath, refresh.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
            var dailyPlanPath = Path.Combine(options.SnapshotDir, "candidate-compile-response-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(dailyPlanPath, refresh.Response.ToJsonString(JsonOptions), Encoding.UTF8);
            var evidencePath = Path.Combine(options.SnapshotDir, "candidate-refresh-evidence-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(evidencePath, refresh.Evidence.ToJsonString(JsonOptions), Encoding.UTF8);
            dailyPlanRanking = refresh.Evidence;
            queue = refresh.Queue;
            decisionModelPlanPath = leasedDecisionModelPlanPath;
            decisionRankingPath = leasedDecisionRankingPath;
            decisionCompiledQueuePath = leasedDecisionCompiledQueuePath;
            decisionSnapshotPath = leasedDecisionSnapshotPath;
            decisionSourceStateHash = leasedDecisionSourceStateHash;
            selectedCandidateId = selectedCandidate.CandidateId;
            selectedQueueIndex = selectedCandidate.QueueIndex;
            resumedSelectedQueueDecision = true;
        }
        else if (closingNativeSaveBoundary)
        {
            var saveBoundary = await BuildNativeSaveBoundaryQueueAsync(
                http,
                options,
                lastStateHash);
            modelPlanPath = Path.Combine(
                options.SnapshotDir,
                "native-save-boundary-plan-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(
                modelPlanPath,
                saveBoundary.Plan.ToJsonString(JsonOptions),
                Encoding.UTF8);
            var responsePath = Path.Combine(
                options.SnapshotDir,
                "native-save-boundary-response-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(
                responsePath,
                saveBoundary.Response.ToJsonString(JsonOptions),
                Encoding.UTF8);
            rankingPath = Path.Combine(
                options.SnapshotDir,
                "native-save-boundary-ranking-" + iteration.ToString("D4") + ".json");
            await File.WriteAllTextAsync(
                rankingPath,
                saveBoundary.Ranking.ToJsonString(JsonOptions),
                Encoding.UTF8);
            dailyPlanRanking = saveBoundary.Ranking;
            queue = saveBoundary.Queue;
            decisionModelPlanPath = modelPlanPath;
            decisionRankingPath = rankingPath;
        }
        else
        {
            var mandatoryMenuRecovery =
                await TryBuildStageOneMandatoryMenuRecoveryQueueAsync(
                    http,
                    options,
                    iteration,
                    snapshotJson,
                    beforeSnapshot,
                    lastStateHash);
            var dailyMaintenancePreemption =
                StageOneDailyMaintenancePreemptionPolicy.Evaluate(
                    beforeSnapshot);
            if (mandatoryMenuRecovery is null &&
                dailyMaintenancePreemption.ShouldPreempt &&
                activeObjectiveContinuation is not null)
            {
                deferredObjectiveContinuationForDailyMaintenance =
                    JsonNode.Parse(
                        activeObjectiveContinuation.ToJsonString(JsonOptions))!
                    .AsObject();
            }
            var collectionTeacher = mandatoryMenuRecovery ??
                (dailyMaintenancePreemption.ShouldPreempt
                    ? NotAttemptedStageOneCollectionTeacher()
                    : await TryBuildStageOneCollectionTeacherQueueAsync(
                    http,
                    options,
                    iteration,
                    snapshotJson,
                    beforeSnapshot,
                    lastStateHash,
                    allowObjectiveSafeCapacityRecovery: true,
                    activeObjectiveContinuation:
                        activeObjectiveContinuation,
                    suppressedObjectiveContinuations:
                        suppressedObjectiveContinuations));
            if (collectionTeacher.Queue is not null)
            {
                queue = collectionTeacher.Queue;
                dailyPlanRanking = collectionTeacher.Ranking;
                modelPlanPath = collectionTeacher.PlanPath;
                rankingPath = collectionTeacher.RankingPath;
                decisionModelPlanPath = collectionTeacher.PlanPath;
                decisionRankingPath = collectionTeacher.RankingPath;
                decisionCompiledQueuePath = collectionTeacher.QueuePath;
                decisionSnapshotPath = collectionTeacher.SnapshotPath;
                decisionSourceStateHash = lastStateHash;
                selectedCandidateId = collectionTeacher.SelectedCandidateId;
                selectedQueueIndex = 0;
                useDailyPlanDecisionLease = true;
                var isInventoryCapacityRecovery = string.Equals(
                    collectionTeacher.Status,
                    "ready_inventory_capacity_recovery",
                    StringComparison.Ordinal);
                var isControlOnlyQueue = string.Equals(
                        collectionTeacher.Status,
                        "ready_authoritative_tie_control",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        collectionTeacher.Status,
                        "ready_native_control_settle",
                        StringComparison.Ordinal) ||
                    isInventoryCapacityRecovery ||
                    string.Equals(
                        collectionTeacher.Status,
                        "ready_mandatory_menu_recovery_control",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        collectionTeacher.Status,
                        "ready_ginger_island_prerequisite_control",
                        StringComparison.Ordinal);
                if (isInventoryCapacityRecovery &&
                    activeObjectiveContinuation is not null)
                {
                    if (deferredObjectiveContinuationForCapacityRecovery is not null)
                    {
                        throw new InvalidOperationException(
                            "inventory_capacity_recovery_objective_already_deferred");
                    }

                    deferredObjectiveContinuationForCapacityRecovery =
                        JsonNode.Parse(
                            activeObjectiveContinuation.ToJsonString(JsonOptions))!
                        .AsObject();
                    activeObjectiveContinuation = null;
                }
                inventoryCapacityRecoveryControlActive =
                    isInventoryCapacityRecovery;
                leasedDecisionIsInventoryCapacityRecovery =
                    isInventoryCapacityRecovery;
                if (isControlOnlyQueue)
                    stageOneCollectionControlQueueCount++;
                else
                    stageOneCollectionTeacherQueueCount++;
                lastStageOneCollectionTeacherFallbackSignature = string.Empty;
                AppendProgress(
                    options,
                    isControlOnlyQueue
                        ? "stage_one_collection_control_queue_compiled"
                        : "stage_one_collection_teacher_queue_compiled",
                    iteration,
                    lastStateHash,
                    ReadString(queue, "queue_id"),
                    "candidate_id=" + selectedCandidateId +
                    " teacher_queue_count=" +
                    stageOneCollectionTeacherQueueCount +
                    " control_queue_count=" +
                    stageOneCollectionControlQueueCount +
                    " " + collectionTeacher.Detail);
            }
            else
            {
                if (dailyMaintenancePreemption.ShouldPreempt)
                {
                    AppendProgress(
                        options,
                        "stage_one_collection_yielded_to_daily_maintenance",
                        iteration,
                        lastStateHash,
                        string.Empty,
                        "reason=" + dailyMaintenancePreemption.Reason +
                        " watering_candidate_count=" +
                        dailyMaintenancePreemption.WateringCandidateCount +
                        " active_objective_continuation_preserved=" +
                        (activeObjectiveContinuation is not null)
                            .ToString()
                            .ToLowerInvariant());
                }
                var yieldCollectionControlToDailyPlan =
                    collectionTeacher.Attempted &&
                    StageOneCollectionFallbackPolicy.ShouldYieldToDailyPlan(
                        options.RequireGoldenWalnutSourceScheduleReady,
                        collectionTeacher.Status,
                        collectionTeacher.Detail);
                if (collectionTeacher.Attempted)
                {
                    var relaxedMasterAnglerContinuation =
                        QueueReplanFilter
                            .RelaxUnavailableMasterAnglerSourceVariant(
                                activeObjectiveContinuation,
                                collectionTeacher.Ranking);
                    if (relaxedMasterAnglerContinuation is not null)
                    {
                        activeObjectiveContinuation =
                            relaxedMasterAnglerContinuation;
                        noProgressBackoff.Reset();
                        AppendProgress(
                            options,
                            "continuation_source_variant_replan",
                            iteration,
                            lastStateHash,
                            string.Empty,
                            "reason=no_available_master_angler_source_variant" +
                            " option_id=" + ReadString(
                                relaxedMasterAnglerContinuation,
                                "option_id") +
                            " target_qualified_item_id=" + ReadString(
                                relaxedMasterAnglerContinuation,
                                "master_angler_target_qualified_item_id") +
                            " suppressed_until_day_change=false");
                        await DelayBeforeNextAttemptAsync(
                            options,
                            attemptOrdinal);
                        continue;
                    }
                    if (QueueReplanFilter.ShouldReleaseUnavailableContinuation(
                            activeObjectiveContinuation,
                            collectionTeacher.Ranking))
                    {
                        var releasedContinuation =
                            activeObjectiveContinuation!;
                        var releasedOptionId = ReadString(
                            releasedContinuation,
                            "option_id");
                        QueueReplanFilter.AddSuppressedContinuation(
                            suppressedObjectiveContinuations,
                            releasedContinuation);
                        activeObjectiveContinuation = null;
                        noProgressBackoff.Reset();
                        AppendProgress(
                            options,
                            "continuation_released",
                            iteration,
                            lastStateHash,
                            string.Empty,
                            "reason=no_available_stage_one_collection_" +
                            "continuation_candidate option_id=" +
                            releasedOptionId +
                            " suppressed_until_day_change=true" +
                            " suppressed_count=" +
                            suppressedObjectiveContinuations.Count);
                        await DelayBeforeNextAttemptAsync(
                            options,
                            attemptOrdinal);
                        continue;
                    }

                    var fallbackSignature = collectionTeacher.Status + "|" +
                        collectionTeacher.Detail;
                    if (!string.Equals(
                            fallbackSignature,
                            lastStageOneCollectionTeacherFallbackSignature,
                            StringComparison.Ordinal))
                    {
                        lastStageOneCollectionTeacherFallbackSignature =
                            fallbackSignature;
                        AppendProgress(
                            options,
                            "stage_one_collection_teacher_fallback",
                            iteration,
                            lastStateHash,
                            string.Empty,
                            "status=" + collectionTeacher.Status +
                            " reason=" + collectionTeacher.Detail);
                    }

                    if (!yieldCollectionControlToDailyPlan)
                    {
                        stopReason =
                            "stage_one_collection_teacher_blocked status=" +
                            collectionTeacher.Status +
                            " reason=" + collectionTeacher.Detail;
                        AppendProgress(
                            options,
                            "stopped",
                            iteration,
                            lastStateHash,
                            string.Empty,
                            stopReason);
                        break;
                    }

                    AppendProgress(
                        options,
                        "stage_one_collection_teacher_yielded",
                        iteration,
                        lastStateHash,
                        string.Empty,
                        "status=" + collectionTeacher.Status +
                        " reason=" + collectionTeacher.Detail +
                        " next=shared_daily_plan");
                }

                var dailyPlan = await BuildQueueFromDailyPlanAsync(
                    http,
                    options,
                    lastStateHash,
                    suppressedObjectiveContinuations:
                        suppressedObjectiveContinuations,
                    candidateOptionIdsOverride:
                        dailyMaintenancePreemption.ShouldPreempt
                            ? StageOneDailyMaintenancePreemptionPolicy
                                .CandidateOptionIds
                            : null,
                    candidateKindOverride:
                        dailyMaintenancePreemption.ShouldPreempt
                            ? StageOneDailyMaintenancePreemptionPolicy
                                .CandidateKind
                            : null,
                    activeObjectiveContinuation:
                        dailyMaintenancePreemption.ShouldPreempt
                            ? null
                            : activeObjectiveContinuation);
                modelPlanPath = Path.Combine(options.SnapshotDir, "model-plan-" + iteration.ToString("D4") + ".json");
                await File.WriteAllTextAsync(modelPlanPath, dailyPlan.Plan.ToJsonString(JsonOptions), Encoding.UTF8);
                var dailyPlanPath = Path.Combine(options.SnapshotDir, "daily-plan-response-" + iteration.ToString("D4") + ".json");
                await File.WriteAllTextAsync(dailyPlanPath, dailyPlan.Response.ToJsonString(JsonOptions), Encoding.UTF8);
                rankingPath = Path.Combine(options.SnapshotDir, "ranking-response-" + iteration.ToString("D4") + ".json");
                await File.WriteAllTextAsync(rankingPath, dailyPlan.Ranking.ToJsonString(JsonOptions), Encoding.UTF8);
                dailyPlanRanking = dailyPlan.Ranking;
                queue = dailyPlan.Queue;
                decisionModelPlanPath = modelPlanPath;
                decisionRankingPath = rankingPath;
                if (PlanningHandoffCapture
                    .ShouldDeferCropProductionTeacherHandoffForExclusiveRecovery(
                        dailyPlanRanking))
                {
                    AppendProgress(
                        options,
                        "planning_handoff_deferred_for_exclusive_recovery",
                        iteration,
                        lastStateHash,
                        string.Empty,
                        "kind=crop_production_teacher " +
                        "next=execute_exclusive_recovery_queue_then_fresh_replan");
                }
                else if (PlanningHandoffCapture
                    .TryReadCropProductionTeacherHandoff(
                        dailyPlanRanking,
                        out var planningHandoff))
                {
                    var dispatch = await DispatchCropTeacherAsync(
                        http,
                        options,
                        iteration,
                        rawSnapshotJson,
                        planningHandoff,
                        "initial");
                    if (options.UseManagedCropTeacher)
                        cropTeacherManagedOwner.Attach(await OpenCropTeacherManagedOwnerAsync(http, options, iteration, dispatch));
                    lastPlanningHandoffCaptureManifestPath =
                        dispatch.Capture.ManifestPath;
                    lastPlanningHandoffTeacherDispatchManifestPath =
                        dispatch.TeacherDispatchManifestPath;
                    lastPlanningHandoffNativeRunPlanPath =
                        dispatch.NativeRunPlanPath;
                    activeCropTeacherRunPlanRequest = dispatch.RunPlanRequest;
                    activeCropTeacherRunPlan = dispatch.RunPlan;
                    cropTeacherTransitionLedgerPath =
                        dispatch.TransitionLedgerPath;
                    activeCropTeacherTransitionLedger =
                        dispatch.TransitionLedger;
                    cropTeacherHarvestReceiptCount =
                        activeCropTeacherTransitionLedger.HarvestReceipts.Count;
                    cropTeacherDayBoundaryReceiptCount =
                        activeCropTeacherTransitionLedger.DayBoundaryReceipts.Count;
                    pendingCropTeacherDayBoundary = null;
                    cropTeacherPreparedCommitmentIds.Clear();
                    cropTeacherControlAttemptsStarted = 0;
                    cropTeacherRunCompleted = false;
                    cropTeacherReplanRequired = false;
                    cropTeacherReplanReason = string.Empty;
                    activeObjectiveContinuation = null;
                    deferredObjectiveContinuationForCapacityRecovery = null;
                    leasedDecisionModelPlanPath = string.Empty;
                    leasedDecisionRankingPath = string.Empty;
                    leasedDecisionCompiledQueuePath = string.Empty;
                    leasedDecisionSnapshotPath = string.Empty;
                    leasedDecisionSourceStateHash = string.Empty;
                    leasedDecisionGoalId = string.Empty;
                    leasedSelectedCandidateId = string.Empty;
                    leasedSelectedQueueIndex = -1;
                    leasedDecisionIsInventoryCapacityRecovery = false;
                    AppendProgress(
                        options,
                        "planning_handoff_dispatched",
                        iteration,
                        lastStateHash,
                        string.Empty,
                        "kind=crop_production_teacher manifest=" +
                        dispatch.Capture.ManifestPath +
                        " map=" + dispatch.Capture.MapWidth + "x" +
                        dispatch.Capture.MapHeight +
                        " layout_snapshots=" +
                        dispatch.Capture.LayoutSnapshotCount +
                        " native_run_plan=" +
                        dispatch.NativeRunPlanPath +
                        " actions=" + dispatch.ActionCount);
                    await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                    continue;
                }
            }
        }
    }
    else if (options.UseParameterizedAction)
    {
        var modelActionJson = BuildParameterizedActionRequest(options, lastStateHash);
        modelPlanPath = Path.Combine(options.SnapshotDir, "model-action-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(modelPlanPath, modelActionJson, Encoding.UTF8);
        queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/action-queue/compile", modelActionJson);
    }
    else if (options.UsePlanOutput)
    {
        var modelPlanJson = BuildMovePlanRequest(options, lastStateHash);
        modelPlanPath = Path.Combine(options.SnapshotDir, "model-plan-" + iteration.ToString("D4") + ".json");
        await File.WriteAllTextAsync(modelPlanPath, modelPlanJson, Encoding.UTF8);
        queue = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/small-model/plan/action-queue/compile", modelPlanJson);
    }
    else
    {
        queue = await BuildQueueFromMockActionAsync(http, options, lastStateHash);
    }
    executionSnapshotProfile = ExecutionSnapshotProfileSelector.ResolveForQueue(
        queue,
        executionSnapshotProfile);
    var queuePath = Path.Combine(options.SnapshotDir, "compiled-queue-" + iteration.ToString("D4") + ".json");
    await File.WriteAllTextAsync(queuePath, queue.ToJsonString(JsonOptions), Encoding.UTF8);
    if (cropSeedSupportForExecution is not null)
    {
        cropSeedSupportForExecution.ValidateDispatchQueue(queuePath);
        selectedCandidateId = EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(ExecutableQueueItems(queue).FirstOrDefault());
        if (string.IsNullOrWhiteSpace(selectedCandidateId))
            throw new InvalidDataException("Managed crop seed queue lost its original selected candidate identity.");
    }
    if (options.UseTeacherPreferenceQueue || options.UsePrecompiledQueue)
    {
        decisionCompiledQueuePath = queuePath;
    }
    else if (!useDailyPlanDecisionLease &&
        activeCropTeacherRunPlan is not null)
    {
        decisionCompiledQueuePath = queuePath;
    }
    if (useDailyPlanDecisionLease && !resumedSelectedQueueDecision)
    {
        var firstSelectedItem = ExecutableQueueItems(queue).FirstOrDefault();
        selectedCandidateId = EffectiveDecisionArtifactTracker.ReadQueueItemCandidateId(firstSelectedItem);
        selectedQueueIndex = QueueReplanFilter.ReadAcceptedCandidateIndex(firstSelectedItem);
        decisionCompiledQueuePath = queuePath;
        leasedDecisionModelPlanPath = decisionModelPlanPath;
        leasedDecisionRankingPath = decisionRankingPath;
        leasedDecisionCompiledQueuePath = decisionCompiledQueuePath;
        leasedDecisionSnapshotPath = decisionSnapshotPath;
        leasedDecisionSourceStateHash = decisionSourceStateHash;
        leasedDecisionGoalId = ReadString(queue, "goal_id");
        if (string.IsNullOrWhiteSpace(leasedDecisionGoalId))
        {
            leasedDecisionGoalId = options.Goal;
        }
        leasedSelectedCandidateId = selectedCandidateId;
        leasedSelectedQueueIndex = selectedQueueIndex;
        leasedDecisionIsInventoryCapacityRecovery =
            inventoryCapacityRecoveryControlActive;
    }
    lastQueueId = ReadString(queue, "queue_id");
    var queueStatus = ReadString(queue, "status");
    var noProgressDecision = noProgressBackoff.Observe(queue);
    if (options.RequireExecutorFeedback)
    {
        if (!string.Equals(queueStatus, "pending", StringComparison.Ordinal))
        {
            var executableSubsetCount = ExecutableQueueItems(queue).Length;
            if (useDailyPlanDecisionLease &&
                resumedSelectedQueueDecision &&
                executableSubsetCount == 0)
            {
                var invalidatedCandidateId = leasedSelectedCandidateId;
                leasedDecisionModelPlanPath = string.Empty;
                leasedDecisionRankingPath = string.Empty;
                leasedDecisionCompiledQueuePath = string.Empty;
                leasedDecisionSnapshotPath = string.Empty;
                leasedDecisionSourceStateHash = string.Empty;
                leasedDecisionGoalId = string.Empty;
                leasedSelectedCandidateId = string.Empty;
                leasedSelectedQueueIndex = -1;
                leasedDecisionIsInventoryCapacityRecovery = false;
                if (deferredObjectiveContinuationForCapacityRecovery is not null)
                {
                    activeObjectiveContinuation =
                        deferredObjectiveContinuationForCapacityRecovery;
                    deferredObjectiveContinuationForCapacityRecovery = null;
                }
                noProgressBackoff.Reset();
                AppendProgress(
                    options,
                    "selected_queue_invalidated",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "reason=locked_candidate_unavailable_on_fresh_snapshot candidate_id=" +
                    invalidatedCandidateId +
                    " policy_model_invoked=false");
                await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                continue;
            }
            if (!options.ContinueAfterBlockedQueueItems || executableSubsetCount == 0)
            {
                var relaxedMasterAnglerContinuation =
                    QueueReplanFilter
                        .RelaxUnavailableMasterAnglerSourceVariant(
                            activeObjectiveContinuation,
                            dailyPlanRanking);
                if (executableSubsetCount == 0 &&
                    relaxedMasterAnglerContinuation is not null)
                {
                    activeObjectiveContinuation =
                        relaxedMasterAnglerContinuation;
                    noProgressBackoff.Reset();
                    AppendProgress(
                        options,
                        "continuation_source_variant_replan",
                        iteration,
                        lastStateHash,
                        lastQueueId,
                        "reason=no_available_master_angler_source_variant" +
                        " option_id=" + ReadString(
                            relaxedMasterAnglerContinuation,
                            "option_id") +
                        " target_qualified_item_id=" + ReadString(
                            relaxedMasterAnglerContinuation,
                            "master_angler_target_qualified_item_id") +
                        " suppressed_until_day_change=false");
                    await DelayBeforeNextAttemptAsync(
                        options,
                        attemptOrdinal);
                    continue;
                }
                if (executableSubsetCount == 0 &&
                    QueueReplanFilter.ShouldReleaseUnavailableContinuation(
                        activeObjectiveContinuation,
                        dailyPlanRanking))
                {
                    var releasedContinuation = activeObjectiveContinuation!;
                    var releasedOptionId = ReadString(
                        releasedContinuation,
                        "option_id");
                    QueueReplanFilter.AddSuppressedContinuation(
                        suppressedObjectiveContinuations,
                        releasedContinuation);
                    activeObjectiveContinuation = null;
                    noProgressBackoff.Reset();
                    AppendProgress(
                        options,
                        "continuation_released",
                        iteration,
                        lastStateHash,
                        lastQueueId,
                        "reason=no_available_continuation_candidate option_id=" +
                        releasedOptionId +
                        " suppressed_until_day_change=true suppressed_count=" +
                        suppressedObjectiveContinuations.Count);
                    await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
                    continue;
                }
                AppendProgress(
                    options,
                    "blocked",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "queue_not_pending executor_feedback_required" +
                    NoProgressDetail(noProgressDecision));
                if (NoProgressBackoffPolicy.ReachedLimit(
                        noProgressDecision,
                        options.MaxQueueNoProgressStreak))
                {
                    stopReason =
                        "max_queue_no_progress_streak_reached count=" +
                        noProgressDecision.Streak +
                        " queue_status=" + queueStatus;
                    AppendProgress(
                        options,
                        "stopped",
                        iteration,
                        lastStateHash,
                        lastQueueId,
                        stopReason);
                    break;
                }
                await DelayBeforeNextAttemptAsync(
                    options,
                    attemptOrdinal,
                    noProgressDecision.DelayMs);
                continue;
            }

            AppendProgress(
                options,
                "partial_queue",
                iteration,
                lastStateHash,
                lastQueueId,
                "executing_pending_subset count=" + executableSubsetCount + " queue_status=" + queueStatus);
        }

        RequireManagedCropDispatch(cropTeacherManagedOwner.Current, cropManagedOperationForExecution);
        var cropTeacherDayBoundaryStart =
            await CaptureCropTeacherDayBoundaryStartAsync(
                options,
                cropTeacherStepForExecution,
                beforeSnapshot,
                lastStateHash);
        var cropTeacherDayBoundaryLedger =
            cropTeacherDayBoundaryStart is null
                ? null
                : activeCropTeacherTransitionLedger;
        if (cropTeacherDayBoundaryStart is null &&
            completedCropTeacherBoundaryStart is not null)
        {
            cropTeacherDayBoundaryStart = completedCropTeacherBoundaryStart;
            cropTeacherDayBoundaryLedger = completedCropTeacherBoundaryLedger;
        }
        var capacityRecoveryControlActive =
            inventoryCapacityRecoveryControlActive ||
            deferredObjectiveContinuationForCapacityRecovery is not null;
        var executionSuppressedContinuations = capacityRecoveryControlActive
            ? new List<JsonObject>()
            : suppressedObjectiveContinuations;
        async Task<JsonObject> ExecuteOriginalCropQueue() => options.UseRuntimeTestHarnessExecutor
            ? await ExecuteRuntimeTestHarnessCoreAsync(http, executorHttp, options, iteration, snapshotPath, beforeSnapshot, queue, lastStateHash, lastQueueId, decisionModelPlanPath, decisionRankingPath, decisionCompiledQueuePath, decisionSnapshotPath, decisionSourceStateHash, selectedCandidateId, selectedQueueIndex, queuePath, deferredObjectiveContinuationForDailyMaintenance is null ? activeObjectiveContinuation : null, executionSuppressedContinuations, useDailyPlanDecisionLease, executionSnapshotUrl, executionSnapshotProfile, cropSeedSupportForExecution as ICropTeacherSeedPrefixRuntime)
            : await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/action-queues/" + Uri.EscapeDataString(lastQueueId) + "/execute-training-sandbox", "{}");
        var execution = cropManagedOperationForExecution is null ? await ExecuteOriginalCropQueue() :
            await ExecuteCropTeacherManagedOperationAsync(http, executorHttp, options, iteration, cropTeacherManagedOwner.Current!,
                cropManagedOperationForExecution, queuePath, ExecuteOriginalCropQueue);
        if (cropSeedSupportForExecution is not null && cropManagedOperationForExecution is null)
            await cropSeedSupportForExecution.CompleteAsync(queuePath, ReadString(execution, "execution_path"),
                ReadString(execution, "after_snapshot_path"), selectedCandidateId);
        if (activeCropTeacherTransitionLedger is not null &&
            cropTeacherStepForExecution is not null)
        {
            var receiptsAppended =
                activeCropTeacherTransitionLedger.RecordVerifiedHarvestResults(
                    execution,
                    cropTeacherStepForExecution,
                    DateTimeOffset.UtcNow.ToString("O"));
            if (receiptsAppended > 0)
            {
                cropTeacherHarvestReceiptCount =
                    activeCropTeacherTransitionLedger.HarvestReceipts.Count;
                AppendProgress(
                    options,
                    "crop_teacher_harvest_receipts_appended",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "count=" + receiptsAppended +
                    " total=" +
                    activeCropTeacherTransitionLedger.HarvestReceipts.Count +
                    " ledger=" + cropTeacherTransitionLedgerPath);
            }
        }
        var feedbackAvailable = execution["feedback_available"]?.GetValue<bool>() == true;
        if (!feedbackAvailable && !options.UseRuntimeTestHarnessExecutor)
        {
            AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, "executor_feedback_unavailable status=" + ReadString(execution, "status"));
            await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
            continue;
        }

        if (options.UseRuntimeTestHarnessExecutor)
        {
            var completedCandidateRepresentative =
                QueueReplanFilter.LastTrainingVerifiedCompletedSelectedCandidateStep(
                    execution);
            var primitiveVerified =
                QueueReplanFilter.IsTrainingVerifiedExecution(execution) ||
                completedCandidateRepresentative is not null;
            if (!primitiveVerified)
            {
                WriteGoldenWalnutUnverifiedContinuityEpisodes(
                    options,
                    iteration,
                    snapshotPath,
                    modelPlanPath,
                    queuePath,
                    queue,
                    execution,
                    lastStateHash,
                    lastQueueId);
                var unverifiedDecision =
                    unverifiedExecutionBackoff.ObserveUnverifiedExecution(
                        queue,
                        execution);
                AppendProgress(options, "blocked", iteration, lastStateHash, lastQueueId, options.ExecutorUnverifiedSource + " status=" + ReadString(execution, "status") + " primitive=" + ReadString(execution, "primitive_verification_status") + NoProgressDetail(unverifiedDecision));
                if (options.MaxUnverifiedExecutionStreak > 0 &&
                    unverifiedDecision.Streak >=
                        options.MaxUnverifiedExecutionStreak)
                {
                    stopReason =
                        "max_unverified_execution_streak_reached count=" +
                        unverifiedDecision.Streak +
                        " option=" + ReadString(execution, "option_id") +
                        " failure=" +
                        ReadString(execution, "failure_category");
                    AppendProgress(
                        options,
                        "stopped",
                        iteration,
                        lastStateHash,
                        lastQueueId,
                        stopReason);
                    break;
                }
                await DelayBeforeNextAttemptAsync(
                    options,
                    attemptOrdinal,
                    unverifiedDecision.DelayMs);
                continue;
            }

            unverifiedExecutionBackoff.Reset();

            var trainingRepresentative =
                QueueReplanFilter.IsTrainingVerifiedExecution(execution)
                    ? execution
                    : completedCandidateRepresentative!;
            if (cropTeacherDayBoundaryStart is not null &&
                string.Equals(
                    ReadString(trainingRepresentative, "option_id"),
                    "executor.sleep",
                    StringComparison.Ordinal))
            {
                pendingCropTeacherDayBoundary =
                    CreatePendingCropTeacherDayBoundary(
                        cropTeacherDayBoundaryStart,
                        trainingRepresentative);
                pendingCropTeacherDayBoundaryLedger =
                    cropTeacherDayBoundaryLedger ??
                    throw new InvalidOperationException(
                        "crop_teacher_transition_ledger_missing");
            }
            var executionNoProgress =
                noProgressBackoff.ObserveExecution(queue, trainingRepresentative);
            var routeOscillation = RouteOscillationDecision.None;
            if (RouteOscillationGuard.IsObservedRouteExecution(execution))
            {
                var afterSnapshotPath = ReadString(
                    execution,
                    "after_snapshot_path");
                if (!string.IsNullOrWhiteSpace(afterSnapshotPath) &&
                    File.Exists(afterSnapshotPath))
                {
                    var afterSnapshotText =
                        await ContentAddressedJsonArtifactStore
                            .ReadAllTextAsync(afterSnapshotPath);
                    var afterSnapshot = JsonNode.Parse(afterSnapshotText)
                        ?.AsObject() ?? new JsonObject();
                    routeOscillation = routeOscillationGuard.Observe(
                        execution,
                        beforeSnapshot,
                        afterSnapshot);
                }
                else
                {
                    routeOscillationGuard.Reset();
                }
            }
            else
            {
                routeOscillationGuard.Reset();
            }
            var realAppend = AppendRealExecutionRow(
                options,
                beforeSnapshot,
                queue,
                trainingRepresentative,
                lastStateHash,
                lastQueueId,
                appendToDataset: !executionNoProgress.NoProgress &&
                    !routeOscillation.Detected);
            if (!capacityRecoveryControlActive)
            {
                objectiveCompleted |= execution[
                    "objective_continuation_completed"]?.GetValue<bool>() == true;
                socialObjectiveCompleted |= execution[
                    "social_objective_completed"]?.GetValue<bool>() == true;
                if (execution["completed_objective_continuations"] is JsonArray completedContinuations)
                {
                    foreach (var completedContinuation in completedContinuations
                        .Select(node => node as JsonObject)
                        .Where(node => node is not null))
                    {
                        QueueReplanFilter.AddSuppressedContinuation(
                            suppressedObjectiveContinuations,
                            completedContinuation);
                    }
                }
            }
            activeObjectiveContinuation = execution["objective_continuation"] is JsonObject continuation
                ? JsonNode.Parse(continuation.ToJsonString(JsonOptions))?.AsObject()
                : null;
            if (deferredObjectiveContinuationForDailyMaintenance is not null)
            {
                if (activeObjectiveContinuation is not null)
                {
                    throw new InvalidOperationException(
                        "daily_maintenance_preemption_produced_unexpected_" +
                        "objective_continuation");
                }

                activeObjectiveContinuation =
                    deferredObjectiveContinuationForDailyMaintenance;
            }
            var selectedQueueDecisionComplete = execution[
                "selected_queue_decision_complete"]?.GetValue<bool>() == true;
            var selectedQueueRedecisionRequired = execution[
                "selected_queue_redecision_required"]?.GetValue<bool>() == true;
            var selectedQueueResumeIndex = execution[
                "selected_queue_resume_index"]?.GetValue<int>() ?? -1;
            var selectedQueueDecisionLeaseContinues =
                useDailyPlanDecisionLease &&
                !selectedQueueDecisionComplete &&
                !selectedQueueRedecisionRequired &&
                selectedQueueResumeIndex >= 0;
            if (selectedQueueDecisionLeaseContinues)
            {
                leasedSelectedCandidateId = ReadString(
                    execution,
                    "selected_queue_resume_candidate_id");
                leasedSelectedQueueIndex = selectedQueueResumeIndex;
            }
            else
            {
                leasedDecisionModelPlanPath = string.Empty;
                leasedDecisionRankingPath = string.Empty;
                leasedDecisionCompiledQueuePath = string.Empty;
                leasedDecisionSnapshotPath = string.Empty;
                leasedDecisionSourceStateHash = string.Empty;
                leasedDecisionGoalId = string.Empty;
                leasedSelectedCandidateId = string.Empty;
                leasedSelectedQueueIndex = -1;
                leasedDecisionIsInventoryCapacityRecovery = false;
            }
            if (deferredObjectiveContinuationForCapacityRecovery is not null &&
                !selectedQueueDecisionLeaseContinues)
            {
                activeObjectiveContinuation =
                    deferredObjectiveContinuationForCapacityRecovery;
                deferredObjectiveContinuationForCapacityRecovery = null;
                AppendProgress(
                    options,
                    "inventory_capacity_recovery_objective_restored",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "selected_queue_decision_complete=" +
                    selectedQueueDecisionComplete.ToString().ToLowerInvariant() +
                    " selected_queue_redecision_required=" +
                    selectedQueueRedecisionRequired.ToString().ToLowerInvariant() +
                    " selected_queue_resume_index=" +
                    selectedQueueResumeIndex);
            }
            WritePlanExecutionEpisode(options, iteration, snapshotPath, modelPlanPath, queuePath, queue, execution, realAppend, lastStateHash, lastQueueId);
            if (routeOscillation.Detected)
            {
                stopReason =
                    "semantic_route_oscillation_detected objective=" +
                    routeOscillation.ObjectiveKey +
                    " reverse_transition_streak=" +
                    routeOscillation.ReverseTransitionStreak +
                    " transition=" + routeOscillation.FromLocation +
                    "->" + routeOscillation.ToLocation +
                    " total_day=" + routeOscillation.TotalDay +
                    " time=" + routeOscillation.Time;
                AppendProgress(
                    options,
                    "stopped",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    stopReason);
                break;
            }
            if (options.RequireGoldenWalnutSourceScheduleReady &&
                execution["after_snapshot_fresh"]?.GetValue<bool>() == true)
            {
                var milestoneAfterPath = ReadString(
                    execution,
                    "after_snapshot_path");
                if (!string.IsNullOrWhiteSpace(milestoneAfterPath) &&
                    File.Exists(milestoneAfterPath))
                {
                    goldenWalnutSourceMilestone =
                        await EvaluateGoldenWalnutSourceMilestoneArtifactAsync(
                            milestoneAfterPath);
                    var milestoneSignature =
                        GoldenWalnutSourceMilestoneSignature(
                            goldenWalnutSourceMilestone);
                    if (!string.Equals(
                            milestoneSignature,
                            goldenWalnutSourceMilestoneSignature,
                            StringComparison.Ordinal))
                    {
                        goldenWalnutSourceMilestoneSignature =
                            milestoneSignature;
                        AppendGoldenWalnutSourceMilestoneProgress(
                            options,
                            goldenWalnutSourceMilestone,
                            iteration,
                            ReadString(execution, "after_state_hash"),
                            lastQueueId);
                    }
                    if (goldenWalnutSourceMilestone.CurrentTotalDay >=
                        GoldenWalnutUnlockScheduleProofBuilder
                            .DeadlineTotalDayExclusive)
                    {
                        stopReason =
                            "golden_walnut_deadline_total_day_reached current=" +
                            goldenWalnutSourceMilestone.CurrentTotalDay +
                            " deadline_exclusive=" +
                            GoldenWalnutUnlockScheduleProofBuilder
                                .DeadlineTotalDayExclusive;
                        AppendProgress(
                            options,
                            "stopped",
                            iteration,
                            ReadString(execution, "after_state_hash"),
                            lastQueueId,
                            stopReason);
                        break;
                    }
                    if (goldenWalnutSourceMilestone.Ready)
                    {
                        activeCropTeacherRunPlan = null;
                        activeCropTeacherRunPlanRequest = null;
                        activeCropTeacherTransitionLedger = null;
                        pendingCropTeacherDayBoundary = null;
                        pendingCropTeacherDayBoundaryLedger = null;
                    }
                }
            }
            if (options.RequireGourmandContinuationReady &&
                execution["after_snapshot_fresh"]?.GetValue<bool>() == true)
            {
                var gourmandAfterPath = ReadString(
                    execution,
                    "after_snapshot_path");
                if (!string.IsNullOrWhiteSpace(gourmandAfterPath) &&
                    File.Exists(gourmandAfterPath))
                {
                    gourmandContinuationMilestone =
                        await EvaluateGourmandContinuationMilestoneArtifactAsync(
                            gourmandAfterPath);
                    var gourmandSignature =
                        GourmandContinuationMilestoneSignature(
                            gourmandContinuationMilestone);
                    if (!string.Equals(
                            gourmandSignature,
                            gourmandContinuationMilestoneSignature,
                            StringComparison.Ordinal))
                    {
                        gourmandContinuationMilestoneSignature =
                            gourmandSignature;
                        AppendGourmandContinuationMilestoneProgress(
                            options,
                            gourmandContinuationMilestone,
                            iteration,
                            ReadString(execution, "after_state_hash"),
                            lastQueueId);
                    }
                }
            }
            var horizonObservations = AppendClosedHorizonObservations(options, beforeSnapshot, execution);
            var policyAppend = executionNoProgress.NoProgress ||
                closingNativeSaveBoundary
                ? new PolicyTrajectoryAppendBatchResult()
                : AppendPolicyDecisionTrajectories(options, iteration, execution, realAppend);
            rowsAppended = realAppend.RowCount;
            if (!executionNoProgress.NoProgress)
            {
                if (!closingNativeSaveBoundary)
                {
                    verifiedActions++;
                }
                AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended + " policy_trajectories=" + policyAppend.AppendedCount + " policy_trajectory_skips=" + policyAppend.SkippedCount + " policy_trajectory_first_skip=" + policyAppend.FirstSkipReason + " horizon_observations=" + horizonObservations + " verified_actions=" + verifiedActions + " required_verified_actions=" + options.RequiredVerifiedActions + " source=" + options.ExecutorFeedbackSource);
                var train = options.RequireStructuredPolicy &&
                    policyAppend.AppendedCount == 0 &&
                    horizonObservations == 0
                        ? (TrainingReport: (JsonObject?)null, Prediction: (JsonObject?)null)
                        : await TrainIfNeededAsync(http, options, iteration);
                if (train.TrainingReport is not null)
                {
                    lastTrainingReport = train.TrainingReport;
                    lastPrediction = train.Prediction;
                }
                if (closingNativeSaveBoundary)
                {
                    var afterSnapshotPath = ReadString(
                        execution,
                        "after_snapshot_path");
                    var afterSnapshot = string.IsNullOrWhiteSpace(afterSnapshotPath) ||
                        !File.Exists(afterSnapshotPath)
                            ? new JsonObject()
                            : JsonNode.Parse(
                                await ContentAddressedJsonArtifactStore.ReadAllTextAsync(
                                    afterSnapshotPath))?.AsObject() ?? new JsonObject();
                    nativeSaveBoundaryCurrentDayKey =
                        QueueReplanFilter.SnapshotDayKey(afterSnapshot);
                    currentSaveFingerprint = await NativeSaveBoundaryVerifier.CaptureWithRetryAsync(
                        options.SaveIsolationPath,
                        options.SaveSlot);
                    var saveBoundaryObservation =
                        NativeSaveBoundaryVerifier.Evaluate(
                            nativeSaveBoundaryInitialDayKey,
                            nativeSaveBoundaryCurrentDayKey,
                            initialSaveFingerprint!,
                            currentSaveFingerprint);
                    nativeSaveBoundaryVerified =
                        saveBoundaryObservation.Verified;
                    AppendProgress(
                        options,
                        nativeSaveBoundaryVerified
                            ? "native_save_boundary_verified"
                            : "native_save_boundary_pending",
                        iteration,
                        lastStateHash,
                        lastQueueId,
                        "day_advanced=" + saveBoundaryObservation.DayAdvanced +
                        " save_changed=" + saveBoundaryObservation.SaveChanged +
                        " initial_day=" + nativeSaveBoundaryInitialDayKey +
                        " current_day=" + nativeSaveBoundaryCurrentDayKey);
                    if (nativeSaveBoundaryVerified &&
                        pendingCropTeacherDayBoundary is not null)
                    {
                        var terminalBoundaryCheck =
                            await CheckCropTeacherDayBoundaryAsync(
                                options,
                                pendingCropTeacherDayBoundary,
                                afterSnapshot);
                        if (!terminalBoundaryCheck.Verified)
                        {
                            throw new InvalidDataException(
                                "crop_teacher_terminal_save_boundary_not_settled " +
                                terminalBoundaryCheck.Detail);
                        }
                        var terminalLedger =
                            pendingCropTeacherDayBoundaryLedger ??
                            throw new InvalidOperationException(
                                "crop_teacher_transition_ledger_missing");
                        terminalLedger.RecordVerifiedDayBoundary(
                            terminalBoundaryCheck.Receipt ??
                            throw new InvalidOperationException(
                                "crop_teacher_day_boundary_receipt_missing"));
                        cropTeacherDayBoundaryReceiptCount =
                            terminalLedger.DayBoundaryReceipts.Count;
                        AppendProgress(
                            options,
                            "crop_teacher_terminal_save_boundary_settled",
                            iteration,
                            ReadString(afterSnapshot, "state_hash"),
                            pendingCropTeacherDayBoundary.QueueId,
                            "from_total_day=" +
                            pendingCropTeacherDayBoundary.FromTotalDay +
                            " receipt_count=" +
                            cropTeacherDayBoundaryReceiptCount);
                        pendingCropTeacherDayBoundary = null;
                        pendingCropTeacherDayBoundaryLedger = null;
                        completedCropTeacherBoundaryStart = null;
                        completedCropTeacherBoundaryLedger = null;
                    }
                }
            }
            else
            {
                AppendProgress(
                    options,
                    "idle_backoff",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    "recovery_refresh_wait_only dataset_rows=" +
                    rowsAppended +
                    NoProgressDetail(executionNoProgress));
            }

            if (NoProgressBackoffPolicy.ReachedLimit(
                    executionNoProgress,
                    options.MaxQueueNoProgressStreak))
            {
                stopReason =
                    "max_queue_no_progress_streak_reached count=" +
                    executionNoProgress.Streak +
                    " phase=verified_recovery_wait";
                AppendProgress(
                    options,
                    "stopped",
                    iteration,
                    lastStateHash,
                    lastQueueId,
                    stopReason);
                break;
            }

            await DelayBeforeNextAttemptAsync(
                options,
                attemptOrdinal,
                executionNoProgress.DelayMs);
            continue;
        }
    }

    var appendRequest = JsonSerializer.Serialize(new
    {
        dataset_path = Path.GetFullPath(options.DatasetPath)
    }, JsonOptions);
    var append = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/action-queues/" + Uri.EscapeDataString(lastQueueId) + "/training-feature-row/append", appendRequest);
    rowsAppended = append["row_count"]?.GetValue<int>() ?? rowsAppended;
    AppendProgress(options, "append", iteration, lastStateHash, lastQueueId, "dataset_rows=" + rowsAppended);

    if (iteration % options.TrainEvery == 0 || iteration == options.MaxAttempts)
    {
        var trainRequest = JsonSerializer.Serialize(new
        {
            dataset_path = Path.GetFullPath(options.DatasetPath)
        }, JsonOptions);
        lastTrainingReport = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/training/baseline/train", trainRequest);
        lastPrediction = await PostJsonStringAsync(http, options.BackendUrl + "/api/v1/planner/baseline/rank-options", trainRequest);
        var bestOption = lastPrediction["ranked_options"]?[0]?["option_id"]?.GetValue<string>() ?? string.Empty;
        AppendProgress(options, "train", iteration, lastStateHash, lastQueueId, "best_option=" + bestOption);
    }

    await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
    }
    catch (Exception ex)
    {
        consecutiveErrors = lastErrorIteration == iteration - 1 ? consecutiveErrors + 1 : 1;
        lastErrorIteration = iteration;
        var detail = "error_type=" + ex.GetType().Name + " message=" + SanitizeProgressValue(ex.Message);
        AppendProgress(options, "error", iteration, lastStateHash, lastQueueId, detail);
        Console.Error.WriteLine("Live training iteration " + iteration + " failed: " + ex);
        if (options.UseManagedCropTeacher)
        {
            stopReason = "crop_teacher_managed_interrupted_preserve_original_evidence";
            break;
        }
        if (consecutiveErrors >= options.MaxConsecutiveErrors)
        {
            stopReason = "max_consecutive_errors_reached count=" + consecutiveErrors;
            AppendProgress(options, "stopped", iteration, lastStateHash, lastQueueId, stopReason);
            break;
        }
        await DelayBeforeNextAttemptAsync(options, attemptOrdinal);
    }
}

var goldenWalnutFreshSaveEpisodeManifestPath = string.Empty;
var goldenWalnutFreshSaveEpisodeCount = 0;
var goldenWalnutSourceSchedulePath = string.Empty;
var goldenWalnutSourceScheduleVerified = false;
var goldenWalnutGourmandContinuityVerified = false;
var goldenWalnutGourmandVerifiedTransitionCount = 0;
var goldenWalnutGourmandContinuityReceiptPath = string.Empty;
var goldenWalnutGourmandContinuityBlockingReasons =
    new[] { "not_evaluated" };
if (options.CaptureGoldenWalnutFreshSaveEvidence)
{
    try
    {
        goldenWalnutFreshSaveEpisodeCount = Directory.EnumerateFiles(
            options.SnapshotDir,
            GoldenWalnutFreshSaveEpisodeArchive.EpisodeSearchPattern,
            SearchOption.TopDirectoryOnly).Count();
        if (goldenWalnutFreshSaveEpisodeCount > 0)
        {
            GoldenWalnutFreshSaveEpisodeManifestBuilder.WriteFromDirectory(
                options.GoldenWalnutFreshSaveEpisodeManifestPath,
                options.SnapshotDir,
                options.GoldenWalnutFreshSaveScheduleId,
                options.RunId);
            goldenWalnutFreshSaveEpisodeManifestPath =
                options.GoldenWalnutFreshSaveEpisodeManifestPath;
        }
    }
    catch (Exception ex) when (
        ex is IOException or InvalidDataException or InvalidOperationException or
            JsonException or ArgumentException)
    {
        stopReason = "golden_walnut_fresh_save_manifest_invalid message=" +
            SanitizeProgressValue(ex.Message);
    }
}
if (options.RequireGourmandContinuationReady &&
    gourmandContinuationMilestone.Ready &&
    !string.IsNullOrWhiteSpace(goldenWalnutFreshSaveEpisodeManifestPath))
{
    var continuity = WriteGourmandContinuationReceipt(
        options,
        goldenWalnutFreshSaveEpisodeManifestPath);
    goldenWalnutGourmandContinuityVerified = continuity.Verified;
    goldenWalnutGourmandVerifiedTransitionCount =
        continuity.VerifiedTransitionCount;
    goldenWalnutGourmandContinuityReceiptPath = continuity.ReceiptPath;
    goldenWalnutGourmandContinuityBlockingReasons =
        continuity.BlockingReasons;
    if (!string.IsNullOrWhiteSpace(continuity.StopReason))
        stopReason = continuity.StopReason;
}
if (options.RequireGoldenWalnutSourceScheduleReady &&
    goldenWalnutSourceMilestone.Ready &&
    !string.IsNullOrWhiteSpace(goldenWalnutFreshSaveEpisodeManifestPath))
{
    try
    {
        GoldenWalnutFreshSaveEpisodeScheduleBuilder.Write(
            options.GoldenWalnutFreshSaveSourceSchedulePath,
            goldenWalnutFreshSaveEpisodeManifestPath);
        goldenWalnutSourceSchedulePath =
            options.GoldenWalnutFreshSaveSourceSchedulePath;
        goldenWalnutSourceScheduleVerified = true;
    }
    catch (Exception ex) when (
        ex is IOException or InvalidDataException or InvalidOperationException or
            JsonException or ArgumentException)
    {
        stopReason = "golden_walnut_source_schedule_invalid message=" +
            SanitizeProgressValue(ex.Message);
    }
}
else if (options.RequireGoldenWalnutSourceScheduleReady &&
    goldenWalnutSourceMilestone.Ready)
{
    stopReason = "golden_walnut_source_schedule_invalid message=" +
        "fresh_save_episode_manifest_missing";
}

var ordinaryTargetMet =
    (options.RequiredVerifiedActions <= 0 ||
        verifiedActions >= options.RequiredVerifiedActions) &&
    (!options.StopAfterObjectiveComplete || objectiveCompleted) &&
    (!options.StopAfterSocialObjectiveComplete || socialObjectiveCompleted) &&
    (!options.RequireGoldenWalnutSourceScheduleReady ||
        (goldenWalnutSourceMilestone.Ready &&
         goldenWalnutSourceScheduleVerified)) &&
    (!options.RequireGourmandContinuationReady ||
        (gourmandContinuationMilestone.Ready &&
         goldenWalnutGourmandContinuityVerified)) &&
    (!options.RequireNativeSaveBoundary || nativeSaveBoundaryVerified);
var verifiedTargetMet = string.IsNullOrWhiteSpace(stopReason) &&
    ordinaryTargetMet;
var loopStatus = verifiedTargetMet ? "ok" : "incomplete";
if (!verifiedTargetMet)
{
    Environment.ExitCode = 2;
}
trainingDataTransaction.Complete(verifiedTargetMet);
if (trainingDataTransaction.CanonicalArtifactsUpdated &&
    lastTrainingReport is not null)
{
    var canonicalDatasetManifestPath = Path.Combine(
        options.Root,
        "datasets",
        "formal-policy",
        "policy-dataset-manifest.json");
    var checkpointSha256 = StructuredPolicyCheckpointStore.HashFile(
        options.PolicyCheckpointPath);
    var canonicalCheckpoint = new StructuredPolicyCheckpointStore().Load(
        options.PolicyCheckpointPath);
    new FormalTrainingManifestStore().UpdateArtifacts(
        options.ManifestPath,
        options.RunId,
        canonicalDatasetManifestPath,
        options.PolicyCheckpointPath,
        checkpointSha256);
    lastTrainingReport["checkpoint_path"] = Path.GetFullPath(
        options.PolicyCheckpointPath);
    lastTrainingReport["checkpoint_sha256"] = checkpointSha256;
    lastTrainingReport["checkpoint_id"] = canonicalCheckpoint.CheckpointId;
    lastTrainingReport["dataset_manifest_path"] = Path.GetFullPath(
        canonicalDatasetManifestPath);
    lastTrainingReport["dataset_manifest_sha256"] =
        canonicalCheckpoint.Dataset.ManifestSha256;
}

var report = new LiveTrainingLoopReport
{
    RunId = options.RunId,
    ManifestPath = options.ManifestPath,
    BackendUrl = options.BackendUrl,
    BridgeSnapshotUrl = options.BridgeSnapshotUrl,
    ExecutionSnapshotProfile = options.ExecutionSnapshotProfile,
    SnapshotFile = options.SnapshotFile,
    SnapshotFileProfile = options.SnapshotFileProfile,
    SnapshotArtifactMode = options.SnapshotArtifactMode,
    ArtifactRetentionMode = options.ArtifactRetentionMode,
    MaxPersistedIterations = options.MaxPersistedIterations,
    DatasetPath = options.DatasetPath,
    ProgressLogPath = options.ProgressLogPath,
    SnapshotDir = options.SnapshotDir,
    GoldenWalnutFreshSaveEvidenceCaptureEnabled =
        options.CaptureGoldenWalnutFreshSaveEvidence,
    GoldenWalnutFreshSaveEpisodeManifestPath =
        goldenWalnutFreshSaveEpisodeManifestPath,
    GoldenWalnutFreshSaveEpisodeCount = goldenWalnutFreshSaveEpisodeCount,
    GoldenWalnutSourceScheduleRequired =
        options.RequireGoldenWalnutSourceScheduleReady,
    GoldenWalnutSourceMilestoneReady = goldenWalnutSourceMilestone.Ready,
    GoldenWalnutSourceCurrentTotalDay =
        goldenWalnutSourceMilestone.CurrentTotalDay,
    GoldenWalnutDeadlineTotalDayExclusive =
        GoldenWalnutUnlockScheduleProofBuilder.DeadlineTotalDayExclusive,
    GoldenWalnutSourceMilestoneBlockingReasons =
        goldenWalnutSourceMilestone.BlockingReasons,
    GoldenWalnutSourceScheduleVerified = goldenWalnutSourceScheduleVerified,
    GoldenWalnutSourceSchedulePath = goldenWalnutSourceSchedulePath,
    GoldenWalnutGourmandContinuationRequired =
        options.RequireGourmandContinuationReady,
    GoldenWalnutGourmandContinuationReady =
        gourmandContinuationMilestone.Ready,
    GoldenWalnutGourmandRequestsFulfilled =
        gourmandContinuationMilestone.RequestsFulfilled,
    GoldenWalnutGourmandCollectedQuantity =
        gourmandContinuationMilestone.CollectedGourmandWalnutQuantity,
    GoldenWalnutGourmandBlockingReasons =
        gourmandContinuationMilestone.BlockingReasons,
    GoldenWalnutGourmandContinuityVerified =
        goldenWalnutGourmandContinuityVerified,
    GoldenWalnutGourmandVerifiedTransitionCount =
        goldenWalnutGourmandVerifiedTransitionCount,
    GoldenWalnutGourmandContinuityReceiptPath =
        goldenWalnutGourmandContinuityReceiptPath,
    GoldenWalnutGourmandContinuityBlockingReasons =
        goldenWalnutGourmandContinuityBlockingReasons,
    PlanningHandoffCaptureManifestPath =
        lastPlanningHandoffCaptureManifestPath,
    PlanningHandoffTeacherDispatchManifestPath =
        lastPlanningHandoffTeacherDispatchManifestPath,
    PlanningHandoffNativeRunPlanPath =
        lastPlanningHandoffNativeRunPlanPath,
    CropTeacherFirstDayCompleted = cropTeacherFirstDayCompleted,
    CropTeacherRunCompleted = cropTeacherRunCompleted,
    CropTeacherReplanRequired = cropTeacherReplanRequired,
    CropTeacherReplanReason = cropTeacherReplanReason,
    CropTeacherReplanCount = cropTeacherReplanCount,
    CropTeacherTransitionLedgerPath = cropTeacherTransitionLedgerPath,
    CropTeacherHarvestReceiptCount = cropTeacherHarvestReceiptCount,
    CropTeacherDayBoundaryReceiptCount =
        cropTeacherDayBoundaryReceiptCount,
    StageOneCollectionTeacherQueueCount =
        stageOneCollectionTeacherQueueCount,
    StageOneCollectionControlQueueCount =
        stageOneCollectionControlQueueCount,
    CropTeacherControlAttemptsStarted = cropTeacherControlAttemptsStarted,
    Iterations = attemptsStarted,
    MaxAttempts = options.MaxAttempts,
    AttemptsStarted = attemptsStarted,
    RowsAppended = rowsAppended,
    VerifiedActions = verifiedActions,
    RequiredVerifiedActions = options.RequiredVerifiedActions,
    PrimaryAttemptsStarted = primaryAttemptsStarted,
    NativeSaveBoundaryRequired = options.RequireNativeSaveBoundary,
    NativeSaveBoundaryAttemptsStarted = nativeSaveBoundaryAttemptsStarted,
    NativeSaveBoundaryVerified = nativeSaveBoundaryVerified,
    TrainingDataTransactionStatus = trainingDataTransaction.Status,
    TrainingDataTransactionPath = trainingDataTransaction.StagingRoot,
    CanonicalTrainingArtifactsUpdated =
        trainingDataTransaction.CanonicalArtifactsUpdated,
    NativeSaveBoundaryInitialDayKey = nativeSaveBoundaryInitialDayKey,
    NativeSaveBoundaryCurrentDayKey = nativeSaveBoundaryCurrentDayKey,
    NativeSaveBoundaryInitialSaveSha256 =
        initialSaveFingerprint?.Sha256 ?? string.Empty,
    NativeSaveBoundaryCurrentSaveSha256 =
        currentSaveFingerprint?.Sha256 ?? string.Empty,
    StopReason = stopReason,
    ObjectiveCompleted = objectiveCompleted,
    ActiveObjectiveContinuation = activeObjectiveContinuation,
    SocialObjectiveCompleted = socialObjectiveCompleted,
    ActiveSocialContinuation = string.Equals(ReadString(activeObjectiveContinuation, "kind"), "social", StringComparison.Ordinal)
        ? activeObjectiveContinuation
        : null,
    LastStateHash = lastStateHash,
    LastQueueId = lastQueueId,
    Concurrency = 1,
    Execution = options.TargetExecutionMode + ":" + options.FeedbackMode,
    ExecutorFeedbackRequired = options.RequireExecutorFeedback,
    TrainingReport = lastTrainingReport,
    Prediction = lastPrediction
};

var reportPath = Path.Combine(options.RunDir, "live-training-loop-report.json");
await File.WriteAllTextAsync(reportPath, JsonSerializer.Serialize(report, JsonOptions), Encoding.UTF8);
AppendProgress(options, verifiedTargetMet ? "complete" : "incomplete", attemptsStarted, lastStateHash, lastQueueId, "report=" + reportPath + " verified_actions=" + verifiedActions + " required_verified_actions=" + options.RequiredVerifiedActions + " max_attempts=" + options.MaxAttempts);

Console.WriteLine(JsonSerializer.Serialize(new
{
    status = loopStatus,
    run_id = options.RunId,
    iterations = attemptsStarted,
    max_attempts = options.MaxAttempts,
    attempts_started = attemptsStarted,
    rows_appended = rowsAppended,
    verified_actions = verifiedActions,
    required_verified_actions = options.RequiredVerifiedActions,
    primary_attempts_started = primaryAttemptsStarted,
    native_save_boundary_required = options.RequireNativeSaveBoundary,
    native_save_boundary_attempts_started = nativeSaveBoundaryAttemptsStarted,
    native_save_boundary_verified = nativeSaveBoundaryVerified,
    stop_reason = stopReason,
    dataset_path = options.DatasetPath,
    report_path = reportPath,
    golden_walnut_fresh_save_episode_manifest_path =
        goldenWalnutFreshSaveEpisodeManifestPath,
    golden_walnut_fresh_save_episode_count =
        goldenWalnutFreshSaveEpisodeCount,
    golden_walnut_source_schedule_required =
        options.RequireGoldenWalnutSourceScheduleReady,
    golden_walnut_source_milestone_ready =
        goldenWalnutSourceMilestone.Ready,
    golden_walnut_source_current_total_day =
        goldenWalnutSourceMilestone.CurrentTotalDay,
    golden_walnut_deadline_total_day_exclusive =
        GoldenWalnutUnlockScheduleProofBuilder.DeadlineTotalDayExclusive,
    golden_walnut_source_milestone_blocking_reasons =
        goldenWalnutSourceMilestone.BlockingReasons,
    golden_walnut_source_schedule_verified =
        goldenWalnutSourceScheduleVerified,
    golden_walnut_source_schedule_path = goldenWalnutSourceSchedulePath,
    golden_walnut_gourmand_continuation_required =
        options.RequireGourmandContinuationReady,
    golden_walnut_gourmand_continuation_ready =
        gourmandContinuationMilestone.Ready,
    golden_walnut_gourmand_requests_fulfilled =
        gourmandContinuationMilestone.RequestsFulfilled,
    golden_walnut_gourmand_collected_quantity =
        gourmandContinuationMilestone.CollectedGourmandWalnutQuantity,
    golden_walnut_gourmand_blocking_reasons =
        gourmandContinuationMilestone.BlockingReasons,
    golden_walnut_gourmand_continuity_verified =
        goldenWalnutGourmandContinuityVerified,
    golden_walnut_gourmand_verified_transition_count =
        goldenWalnutGourmandVerifiedTransitionCount,
    golden_walnut_gourmand_continuity_receipt_path =
        goldenWalnutGourmandContinuityReceiptPath,
    golden_walnut_gourmand_continuity_blocking_reasons =
        goldenWalnutGourmandContinuityBlockingReasons,
    progress_log_path = options.ProgressLogPath,
    concurrency = 1,
    execution = options.TargetExecutionMode,
    feedback = options.FeedbackMode,
    executor_feedback_required = options.RequireExecutorFeedback,
    objective_completed = objectiveCompleted,
    social_objective_completed = socialObjectiveCompleted,
    crop_teacher_first_day_completed = cropTeacherFirstDayCompleted,
    crop_teacher_run_completed = cropTeacherRunCompleted,
    crop_teacher_replan_required = cropTeacherReplanRequired,
    crop_teacher_replan_reason = cropTeacherReplanReason,
    crop_teacher_replan_count = cropTeacherReplanCount,
    crop_teacher_transition_ledger_path = cropTeacherTransitionLedgerPath,
    crop_teacher_harvest_receipt_count = cropTeacherHarvestReceiptCount,
    crop_teacher_day_boundary_receipt_count =
        cropTeacherDayBoundaryReceiptCount,
    crop_teacher_control_attempts_started = cropTeacherControlAttemptsStarted
}, JsonOptions));

static partial class Program
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly JsonSerializerOptions JsonlOptions = new(JsonSerializerDefaults.Web);

    private static async Task DelayBeforeNextAttemptAsync(
        LiveTrainingOptions options,
        int attemptOrdinal,
        int minimumDelayMs = 0)
    {
        var delayMs = Math.Max(options.SleepMs, minimumDelayMs);
        if (delayMs > 0)
        {
            await Task.Delay(delayMs);
        }
    }

    private static string NoProgressDetail(
        NoProgressBackoffDecision decision)
    {
        return decision.NoProgress
            ? " no_progress_streak=" + decision.Streak +
                " retry_delay_ms=" + decision.DelayMs
            : string.Empty;
    }

    private static string SanitizeProgressValue(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }
}
