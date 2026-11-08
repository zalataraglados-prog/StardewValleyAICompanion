using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

// Observed false/zero differs from an unavailable field. These are raw caches/counters.
public sealed class ActionOriginObjectTransientState
{
    [JsonPropertyName("has_held_object")] public bool? HasHeldObject { get; set; }
    [JsonPropertyName("has_light_source")] public bool? HasLightSource { get; set; }
    [JsonPropertyName("shake_timer")] public int? ShakeTimer { get; set; }
    [JsonPropertyName("health")] public int? Health { get; set; }
}
