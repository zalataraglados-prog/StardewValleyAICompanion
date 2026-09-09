using System.Text.Json;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;

namespace StardewAI.DemonstrationRecorder;

public sealed class ModEntry : Mod
{
    private readonly HttpClient snapshotClient = new()
    {
        Timeout = TimeSpan.FromSeconds(30)
    };
    private RecorderConfig config = new();
    private RecordingSession? active;
    private long lastSnapshotTick = long.MinValue;
    private int snapshotRequestActive;

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<RecorderConfig>();
        helper.ConsoleCommands.Add(
            "stardewai_demo_start",
            "Start a read-only human demonstration: stardewai_demo_start [goal_id] [label]",
            StartCommand);
        helper.ConsoleCommands.Add(
            "stardewai_demo_mark",
            "Mark the current intent: stardewai_demo_mark <method_id> [option_id]",
            MarkCommand);
        helper.ConsoleCommands.Add(
            "stardewai_demo_stop",
            "Stop and seal the current demonstration recording.",
            StopCommand);

        helper.Events.Input.ButtonsChanged += OnButtonsChanged;
        helper.Events.Player.Warped += OnWarped;
        helper.Events.Player.InventoryChanged += OnInventoryChanged;
        helper.Events.Display.MenuChanged += OnMenuChanged;
        helper.Events.GameLoop.DayStarted += OnDayStarted;
        helper.Events.GameLoop.TimeChanged += OnTimeChanged;
        helper.Events.GameLoop.Saving += OnSaving;
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => StopActive("returned_to_title");
    }

    private void StartCommand(string command, string[] args)
    {
        if (!Context.IsWorldReady)
        {
            Monitor.Log("Load a save before starting a demonstration.", LogLevel.Warn);
            return;
        }
        if (active is not null)
        {
            Monitor.Log("A demonstration is already active: " + active.SessionId, LogLevel.Warn);
            return;
        }

        var goalId = args.ElementAtOrDefault(0) ?? config.DefaultGoalId;
        var label = args.ElementAtOrDefault(1) ?? string.Empty;
        var root = Path.IsPathRooted(config.OutputRoot)
            ? config.OutputRoot
            : Path.Combine(Helper.DirectoryPath, config.OutputRoot);
        Directory.CreateDirectory(root);
        active = new RecordingSession(root, goalId, label);
        active.Enqueue("session_started", new
        {
            source_kind = "human_player",
            expert_admitted = false,
            game_anchor = GameAnchor(),
            input_policy = "read_only_no_suppression_no_injection"
        });
        ScheduleSnapshot("session_started", forceFull: true);
        Monitor.Log("Human demonstration started: " + active.SessionId, LogLevel.Info);
    }

    private void MarkCommand(string command, string[] args)
    {
        var session = active;
        if (session is null)
        {
            Monitor.Log("No active demonstration.", LogLevel.Warn);
            return;
        }
        if (args.Length == 0 || string.IsNullOrWhiteSpace(args[0]))
        {
            Monitor.Log("A method_id is required.", LogLevel.Warn);
            return;
        }
        session.Enqueue("intent_marked", new
        {
            method_id = args[0],
            option_id = args.ElementAtOrDefault(1) ?? string.Empty,
            game_anchor = GameAnchor()
        });
        ScheduleSnapshot("intent_marked", forceFull: false);
    }

    private void StopCommand(string command, string[] args) => StopActive("operator_stop");

    private void OnButtonsChanged(object? sender, ButtonsChangedEventArgs e)
    {
        var session = active;
        if (session is null || !Context.IsWorldReady)
            return;
        session.Enqueue("input_changed", new
        {
            pressed = e.Pressed.Select(value => value.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            held = e.Held.Select(value => value.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            released = e.Released.Select(value => value.ToString()).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            cursor = new
            {
                screen_x = e.Cursor.ScreenPixels.X,
                screen_y = e.Cursor.ScreenPixels.Y,
                tile_x = e.Cursor.Tile.X,
                tile_y = e.Cursor.Tile.Y,
                grab_tile_x = e.Cursor.GrabTile.X,
                grab_tile_y = e.Cursor.GrabTile.Y
            },
            game_anchor = GameAnchor()
        });
    }

    private void OnWarped(object? sender, WarpedEventArgs e)
    {
        if (active is null || !e.IsLocalPlayer)
            return;
        active.Enqueue("semantic_boundary", new
        {
            reason = "location_changed",
            old_location = e.OldLocation.NameOrUniqueName,
            new_location = e.NewLocation.NameOrUniqueName,
            game_anchor = GameAnchor()
        });
        ScheduleSnapshot("location_changed", forceFull: false);
    }

    private void OnInventoryChanged(object? sender, InventoryChangedEventArgs e)
    {
        if (active is null || !e.IsLocalPlayer)
            return;
        active.Enqueue("semantic_boundary", new
        {
            reason = "inventory_changed",
            added = e.Added.Select(ItemAnchor).ToArray(),
            removed = e.Removed.Select(ItemAnchor).ToArray(),
            quantity_changed = e.QuantityChanged.Select(change => new
            {
                item = ItemAnchor(change.Item),
                old_size = change.OldSize,
                new_size = change.NewSize
            }).ToArray(),
            game_anchor = GameAnchor()
        });
        ScheduleSnapshot("inventory_changed", forceFull: false);
    }

    private void OnMenuChanged(object? sender, MenuChangedEventArgs e)
    {
        if (active is null)
            return;
        active.Enqueue("semantic_boundary", new
        {
            reason = "menu_changed",
            old_menu = e.OldMenu?.GetType().FullName ?? "none",
            new_menu = e.NewMenu?.GetType().FullName ?? "none",
            game_anchor = GameAnchor()
        });
        ScheduleSnapshot("menu_changed", forceFull: false);
    }

    private void OnDayStarted(object? sender, DayStartedEventArgs e)
    {
        if (active is null)
            return;
        active.Enqueue("semantic_boundary", new
        {
            reason = "day_started",
            game_anchor = GameAnchor()
        });
        ScheduleSnapshot("day_started", forceFull: true);
    }

    private void OnTimeChanged(object? sender, TimeChangedEventArgs e)
    {
        if (active is null || !config.CaptureHourlyAnchor || e.NewTime % 100 != 0)
            return;
        active.Enqueue("clock_anchor", new
        {
            old_time = e.OldTime,
            new_time = e.NewTime,
            game_anchor = GameAnchor()
        });
        ScheduleSnapshot("hourly_anchor", forceFull: false);
    }

    private void OnSaving(object? sender, SavingEventArgs e)
    {
        if (active is null)
            return;
        active.Enqueue("semantic_boundary", new
        {
            reason = "saving",
            game_anchor = GameAnchor()
        });
        ScheduleSnapshot("saving", forceFull: true);
    }

    private void ScheduleSnapshot(string reason, bool forceFull)
    {
        var session = active;
        if (session is null || session.SnapshotCount >= config.MaximumSnapshotsPerSession)
            return;
        var tick = Context.IsWorldReady ? unchecked((long)Game1.ticks) : 0;
        if (!forceFull && tick - Interlocked.Read(ref lastSnapshotTick) < config.MinimumSnapshotIntervalTicks)
            return;
        if (Interlocked.CompareExchange(ref snapshotRequestActive, 1, 0) != 0)
            return;
        Interlocked.Exchange(ref lastSnapshotTick, tick);
        var task = CaptureSnapshotAsync(session, reason, forceFull);
        session.Track(task);
    }

    private async Task CaptureSnapshotAsync(RecordingSession session, string reason, bool forceFull)
    {
        try
        {
            var profile = forceFull ? "full" : ProfileForCurrentState();
            var separator = config.TransparentBridgeSnapshotEndpoint.Contains('?') ? '&' : '?';
            var url = config.TransparentBridgeSnapshotEndpoint + separator +
                "profile=" + Uri.EscapeDataString(profile) + "&fresh=1";
            var json = await snapshotClient.GetStringAsync(url).ConfigureAwait(false);
            using var document = JsonDocument.Parse(json);
            var stateHash = document.RootElement.TryGetProperty("state_hash", out var hash)
                ? hash.GetString() ?? string.Empty
                : string.Empty;
            var number = session.NextSnapshotNumber();
            var name = $"snapshot-{number:D5}-{Safe(reason)}.json";
            var path = Path.Combine(session.SnapshotRoot, name);
            var temporary = path + ".tmp";
            await File.WriteAllTextAsync(temporary, json + Environment.NewLine).ConfigureAwait(false);
            File.Move(temporary, path, true);
            session.Enqueue("snapshot_captured", new
            {
                reason,
                profile,
                state_hash = stateHash,
                path = Path.GetRelativePath(session.Root, path).Replace('\\', '/')
            });
        }
        catch (Exception ex)
        {
            session.Enqueue("snapshot_capture_failed", new
            {
                reason,
                error = ex.GetType().Name,
                message = ex.Message
            });
        }
        finally
        {
            Interlocked.Exchange(ref snapshotRequestActive, 0);
        }
    }

    private string ProfileForCurrentState()
    {
        if (!Context.IsWorldReady)
            return "identity";
        var locationType = Game1.currentLocation?.GetType().Name ?? string.Empty;
        if (locationType.Contains("Volcano", StringComparison.Ordinal))
            return "volcano";
        if (locationType.Contains("MineShaft", StringComparison.Ordinal))
            return "mining";
        if (Game1.player?.CurrentTool?.GetType().Name.Contains("Fishing", StringComparison.Ordinal) == true)
            return "fishing";
        return "daily";
    }

    private void StopActive(string reason)
    {
        var session = active;
        if (session is null)
            return;
        active = null;
        _ = Task.Run(async () =>
        {
            try
            {
                await session.FinalizeAsync(reason).ConfigureAwait(false);
                Monitor.Log("Human demonstration sealed: " + session.Root, LogLevel.Info);
            }
            catch (Exception ex)
            {
                Monitor.Log("Could not seal demonstration: " + ex, LogLevel.Error);
            }
        });
    }

    private static object GameAnchor()
    {
        if (!Context.IsWorldReady)
            return new { world_ready = false };
        var player = Game1.player;
        return new
        {
            world_ready = true,
            game_tick = unchecked((long)Game1.ticks),
            year = Game1.year,
            season = Game1.currentSeason,
            day = Game1.dayOfMonth,
            time = Game1.timeOfDay,
            location_id = Game1.currentLocation?.NameOrUniqueName ?? string.Empty,
            tile_x = player.TilePoint.X,
            tile_y = player.TilePoint.Y,
            facing = player.FacingDirection,
            stamina = player.Stamina,
            health = player.health,
            money = player.Money,
            selected_slot = player.CurrentToolIndex,
            selected_item = player.CurrentItem?.QualifiedItemId ?? string.Empty,
            using_tool = player.UsingTool,
            can_move = player.CanMove,
            active_menu = Game1.activeClickableMenu?.GetType().FullName ?? "none"
        };
    }

    private static object ItemAnchor(Item value) => new
    {
        qualified_item_id = value.QualifiedItemId,
        stack = value.Stack,
        quality = value.Quality,
        runtime_type = value.GetType().FullName ?? value.GetType().Name
    };

    private static string Safe(string value) => string.Concat(value.Select(character =>
        char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_'));
}
