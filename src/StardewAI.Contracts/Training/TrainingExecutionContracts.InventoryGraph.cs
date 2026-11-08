using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed partial class TrainingExecutionRequest
{
    [JsonPropertyName("action_origin_inventory_graph")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool ActionOriginInventoryGraph { get; set; }
}

public sealed partial class ActionOriginInventoryPayload
{
    [JsonPropertyName("graph_state")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ActionOriginInventoryGraphState? GraphState { get; set; }
}

public sealed class ActionOriginInventoryGraphState
{
    [JsonPropertyName("schema_version")] public string SchemaVersion { get; set; } = string.Empty;
    [JsonPropertyName("actor_reference_id")] public string? ActorReferenceId { get; set; }
    [JsonPropertyName("nodes")] public ActionOriginInventoryGraphNode[]? Nodes { get; set; }
    [JsonPropertyName("edges")] public ActionOriginInventoryGraphEdge[]? Edges { get; set; }
    [JsonPropertyName("actor_enchantments")] public ActionOriginGraphReference[]? ActorEnchantments { get; set; }
    [JsonPropertyName("node_occurrences")] public int? NodeOccurrences { get; set; }
    [JsonPropertyName("edge_occurrences")] public int? EdgeOccurrences { get; set; }
}

public sealed class ActionOriginInventoryGraphNode
{
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
    [JsonPropertyName("runtime_type")] public string? RuntimeType { get; set; }
    [JsonPropertyName("kind")] public string? Kind { get; set; }
    [JsonPropertyName("qualified_item_id")] public string? QualifiedItemId { get; set; }
    [JsonPropertyName("is_tool")] public bool? IsTool { get; set; }
    [JsonPropertyName("quality")] public int? Quality { get; set; }
    [JsonPropertyName("stack")] public int? Stack { get; set; }
    // Root Item raw state is supplied by its existing slot; nested-only Items carry it here.
    [JsonPropertyName("item_state")] public ActionOriginItemTransientState? ItemState { get; set; }
    [JsonPropertyName("enchantment_state")] public ActionOriginEnchantmentTransientState? EnchantmentState { get; set; }
    [JsonPropertyName("attachment_count")] public int? AttachmentCount { get; set; }
    [JsonPropertyName("enchantment_count")] public int? EnchantmentCount { get; set; }
    [JsonPropertyName("combined_ring_count")] public int? CombinedRingCount { get; set; }
}

public sealed class ActionOriginEnchantmentTransientState
{
    [JsonPropertyName("applied")] public bool? Applied { get; set; }
    [JsonPropertyName("display_name")] public ActionOriginNullableText? DisplayName { get; set; }
    [JsonPropertyName("level")] public int? Level { get; set; }
}

public sealed class ActionOriginInventoryGraphEdge
{
    [JsonPropertyName("parent_reference_id")] public string? ParentReferenceId { get; set; }
    [JsonPropertyName("role")] public string? Role { get; set; }
    [JsonPropertyName("ordinal")] public int? Ordinal { get; set; }
    [JsonPropertyName("target")] public ActionOriginGraphReference? Target { get; set; }
}

public sealed class ActionOriginGraphReference
{
    [JsonPropertyName("is_null")] public bool? IsNull { get; set; }
    [JsonPropertyName("reference_id")] public string? ReferenceId { get; set; }
}
