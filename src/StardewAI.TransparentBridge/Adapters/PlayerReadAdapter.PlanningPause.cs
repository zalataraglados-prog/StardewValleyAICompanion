using System.Text.Json;

namespace StardewAI.TransparentBridge.Adapters;

public interface IExecutorPlanningPauseReadApi
{
    string ReadPlanningPauseEvidence();
}

public sealed partial class PlayerReadAdapter
{
    private JsonElement? ReadExecutorOwnedIdlePause()
    {
        try
        {
            if (!StardewValley.Game1.paused) return null;
            if (modRegistry?.IsLoaded("StardewAI.RuntimeTestHarness") != true)
                return UnavailablePlanningPause("owner_not_loaded");
            var api = modRegistry?.GetApi<IExecutorPlanningPauseReadApi>("StardewAI.RuntimeTestHarness");
            if (api is null) return UnavailablePlanningPause("api_unavailable");
            var json = api.ReadPlanningPauseEvidence();
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4096)
                return UnavailablePlanningPause("api_payload_invalid");
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone() : UnavailablePlanningPause("api_payload_invalid");
        }
        catch (Exception exception)
        {
            var detail = exception.GetType().Name + ":" + exception.Message;
            return UnavailablePlanningPause("api_read_failed", detail.Length > 512 ? detail[..512] : detail);
        }
    }

    private static JsonElement UnavailablePlanningPause(string status, string? detail = null) =>
        JsonSerializer.SerializeToElement(new { schema_version = "executor_idle_pause_probe.v1", status, detail });
}
