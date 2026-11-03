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
            if (!StardewValley.Game1.paused || modRegistry?.IsLoaded("StardewAI.RuntimeTestHarness") != true)
                return null;
            var api = modRegistry?.GetApi<IExecutorPlanningPauseReadApi>("StardewAI.RuntimeTestHarness");
            if (api is null) return null;
            var json = api.ReadPlanningPauseEvidence();
            if (string.IsNullOrWhiteSpace(json) || json.Length > 4096) return null;
            using var document = JsonDocument.Parse(json);
            return document.RootElement.ValueKind == JsonValueKind.Object
                ? document.RootElement.Clone() : null;
        }
        catch (Exception)
        {
            // An absent or incompatible optional API cannot authorize pause recovery.
            return null;
        }
    }
}
