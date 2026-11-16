using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

// The original owner's accepted readback, not another game action or a recovery command.
public sealed record MoneyRevenueCropAcceptedSavedReadbackReference(
    [property: JsonPropertyName("owner_sequence")] int OwnerSequence,
    [property: JsonPropertyName("admission_path")] string AdmissionPath,
    [property: JsonPropertyName("acceptance_path")] string AcceptancePath,
    [property: JsonPropertyName("observation_path")] string ObservationPath,
    [property: JsonPropertyName("input_artifact_hashes")] IReadOnlyDictionary<string, string> InputArtifactHashes);

public sealed record CropTeacherAcceptedSavedReadbackBoundary(string PersistentStateSha256, string[] AcceptanceSha256s);
