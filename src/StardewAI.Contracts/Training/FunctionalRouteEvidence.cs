using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Training;

public sealed record FrozenRouteArtifact(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record FunctionalNativeActionReference(
    [property: JsonPropertyName("queue")] FrozenRouteArtifact Queue,
    [property: JsonPropertyName("receipt")] FrozenRouteArtifact Receipt,
    [property: JsonPropertyName("after_snapshot")] FrozenRouteArtifact AfterSnapshot,
    [property: JsonPropertyName("before_snapshot")] FrozenRouteArtifact BeforeSnapshot,
    [property: JsonPropertyName("request_journal")] FrozenRouteArtifact RequestJournal,
    [property: JsonPropertyName("request_gzip")] FrozenRouteArtifact RequestGzip);

// Functional sample time is retained as evidence. It is not the calendar of
// a future three-year rollout, nor a certificate of acquiring prerequisites.
public sealed record FunctionalRouteEvidence(
    [property: JsonPropertyName("schema_version")] string SchemaVersion,
    [property: JsonPropertyName("snapshot")] FrozenRouteArtifact Snapshot,
    [property: JsonPropertyName("compile_request")] FrozenRouteArtifact CompileRequest,
    [property: JsonPropertyName("compile_response")] FrozenRouteArtifact CompileResponse,
    [property: JsonPropertyName("target_qualified_item_id")] string TargetQualifiedItemId,
    [property: JsonPropertyName("native_actions")] FunctionalNativeActionReference[] NativeActions);
