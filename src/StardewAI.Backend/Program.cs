using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Audit;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Events;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Goals;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.Previews;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Goals;
using StardewAI.Core.MockModel;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.PreviewCompiler;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

const long MaxTransparentSnapshotRequestBodyBytes =
    128L * 1024 * 1024;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize =
        MaxTransparentSnapshotRequestBodyBytes;
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddSingleton<StateStore>();
builder.Services.AddSingleton<PlanningPreviewCompiler>();
builder.Services.AddSingleton<ActionQueueCompiler>();
builder.Services.AddSingleton<TimeBudgetValidator>();
builder.Services.AddSingleton<IExecutorPort, DryRunExecutorPort>();
builder.Services.AddSingleton<TrainingSandboxExecutorPort>();
builder.Services.AddSingleton<TrainingStateTransitionSimulator>();
builder.Services.AddSingleton<MockSmallModelPolicy>();
builder.Services.AddSingleton<WorldModelProjector>();
builder.Services.AddSingleton<GrandpaEvaluationGoalEvaluator>();
builder.Services.AddSingleton<GrandpaTrainingSampleAdapter>();
builder.Services.AddSingleton<GrandpaStrategyFeatureRowBuilder>();
builder.Services.AddSingleton<TrainingEpisodeRewardCalculator>();
builder.Services.AddSingleton<TrainingEpisodeAdapter>();
builder.Services.AddSingleton<TrainingFeatureRowExporter>();
builder.Services.AddSingleton<JsonlTrainingDatasetWriter>();
builder.Services.AddSingleton<BaselineFeatureRowTrainer>();
builder.Services.AddSingleton<BaselinePolicyPredictor>();
builder.Services.AddSingleton<BaselineOptionRanker>();
builder.Services.AddSingleton<EventCandidateRanker>();
builder.Services.AddSingleton<DailyPlanCompiler>();
builder.Services.AddSingleton<StardewTrainingSessionLauncher>();
builder.Services.AddSingleton<TrainingReadyProbe>();
builder.Services.AddSingleton<CandidateOptionAvailabilityEvaluator>();
builder.Services.AddSingleton<GrandpaDirectionDailyCandidateBinding>();
builder.Services.AddSingleton<GrandpaDailySubgoalResolver>();
builder.Services.AddSingleton<IStrategyCommitmentRepository, FileStrategyCommitmentRepository>();
builder.Services.AddSingleton<ActionQueueDispatchReadinessService>();

var app = builder.Build();

app.MapGet("/health", () => new
{
    status = "ok",
    service = "stardewai-backend",
    phase = "Phase 1A",
    transport = "aspnet-minimal-api"
});

app.MapPost("/api/v1/snapshots", async (HttpRequest request, StateStore store) =>
{
    store.PrepareForSnapshotIngest();
    var profile = request.Query["profile"].ToString();
    var (errors, snapshot) = await SnapshotValidator.ValidateAsync(
        request.Body,
        request.HttpContext.RequestAborted,
        profile);
    if (errors.Count > 0)
    {
        return Results.UnprocessableEntity(new { message = "snapshot validation failed", errors });
    }

    var acceptedSnapshot = snapshot!;
    store.StoreSnapshot(acceptedSnapshot);
    store.AppendAudit("SnapshotIngested", acceptedSnapshot.GameTick, acceptedSnapshot.StateHash);
    return Results.Ok(new { accepted = true, state_hash = acceptedSnapshot.StateHash });
});

app.MapGet("/api/v1/snapshots/latest", (StateStore store) =>
{
    var latest = store.LatestSnapshot();
    return latest is null ? Results.NotFound(new { detail = "no snapshots ingested" }) : Results.Ok(latest);
});

app.MapGet("/api/v1/strategy/commitments/latest", (string? stateHash, StateStore store, IStrategyCommitmentRepository repository) =>
{
    if (!string.IsNullOrWhiteSpace(stateHash) && !store.Snapshots.TryGetValue(stateHash, out _))
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }
    var snapshot = !string.IsNullOrWhiteSpace(stateHash)
        ? store.Snapshots[stateHash]
        : store.LatestSnapshot();
    return snapshot is null
        ? Results.NotFound(new { detail = "no matching snapshot available" })
        : Results.Ok(repository.Get(snapshot));
});

app.MapPost("/api/v1/strategy/commitments/crops/upsert", (CropPlantingCommitmentUpsertRequest request, StateStore store, IStrategyCommitmentRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.StateHash) || !store.Snapshots.TryGetValue(request.StateHash, out var snapshot))
    {
        return Results.UnprocessableEntity(new { detail = "state_hash does not match an ingested snapshot" });
    }
    var result = repository.Upsert(snapshot, request);
    if (!result.Accepted)
    {
        return result.Errors.Contains("ledger_revision_conflict", StringComparer.Ordinal)
            ? Results.Conflict(result)
            : Results.UnprocessableEntity(result);
    }
    store.AppendAudit("CropStrategyCommitmentUpserted", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/strategy/commitments/crops/{commitmentId}/cancel", (string commitmentId, StrategyCommitmentCancelRequest request, StateStore store, IStrategyCommitmentRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.StateHash) || !store.Snapshots.TryGetValue(request.StateHash, out var snapshot))
    {
        return Results.UnprocessableEntity(new { detail = "state_hash does not match an ingested snapshot" });
    }
    var result = repository.Cancel(snapshot, commitmentId, request);
    if (!result.Accepted)
    {
        return result.Errors.Contains("ledger_revision_conflict", StringComparer.Ordinal)
            ? Results.Conflict(result)
            : Results.UnprocessableEntity(result);
    }
    store.AppendAudit("CropStrategyCommitmentCancelled", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/strategy/commitments/materials/upsert", (MaterialReservationUpsertRequest request, StateStore store, IStrategyCommitmentRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.StateHash) || !store.Snapshots.TryGetValue(request.StateHash, out var snapshot))
    {
        return Results.UnprocessableEntity(new { detail = "state_hash does not match an ingested snapshot" });
    }
    var result = repository.UpsertMaterial(snapshot, request);
    if (!result.Accepted)
    {
        return result.Errors.Contains("ledger_revision_conflict", StringComparer.Ordinal)
            ? Results.Conflict(result)
            : Results.UnprocessableEntity(result);
    }
    store.AppendAudit("MaterialStrategyReservationUpserted", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/strategy/commitments/materials/{reservationId}/cancel", (string reservationId, StrategyCommitmentCancelRequest request, StateStore store, IStrategyCommitmentRepository repository) =>
{
    if (string.IsNullOrWhiteSpace(request.StateHash) || !store.Snapshots.TryGetValue(request.StateHash, out var snapshot))
    {
        return Results.UnprocessableEntity(new { detail = "state_hash does not match an ingested snapshot" });
    }
    var result = repository.CancelMaterial(snapshot, reservationId, request);
    if (!result.Accepted)
    {
        return result.Errors.Contains("ledger_revision_conflict", StringComparer.Ordinal)
            ? Results.Conflict(result)
            : Results.UnprocessableEntity(result);
    }
    store.AppendAudit("MaterialStrategyReservationCancelled", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/events", async (HttpRequest request, StateStore store) =>
{
    using var reader = new StreamReader(request.Body);
    var rawPayload = await reader.ReadToEndAsync();
    var errors = EventValidator.ValidateRaw(rawPayload, store, out var gameEvent);
    if (errors.Count > 0)
    {
        return Results.UnprocessableEntity(new { message = "event validation failed", errors });
    }

    var acceptedEvent = gameEvent!;
    store.Events.Add(acceptedEvent);
    store.RawPayloads[acceptedEvent.EventId] = new RawIngestRecord("event", acceptedEvent.EventId, DateTimeOffset.UtcNow.ToString("O"), rawPayload);
    store.AppendAudit(
        "EventIngested",
        acceptedEvent.GameTick,
        acceptedEvent.PublishedSnapshotHash ?? acceptedEvent.ObservedSnapshotHash);
    return Results.Ok(new { accepted = true, count = store.Events.Count });
});

app.MapGet("/api/v1/events", (long? afterTick, int? limit, StateStore store) =>
{
    var selected = store.Events.AsEnumerable();
    if (afterTick.HasValue)
    {
        selected = selected.Where(item => item.GameTick > afterTick.Value);
    }

    return Results.Ok(selected.TakeLast(limit.GetValueOrDefault(100)).ToArray());
});

app.MapPost("/api/v1/capabilities", (CapabilityManifest manifest, StateStore store) =>
{
    var errors = CapabilityValidator.Validate(manifest);
    if (errors.Count > 0)
    {
        return Results.UnprocessableEntity(new { message = "capability validation failed", errors });
    }

    store.CapabilityManifest = manifest;
    foreach (var capability in manifest.Capabilities)
    {
        store.Capabilities[capability.CapabilityId] = capability;
    }

    store.AppendAudit("CapabilityIngested", store.LatestSnapshot()?.GameTick ?? 0, store.LatestSnapshot()?.StateHash ?? string.Empty);
    return Results.Ok(new
    {
        accepted = true,
        count = manifest.Capabilities.Length,
        capability_ids = manifest.Capabilities.Select(item => item.CapabilityId).ToArray()
    });
});

app.MapGet("/api/v1/capabilities", (StateStore store) => Results.Ok(store.CapabilityManifest));

app.MapGet("/api/v1/audit", (int? limit, StateStore store) =>
    Results.Ok(store.Audit.TakeLast(limit.GetValueOrDefault(100)).ToArray()));

app.MapGet("/api/v1/sync", (long? afterTick, StateStore store) =>
{
    var events = store.Events.AsEnumerable();
    if (afterTick.HasValue)
    {
        events = events.Where(item => item.GameTick > afterTick.Value);
    }

    return Results.Ok(new
    {
        latest_snapshot = store.LatestSnapshot(),
        snapshot_count = store.Snapshots.Count,
        event_count = store.Events.Count,
        capability_count = store.Capabilities.Count,
        events = events.ToArray(),
        capabilities = store.CapabilityManifest,
        audit_head = store.Audit.TakeLast(10).ToArray()
    });
});

app.MapGet("/api/v1/stardew/input/latest", (string? goal, string? mode, StateStore store, WorldModelProjector projector) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.NotFound(new { detail = "no snapshots ingested" });
    }

    return Results.Ok(projector.Project(latest, goal ?? string.Empty, mode ?? "relaxed"));
});

app.MapGet("/api/v1/goals/grandpa-evaluation/latest", (StateStore store, WorldModelProjector projector, GrandpaEvaluationGoalEvaluator evaluator) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.NotFound(new { detail = "no snapshots ingested" });
    }

    var model = projector.Project(latest, GrandpaEvaluationGoalDefinition.StrategicGoal, "strategic");
    return Results.Ok(evaluator.Evaluate(model));
});

app.MapGet("/api/v1/training/grandpa-evaluation/latest", (StateStore store, WorldModelProjector projector, GrandpaEvaluationGoalEvaluator evaluator, GrandpaTrainingSampleAdapter adapter) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.NotFound(new { detail = "no snapshots ingested" });
    }

    var model = projector.Project(latest, GrandpaEvaluationGoalDefinition.StrategicGoal, "strategic");
    var report = evaluator.Evaluate(model);
    return Results.Ok(adapter.Build(model, report));
});

app.MapGet("/api/v1/training/grandpa-evaluation/latest/feature-rows", (int? maxRows, StateStore store, WorldModelProjector projector, GrandpaEvaluationGoalEvaluator evaluator, GrandpaTrainingSampleAdapter adapter, GrandpaStrategyFeatureRowBuilder builder) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.NotFound(new { detail = "no snapshots ingested" });
    }

    var model = projector.Project(latest, GrandpaEvaluationGoalDefinition.StrategicGoal, "strategic");
    var report = evaluator.Evaluate(model);
    var sample = adapter.Build(model, report);
    return Results.Ok(builder.Build(model, sample, maxRows.GetValueOrDefault(5)));
});

app.MapPost("/api/v1/training/grandpa-evaluation/latest/feature-rows/append", (TrainingDatasetRequest? request, int? maxRows, StateStore store, WorldModelProjector projector, GrandpaEvaluationGoalEvaluator evaluator, GrandpaTrainingSampleAdapter adapter, GrandpaStrategyFeatureRowBuilder builder, JsonlTrainingDatasetWriter writer) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.NotFound(new { detail = "no snapshots ingested" });
    }

    var model = projector.Project(latest, GrandpaEvaluationGoalDefinition.StrategicGoal, "strategic");
    var report = evaluator.Evaluate(model);
    var sample = adapter.Build(model, report);
    var rows = builder.Build(model, sample, maxRows.GetValueOrDefault(5));
    var datasetPath = DatasetPathResolver.Resolve(request?.DatasetPath);
    return Results.Ok(writer.AppendMany(datasetPath, rows));
});

app.MapPost("/api/v1/action-compiler/compile", (CompileRequest request, StateStore store, PlanningPreviewCompiler compiler) =>
{
    var snapshot = !string.IsNullOrWhiteSpace(request.StateHash) && store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();

    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    CommandPreview preview = compiler.Compile(snapshot, request.Goal ?? string.Empty, request.Mode ?? "relaxed");
    store.AppendAudit("CommandPreviewGenerated", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(preview);
});

app.MapPost("/api/v1/small-model/action-queue/compile", (SmallModelActionEnvelope request, StateStore store, ActionQueueCompiler compiler, IStrategyCommitmentRepository commitmentRepository) =>
{
    var snapshot = !string.IsNullOrWhiteSpace(request.StateHash) && store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();

    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var queue = compiler.Compile(request, snapshot, commitmentRepository.Get(snapshot));
    store.ActionQueues[queue.QueueId] = queue;
    store.AppendAudit("ActionQueueCompiled", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(queue);
});

app.MapPost("/api/v1/small-model/plan/action-queue/compile", (SmallModelPlanEnvelope request, StateStore store, ActionQueueCompiler compiler, IStrategyCommitmentRepository commitmentRepository) =>
{
    var snapshot = !string.IsNullOrWhiteSpace(request.StateHash) && store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();

    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var queue = compiler.Compile(request, snapshot, commitmentRepository.Get(snapshot));
    store.ActionQueues[queue.QueueId] = queue;
    store.AppendAudit("PlanActionQueueCompiled", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(queue);
});

app.MapPost("/api/v1/mock-model/small-model-action", (MockModelActionRequest request, StateStore store, MockSmallModelPolicy policy) =>
{
    var snapshot = !string.IsNullOrWhiteSpace(request.StateHash) && store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();

    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var output = policy.Generate(snapshot, request.Goal ?? string.Empty, request.ExecutionMode ?? "training_singleplayer");
    store.AppendAudit("MockSmallModelActionGenerated", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(output);
});

app.MapPost("/api/v1/planner/options/availability", (OptionAvailabilityRequest request, StateStore store, CandidateOptionAvailabilityEvaluator evaluator, IStrategyCommitmentRepository commitmentRepository) =>
{
    var snapshot = !string.IsNullOrWhiteSpace(request.StateHash) && store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();

    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var availability = request.Candidates.Length > 0
        ? evaluator.Evaluate(snapshot, request.Candidates, request.IncludeExecutorCalibrationOptions, commitmentRepository.Get(snapshot))
        : evaluator.Evaluate(snapshot, request.CandidateOptionIds, request.IncludeExecutorCalibrationOptions, commitmentRepository.Get(snapshot));
    store.AppendAudit("OptionAvailabilityEvaluated", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(availability);
});

app.MapGet("/api/v1/action-queues/{queueId}", (string queueId, StateStore store) =>
    store.ActionQueues.TryGetValue(queueId, out var queue)
        ? Results.Ok(queue)
        : Results.NotFound(new { detail = "queue not found" }));

app.MapGet("/api/v1/action-queues/{queueId}/items/{queueItemId}/dispatch-readiness", (
    string queueId,
    string queueItemId,
    string? stateHash,
    StateStore store,
    IStrategyCommitmentRepository repository,
    ActionQueueDispatchReadinessService readinessService) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }
    var item = queue.Items.SingleOrDefault(row =>
        string.Equals(row.QueueItemId, queueItemId, StringComparison.Ordinal));
    if (item is null)
    {
        return Results.NotFound(new { detail = "queue item not found" });
    }
    if (string.IsNullOrWhiteSpace(stateHash) ||
        !store.Snapshots.TryGetValue(stateHash, out var snapshot))
    {
        return Results.UnprocessableEntity(new
        {
            detail = "state_hash does not match an ingested snapshot"
        });
    }

    var result = readinessService.Evaluate(
        queue,
        item,
        repository.Get(snapshot),
        snapshot.StateHash);
    store.AppendAudit(
        result.Ready ? "ActionQueueDispatchReady" : "ActionQueueDispatchBlocked",
        snapshot.GameTick,
        snapshot.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/action-queues/{queueId}/execute", (string queueId, StateStore store, IExecutorPort executor) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }

    var result = executor.Execute(queue);
    store.ExecutionResults[result.QueueId] = result;
    store.AppendAudit("ActionQueueExecutionAttempted", store.LatestSnapshot()?.GameTick ?? 0, queue.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/action-queues/{queueId}/execute-training-sandbox", (string queueId, StateStore store, TrainingSandboxExecutorPort executor) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }

    var result = executor.Execute(queue);
    store.ExecutionResults[result.QueueId] = result;
    store.AppendAudit("TrainingSandboxExecutionAttempted", store.LatestSnapshot()?.GameTick ?? 0, queue.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/action-queues/{queueId}/simulate-training-transition", (string queueId, StateStore store, WorldModelProjector projector, TrainingStateTransitionSimulator simulator) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }

    if (!store.Snapshots.TryGetValue(queue.StateHash, out var snapshot))
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var model = projector.Project(snapshot, queue.GoalId, "training");
    var result = simulator.Simulate(model, queue);
    store.AppendAudit("TrainingTransitionSimulated", snapshot.GameTick, queue.StateHash);
    return Results.Ok(result);
});

app.MapGet("/api/v1/action-queues/{queueId}/time-budget", (string queueId, StateStore store, WorldModelProjector projector, TimeBudgetValidator validator) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }

    if (!store.Snapshots.TryGetValue(queue.StateHash, out var snapshot))
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var model = projector.Project(snapshot, queue.GoalId, "training");
    return Results.Ok(validator.Validate(model, queue));
});

app.MapGet("/api/v1/action-queues/{queueId}/training-episode", (string queueId, StateStore store, WorldModelProjector projector, TimeBudgetValidator timeBudgetValidator, TrainingStateTransitionSimulator simulator, TrainingEpisodeAdapter adapter) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }

    if (!store.Snapshots.TryGetValue(queue.StateHash, out var snapshot))
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var model = projector.Project(snapshot, queue.GoalId, "training");
    var timeBudget = timeBudgetValidator.Validate(model, queue);
    var transition = simulator.Simulate(model, queue);
    return Results.Ok(adapter.Build(queue, timeBudget, transition));
});

app.MapGet("/api/v1/action-queues/{queueId}/training-feature-row", (string queueId, StateStore store, WorldModelProjector projector, TimeBudgetValidator timeBudgetValidator, TrainingStateTransitionSimulator simulator, TrainingEpisodeAdapter adapter, TrainingFeatureRowExporter exporter) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }

    if (!store.Snapshots.TryGetValue(queue.StateHash, out var snapshot))
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var model = projector.Project(snapshot, queue.GoalId, "training");
    var timeBudget = timeBudgetValidator.Validate(model, queue);
    var transition = simulator.Simulate(model, queue);
    var episode = adapter.Build(queue, timeBudget, transition);
    return Results.Ok(exporter.Build(model, episode));
});

app.MapPost("/api/v1/action-queues/{queueId}/training-feature-row/append", (string queueId, TrainingDatasetRequest? request, StateStore store, WorldModelProjector projector, TimeBudgetValidator timeBudgetValidator, TrainingStateTransitionSimulator simulator, TrainingEpisodeAdapter adapter, TrainingFeatureRowExporter exporter, JsonlTrainingDatasetWriter writer) =>
{
    if (!store.ActionQueues.TryGetValue(queueId, out var queue))
    {
        return Results.NotFound(new { detail = "queue not found" });
    }

    if (!store.Snapshots.TryGetValue(queue.StateHash, out var snapshot))
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var model = projector.Project(snapshot, queue.GoalId, "training");
    var timeBudget = timeBudgetValidator.Validate(model, queue);
    var transition = simulator.Simulate(model, queue);
    var episode = adapter.Build(queue, timeBudget, transition);
    var row = exporter.Build(model, episode);
    var datasetPath = DatasetPathResolver.Resolve(request?.DatasetPath);
    var result = writer.Append(datasetPath, row);
    store.AppendAudit("TrainingFeatureRowAppended", snapshot.GameTick, queue.StateHash);
    return Results.Ok(result);
});

app.MapPost("/api/v1/training/baseline/train", (TrainingDatasetRequest? request, BaselineFeatureRowTrainer trainer) =>
{
    var datasetPath = DatasetPathResolver.Resolve(request?.DatasetPath);
    return Results.Ok(trainer.Train(datasetPath));
});

app.MapPost("/api/v1/training/session/prepare", (TrainingLaunchRequest request, StardewTrainingSessionLauncher launcher) =>
    Results.Ok(launcher.Prepare(request)));

app.MapPost("/api/v1/training/session/launch", (TrainingLaunchRequest request, StardewTrainingSessionLauncher launcher) =>
    Results.Ok(launcher.Launch(request)));

app.MapGet("/api/v1/training/session/ready-probe", (HttpRequest request, StateStore store, TrainingReadyProbe probe) =>
{
    var manifestPath = request.Query.TryGetValue("manifest_path", out var value) ? value.ToString() : null;
    return Results.Ok(probe.Check(store.LatestSnapshot(), store.LatestSnapshot() is not null, manifestPath));
});

app.MapPost("/api/v1/training/baseline/predict", (BaselinePredictionRequest request, BaselineFeatureRowTrainer trainer, BaselinePolicyPredictor predictor) =>
{
    var report = request.TrainingReport;
    if (report is null)
    {
        var datasetPath = DatasetPathResolver.Resolve(request.DatasetPath);
        report = trainer.Train(datasetPath);
    }

    return Results.Ok(predictor.Predict(report, request.CandidateOptionIds));
});

app.MapPost("/api/v1/planner/baseline/rank-options", (BaselinePredictionRequest request, StateStore store, BaselineFeatureRowTrainer trainer, BaselineOptionRanker ranker, EventCandidateRanker eventCandidateRanker, CandidateOptionAvailabilityEvaluator availabilityEvaluator, GrandpaDailySubgoalResolver goalResolver, IStrategyCommitmentRepository commitmentRepository) =>
{
    var report = request.TrainingReport;
    if (report is null)
    {
        var datasetPath = DatasetPathResolver.Resolve(request.DatasetPath);
        report = trainer.Train(datasetPath);
    }

    if (string.IsNullOrWhiteSpace(request.StateHash))
    {
        return Results.Ok(ranker.Rank(report, request.CandidateOptionIds));
    }

    var snapshot = store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();
    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var availability = request.Candidates.Length > 0
        ? availabilityEvaluator.Evaluate(snapshot, request.Candidates, commitmentLedger: commitmentRepository.Get(snapshot))
        : availabilityEvaluator.Evaluate(snapshot, request.CandidateOptionIds, commitmentLedger: commitmentRepository.Get(snapshot));
    var rankedCandidates = request.IncludeBlockedOptions
        ? availability.Options.Select(option => option.OptionId).ToArray()
        : availability.Options.Where(option => option.Available).Select(option => option.OptionId).ToArray();

    var broadRankedEventCandidates = eventCandidateRanker.Rank(
        report,
        availability,
        request.GoalId);
    var detailedGoalResolution = goalResolver.ResolveWithBinding(
        snapshot,
        request.GoalId,
        broadRankedEventCandidates);
    var goalResolution = detailedGoalResolution.GoalResolution;
    var effectiveGoalId = string.IsNullOrWhiteSpace(
        goalResolution.EffectiveGoalId)
            ? request.GoalId
            : goalResolution.EffectiveGoalId;
    var rankedEventCandidates = string.Equals(
        effectiveGoalId,
        request.GoalId,
        StringComparison.Ordinal)
            ? broadRankedEventCandidates
            : eventCandidateRanker.Rank(
                report,
                availability,
                effectiveGoalId);
    rankedEventCandidates = goalResolver.ApplyBindingProvenance(
        rankedEventCandidates,
        detailedGoalResolution);

    return Results.Ok(new AvailabilityAwarePolicyPredictionEnvelope
    {
        Prediction = rankedCandidates.Length == 0
            ? new PolicyPredictionEnvelope()
            : ranker.Rank(report, rankedCandidates),
        Availability = availability,
        RankedEventCandidates = rankedEventCandidates,
        GoalResolution = goalResolution
    });
});

app.MapPost("/api/v1/planner/daily-plan/compile", (DailyPlanCompileRequest request, StateStore store, DailyPlanCompiler dailyPlanCompiler, ActionQueueCompiler actionQueueCompiler, IStrategyCommitmentRepository commitmentRepository) =>
{
    var snapshot = !string.IsNullOrWhiteSpace(request.StateHash) && store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();
    var plan = dailyPlanCompiler.Compile(
        request.RankedEventCandidates,
        request.StateHash,
        string.IsNullOrWhiteSpace(request.GoalId) ? "daily.closed_loop" : request.GoalId,
        string.IsNullOrWhiteSpace(request.ExecutionMode) ? "training_singleplayer" : request.ExecutionMode,
        request.MaxCandidates <= 0 ? 4 : request.MaxCandidates,
        DailyPlanBudgetReader.AvailablePlanMinutes(snapshot),
        DailyPlanBudgetReader.ReadSnapshotInt(snapshot, "player", "energy"));

    if (!request.CompileActionQueue)
    {
        return Results.Ok(new
        {
            schema_version = "daily_plan_compile_response.v1",
            plan,
            action_queue = (ActionQueueEnvelope?)null
        });
    }

    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var relocationBinding =
        MachineRelocationIntentPlanBinder.Bind(
            plan,
            snapshot,
            commitmentRepository);
    if (relocationBinding is not null &&
        (!relocationBinding.Accepted ||
         relocationBinding.Ledger is null))
    {
        return Results.UnprocessableEntity(new
        {
            detail = "machine relocation intent binding rejected",
            errors = relocationBinding.Errors
        });
    }
    var commitmentLedger = relocationBinding?.Ledger ??
        commitmentRepository.Get(snapshot);
    var supportBinding =
        MachineSupportIntentPlanBinder.Bind(
            plan,
            snapshot,
            commitmentRepository);
    if (supportBinding is not null &&
        (!supportBinding.Accepted ||
         supportBinding.Ledger is null))
    {
        return Results.UnprocessableEntity(new
        {
            detail = "machine support intent binding rejected",
            errors = supportBinding.Errors
        });
    }
    commitmentLedger = supportBinding?.Ledger ??
        commitmentLedger;
    var actionQueue = actionQueueCompiler.Compile(
        plan,
        snapshot,
        commitmentLedger);
    store.ActionQueues[actionQueue.QueueId] = actionQueue;
    store.AppendAudit("DailyPlanActionQueueCompiled", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(new
    {
        schema_version = "daily_plan_compile_response.v1",
        plan,
        action_queue = actionQueue
    });
});

app.MapPost("/api/v1/planner/grandpa-direction-binding/bind", (GrandpaDirectionBindingRequest request, StateStore store, GrandpaDirectionDailyCandidateBinding binding) =>
{
    if (string.IsNullOrWhiteSpace(request.StateHash))
    {
        return Results.UnprocessableEntity(new { detail = "state_hash is required" });
    }

    if (string.IsNullOrWhiteSpace(request.DirectionId))
    {
        return Results.UnprocessableEntity(new { detail = "direction_id is required" });
    }

    if (!store.Snapshots.TryGetValue(request.StateHash, out var snapshot) || snapshot is null)
    {
        return Results.UnprocessableEntity(new { detail = "state_hash does not match an ingested snapshot" });
    }

    var result = binding.Bind(request, snapshot);
    store.AppendAudit("GrandpaDirectionBound", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(result);
});

app.MapGet("/api/v1/action-compiler/check", (StateStore store, PlanningPreviewCompiler compiler) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.Ok(new { status = "blocked", reason = "no_snapshots_ingested", compiler_loaded = true });
    }

    var preview = compiler.Compile(latest, "stabilize current day", "recovery");
    return Results.Ok(new
    {
        status = "ok",
        compiler_loaded = true,
        feasibility = preview.Feasibility,
        execution_permission = preview.ExecutionPermission,
        preview_only = preview.PreviewOnly,
        state_hash = latest.StateHash
    });
});

app.Run();

public sealed class CompileRequest
{
    [JsonPropertyName("goal")]
    public string? Goal { get; set; }

    [JsonPropertyName("mode")]
    public string? Mode { get; set; }

    [JsonPropertyName("state_hash")]
    public string? StateHash { get; set; }
}

public sealed class MockModelActionRequest
{
    [JsonPropertyName("goal")]
    public string? Goal { get; set; }

    [JsonPropertyName("state_hash")]
    public string? StateHash { get; set; }

    [JsonPropertyName("execution_mode")]
    public string? ExecutionMode { get; set; }
}

public sealed class TrainingDatasetRequest
{
    [JsonPropertyName("dataset_path")]
    public string? DatasetPath { get; set; }
}

public static class DatasetPathResolver
{
    private const string DefaultIsolatedDatasetPath = @"E:\StardewAITraining\datasets\training-feature-rows.jsonl";

    public static string Resolve(string? requestedPath)
    {
        if (!string.IsNullOrWhiteSpace(requestedPath))
        {
            return Path.GetFullPath(requestedPath);
        }

        if (Directory.Exists(Path.GetPathRoot(DefaultIsolatedDatasetPath)))
        {
            return DefaultIsolatedDatasetPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "datasets", "training-feature-rows.jsonl");
    }
}

public sealed class StateStore
{
    private const int MaxRetainedSnapshots = 2;
    private readonly object snapshotGate = new();
    private readonly Queue<string> snapshotOrder = new();

    public Dictionary<string, SnapshotEnvelope> Snapshots { get; } = new Dictionary<string, SnapshotEnvelope>();
    public List<GameEvent> Events { get; } = new List<GameEvent>();
    public Dictionary<string, Capability> Capabilities { get; } = new Dictionary<string, Capability>();
    public CapabilityManifest? CapabilityManifest { get; set; }
    public Dictionary<string, RawIngestRecord> RawPayloads { get; } = new Dictionary<string, RawIngestRecord>();
    public List<AuditRecord> Audit { get; } = new List<AuditRecord>();
    public Dictionary<string, ActionQueueEnvelope> ActionQueues { get; } = new Dictionary<string, ActionQueueEnvelope>();
    public Dictionary<string, ExecutionBatchResult> ExecutionResults { get; } = new Dictionary<string, ExecutionBatchResult>();

    public SnapshotEnvelope? LatestSnapshot()
    {
        lock (snapshotGate)
        {
            return Snapshots.Values.OrderByDescending(item => item.GameTick).FirstOrDefault();
        }
    }

    public void PrepareForSnapshotIngest()
    {
        lock (snapshotGate)
        {
            while (snapshotOrder.Count >= MaxRetainedSnapshots)
            {
                RemoveOldestSnapshot();
            }
        }
    }

    public void StoreSnapshot(SnapshotEnvelope snapshot)
    {
        lock (snapshotGate)
        {
            if (!Snapshots.ContainsKey(snapshot.StateHash))
            {
                snapshotOrder.Enqueue(snapshot.StateHash);
            }

            Snapshots[snapshot.StateHash] = snapshot;
            while (snapshotOrder.Count > MaxRetainedSnapshots)
            {
                RemoveOldestSnapshot();
            }
        }
    }

    private void RemoveOldestSnapshot()
    {
        if (!snapshotOrder.TryDequeue(out var stateHash))
        {
            return;
        }

        Snapshots.Remove(stateHash);
        RawPayloads.Remove(stateHash);
    }

    public void AppendAudit(string eventType, long gameTick, string stateHash)
    {
        Audit.Add(new AuditRecord
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            GameTick = gameTick,
            StateHash = stateHash
        });
    }
}

public sealed record RawIngestRecord(string PayloadType, string Id, string ReceivedAt, string RawPayload);

public static class SnapshotValidator
{
    private static readonly string[] RequiredDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "mods",
        "farm",
        "current_location",
        "npcs",
        "quests",
        "world_progress",
        "menus",
        "modded_state"
    };

    private static readonly string[] MiningRequiredDomains =
    {
        "environment",
        "identity",
        "time",
        "player",
        "options",
        "menus",
        "transport",
        "mining"
    };

    public static async Task<(List<string> Errors, SnapshotEnvelope? Snapshot)> ValidateAsync(
        Stream rawPayload,
        CancellationToken cancellationToken = default,
        string? profile = null)
    {
        try
        {
            var snapshot = await JsonSerializer.DeserializeAsync<SnapshotEnvelope>(rawPayload, JsonOptions, cancellationToken);
            return (ValidateDeserialized(snapshot, profile), snapshot);
        }
        catch (JsonException ex)
        {
            return (new List<string> { "invalid json: " + ex.Message }, null);
        }
    }

    public static List<string> ValidateRaw(
        string rawPayload,
        out SnapshotEnvelope? snapshot,
        string? profile = null)
    {
        try
        {
            snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(rawPayload, JsonOptions);
            return ValidateDeserialized(snapshot, profile);
        }
        catch (JsonException ex)
        {
            snapshot = null;
            return new List<string> { "invalid json: " + ex.Message };
        }
    }

    private static List<string> ValidateDeserialized(
        SnapshotEnvelope? snapshot,
        string? profile)
    {
        if (snapshot is null)
        {
            return new List<string> { "snapshot deserialization failed" };
        }

        var errors = Validate(snapshot, profile);
        var computed = SnapshotHash.ComputeStateHash(snapshot.State);
        if (!string.Equals(snapshot.StateHash, computed, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("state_hash mismatch");
        }
        return errors;
    }

    public static List<string> Validate(
        SnapshotEnvelope snapshot,
        string? profile = null)
    {
        var errors = new List<string>();
        if (snapshot.SchemaVersion != "snapshot.v1")
        {
            errors.Add("schema_version must be snapshot.v1");
        }

        if (string.IsNullOrWhiteSpace(snapshot.BridgeVersion))
        {
            errors.Add("bridge_version is required");
        }

        if (string.IsNullOrWhiteSpace(snapshot.RealTimestamp))
        {
            errors.Add("real_timestamp is required");
        }

        if (string.IsNullOrWhiteSpace(snapshot.StateHash))
        {
            errors.Add("state_hash is required");
        }

        var requiredDomains = RequiredDomainsForProfile(profile, errors);
        foreach (var domain in requiredDomains)
        {
            if (!snapshot.State.ContainsKey(domain))
            {
                errors.Add("missing state domain: " + domain);
            }
        }

        foreach (var entry in snapshot.State)
        {
            ValidateTransparentFields(entry.Value, "state." + entry.Key, errors);
        }

        return errors;
    }

    private static string[] RequiredDomainsForProfile(
        string? profile,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(profile) ||
            string.Equals(profile, "full", StringComparison.OrdinalIgnoreCase))
        {
            return RequiredDomains;
        }

        if (string.Equals(profile, "mining", StringComparison.OrdinalIgnoreCase))
        {
            return MiningRequiredDomains;
        }

        errors.Add("unsupported snapshot profile: " + profile);
        return Array.Empty<string>();
    }

    private static void ValidateTransparentFields(JsonElement element, string path, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(path + " must be an object");
            return;
        }

        if (element.TryGetProperty("status", out _))
        {
            foreach (var required in new[] { "value", "status", "source", "adapter", "read_at_tick", "confidence" })
            {
                if (!element.TryGetProperty(required, out _))
                {
                    errors.Add(path + " missing field envelope key: " + required);
                }
            }

            var status = element.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
            if (!FieldStatus.IsKnown(status))
            {
                errors.Add(path + " has invalid status: " + status);
            }

            var hasValue = element.TryGetProperty("value", out var valueElement) && valueElement.ValueKind != JsonValueKind.Null;
            if (FieldEnvelopeValidator.IsReadableStatus(status) && !hasValue)
            {
                errors.Add(path + " readable status requires non-null value");
            }

            if (!FieldEnvelopeValidator.IsReadableStatus(status) && hasValue)
            {
                errors.Add(path + " non-readable status must not carry a default value");
            }

            if (element.TryGetProperty("confidence", out var confidence) &&
                (!confidence.TryGetDouble(out var confidenceValue) || confidenceValue < 0 || confidenceValue > 1))
            {
                errors.Add(path + " confidence must be between 0 and 1");
            }

            if (status == FieldStatus.Derived && !element.TryGetProperty("derivation", out _))
            {
                errors.Add(path + " derived status requires derivation");
            }

            if ((status == FieldStatus.Unavailable || status == FieldStatus.Stale || status == FieldStatus.Error) &&
                !element.TryGetProperty("reason", out _))
            {
                errors.Add(path + " non-readable status requires reason");
            }

            return;
        }

        foreach (var child in element.EnumerateObject())
        {
            ValidateTransparentFields(child.Value, path + "." + child.Name, errors);
        }
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static class EventValidator
{
    public static List<string> ValidateRaw(string rawPayload, StateStore store, out GameEvent? gameEvent)
    {
        gameEvent = null;
        var errors = new List<string>();
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(rawPayload);
        }
        catch (JsonException ex)
        {
            errors.Add("invalid json: " + ex.Message);
            return errors;
        }

        using (document)
        {
            if (!document.RootElement.TryGetProperty("schema_version", out var schema) || schema.GetString() != "event.v2")
            {
                errors.Add("schema_version must be event.v2");
                return errors;
            }
        }

        gameEvent = JsonSerializer.Deserialize<GameEvent>(rawPayload, JsonOptions);
        if (gameEvent is null)
        {
            errors.Add("event deserialization failed");
            return errors;
        }

        if (string.IsNullOrWhiteSpace(gameEvent.EventId) || string.IsNullOrWhiteSpace(gameEvent.EventType))
        {
            errors.Add("event_id and event_type are required");
        }

        if (gameEvent.ChangedFields.Length == 0 && gameEvent.EventType != "SnapshotPublished")
        {
            errors.Add("changed_fields is required for change events");
        }

        if (string.IsNullOrWhiteSpace(gameEvent.ObservedSnapshotHash))
        {
            errors.Add("observed_snapshot_hash is required");
        }
        else if (gameEvent.ObservedSnapshotHash != "unavailable" &&
            !store.Snapshots.ContainsKey(gameEvent.ObservedSnapshotHash))
        {
            errors.Add("observed_snapshot_hash does not match an ingested snapshot");
        }

        if (gameEvent.EventType == "SnapshotPublished")
        {
            if (gameEvent.SnapshotRelation != "snapshot_published")
            {
                errors.Add("SnapshotPublished requires snapshot_relation=snapshot_published");
            }

            if (string.IsNullOrWhiteSpace(gameEvent.PublishedSnapshotHash) ||
                !store.Snapshots.ContainsKey(gameEvent.PublishedSnapshotHash))
            {
                errors.Add("published_snapshot_hash does not match an ingested snapshot");
            }
        }
        else
        {
            if (gameEvent.SnapshotRelation != "observed_after_snapshot")
            {
                errors.Add("change events require snapshot_relation=observed_after_snapshot");
            }

            if (!string.IsNullOrWhiteSpace(gameEvent.PublishedSnapshotHash))
            {
                errors.Add("change events cannot claim a published_snapshot_hash");
            }
        }

        return errors;
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}

public static class CapabilityValidator
{
    public static List<string> Validate(CapabilityManifest manifest)
    {
        var errors = new List<string>();
        if (manifest.SchemaVersion != "capabilities.v1")
        {
            errors.Add("schema_version must be capabilities.v1");
        }

        if (manifest.PermissionMode != "observer")
        {
            errors.Add("permission_mode must be observer");
        }

        if (manifest.CanExecuteCommands || manifest.CanWriteGameState)
        {
            errors.Add("Phase 1A-2 capabilities must not declare write or execute permission");
        }

        if (manifest.CompatibilityStatus is not ("identity_observed_unverified" or "identity_incomplete"))
        {
            errors.Add("compatibility_status must not claim verification without indexed evidence");
        }

        ValidateBinaryIdentity("game_binary_identity", manifest.GameBinaryIdentity, errors);
        ValidateBinaryIdentity("smapi_binary_identity", manifest.SmapiBinaryIdentity, errors);

        var capabilityIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var capability in manifest.Capabilities)
        {
            if (string.IsNullOrWhiteSpace(capability.CapabilityId))
            {
                errors.Add("capability_id is required");
            }

            else if (!capabilityIds.Add(capability.CapabilityId))
            {
                errors.Add("duplicate capability_id: " + capability.CapabilityId);
            }

            if (capability.AccessMode == "execute" &&
                capability.Status is not ("disabled" or "blocked"))
            {
                errors.Add(capability.CapabilityId + " execute capability must be blocked");
            }
        }

        return errors;
    }

    private static void ValidateBinaryIdentity(
        string field,
        BinaryIdentity identity,
        ICollection<string> errors)
    {
        if (identity.IdentityStatus == "hash_observed" &&
            (string.IsNullOrWhiteSpace(identity.AssemblyName) ||
             string.IsNullOrWhiteSpace(identity.AssemblyVersion) ||
             !Guid.TryParse(identity.Mvid, out var mvid) ||
             mvid == Guid.Empty ||
             identity.ByteLength is null or <= 0 ||
             identity.Sha256.Length != 64 ||
             identity.Sha256.Any(value => !Uri.IsHexDigit(value))))
        {
            errors.Add(field + " hash_observed identity is incomplete");
        }
    }
}

public static class DailyPlanBudgetReader
{
    public static int? AvailablePlanMinutes(SnapshotEnvelope? snapshot)
    {
        var currentTime = ReadSnapshotInt(snapshot, "time", "time");
        if (!currentTime.HasValue)
        {
            return null;
        }

        return Math.Max(0, ClockMinutesBetween(currentTime.Value, 2600) - 60);
    }

    public static int? ReadSnapshotInt(SnapshotEnvelope? snapshot, string sectionName, string fieldName)
    {
        if (snapshot is null ||
            !snapshot.State.TryGetValue(sectionName, out var section) ||
            section.ValueKind != JsonValueKind.Object ||
            !section.TryGetProperty(fieldName, out var field) ||
            field.ValueKind != JsonValueKind.Object ||
            !field.TryGetProperty("value", out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var result))
        {
            return null;
        }

        return result;
    }

    private static int ClockMinutesBetween(int start, int end)
    {
        return ToAbsoluteMinutes(end) - ToAbsoluteMinutes(start);
    }

    private static int ToAbsoluteMinutes(int hhmm)
    {
        var hours = hhmm / 100;
        var minutes = hhmm % 100;
        return hours * 60 + minutes;
    }
}

public partial class Program
{
}
