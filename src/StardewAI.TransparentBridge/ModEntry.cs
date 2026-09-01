using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
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

public sealed partial class ModEntry : Mod
{
    private const uint SnapshotRefreshTickInterval = 600;
    private const long SnapshotProfileMaxAgeTicks = 30;

    private readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    private HttpListener? listener;
    private TcpListener? eventWebSocketListener;
    private CancellationTokenSource? serverCancellation;
    private readonly List<AuditRecord> audit = new();
    private readonly List<GameEvent> events = new();
    private readonly object eventLock = new();
    private readonly object snapshotLock = new();
    private readonly ConcurrentQueue<PendingSnapshotRequest> pendingSnapshotRequests = new();
    private BridgeConfig config = new();
    private TransparentStateCollector? stateCollector;
    private SnapshotEnvelope? latestSnapshot;
    private readonly Dictionary<string, SnapshotEnvelope> profileSnapshots = new(StringComparer.OrdinalIgnoreCase);
    private string currentStateHash = "unavailable";
    private long nextEventSequence = 1;
    private string latestEventHash = "genesis";
    private long latestGameTick;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<BridgeConfig>();
        stateCollector = CreateStateCollector(helper);

        helper.Events.GameLoop.GameLaunched += OnGameLaunched;
        helper.Events.GameLoop.SaveLoaded += (_, _) =>
        {
            PublishEvent("SaveLoaded", new[] { "identity.save_id", "identity.player_id" });
        };
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
                FarmReadAdapter.RefreshMachineProbeCache();
            }
        };
        helper.Events.Display.MenuChanged += (_, e) => PublishEvent("MenuChanged", new[] { "player.active_menu" }, MenuSummary(e.OldMenu), MenuSummary(e.NewMenu));
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => AddAudit("ReturnedToTitle");
        helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;

        StartReadOnlyServer();
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        RecordUpdateGap();
        Interlocked.Exchange(ref latestGameTick, unchecked((long)Game1.ticks));
        ProcessPendingSnapshotRequests();

        if (e.IsMultipleOf(10))
        {
            FarmReadAdapter.RefreshMachineProbeCache();
        }

        if (e.IsMultipleOf(SnapshotRefreshTickInterval))
        {
            RefreshSnapshotCache("clock", publishSnapshotEvent: false);
        }
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
        var httpPrefixHost = string.Equals(config.Host, "0.0.0.0", StringComparison.Ordinal)
            ? "*"
            : config.Host;
        listener.Prefixes.Add($"http://{httpPrefixHost}:{config.Port}/");
        listener.Start();

        Monitor.Log($"TransparentBridge read-only API listening on http://{config.Host}:{config.Port}/", LogLevel.Info);
        _ = Task.Run(() => ServeAsync(serverCancellation.Token));
        StartEventWebSocketServer();
    }

    private void StartEventWebSocketServer()
    {
        if (!IPAddress.TryParse(config.Host, out var address))
        {
            Monitor.Log($"Event WebSocket disabled: host '{config.Host}' is not an IP address.", LogLevel.Warn);
            return;
        }

        eventWebSocketListener = new TcpListener(address, config.WebSocketPort);
        eventWebSocketListener.Start();
        Monitor.Log($"TransparentBridge read-only event WebSocket listening on ws://{config.Host}:{config.WebSocketPort}/api/v1/events/ws", LogLevel.Info);
        _ = Task.Run(() => ServeEventWebSocketAsync(serverCancellation?.Token ?? CancellationToken.None));
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

        object response;
        if (path == "/api/v1/snapshot")
        {
            response = await GetLatestSnapshotAsync(context.Request);
        }
        else
        {
            response = path switch
            {
                "/api/v1/capabilities" => BuildCapabilities(),
                "/api/v1/events" => BuildEventStreamResponse(context.Request),
                "/api/v1/events/ws" => new
                {
                    error = "use_websocket_endpoint",
                    endpoint = $"ws://{config.Host}:{config.WebSocketPort}/api/v1/events/ws",
                    schema_version = "event_stream.v2"
                },
                "/api/v1/audit" => ReadAuditTail(),
                "/api/v1/performance" => BuildPerformanceResponse(),
                "/" => new { service = "StardewAI.TransparentBridge", version = "0.1.0" },
                _ => new { error = "not_found", path }
            };
        }

        context.Response.StatusCode = path is "/api/v1/snapshot" or "/api/v1/capabilities" or "/api/v1/events" or "/api/v1/events/ws" or "/api/v1/audit" or "/api/v1/performance" or "/" ? 200 : 404;
        context.Response.ContentType = "application/json; charset=utf-8";

        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, jsonOptions);
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private async Task ServeEventWebSocketAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && eventWebSocketListener is not null)
        {
            try
            {
                var client = await eventWebSocketListener.AcceptTcpClientAsync();
                _ = Task.Run(() => HandleEventWebSocketClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (ObjectDisposedException)
            {
                return;
            }
            catch (SocketException)
            {
                return;
            }
            catch (Exception ex)
            {
                Monitor.Log($"Event WebSocket server loop failed: {ex}", LogLevel.Error);
            }
        }
    }

    private async Task HandleEventWebSocketClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        await using var stream = client.GetStream();
        try
        {
            using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 4096, leaveOpen: true);
            var requestLine = await reader.ReadLineAsync();
            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? line;
            while (!string.IsNullOrEmpty(line = await reader.ReadLineAsync()))
            {
                var separator = line.IndexOf(':');
                if (separator > 0)
                {
                    headers[line[..separator].Trim()] = line[(separator + 1)..].Trim();
                }
            }

            var target = requestLine?.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault() ?? "/";
            var uri = new Uri("http://localhost" + target);
            if (!string.Equals(uri.AbsolutePath, "/api/v1/events/ws", StringComparison.OrdinalIgnoreCase) ||
                !headers.TryGetValue("Sec-WebSocket-Key", out var key))
            {
                var badRequest = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(badRequest, 0, badRequest.Length, cancellationToken);
                return;
            }

            var response = Encoding.ASCII.GetBytes(
                "HTTP/1.1 101 Switching Protocols\r\n" +
                "Upgrade: websocket\r\n" +
                "Connection: Upgrade\r\n" +
                $"Sec-WebSocket-Accept: {ComputeWebSocketAccept(key)}\r\n" +
                "\r\n");
            await stream.WriteAsync(response, 0, response.Length, cancellationToken);

            var query = ParseQuery(uri.Query);
            var afterSequence = ParseLongQuery(query, "after_sequence") ?? 0;
            var afterTick = ParseLongQuery(query, "after_tick");
            var limit = Math.Clamp((int)(ParseLongQuery(query, "limit") ?? 200), 1, 500);

            while (!cancellationToken.IsCancellationRequested && client.Connected)
            {
                var envelope = BuildEventStreamResponse(afterSequence, afterTick, limit);
                await SendWebSocketJsonAsync(stream, envelope);
                afterSequence = envelope.NextAfterSequence ?? envelope.LatestEventSequence;
                await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Monitor.Log($"Event WebSocket failed: {ex}", LogLevel.Warn);
        }
        finally
        {
            client.Dispose();
        }
    }

    private string ComputeWebSocketAccept(string key)
    {
        var raw = Encoding.ASCII.GetBytes(key.Trim() + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11");
        using var sha1 = SHA1.Create();
        return Convert.ToBase64String(sha1.ComputeHash(raw));
    }

    private async Task SendWebSocketJsonAsync(Stream outputStream, object payload)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, jsonOptions);
        var header = BuildWebSocketTextHeader(bytes.Length);
        await outputStream.WriteAsync(header, 0, header.Length, serverCancellation?.Token ?? CancellationToken.None);
        await outputStream.WriteAsync(bytes, 0, bytes.Length, serverCancellation?.Token ?? CancellationToken.None);
        await outputStream.FlushAsync(serverCancellation?.Token ?? CancellationToken.None);
    }

    private static byte[] BuildWebSocketTextHeader(int payloadLength)
    {
        if (payloadLength <= 125)
        {
            return new[] { (byte)0x81, (byte)payloadLength };
        }

        if (payloadLength <= ushort.MaxValue)
        {
            return new[]
            {
                (byte)0x81,
                (byte)126,
                (byte)((payloadLength >> 8) & 0xff),
                (byte)(payloadLength & 0xff)
            };
        }

        var header = new byte[10];
        header[0] = 0x81;
        header[1] = 127;
        var length = (ulong)payloadLength;
        for (var i = 0; i < 8; i++)
        {
            header[9 - i] = (byte)(length & 0xff);
            length >>= 8;
        }

        return header;
    }

    private async Task<SnapshotEnvelope> GetLatestSnapshotAsync(HttpListenerRequest request)
    {
        var profile = SnapshotProfile(request);
        var forceRefresh = SnapshotForceRefresh(request);
        var expectedStateHash = string.Empty;
        var expectedGameTick = 0L;
        var hasExpectedCacheIdentity =
            string.Equals(profile, "full", StringComparison.OrdinalIgnoreCase) &&
            TryReadExpectedSnapshotCacheIdentity(
                request,
                out expectedStateHash,
                out expectedGameTick);
        if (!forceRefresh)
        {
            lock (snapshotLock)
            {
                if (profileSnapshots.TryGetValue(profile, out var cached) &&
                    (hasExpectedCacheIdentity
                        ? SnapshotMatchesExpectedCacheIdentity(
                            cached,
                            expectedStateHash,
                            expectedGameTick)
                        : IsSnapshotFresh(
                            cached,
                            Interlocked.Read(ref latestGameTick))))
                {
                    return cached;
                }
            }
        }

        // A caller requesting an exact cached post-action snapshot must never receive
        // a different merely-recent snapshot when that cache entry is unavailable.
        if (hasExpectedCacheIdentity)
        {
            forceRefresh = true;
        }

        var completion = new TaskCompletionSource<SnapshotEnvelope>(TaskCreationOptions.RunContinuationsAsynchronously);
        pendingSnapshotRequests.Enqueue(new PendingSnapshotRequest(profile, forceRefresh, completion));
        return await completion.Task.WaitAsync(TimeSpan.FromSeconds(180));
    }

    private static bool TryReadExpectedSnapshotCacheIdentity(
        HttpListenerRequest request,
        out string stateHash,
        out long gameTick)
    {
        var query = ParseQuery(request.Url?.Query ?? string.Empty);
        stateHash = query.TryGetValue("expected_state_hash", out var hash)
            ? hash
            : string.Empty;
        gameTick = query.TryGetValue("expected_game_tick", out var tick) &&
            long.TryParse(
                tick,
                System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture,
                out var parsedTick)
                ? parsedTick
                : 0;
        return stateHash.Length == 64 &&
            stateHash.All(Uri.IsHexDigit) &&
            gameTick > 0;
    }

    private static bool SnapshotMatchesExpectedCacheIdentity(
        SnapshotEnvelope snapshot,
        string stateHash,
        long gameTick) =>
        snapshot.GameTick == gameTick &&
        string.Equals(snapshot.StateHash, stateHash, StringComparison.OrdinalIgnoreCase);

    private static bool IsSnapshotFresh(SnapshotEnvelope snapshot, long currentGameTick)
    {
        if (snapshot.State.TryGetValue("player", out var playerElement) &&
            playerElement.TryGetProperty("location_id", out var locationElement) &&
            locationElement.TryGetProperty("value", out var valueElement) &&
            valueElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(valueElement.GetString()))
        {
            return currentGameTick - snapshot.GameTick <= SnapshotProfileMaxAgeTicks;
        }

        return currentGameTick - snapshot.GameTick <= SnapshotProfileMaxAgeTicks;
    }

    private void ProcessPendingSnapshotRequests()
    {
        if (pendingSnapshotRequests.IsEmpty)
        {
            return;
        }

        var requests = new List<PendingSnapshotRequest>();
        while (pendingSnapshotRequests.TryDequeue(out var request))
        {
            requests.Add(request);
        }

        foreach (var group in requests.GroupBy(
                     item => (Profile: item.Profile.ToLowerInvariant(), item.ForceRefresh)))
        {
            try
            {
                var currentGameTick = Interlocked.Read(ref latestGameTick);
                SnapshotEnvelope snapshot;
                lock (snapshotLock)
                {
                    snapshot = !group.Key.ForceRefresh &&
                               profileSnapshots.TryGetValue(group.Key.Profile, out var cached) &&
                               IsSnapshotFresh(cached, currentGameTick)
                        ? cached
                        : RefreshSnapshotCache(group.Key.Profile, publishSnapshotEvent: true);
                }

                foreach (var request in group)
                {
                    request.Completion.TrySetResult(snapshot);
                }
            }
            catch (Exception ex)
            {
                Monitor.Log($"Main-thread snapshot collection failed for profile '{group.Key.Profile}': {ex}", LogLevel.Error);
                foreach (var request in group)
                {
                    request.Completion.TrySetException(ex);
                }
            }
        }
    }

    private CapabilityManifest BuildCapabilities()
    {
        var gameIdentity = ObserveBinaryIdentity(typeof(Game1).Assembly);
        var smapiIdentity = ObserveBinaryIdentity(typeof(IModHelper).Assembly);
        var adapterCapabilities = (stateCollector?.Adapters ?? Array.Empty<IStateAdapter>())
            .Select(adapter => Capability(
                adapter.Domain == "unavailable_fields"
                    ? "read.transport_metadata"
                    : "read." + adapter.Domain,
                "read",
                adapter.Domain == "unavailable_fields" ? "available" : "partial",
                new
                {
                    source_adapter = adapter.GetType().FullName,
                    adapter.Domain,
                    adapter.Priority
                },
                "FieldEnvelope status and provenance remain authoritative for each field."))
            .ToList();
        adapterCapabilities.Add(Capability(
            "read.environment",
            "read",
            "partial",
            "TransparentStateCollector environment projection",
            "Values are observed; binary compatibility is not inferred."));
        adapterCapabilities.Add(Capability(
            "read.identity",
            "read",
            Context.IsWorldReady ? "available" : "contextually_unavailable",
            "TransparentStateCollector identity projection",
            "Requires a loaded world."));
        adapterCapabilities.Add(Capability(
            "stream.events.websocket",
            "read",
            "available",
            "/api/v1/events/ws",
            "Read-only event_stream.v2 push; no inbound commands."));
        adapterCapabilities.Add(Capability(
            "execute.command",
            "execute",
            "blocked",
            "observer permission mode",
            "TransparentBridge has no command or game-state write path."));

        return new CapabilityManifest
        {
            SchemaVersion = "capabilities.v1",
            BridgeVersion = ModManifest.Version.ToString(),
            PermissionMode = "observer",
            CompatibilityStatus =
                gameIdentity.IdentityStatus == "hash_observed" &&
                smapiIdentity.IdentityStatus == "hash_observed"
                    ? "identity_observed_unverified"
                    : "identity_incomplete",
            ObservedGameVersion = Game1.version,
            ObservedSmapiVersion = Constants.ApiVersion.ToString(),
            GameBinaryIdentity = gameIdentity,
            SmapiBinaryIdentity = smapiIdentity,
            CanExecuteCommands = false,
            CanWriteGameState = false,
            OptionCapabilitySchemaVersion = OptionCapabilityRegistrySource.SchemaVersion,
            OptionCapabilities = OptionCapabilityRegistrySource.All.ToArray(),
            Capabilities = adapterCapabilities
                .OrderBy(capability => capability.CapabilityId, StringComparer.Ordinal)
                .ToArray()
        };
    }

    private static Capability Capability(
        string id,
        string accessMode,
        string status,
        object source,
        object limitations) => new()
    {
        CapabilityId = id,
        AccessMode = accessMode,
        Status = status,
        Source = source,
        Limitations = limitations,
        RequiredPermission = "observer"
    };

    private static BinaryIdentity ObserveBinaryIdentity(System.Reflection.Assembly assembly)
    {
        var location = assembly.Location;
        var identity = new BinaryIdentity
        {
            AssemblyName = assembly.GetName().Name ?? string.Empty,
            AssemblyVersion = assembly.GetName().Version?.ToString() ?? string.Empty,
            Mvid = assembly.ManifestModule.ModuleVersionId.ToString("D")
        };
        if (string.IsNullOrWhiteSpace(location) || !File.Exists(location))
        {
            identity.IdentityStatus = "metadata_only";
            return identity;
        }

        identity.ByteLength = new FileInfo(location).Length;
        using var stream = File.OpenRead(location);
        using var sha256 = SHA256.Create();
        identity.Sha256 = Convert.ToHexString(sha256.ComputeHash(stream)).ToLowerInvariant();
        identity.IdentityStatus = "hash_observed";
        return identity;
    }

    private void AddAudit(string eventType, object? details = null)
    {
        lock (eventLock)
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
    }

    private AuditRecord[] ReadAuditTail()
    {
        lock (eventLock)
        {
            return audit.TakeLast(200).ToArray();
        }
    }

    private void PublishEvent(string eventType, string[] changedFields, object? before = null, object? after = null)
    {
        AppendEvent(new GameEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = eventType,
            SchemaVersion = "event.v2",
            GameTick = unchecked((long)Game1.ticks),
            InGameTime = Context.IsWorldReady ? Game1.timeOfDay : null,
            RealTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Source = "SMAPI event subscription",
            ObservedSnapshotHash = currentStateHash,
            SnapshotRelation = "observed_after_snapshot",
            ChangedFields = changedFields,
            Before = before is null ? null : JsonSerializer.SerializeToElement(before, jsonOptions),
            After = after is null ? null : JsonSerializer.SerializeToElement(after, jsonOptions)
        });
        AddAudit(eventType, after);
    }

    private SnapshotEnvelope BuildSnapshotWithoutEvent(string profile = "light")
    {
        var previousProfile = SnapshotProfileContext.Current;
        try
        {
            SnapshotProfileContext.Current = profile;
            return (stateCollector ?? CreateStateCollector(Helper)).BuildSnapshot(AllowedDomainsForProfile(profile));
        }
        finally
        {
            SnapshotProfileContext.Current = previousProfile;
        }
    }

    private SnapshotEnvelope RefreshSnapshotCache(bool publishSnapshotEvent)
    {
        return RefreshSnapshotCache("light", publishSnapshotEvent);
    }

    private SnapshotEnvelope RefreshSnapshotCache(string profile, bool publishSnapshotEvent)
    {
        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var snapshot = BuildSnapshotWithoutEvent(profile);
            CacheSnapshot(profile, snapshot, publishSnapshotEvent);
            return snapshot;
        }
        finally
        {
            RecordSnapshotBuild(profile, startedAt);
        }
    }

    private void CacheSnapshot(string profile, SnapshotEnvelope snapshot, bool publishSnapshotEvent)
    {
        if (publishSnapshotEvent)
        {
            PublishSnapshotEvent(snapshot);
        }

        currentStateHash = snapshot.StateHash;
        lock (snapshotLock)
        {
            latestSnapshot = snapshot;
            profileSnapshots[profile] = snapshot;
        }
    }

    private static string SnapshotProfile(HttpListenerRequest request)
    {
        var value = ParseQuery(request.Url?.Query ?? string.Empty).TryGetValue("profile", out var profile)
            ? profile
            : "light";
        return value is "daily" or "clock" or "identity" or "route" or "shop" or "machine" or "training_machine" or "fishing" or "mining" or "volcano" or "full" ? value : "light";
    }

    private static bool SnapshotForceRefresh(HttpListenerRequest request)
    {
        return ParseQuery(request.Url?.Query ?? string.Empty).TryGetValue("fresh", out var value) &&
               (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1");
    }

    private static ISet<string>? AllowedDomainsForProfile(string profile)
    {
        if (string.Equals(profile, "full", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (profile is "clock" or "identity")
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        var domains = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "world",
            "player",
            "menus",
            "options",
            "unavailable_fields"
        };

        if (profile is "daily" or "route" or "shop" or "machine")
        {
            domains.Add("current_location");
            domains.Add("locations");
        }

        if (profile is "route" or "shop")
        {
            domains.Add("npcs");
        }

        if (profile is "machine")
        {
            domains.Add("farm");
        }

        if (profile is "daily" or "training_machine")
        {
            domains.Add("farm");
            domains.Add("current_location");
            domains.Add("locations");
            domains.Add("npcs");
            domains.Add("quests_progress");
            domains.Add("world_progress");
            domains.Add("mods");
            domains.Add("modded_state");
        }

        if (profile is "fishing")
        {
            domains.Add("fishing");
            domains.Add("current_location");
            domains.Add("locations");
            domains.Add("farm");
            domains.Add("npcs");
            domains.Add("quests_progress");
            domains.Add("world_progress");
            domains.Add("mods");
            domains.Add("modded_state");
        }

        if (profile is "mining")
        {
            domains.Add("mining");
        }

        if (profile is "volcano")
        {
            domains.Add("volcano");
        }

        return domains;
    }

    private void PublishSnapshotEvent(SnapshotEnvelope snapshot)
    {
        lock (eventLock)
        {
            if (events.Count > 0 &&
                events[^1].EventType == "SnapshotPublished" &&
                events[^1].PublishedSnapshotHash == snapshot.StateHash)
            {
                return;
            }
        }

        AppendEvent(new GameEvent
        {
            EventId = Guid.NewGuid().ToString("N"),
            EventType = "SnapshotPublished",
            SchemaVersion = "event.v2",
            GameTick = snapshot.GameTick,
            InGameTime = Context.IsWorldReady ? Game1.timeOfDay : null,
            RealTimestamp = DateTimeOffset.UtcNow.ToString("O"),
            Source = "TransparentStateCollector.BuildSnapshot",
            ObservedSnapshotHash = currentStateHash,
            PublishedSnapshotHash = snapshot.StateHash,
            SnapshotRelation = "snapshot_published",
            ChangedFields = Array.Empty<string>()
        });
    }

    private EventStreamEnvelope BuildEventStreamResponse(HttpListenerRequest request)
    {
        var afterSequence = ParseLongQuery(request, "after_sequence");
        var afterTick = ParseLongQuery(request, "after_tick");
        var limit = Math.Clamp((int)(ParseLongQuery(request, "limit") ?? 200), 1, 500);
        return BuildEventStreamResponse(afterSequence, afterTick, limit);
    }

    private EventStreamEnvelope BuildEventStreamResponse(long? afterSequence, long? afterTick, int limit)
    {
        GameEvent[] selected;
        long latestSequence;
        string streamEventHash;

        lock (eventLock)
        {
            IEnumerable<GameEvent> query = events;
            if (afterSequence.HasValue)
            {
                query = query.Where(item => item.EventSequence > afterSequence.Value);
            }

            if (afterTick.HasValue)
            {
                query = query.Where(item => item.GameTick > afterTick.Value);
            }

            selected = query.Take(limit).ToArray();
            latestSequence = nextEventSequence - 1;
            streamEventHash = latestEventHash;
        }

        return new EventStreamEnvelope
        {
            LatestSnapshotHash = currentStateHash,
            LatestEventSequence = latestSequence,
            LatestEventHash = streamEventHash,
            Events = selected,
            Count = selected.Length,
            NextAfterSequence = selected.Length == 0 ? afterSequence : selected[^1].EventSequence,
            ChainStatus = VerifyEventChain(selected) ? "ok" : "broken"
        };
    }

    private void AppendEvent(GameEvent gameEvent)
    {
        lock (eventLock)
        {
            gameEvent.EventSequence = nextEventSequence++;
            gameEvent.PreviousEventHash = latestEventHash;
            gameEvent.EventHash = ComputeEventHash(gameEvent);
            latestEventHash = gameEvent.EventHash;
            events.Add(gameEvent);
        }
    }

    private string ComputeEventHash(GameEvent gameEvent)
    {
        var hashPayload = JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["schema_version"] = gameEvent.SchemaVersion,
            ["event_id"] = gameEvent.EventId,
            ["event_sequence"] = gameEvent.EventSequence,
            ["event_type"] = gameEvent.EventType,
            ["game_tick"] = gameEvent.GameTick,
            ["real_timestamp"] = gameEvent.RealTimestamp,
            ["source"] = gameEvent.Source,
            ["in_game_time"] = gameEvent.InGameTime,
            ["observed_snapshot_hash"] = gameEvent.ObservedSnapshotHash,
            ["published_snapshot_hash"] = gameEvent.PublishedSnapshotHash,
            ["snapshot_relation"] = gameEvent.SnapshotRelation,
            ["previous_event_hash"] = gameEvent.PreviousEventHash,
            ["changed_fields"] = gameEvent.ChangedFields,
            ["before"] = gameEvent.Before,
            ["after"] = gameEvent.After
        }, jsonOptions);

        var canonical = SnapshotHash.Canonicalize(hashPayload);
        using var sha256 = SHA256.Create();
        var bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2"));
        }

        return builder.ToString();
    }

    private bool VerifyEventChain(GameEvent[] selected)
    {
        GameEvent? previous = null;
        foreach (var gameEvent in selected)
        {
            if (!string.Equals(gameEvent.EventHash, ComputeEventHash(gameEvent), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (previous is not null && !string.Equals(gameEvent.PreviousEventHash, previous.EventHash, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            previous = gameEvent;
        }

        return true;
    }

    private static long? ParseLongQuery(HttpListenerRequest request, string name)
    {
        var value = request.QueryString[name];
        return long.TryParse(value, out var parsed) ? parsed : null;
    }

    private static long? ParseLongQuery(IReadOnlyDictionary<string, string> query, string name)
    {
        return query.TryGetValue(name, out var value) && long.TryParse(value, out var parsed) ? parsed : null;
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
        {
            return values;
        }

        var trimmed = query.TrimStart('?');
        foreach (var part in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf('=');
            var key = separator >= 0 ? part[..separator] : part;
            var value = separator >= 0 ? part[(separator + 1)..] : string.Empty;
            values[Uri.UnescapeDataString(key)] = Uri.UnescapeDataString(value);
        }

        return values;
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
            new OptionsReadAdapter(),
            new FarmReadAdapter(),
            new CurrentLocationReadAdapter(),
            new MiningReadAdapter(),
            new VolcanoReadAdapter(),
            new FishingReadAdapter(),
            new ShopAccessReadAdapter(),
            new NpcReadAdapter(),
            new ProgressQuestReadAdapter(),
            new WorldProgressReadAdapter(),
            new MenuReadAdapter(),
            new ModReadAdapter(helper.ModRegistry),
            new ModdedStateReadAdapter(helper.ModRegistry),
            new UnavailableFieldsAdapter(config.Host, config.WebSocketPort)
        },
        RecordAdapterCollection);

    private sealed record PendingSnapshotRequest(
        string Profile,
        bool ForceRefresh,
        TaskCompletionSource<SnapshotEnvelope> Completion);
}
