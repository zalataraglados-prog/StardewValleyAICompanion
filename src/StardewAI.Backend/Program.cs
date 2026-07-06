using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Audit;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Events;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Previews;
using StardewAI.Contracts.State;
using StardewAI.Contracts;
using StardewAI.Contracts.Training;
using StardewAI.Core.Execution;
using StardewAI.Core.Goals;
using StardewAI.Core.MockModel;
using StardewAI.Core.PreviewCompiler;
using StardewAI.Core.Training;
using StardewAI.Core.WorldModel;

var builder = WebApplication.CreateBuilder(args);
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
builder.Services.AddSingleton<TrainingEpisodeRewardCalculator>();
builder.Services.AddSingleton<TrainingEpisodeAdapter>();
builder.Services.AddSingleton<TrainingFeatureRowExporter>();
builder.Services.AddSingleton<JsonlTrainingDatasetWriter>();
builder.Services.AddSingleton<BaselineFeatureRowTrainer>();
builder.Services.AddSingleton<BaselinePolicyPredictor>();
builder.Services.AddSingleton<BaselineOptionRanker>();
builder.Services.AddSingleton<StardewTrainingSessionLauncher>();
builder.Services.AddSingleton<TrainingReadyProbe>();

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
    using var reader = new StreamReader(request.Body);
    var rawPayload = await reader.ReadToEndAsync();
    var errors = SnapshotValidator.ValidateRaw(rawPayload, out var snapshot);
    if (errors.Count > 0)
    {
        return Results.UnprocessableEntity(new { message = "snapshot validation failed", errors });
    }

    store.Snapshots[snapshot!.StateHash] = snapshot;
    store.RawPayloads[snapshot.StateHash] = new RawIngestRecord("snapshot", snapshot.StateHash, DateTimeOffset.UtcNow.ToString("O"), rawPayload);
    store.AppendAudit("SnapshotIngested", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(new { accepted = true, state_hash = snapshot.StateHash });
});

app.MapGet("/api/v1/snapshots/latest", (StateStore store) =>
{
    var latest = store.LatestSnapshot();
    return latest is null ? Results.NotFound(new { detail = "no snapshots ingested" }) : Results.Ok(latest);
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
    store.AppendAudit("EventIngested", acceptedEvent.GameTick, acceptedEvent.StateHashAfter);
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

    var model = projector.Project(latest, "grandpa_four_candles_year3", "strategic");
    return Results.Ok(evaluator.Evaluate(model));
});

app.MapGet("/api/v1/training/grandpa-evaluation/latest", (StateStore store, WorldModelProjector projector, GrandpaEvaluationGoalEvaluator evaluator, GrandpaTrainingSampleAdapter adapter) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.NotFound(new { detail = "no snapshots ingested" });
    }

    var model = projector.Project(latest, "grandpa_four_candles_year3", "strategic");
    var report = evaluator.Evaluate(model);
    return Results.Ok(adapter.Build(model, report));
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

app.MapPost("/api/v1/small-model/action-queue/compile", (SmallModelActionEnvelope request, StateStore store, ActionQueueCompiler compiler) =>
{
    var snapshot = !string.IsNullOrWhiteSpace(request.StateHash) && store.Snapshots.TryGetValue(request.StateHash, out var selected)
        ? selected
        : store.LatestSnapshot();

    if (snapshot is null)
    {
        return Results.NotFound(new { detail = "no matching snapshot available" });
    }

    var queue = compiler.Compile(request, snapshot);
    store.ActionQueues[queue.QueueId] = queue;
    store.AppendAudit("ActionQueueCompiled", snapshot.GameTick, snapshot.StateHash);
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

app.MapGet("/api/v1/action-queues/{queueId}", (string queueId, StateStore store) =>
    store.ActionQueues.TryGetValue(queueId, out var queue)
        ? Results.Ok(queue)
        : Results.NotFound(new { detail = "queue not found" }));

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

app.MapGet("/api/v1/training/session/ready-probe", (StateStore store, TrainingReadyProbe probe) =>
    Results.Ok(probe.Check(store.LatestSnapshot(), store.LatestSnapshot() is not null)));

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

app.MapPost("/api/v1/planner/baseline/rank-options", (BaselinePredictionRequest request, BaselineFeatureRowTrainer trainer, BaselineOptionRanker ranker) =>
{
    var report = request.TrainingReport;
    if (report is null)
    {
        var datasetPath = DatasetPathResolver.Resolve(request.DatasetPath);
        report = trainer.Train(datasetPath);
    }

    return Results.Ok(ranker.Rank(report, request.CandidateOptionIds));
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
        return Snapshots.Values.OrderByDescending(item => item.GameTick).FirstOrDefault();
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

    public static List<string> ValidateRaw(string rawPayload, out SnapshotEnvelope? snapshot)
    {
        snapshot = null;
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
            var root = document.RootElement;
            if (!root.TryGetProperty("schema_version", out var schemaVersion))
            {
                errors.Add("schema_version is required");
                return errors;
            }

            if (schemaVersion.GetString() != "snapshot.v1")
            {
                errors.Add("unsupported schema_version: " + schemaVersion.GetString());
                return errors;
            }

            snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(rawPayload, JsonOptions);
            if (snapshot is null)
            {
                errors.Add("snapshot deserialization failed");
                return errors;
            }

            errors.AddRange(Validate(snapshot));
            if (root.TryGetProperty("state", out _))
            {
                var computed = SnapshotHash.ComputeStateHash(snapshot.State);
                if (!string.Equals(snapshot.StateHash, computed, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add("state_hash mismatch");
                }
            }
        }

        return errors;
    }

    public static List<string> Validate(SnapshotEnvelope snapshot)
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

        foreach (var domain in RequiredDomains)
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
            if (!document.RootElement.TryGetProperty("schema_version", out var schema) || schema.GetString() != "event.v1")
            {
                errors.Add("schema_version must be event.v1");
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

        if (!string.IsNullOrWhiteSpace(gameEvent.StateHashAfter) && !store.Snapshots.ContainsKey(gameEvent.StateHashAfter))
        {
            errors.Add("state_hash_after does not match an ingested snapshot");
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

        foreach (var capability in manifest.Capabilities)
        {
            if (string.IsNullOrWhiteSpace(capability.CapabilityId))
            {
                errors.Add("capability_id is required");
            }

            if (capability.AccessMode == "execute" && capability.Status != "disabled")
            {
                errors.Add(capability.CapabilityId + " execute capability must be disabled");
            }
        }

        return errors;
    }
}

public partial class Program
{
}
