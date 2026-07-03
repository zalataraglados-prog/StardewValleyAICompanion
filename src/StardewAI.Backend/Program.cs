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

app.MapPost("/api/v1/snapshots", (SnapshotEnvelope snapshot, StateStore store) =>
{
    var errors = SnapshotValidator.Validate(snapshot);
    if (errors.Count > 0)
    {
        return Results.UnprocessableEntity(new { message = "snapshot validation failed", errors });
    }

    store.Snapshots[snapshot.StateHash] = snapshot;
    store.AppendAudit("SnapshotIngested", snapshot.GameTick, snapshot.StateHash);
    return Results.Ok(new { accepted = true, state_hash = snapshot.StateHash });
});

app.MapGet("/api/v1/snapshots/latest", (StateStore store) =>
{
    var latest = store.LatestSnapshot();
    return latest is null ? Results.NotFound(new { detail = "no snapshots ingested" }) : Results.Ok(latest);
});

app.MapPost("/api/v1/events", (GameEvent gameEvent, StateStore store) =>
{
    if (string.IsNullOrWhiteSpace(gameEvent.EventId) || string.IsNullOrWhiteSpace(gameEvent.EventType))
    {
        return Results.UnprocessableEntity(new { detail = "event_id and event_type are required" });
    }

    store.Events.Add(gameEvent);
    store.AppendAudit("EventIngested", gameEvent.GameTick, string.Empty);
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

app.MapPost("/api/v1/capabilities", (JsonElement payload, StateStore store) =>
{
    var capabilities = payload.ValueKind == JsonValueKind.Array
        ? JsonSerializer.Deserialize<Capability[]>(payload.GetRawText()) ?? Array.Empty<Capability>()
        : new[] { JsonSerializer.Deserialize<Capability>(payload.GetRawText()) ?? new Capability() };

    foreach (var capability in capabilities)
    {
        if (string.IsNullOrWhiteSpace(capability.CapabilityId))
        {
            return Results.UnprocessableEntity(new { detail = "capability_id is required" });
        }

        store.Capabilities[capability.CapabilityId] = capability;
        store.AppendAudit("CapabilityIngested", store.LatestSnapshot()?.GameTick ?? 0, store.LatestSnapshot()?.StateHash ?? string.Empty);
    }

    return Results.Ok(new
    {
        accepted = true,
        count = capabilities.Length,
        capability_ids = capabilities.Select(item => item.CapabilityId).ToArray()
    });
});

app.MapGet("/api/v1/capabilities", (StateStore store) => Results.Ok(store.Capabilities.Values.ToArray()));

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
        capabilities = store.Capabilities.Values.ToArray(),
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

public static class SnapshotValidator
{
    private static readonly string[] RequiredDomains =
    {
        "game",
        "player",
        "farm",
        "locations",
        "npcs",
        "quests",
        "world_progress",
        "menus",
        "mods",
        "modded_state"
    };

    public static List<string> Validate(SnapshotEnvelope snapshot)
    {
        var errors = new List<string>();
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
        if (path == "state.modded_state" && element.ValueKind == JsonValueKind.Object && !element.EnumerateObject().Any())
        {
            return;
        }

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

            return;
        }

        foreach (var child in element.EnumerateObject())
        {
            ValidateTransparentFields(child.Value, path + "." + child.Name, errors);
        }
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
                ["game.current_location"] = ReadValue(snapshot, "game.current_location"),
                ["game.time_of_day"] = ReadValue(snapshot, "game.time_of_day"),
                ["player.money"] = ReadValue(snapshot, "player.money"),
                ["player.stamina"] = ReadValue(snapshot, "player.stamina"),
                ["player.inventory"] = ReadValue(snapshot, "player.inventory"),
                ["menus.active_menu"] = ReadValue(snapshot, "menus.active_menu")
            }
        };
    }

    private static object? ReadValue(SnapshotEnvelope snapshot, string path)
    {
        var current = ReadPath(snapshot, path);
        if (current.HasValue &&
            current.Value.ValueKind == JsonValueKind.Object &&
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
