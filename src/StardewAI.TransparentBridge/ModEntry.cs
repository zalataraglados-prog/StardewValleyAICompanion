using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewAI.TransparentBridge.Adapters;
using StardewAI.TransparentBridge.State;

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
    private TransparentStateCollector? stateCollector;
    private string currentStateHash = "unavailable";

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<BridgeConfig>();
        stateCollector = CreateStateCollector(helper);

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
        var snapshot = (stateCollector ?? CreateStateCollector(Helper)).BuildSnapshot();

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
            Capability("can_read_inventory", "read", "partial", "Game1.player.Items", "item summaries only"),
            Capability("can_read_world_state", "read", "partial", "Game1 date/time/location APIs", "vanilla fields only"),
            Capability("can_read_farm_objects", "read", "partial", "Farm terrain/features/objects/buildings", "counts and safe summaries only"),
            Capability("can_read_npcs", "read", "partial", "current location characters", "current location only; schedules unavailable"),
            Capability("can_read_shops", "read", "unavailable", "not_implemented", "requires active shop/menu adapters"),
            Capability("can_read_maps", "read", "partial", "current location map metadata", "collision grid unavailable"),
            Capability("can_read_mods", "read", "available", "SMAPI ModRegistry", "manifest metadata only"),
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

    private TransparentStateCollector CreateStateCollector(IModHelper helper) => new(
        ModManifest.Version.ToString(),
        helper.ModRegistry,
        new IStateAdapter[]
        {
            new WorldReadAdapter(),
            new PlayerReadAdapter(),
            new FarmObjectsReadAdapter(),
            new NpcReadAdapter(),
            new ShopReadAdapter(),
            new MapReadAdapter(),
            new ModReadAdapter(helper.ModRegistry),
            new UnavailableFieldsAdapter()
        });
}
