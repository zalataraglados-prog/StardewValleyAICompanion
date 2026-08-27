using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("dwarf_statue_power_id")]
    public int? DwarfStatuePowerId { get; set; }

    [JsonPropertyName("dwarf_statue_power_source")]
    public string DwarfStatuePowerSource { get; set; } = string.Empty;

    [JsonPropertyName("dwarf_statue_menu_index")]
    public int? DwarfStatueMenuIndex { get; set; }

    [JsonPropertyName("dwarf_statue_buff_id")]
    public string DwarfStatueBuffId { get; set; } = string.Empty;

    [JsonPropertyName("dwarf_statue_display_text")]
    public string DwarfStatueDisplayText { get; set; } = string.Empty;

    [JsonPropertyName("dwarf_statue_effect_kind")]
    public string DwarfStatueEffectKind { get; set; } = string.Empty;

    [JsonPropertyName("dwarf_statue_exact_effect")]
    public string DwarfStatueExactEffect { get; set; } = string.Empty;

    [JsonPropertyName("dwarf_statue_offered_power_ids_csv")]
    public string DwarfStatueOfferedPowerIdsCsv { get; set; } = string.Empty;

    [JsonPropertyName("dwarf_statue_days_played")]
    public int? DwarfStatueDaysPlayed { get; set; }
}
