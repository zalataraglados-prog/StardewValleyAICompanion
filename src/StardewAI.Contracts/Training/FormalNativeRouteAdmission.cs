using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed record FormalNativeRouteProofReference(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("selected_route_path")] string SelectedRoutePath,
    [property: JsonPropertyName("selected_route_sha256")] string SelectedRouteSha256,
    [property: JsonPropertyName("native_evidence")] GoldenWalnutNativeEvidenceBinding? NativeEvidence);

public sealed record FormalNativeRouteGate(
    [property: JsonPropertyName("gate_id")] string GateId,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("reason")] string Reason)
{
    [JsonIgnore] public bool Satisfied => Status is "verified" or "not_applicable";
}

public sealed record FormalNativeRouteAssessment(
    [property: JsonPropertyName("route_proof")] FormalNativeRouteProofReference? RouteProof,
    [property: JsonPropertyName("source_replay_verified")] bool SourceReplayVerified,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("gates")] FormalNativeRouteGate[] Gates)
{
    [JsonPropertyName("proof_scope")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ProofScope { get; init; }
}
