using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_tool_inputs")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginToolInputs { get; set; }
}

public sealed partial class ActionOriginObservationReceipt
{
    [JsonPropertyName("clearance_tool_calls")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginClearanceToolCall[]? ClearanceToolCalls { get; set; }
}

public sealed class ActionOriginClearanceToolCall
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("ordinal")] public int Ordinal { get; set; }
    [JsonPropertyName("tool_reference_id")] public string ToolReferenceId { get; set; } = string.Empty;
    [JsonPropertyName("tool_runtime_type")] public string ToolRuntimeType { get; set; } = string.Empty;
    [JsonPropertyName("qualified_tool_id")] public string QualifiedToolId { get; set; } = string.Empty;
    [JsonPropertyName("actor_id")] public string ActorId { get; set; } = string.Empty;
    [JsonPropertyName("location_id")] public string LocationId { get; set; } = string.Empty;
    [JsonPropertyName("x")] public int? X { get; set; }
    [JsonPropertyName("y")] public int? Y { get; set; }
    [JsonPropertyName("argument_power")] public int? ArgumentPower { get; set; }
    [JsonPropertyName("native_power")] public int? NativePower { get; set; }
    [JsonPropertyName("before_game_tick")] public long? BeforeGameTick { get; set; }
    [JsonPropertyName("input_game_tick")] public long? InputGameTick { get; set; }
    [JsonPropertyName("debit_game_tick")] public long? DebitGameTick { get; set; }
    [JsonPropertyName("after_game_tick")] public long? AfterGameTick { get; set; }
    [JsonPropertyName("stamina_at_entry")] public float? StaminaAtEntry { get; set; }
    [JsonPropertyName("stamina_before_debit")] public float? StaminaBeforeDebit { get; set; }
    [JsonPropertyName("stamina_after_debit")] public float? StaminaAfterDebit { get; set; }
    [JsonPropertyName("stamina_at_exit")] public float? StaminaAtExit { get; set; }
    [JsonPropertyName("max_stamina")] public int? MaxStamina { get; set; }
    [JsonPropertyName("effective_skill_level")] public int? EffectiveSkillLevel { get; set; }
    [JsonPropertyName("is_efficient")] public bool? IsEfficient { get; set; }
    [JsonPropertyName("has_stamina_decrease_protection")] public bool? HasStaminaDecreaseProtection { get; set; }
    [JsonPropertyName("stamina_setter_invoked")] public bool? StaminaSetterInvoked { get; set; }
}
