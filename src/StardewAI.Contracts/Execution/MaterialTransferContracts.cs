using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Execution;

public sealed class MaterialTransferIntent
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "material_transfer_intent.v1";

    [JsonPropertyName("source_node_id")]
    public string SourceNodeId { get; set; } = string.Empty;

    [JsonPropertyName("destination_node_id")]
    public string DestinationNodeId { get; set; } = string.Empty;

    [JsonPropertyName("source_slot_index")]
    public int SourceSlotIndex { get; set; }

    [JsonPropertyName("qualified_item_id")]
    public string QualifiedItemId { get; set; } = string.Empty;

    [JsonPropertyName("quality")]
    public int Quality { get; set; }

    [JsonPropertyName("quantity")]
    public int Quantity { get; set; }

    [JsonPropertyName("expected_source_stack")]
    public int ExpectedSourceStack { get; set; }
}

public sealed class MaterialTransferProjection
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "material_transfer_projection.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "blocked";

    [JsonPropertyName("native_branch")]
    public string NativeBranch { get; set; } = string.Empty;

    [JsonPropertyName("source_stack_after")]
    public int? SourceStackAfter { get; set; }

    [JsonPropertyName("destination_quantity_before")]
    public int DestinationQuantityBefore { get; set; }

    [JsonPropertyName("destination_quantity_after")]
    public int DestinationQuantityAfter { get; set; }

    [JsonPropertyName("destination_slot_changes")]
    public MaterialTransferSlotChange[] DestinationSlotChanges { get; set; } =
        Array.Empty<MaterialTransferSlotChange>();

    [JsonPropertyName("native_slot_transitions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public MaterialTransferNativeSlotTransition[]? NativeSlotTransitions { get; set; }

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; set; } = Array.Empty<string>();
}

public sealed class MaterialTransferNativeSlotTransition
{
    [JsonPropertyName("node_id")] public string NodeId { get; set; } = string.Empty;
    [JsonPropertyName("before_slot_index"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int? BeforeSlotIndex { get; set; }
    [JsonPropertyName("after_slot_index"), JsonIgnore(Condition = JsonIgnoreCondition.Never)] public int? AfterSlotIndex { get; set; }
    [JsonPropertyName("qualified_item_id")] public string QualifiedItemId { get; set; } = string.Empty;
    [JsonPropertyName("quality")] public int Quality { get; set; }
    [JsonPropertyName("runtime_type")] public string RuntimeType { get; set; } = string.Empty;
    [JsonPropertyName("stack_before")] public int StackBefore { get; set; }
    [JsonPropertyName("stack_after")] public int StackAfter { get; set; }
}

public sealed class MaterialTransferSlotChange
{
    [JsonPropertyName("slot_index")]
    public int SlotIndex { get; set; }

    [JsonPropertyName("stack_before")]
    public int StackBefore { get; set; }

    [JsonPropertyName("stack_after")]
    public int StackAfter { get; set; }
}
