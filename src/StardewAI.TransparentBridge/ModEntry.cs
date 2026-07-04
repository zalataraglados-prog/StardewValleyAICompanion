using System.Net;
using System.Text.Json;
using StardewAI.Contracts.Audit;
using StardewAI.Contracts.Capabilities;
using StardewAI.Contracts.Events;
using StardewAI.Contracts.State;
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
    private readonly List<GameEvent> events = new();
    private BridgeConfig config = new();
    private TransparentStateCollector? stateCollector;
    private string currentStateHash = "unavailable";

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<BridgeConfig>();
        stateCollector = CreateStateCollector(helper);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += (_, _) => PublishEvent("SaveLoaded", new[] { "identity.save_id", "identity.player_id" });
        helper.Events.GameLoop.DayStarted += (_, _) => PublishEvent("DayStarted", new[] { "time.season", "time.day", "time.weather" });
        helper.Events.GameLoop.TimeChanged += (_, e) => PublishEvent("TimeChanged", new[] { "time.time" }, new { e.OldTime }, new { e.NewTime });
        helper.Events.Player.Warped += (_, e) =>
        {
            if (e.IsLocalPlayer)
            {
                PublishEvent("LocationChanged", new[] { "player.location_id" }, new { old_location = e.OldLocation.NameOrUniqueName }, new { new_location = e.NewLocation.NameOrUniqueName });
            }
        };
        helper.Events.Player.InventoryChanged += (_, e) =>
        {
            if (e.IsLocalPlayer)
            {
                PublishEvent("InventoryChanged", new[] { "player.inventory" }, null, new
                {
                    added = e.Added.Select(ItemSummary).ToArray(),
                    removed = e.Removed.Select(ItemSummary).ToArray(),
                    quantity_changed = e.QuantityChanged.Select(change => new
                    {
                        item = ItemSummary(change.Item),
                        old_size = change.OldSize,
                        new_size = change.NewSize
                    }).ToArray()
                });
            }
        };
        helper.Events.Display.MenuChanged += (_, e) => PublishEvent("MenuChanged", new[] { "player.active_menu" }, MenuSummary(e.OldMenu), MenuSummary(e.NewMenu));
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
            "/api/v1/events" => events.TakeLast(200).ToArray(),
            "/api/v1/audit" => audit.TakeLast(200).ToArray(),
            "/" => new { service = "StardewAI.TransparentBridge", version = "0.1.0" },
            _ => new { error = "not_found", path }
        };

        context.Response.StatusCode = path is "/api/v1/snapshot" or "/api/v1/capabilities" or "/api/v1/events" or "/api/v1/audit" or "/" ? 200 : 404;
        context.Response.ContentType = "application/json; charset=utf-8";

        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, jsonOptions);
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private SnapshotEnvelope BuildSnapshot()
    {
        var snapshot = (stateCollector ?? CreateStateCollector(Helper)).BuildSnapshot();

        currentStateHash = snapshot.StateHash;
        PublishSnapshotEvent(snapshot);
        snapshot.StateHash = currentStateHash;
        return snapshot;
    }

    private CapabilityManifest BuildCapabilities() => new()
    {
        SchemaVersion = "capabilities.v1",
        BridgeVersion = ModManifest.Version.ToString(),
        PermissionMode = "observer",
        CompatibilityStatus = "unverified",
        CanExecuteCommands = false,
        CanWriteGameState = false,
        Capabilities = new[]
        {
            Capability("read.environment", "read", "available", "Game1.version; Constants.ApiVersion; IModRegistry.GetAll()", "observer only"),
            Capability("read.identity", "read", "available", "Game1.player.farmName; Game1.player.UniqueMultiplayerID", "world must be loaded"),
            Capability("read.time", "read", "available", "Game1.currentSeason/dayOfMonth/timeOfDay/weather flags", "vanilla fields only"),
            Capability("read.player", "read", "available", "Game1.player location/tile/facing/money/health/stamina/tool/menu", "local player only"),
            Capability("read.inventory", "read", "available", "Game1.player.Items", "slot summaries only"),
            Capability("execute.command", "execute", "disabled", "observer permission mode", "execution forbidden in Phase 1A-2")
        }
    };

    private static Capability Capability(string id, string accessMode, string status, string source, string limitations) => new()
    {
        CapabilityId = id,
        AccessMode = accessMode,
        Status = status,
        Source = source,
        Limitations = limitations,
        RequiredPermission = "observer"
    };

    private void AddAudit(string eventType, object? details = null)
    {
        audit.Add(new AuditRecord
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            GameTick = unchecked((long)Game1.ticks),
            StateHash = currentStateHash,
            Details = details
        });
    }

    private void PublishEvent(string eventType, string[] changedFields, object? before = null, object? after = null)
    {
        var beforeHash = currentStateHash;
        var snapshot = BuildSnapshotWithoutEvent();
        currentStateHash = snapshot.StateHash;
        events.Add(new GameEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            SchemaVersion = "event.v1",
            GameTick = unchecked((long)Game1.ticks),
            InGameTime = Context.IsWorldReady ? Game1.timeOfDay : null,
            RealTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Source = "SMAPI event subscription",
            StateHashBefore = beforeHash,
            StateHashAfter = snapshot.StateHash,
            ChangedFields = changedFields,
            Before = before is null ? null : JsonSerializer.SerializeToElement(before, jsonOptions),
            After = after is null ? null : JsonSerializer.SerializeToElement(after, jsonOptions)
        });
        AddAudit(eventType, after);
    }

    private SnapshotEnvelope BuildSnapshotWithoutEvent()
    {
        return (stateCollector ?? CreateStateCollector(Helper)).BuildSnapshot();
    }

    private void PublishSnapshotEvent(SnapshotEnvelope snapshot)
    {
        if (events.Count > 0 && events[^1].EventType == "SnapshotPublished" && events[^1].StateHashAfter == snapshot.StateHash)
        {
            return;
        }

        events.Add(new GameEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = "SnapshotPublished",
            SchemaVersion = "event.v1",
            GameTick = snapshot.GameTick,
            InGameTime = Context.IsWorldReady ? Game1.timeOfDay : null,
            RealTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Source = "TransparentStateCollector.BuildSnapshot",
            StateHashBefore = currentStateHash,
            StateHashAfter = snapshot.StateHash,
            ChangedFields = Array.Empty<string>()
        });
    }

    private static object ItemSummary(Item item) => new
    {
        item_id = item.ItemId,
        qualified_item_id = item.QualifiedItemId,
        display_name = item.DisplayName,
        stack = item.Stack,
        quality = item.Quality
    };

    private static object MenuSummary(StardewValley.Menus.IClickableMenu? menu) => new
    {
        menu_type = menu?.GetType().FullName,
        state = menu is null ? "closed" : "open"
    };

    private static object? MenuSummary(object? menu)
    {
        return menu is null ? null : menu;
    }

    private TransparentStateCollector CreateStateCollector(IModHelper helper) => new(
        ModManifest.Version.ToString(),
        helper.ModRegistry,
        new IStateAdapter[]
        {
            new WorldReadAdapter(),
            new PlayerReadAdapter(),
            new ModReadAdapter(helper.ModRegistry),
            new UnavailableFieldsAdapter()
        });
}
