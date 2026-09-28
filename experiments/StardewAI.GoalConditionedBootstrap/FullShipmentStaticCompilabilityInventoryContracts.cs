using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class FullShipmentStaticCompilabilityInventoryReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; init; } =
        "full_shipment_static_compilability_inventory.v1";

    [JsonPropertyName("status")]
    public string Status { get; init; } = "blocked";

    [JsonPropertyName("requirement_inventory_sha256")]
    public string RequirementInventorySha256 { get; init; } = string.Empty;

    [JsonPropertyName("acquisition_lowering_sha256")]
    public string AcquisitionLoweringSha256 { get; init; } = string.Empty;

    [JsonPropertyName("action_reconciliation_sha256")]
    public string ActionReconciliationSha256 { get; init; } = string.Empty;

    [JsonPropertyName("support_terminal_coverage_sha256")]
    public string SupportTerminalCoverageSha256 { get; init; } = string.Empty;

    [JsonPropertyName("required_group_count")]
    public int RequiredGroupCount { get; init; }

    [JsonPropertyName("route_occurrence_count")]
    public int RouteOccurrenceCount { get; init; }

    [JsonPropertyName("route_kind_count")]
    public int RouteKindCount { get; init; }

    [JsonPropertyName("authoritative_source_identity_count")]
    public int AuthoritativeSourceIdentityCount { get; init; }

    [JsonPropertyName("referenced_option_count")]
    public int ReferencedOptionCount { get; init; }

    [JsonPropertyName("source_identity_contract_complete")]
    public bool SourceIdentityContractComplete { get; init; }

    [JsonPropertyName("endpoint_option_compilation_complete")]
    public bool EndpointOptionCompilationComplete { get; init; }

    [JsonPropertyName("supporting_option_compilation_complete")]
    public bool SupportingOptionCompilationComplete { get; init; }

    [JsonPropertyName("support_terminal_lineage_complete")]
    public bool SupportTerminalLineageComplete { get; init; }

    [JsonPropertyName("all_requirement_groups_have_compilable_route")]
    public bool AllRequirementGroupsHaveCompilableRoute { get; init; }

    [JsonPropertyName("static_compilability_complete")]
    public bool StaticCompilabilityComplete { get; init; }

    [JsonPropertyName("fresh_save_recurrence_evidence_complete")]
    public bool FreshSaveRecurrenceEvidenceComplete { get; init; }

    [JsonPropertyName("formal_product_training_authorized")]
    public bool FormalProductTrainingAuthorized { get; init; }

    [JsonPropertyName("source_contracts")]
    public FullShipmentSourceContractInventoryRow[] SourceContracts
    { get; init; } = Array.Empty<FullShipmentSourceContractInventoryRow>();

    [JsonPropertyName("options")]
    public FullShipmentOptionCompilabilityRow[] Options { get; init; } =
        Array.Empty<FullShipmentOptionCompilabilityRow>();

    [JsonPropertyName("routes")]
    public FullShipmentRouteCompilabilityRow[] Routes { get; init; } =
        Array.Empty<FullShipmentRouteCompilabilityRow>();

    [JsonPropertyName("blocking_reasons")]
    public string[] BlockingReasons { get; init; } = Array.Empty<string>();

    [JsonPropertyName("remaining_evidence_gaps")]
    public string[] RemainingEvidenceGaps { get; init; } =
        Array.Empty<string>();

    [JsonPropertyName("inventory_policy")]
    public string InventoryPolicy { get; init; } =
        "This inventory reconciles every authoritative Full Shipment route with the exact runtime source-identity contract, registered candidate/compiler/runtime action bindings, and the controller-recomputed five-family supporting-transition lineage gate. Static completeness proves that a fresh live candidate can be compiled when its state prerequisites are satisfied. It does not prove that all 154 items were acquired and shipped on one fresh save, and it never authorizes formal product training.";
}

public sealed record FullShipmentSourceContractInventoryRow(
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("evidence_mode")] string EvidenceMode,
    [property: JsonPropertyName("evidence_fields")] string[] EvidenceFields,
    [property: JsonPropertyName("route_occurrence_count")] int RouteOccurrenceCount,
    [property: JsonPropertyName("authoritative_source_identity_count")] int AuthoritativeSourceIdentityCount,
    [property: JsonPropertyName("contract_complete")] bool ContractComplete);

public sealed record FullShipmentOptionCompilabilityRow(
    [property: JsonPropertyName("option_id")] string OptionId,
    [property: JsonPropertyName("roles")] string[] Roles,
    [property: JsonPropertyName("endpoint_occurrence_count")] int EndpointOccurrenceCount,
    [property: JsonPropertyName("supporting_occurrence_count")] int SupportingOccurrenceCount,
    [property: JsonPropertyName("registration_status")] string RegistrationStatus,
    [property: JsonPropertyName("read_status")] string ReadStatus,
    [property: JsonPropertyName("candidate_status")] string CandidateStatus,
    [property: JsonPropertyName("compiler_status")] string CompilerStatus,
    [property: JsonPropertyName("runtime_status")] string RuntimeStatus,
    [property: JsonPropertyName("runtime_binding")] string RuntimeBinding,
    [property: JsonPropertyName("static_compilation_ready")] bool StaticCompilationReady);

public sealed record FullShipmentRouteCompilabilityRow(
    [property: JsonPropertyName("route_occurrence_id")] string RouteOccurrenceId,
    [property: JsonPropertyName("requirement_id")] string RequirementId,
    [property: JsonPropertyName("qualified_item_id")] string QualifiedItemId,
    [property: JsonPropertyName("route_kind")] string RouteKind,
    [property: JsonPropertyName("source_id")] string SourceId,
    [property: JsonPropertyName("source_evidence_mode")] string SourceEvidenceMode,
    [property: JsonPropertyName("endpoint_option_ids")] string[] EndpointOptionIds,
    [property: JsonPropertyName("supporting_option_ids")] string[] SupportingOptionIds,
    [property: JsonPropertyName("inline_support_transition_kinds")] string[] InlineSupportTransitionKinds,
    [property: JsonPropertyName("source_identity_ready")] bool SourceIdentityReady,
    [property: JsonPropertyName("endpoint_options_ready")] bool EndpointOptionsReady,
    [property: JsonPropertyName("supporting_options_ready")] bool SupportingOptionsReady,
    [property: JsonPropertyName("inline_support_lineage_ready")] bool InlineSupportLineageReady,
    [property: JsonPropertyName("static_compilation_ready")] bool StaticCompilationReady,
    [property: JsonPropertyName("blocking_reasons")] string[] BlockingReasons);
