using HarmonyLib;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.TerrainFeatures;

namespace StardewAI.RuntimeTestHarness;

public sealed class ModEntry : Mod
{
    private HarnessConfig config = new();
    private int ticksSeen;
    private bool loadAttempted;
    private HttpListener? executorListener;
    private CancellationTokenSource? executorCancellation;
    private readonly ConcurrentQueue<PendingExecution> pendingExecutions = new();

    public override void Entry(IModHelper helper)
    {
        config = helper.ReadConfig<HarnessConfig>();
        ApplyEnvironmentOverrides();

        if (string.IsNullOrWhiteSpace(config.SavesPath))
        {
            Monitor.Log("Runtime harness disabled: SavesPath is empty.", LogLevel.Warn);
            return;
        }

        config.SavesPath = Path.GetFullPath(config.SavesPath);
        Directory.CreateDirectory(config.SavesPath);

        SavesFolderPatch.RedirectPath = config.SavesPath;
        new Harmony(ModManifest.UniqueID).Patch(
            original: AccessTools.Method("StardewValley.Program:GetSavesFolder"),
            postfix: new HarmonyMethod(typeof(SavesFolderPatch), nameof(SavesFolderPatch.Postfix)));

        Monitor.Log($"Redirected Stardew save folder to {config.SavesPath}", LogLevel.Info);

        if (config.AutoLoad)
        {
            helper.Events.GameLoop.UpdateTicked += OnUpdateTicked;
        }

        if (config.EnableTrainingExecutor)
        {
            helper.Events.GameLoop.UpdateTicked += OnExecutorUpdateTicked;
            StartTrainingExecutorServer();
        }
    }

    private void ApplyEnvironmentOverrides()
    {
        var savesPath = Environment.GetEnvironmentVariable("STARDEWAI_TEST_SAVES");
        if (!string.IsNullOrWhiteSpace(savesPath))
        {
            config.SavesPath = savesPath;
        }

        var slotName = Environment.GetEnvironmentVariable("STARDEWAI_TEST_SLOT");
        if (!string.IsNullOrWhiteSpace(slotName))
        {
            config.SlotName = slotName;
        }
    }

    private void OnUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (loadAttempted || Context.IsWorldReady || Game1.gameMode != 0)
        {
            return;
        }

        ticksSeen++;
        if (ticksSeen < config.LoadAfterTicks)
        {
            return;
        }

        loadAttempted = true;
        if (string.IsNullOrWhiteSpace(config.SlotName))
        {
            Monitor.Log("AutoLoad skipped: SlotName is empty.", LogLevel.Warn);
            return;
        }

        var slotPath = Path.Combine(config.SavesPath, config.SlotName);
        if (!Directory.Exists(slotPath))
        {
            Monitor.Log($"AutoLoad skipped: save slot not found at {slotPath}", LogLevel.Error);
            return;
        }

        Monitor.Log($"Loading isolated test save slot {config.SlotName}", LogLevel.Info);
        SaveGame.Load(config.SlotName);
        Game1.exitActiveMenu();
    }

    private void StartTrainingExecutorServer()
    {
        if (!HttpListener.IsSupported)
        {
            Monitor.Log("Training executor disabled: HttpListener is not supported.", LogLevel.Warn);
            return;
        }

        executorCancellation = new CancellationTokenSource();
        executorListener = new HttpListener();
        executorListener.Prefixes.Add($"http://{config.ExecutorHost}:{config.ExecutorPort}/");
        executorListener.Start();
        Monitor.Log($"Training executor listening on http://{config.ExecutorHost}:{config.ExecutorPort}/", LogLevel.Info);
        _ = Task.Run(() => ServeTrainingExecutorAsync(executorCancellation.Token));
    }

    private async Task ServeTrainingExecutorAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && executorListener is not null)
        {
            try
            {
                var context = await executorListener.GetContextAsync();
                _ = Task.Run(() => HandleExecutorRequestAsync(context), cancellationToken);
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
                Monitor.Log($"Training executor server loop failed: {ex}", LogLevel.Error);
            }
        }
    }

    private async Task HandleExecutorRequestAsync(HttpListenerContext context)
    {
        var path = context.Request.Url?.AbsolutePath.TrimEnd('/') ?? "/";
        if (context.Request.HttpMethod == "GET" && path == "/health")
        {
            await WriteJsonAsync(context, 200, new { status = "ok", service = "StardewAI.RuntimeTestHarness.Executor" });
            return;
        }

        if (context.Request.HttpMethod != "POST" || path != "/api/v1/training/execute")
        {
            await WriteJsonAsync(context, 404, new { error = "not_found", path });
            return;
        }

        TrainingExecutionRequest? request;
        try
        {
            using var reader = new StreamReader(context.Request.InputStream, context.Request.ContentEncoding);
            request = JsonSerializer.Deserialize<TrainingExecutionRequest>(await reader.ReadToEndAsync(), JsonOptions);
        }
        catch (JsonException ex)
        {
            await WriteJsonAsync(context, 400, new { error = "invalid_json", detail = ex.Message });
            return;
        }

        if (request is null)
        {
            await WriteJsonAsync(context, 400, new { error = "empty_request" });
            return;
        }

        var pending = new PendingExecution(request);
        pendingExecutions.Enqueue(pending);
        var completed = await Task.WhenAny(pending.Completion.Task, Task.Delay(TimeSpan.FromSeconds(10)));
        if (completed != pending.Completion.Task)
        {
            await WriteJsonAsync(context, 504, new { error = "training_execution_timeout" });
            return;
        }

        await WriteJsonAsync(context, 200, pending.Completion.Task.Result);
    }

    private void OnExecutorUpdateTicked(object? sender, UpdateTickedEventArgs e)
    {
        if (!pendingExecutions.TryDequeue(out var pending))
        {
            return;
        }

        try
        {
            pending.Completion.SetResult(ExecuteMaintainCrops(pending.Request));
        }
        catch (Exception ex)
        {
            Monitor.Log($"Training execution failed: {ex}", LogLevel.Error);
            pending.Completion.SetResult(Blocked(pending.Request, "execution_exception:" + ex.GetType().Name));
        }
    }

    private TrainingExecutionResult ExecuteMaintainCrops(TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }

        var started = DateTimeOffset.UtcNow.ToString("O");
        var energyBefore = Game1.player.Stamina;
        var changed = new List<SimulatedFactChange>();
        var watered = 0;
        var farm = Game1.getFarm();
        var limit = Math.Clamp(request.MaxCrops, 1, 1024);

        foreach (var pair in farm.terrainFeatures.Pairs.OrderBy(item => item.Key.Y).ThenBy(item => item.Key.X))
        {
            if (watered >= limit)
            {
                break;
            }

            if (pair.Value is not HoeDirt dirt || dirt.crop is null || !dirt.needsWatering())
            {
                continue;
            }

            var label = ((int)pair.Key.X) + "," + ((int)pair.Key.Y);
            dirt.state.Value = HoeDirt.watered;
            watered++;
            changed.Add(new SimulatedFactChange
            {
                Path = "farm.crops[" + label + "].needs_watering",
                Before = "true",
                After = "false"
            });
            changed.Add(new SimulatedFactChange
            {
                Path = "farm.crops[" + label + "].watered",
                Before = "false",
                After = "true"
            });
        }

        if (watered > 0)
        {
            Game1.player.Stamina = Math.Max(0, Game1.player.Stamina - watered * 2);
        }

        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = watered > 0 ? "applied" : "no_op",
            FeedbackAvailable = true,
            WateredCount = watered,
            EnergyBefore = energyBefore,
            EnergyAfter = Game1.player.Stamina,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            ChangedFacts = changed.ToArray()
        };
    }

    private List<string> ValidateExecutionRequest(TrainingExecutionRequest request)
    {
        var reasons = new List<string>();
        if (request.SchemaVersion != "training_execution_request.v1")
        {
            reasons.Add("unsupported_schema_version");
        }

        if (Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_MODE") != "1")
        {
            reasons.Add("training_mode_env_required");
        }

        var expectedRunId = Environment.GetEnvironmentVariable("STARDEWAI_TRAINING_RUN_ID") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(request.RunId) || request.RunId != expectedRunId)
        {
            reasons.Add("run_id_mismatch");
        }

        var expectedSavePath = Path.GetFullPath(Environment.GetEnvironmentVariable("STARDEWAI_SAVE_ISOLATION_PATH") ?? config.SavesPath);
        var requestedSavePath = string.IsNullOrWhiteSpace(request.SaveIsolationPath)
            ? string.Empty
            : Path.GetFullPath(request.SaveIsolationPath);
        if (string.IsNullOrWhiteSpace(requestedSavePath) ||
            !string.Equals(requestedSavePath, expectedSavePath, StringComparison.OrdinalIgnoreCase))
        {
            reasons.Add("save_isolation_path_mismatch");
        }

        if (!Context.IsWorldReady)
        {
            reasons.Add("world_not_ready");
        }

        if (request.OptionId != "farm.maintain_crops")
        {
            reasons.Add("unsupported_option_id");
        }

        if (request.ExecutionMode != "training_singleplayer")
        {
            reasons.Add("unsupported_execution_mode");
        }

        return reasons;
    }

    private static TrainingExecutionResult Blocked(TrainingExecutionRequest request, params string[] reasons)
    {
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = "blocked",
            FeedbackAvailable = false,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            BlockReasons = reasons
        };
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, int statusCode, object response)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";
        var bytes = JsonSerializer.SerializeToUtf8Bytes(response, JsonOptions);
        await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length);
        context.Response.Close();
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private sealed class PendingExecution
    {
        public PendingExecution(TrainingExecutionRequest request)
        {
            Request = request;
        }

        public TrainingExecutionRequest Request { get; }
        public TaskCompletionSource<TrainingExecutionResult> Completion { get; } = new();
    }
}

internal static class SavesFolderPatch
{
    public static string? RedirectPath { get; set; }

    public static void Postfix(ref string __result)
    {
        if (string.IsNullOrWhiteSpace(RedirectPath))
        {
            return;
        }

        Directory.CreateDirectory(RedirectPath);
        __result = RedirectPath;
    }
}
