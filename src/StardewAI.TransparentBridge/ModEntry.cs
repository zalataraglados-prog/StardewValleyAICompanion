using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewAI.TransparentBridge;

public sealed class ModEntry : Mod
{
    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private HttpListener? listener;
    private CancellationTokenSource? serverCancellation;
    private readonly List<AuditRecord> audit = new();
    private BridgeConfig config = new();
    private string currentStateHash = "unavailable";

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<BridgeConfig>();

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.DayStarted += (_, _) => AddAudit("DayStarted");
        helper.Events.GameLoop.TimeChanged += (_, e) => AddAudit("TimeChanged", new { e.OldTime, e.NewTime });
        helper.Events.Player.InventoryChanged += (_, _) => AddAudit("InventoryChanged");
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => AddAudit("ReturnedToTitle");

        StartReadOnlyServer();
    }

    private void OnGameLaunched(object? sender, GameLaunchedEventArgs e)
    {
        AddAudit("GameLaunched", new
        {
            Mode = config.PermissionMode,
            config.Host,
            config.Port
        });
    }

    private void StartReadOnlyServer()
    {
        if (!HttpListener.IsSupported)
        {
            Monitor.Log("HttpListener is not supported on this platform.", LogLevel.Warn);
            return;
        }

        serverCancellation = new CancellationTokenSource();
        listener = new HttpListener();
        listener.Prefixes.Add($"http://{config.Host}:{config.Port}/");
        listener.Start();

        Monitor.Log($"TransparentBridge read-only API listening on http://{config.Host}:{config.Port}/", LogLevel.Info);
        _ = Task.Run(() => ServeAsync(serverCancellation.Token));
    }

    private async Task ServeAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && listener is not null)
        {
            try
            {
                var context = await listener.GetContextAsync();
                _ = Task.Run(() => HandleRequestAsync(context), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (HttpListenerException)
            {
                return;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Bridge server loop failed: {ex}", LogLevel.Error);
            }
        }
    }

    private async Task HandleRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "/";

        object response = path switch
        {
            "/api/v1/snapshot" => BuildSnapshot(),
            "/api/v1/capabilities" => BuildCapabilities(),
            "/api/v1/audit" => audit.TakeLast(200).ToArray(),
            "/" => new { service = "StardewAI.TransparentBridge", version = "0.1.0" },
            _ => new { error = "not_found", path }
        };

        context.Response.StatusCode = path is "/api/v1/snapshot" or "/api/v1/capabilities" or "/api/v1/audit" or "/" ? 200 : 404;
        context.Response.ContentType = "application/json; charset=utf-8";

        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, jsonOptions);
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private SnapshotEnvelope BuildSnapshot()
    {
        var player = Context.IsWorldReady ? Game1.player : null;
        var location = Context.IsWorldReady ? Game1.currentLocation : null;
        var tick = unchecked((long)Game1.ticks);

        var snapshot = new SnapshotEnvelope
        {
            SchemaVersion = "snapshot.v1",
            BridgeVersion = ModManifest.Version.ToString(),
            SmapiVersion = Constants.ApiVersion.ToString(),
            GameVersion = "unknown",
            InstalledMods = Helper.ModRegistry.GetAll().Select(mod => new InstalledMod(mod.Manifest.UniqueID, mod.Manifest.Name, mod.Manifest.Version.ToString())).ToArray(),
            SaveId = Field(player?.farmName.Value, "Game1.player.farmName", tick),
            PlayerId = Field(player?.UniqueMultiplayerID.ToString(), "Game1.player.UniqueMultiplayerID", tick),
            GameTick = tick,
            InGameTime = Field(Context.IsWorldReady ? (int?)Game1.timeOfDay : null, "Game1.timeOfDay", tick),
            RealTimestamp = DateTimeOffset.UtcNow,
            Completeness = "partial",
            UnavailableFields = new[] { "npc_schedule", "shops", "collision_grid", "event_stream_websocket" },
            State = new
            {
                game_state = new
                {
                    date = Field(Context.IsWorldReady ? $"{Game1.year}-{Game1.currentSeason}-{Game1.dayOfMonth}" : null, "Game1.year/currentSeason/dayOfMonth", tick),
                    weather = Field(Context.IsWorldReady ? Game1.weatherForTomorrow : null, "Game1.weatherForTomorrow", tick),
                    current_map = Field(location?.NameOrUniqueName, "Game1.currentLocation.NameOrUniqueName", tick)
                },
                player_state = new
                {
                    money = Field(player?.Money, "Game1.player.Money", tick),
                    stamina = Field(player?.Stamina, "Game1.player.Stamina", tick),
                    health = Field(player?.health, "Game1.player.health", tick),
                    inventory_count = Field(player?.Items.Count(item => item is not null), "Game1.player.Items", tick)
                },
                task_state = Unavailable("planner_not_connected"),
                memory_state = Unavailable("backend_not_connected"),
                user_state = Unavailable("backend_not_connected")
            }
        };

        currentStateHash = Hash(snapshot.State);
        snapshot.StateHash = currentStateHash;
        return snapshot;
    }

    private object BuildCapabilities() => new
    {
        schema_version = "capabilities.v1",
        bridge_version = ModManifest.Version.ToString(),
        permission_mode = config.PermissionMode,
        state_hash = currentStateHash,
        capabilities = new object[]
        {
            Capability("can_read_basic_player_state", "read", "available", "SMAPI/StardewValley", "observer only"),
            Capability("can_read_inventory", "read", "partial", "Game1.player.Items", "counts only in skeleton"),
            Capability("can_read_crop_state", "read", "unavailable", "not_implemented", "planned for adapter layer"),
            Capability("can_preview_command", "preview", "unavailable", "not_implemented", "phase 4"),
            Capability("can_execute_command", "execute", "disabled", "permission_mode", "execution forbidden in phase 0.5")
        }
    };

    private static object Capability(string id, string accessMode, string status, string source, string limitations) => new
    {
        capability_id = id,
        access_mode = accessMode,
        status,
        source,
        limitations,
        required_permission = "observer",
        supported_game_versions = new[] { "unknown" },
        supported_mods = Array.Empty<string>(),
        known_conflicts = Array.Empty<string>()
    };

    private static FieldEnvelope<T> Field<T>(T value, string source, long readAtTick = 0) => new()
    {
        Value = value,
        Status = value is null ? "unavailable" : "available",
        Source = source,
        Adapter = "vanilla_1_6",
        ReadAtTick = readAtTick,
        Confidence = value is null ? 0.0 : 1.0
    };

    private static object Unavailable(string reason) => new
    {
        value = (object?)null,
        status = "unavailable",
        reason,
        adapter = "not_connected",
        confidence = 0.0
    };

    private void AddAudit(string eventType, object? details = null)
    {
        audit.Add(new AuditRecord(
            Guid.NewGuid().ToString("N"),
            eventType,
            DateTimeOffset.UtcNow,
            unchecked((long)Game1.ticks),
            currentStateHash,
            details));
    }

    private string Hash(object value)
    {
        var json = JsonSerializer.Serialize(value, jsonOptions);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}

public sealed class BridgeConfig
{
    public string Host { get; set; } = "127.0.0.1";
    public int Port { get; set; } = 8765;
    public string PermissionMode { get; set; } = "observer";
}

public sealed class SnapshotEnvelope
{
    public string SchemaVersion { get; set; } = "snapshot.v1";
    public string BridgeVersion { get; set; } = "0.1.0";
    public string SmapiVersion { get; set; } = "unknown";
    public string GameVersion { get; set; } = "unknown";
    public InstalledMod[] InstalledMods { get; set; } = Array.Empty<InstalledMod>();
    public FieldEnvelope<string?> SaveId { get; set; } = new();
    public FieldEnvelope<string?> PlayerId { get; set; } = new();
    public long GameTick { get; set; }
    public FieldEnvelope<int?> InGameTime { get; set; } = new();
    public DateTimeOffset RealTimestamp { get; set; }
    public string StateHash { get; set; } = "unavailable";
    public string Completeness { get; set; } = "partial";
    public string[] UnavailableFields { get; set; } = Array.Empty<string>();
    public object State { get; set; } = new();
}

public sealed record InstalledMod(string ModId, string Name, string Version);
public sealed record AuditRecord(string EventId, string EventType, DateTimeOffset RealTimestamp, long GameTick, string StateHash, object? Details);

public sealed class FieldEnvelope<T>
{
    public T? Value { get; set; }
    public string Status { get; set; } = "unavailable";
    public object Source { get; set; } = "unknown";
    public string Adapter { get; set; } = "unknown";
    public long ReadAtTick { get; set; }
    public double Confidence { get; set; }
}
