using System.Text.Json;
using System.Text.Json.Serialization;
using StardewAI.Contracts.Audit;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Events;
using StardewAI.Contracts.Previews;
using StardewAI.Contracts.State;
using StardewAI.Core.PreviewCompiler;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddSingleton<StateStore>();
builder.Services.AddSingleton<PlanningPreviewCompiler>();

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

app.MapGet("/api/v1/stardew/input/latest", (string? goal, string? mode, StateStore store) =>
{
    var latest = store.LatestSnapshot();
    if (latest is null)
    {
        return Results.NotFound(new { detail = "no snapshots ingested" });
    }

    return Results.Ok(StardewInputProjector.Project(latest, goal ?? string.Empty, mode ?? "relaxed"));
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

public sealed class StateStore
{
    public Dictionary<string, SnapshotEnvelope> Snapshots { get; } = new Dictionary<string, SnapshotEnvelope>();
    public List<GameEvent> Events { get; } = new List<GameEvent>();
    public Dictionary<string, Capability> Capabilities { get; } = new Dictionary<string, Capability>();
    public CapabilityManifest? CapabilityManifest { get; set; }
    public Dictionary<string, RawIngestRecord> RawPayloads { get; } = new Dictionary<string, RawIngestRecord>();
    public List<AuditRecord> Audit { get; } = new List<AuditRecord>();

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
        "world_progress"
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

public static class StardewInputProjector
{
    public static object Project(SnapshotEnvelope snapshot, string goal, string mode)
    {
        return new
        {
            state_hash = snapshot.StateHash,
            game_tick = snapshot.GameTick,
            user_goal = goal,
            mode,
            facts = new Dictionary<string, object?>
            {
                ["game.current_location"] = ReadFirstValue(snapshot, "player.location_id", "game.current_location"),
                ["game.time_of_day"] = ReadFirstValue(snapshot, "time.time", "game.time_of_day"),
                ["player.money"] = ReadValue(snapshot, "player.money"),
                ["player.stamina"] = ReadFirstValue(snapshot, "player.energy", "player.stamina"),
                ["player.inventory"] = ReadValue(snapshot, "player.inventory"),
                ["menus.active_menu"] = ReadFirstValue(snapshot, "player.active_menu", "menus.active_menu")
            }
        };
    }

    private static object? ReadFirstValue(SnapshotEnvelope snapshot, params string[] paths)
    {
        foreach (var path in paths)
        {
            var value = ReadValue(snapshot, path);
            if (value is not null)
            {
                return value;
            }
        }

        return null;
    }

    private static object? ReadValue(SnapshotEnvelope snapshot, string path)
    {
        var current = ReadPath(snapshot, path);
        if (current.HasValue &&
            current.Value.ValueKind == JsonValueKind.Object &&
            current.Value.TryGetProperty("status", out var status) &&
            FieldEnvelopeValidator.IsReadableStatus(status.GetString()) &&
            current.Value.TryGetProperty("value", out var value))
        {
            return value.ValueKind switch
            {
                JsonValueKind.String => value.GetString(),
                JsonValueKind.Number => value.TryGetInt64(out var number) ? number : value.GetDouble(),
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }

        return null;
    }

    private static JsonElement? ReadPath(SnapshotEnvelope snapshot, string path)
    {
        var parts = path.Split('.');
        if (parts.Length == 0 || !snapshot.State.TryGetValue(parts[0], out var current))
        {
            return null;
        }

        for (var i = 1; i < parts.Length; i++)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(parts[i], out current))
            {
                return null;
            }
        }

        return current;
    }
}

public partial class Program
{
}
