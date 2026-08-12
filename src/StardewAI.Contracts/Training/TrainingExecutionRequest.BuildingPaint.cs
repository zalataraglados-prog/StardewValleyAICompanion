using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("paint_data_key")] public string PaintDataKey { get; set; } = string.Empty;
    [JsonPropertyName("paint_region_count")] public int? PaintRegionCount { get; set; }
    [JsonPropertyName("paint_region_index")] public int? PaintRegionIndex { get; set; }
    [JsonPropertyName("paint_region_id")] public string PaintRegionId { get; set; } = string.Empty;
    [JsonPropertyName("current_paint_default")] public bool CurrentPaintDefault { get; set; }
    [JsonPropertyName("current_hue")] public int? CurrentPaintHue { get; set; }
    [JsonPropertyName("current_saturation")] public int? CurrentPaintSaturation { get; set; }
    [JsonPropertyName("current_lightness")] public int? CurrentPaintLightness { get; set; }
    [JsonPropertyName("hue_min")] public int? PaintHueMin { get; set; }
    [JsonPropertyName("hue_max")] public int? PaintHueMax { get; set; }
    [JsonPropertyName("saturation_min")] public int? PaintSaturationMin { get; set; }
    [JsonPropertyName("saturation_max")] public int? PaintSaturationMax { get; set; }
    [JsonPropertyName("lightness_min")] public int? PaintLightnessMin { get; set; }
    [JsonPropertyName("lightness_max")] public int? PaintLightnessMax { get; set; }
    [JsonPropertyName("native_slider_logical_width")] public int? NativePaintSliderLogicalWidth { get; set; }
    [JsonPropertyName("paint_target_mode")] public string PaintTargetMode { get; set; } = string.Empty;
    [JsonPropertyName("target_hue")] public int? TargetPaintHue { get; set; }
    [JsonPropertyName("target_saturation")] public int? TargetPaintSaturation { get; set; }
    [JsonPropertyName("target_lightness")] public int? TargetPaintLightness { get; set; }
}
