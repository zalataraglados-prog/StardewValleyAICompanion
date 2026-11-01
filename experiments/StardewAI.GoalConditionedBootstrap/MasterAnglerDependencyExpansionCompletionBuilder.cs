using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace StardewAI.GoalConditionedBootstrap;

/// <summary>
/// Produces a run-local dependency expansion whose Master Angler root is closed by
/// a replayed, hash-bound fresh-save prerequisite proof.
/// </summary>
public static class MasterAnglerDependencyExpansionCompletionBuilder
{
    public const string DirectionId = "complete_master_angler";

    public static JsonObject Build(
        string completionProofPath,
        string dependencyExpansionPath,
        string expectedGoalId,
        string expectedGameVersion)
    {
        Require(!string.IsNullOrWhiteSpace(expectedGoalId) &&
                !string.IsNullOrWhiteSpace(expectedGameVersion),
            "Explicit goal and game identities are required.");
        var proofFullPath = Path.GetFullPath(completionProofPath);
        var dependencyFullPath = Path.GetFullPath(dependencyExpansionPath);
        var proofBytes = File.ReadAllBytes(proofFullPath);
        var proofNode = ParseUniqueObject(proofBytes, "Completion proof");
        var proof = proofNode.Deserialize<
            MasterAnglerFreshSavePrerequisiteCompletionProof>() ??
            throw new InvalidDataException("Completion proof is null.");
        Require(proof.SchemaVersion ==
                "master_angler_fresh_save_prerequisite_completion_proof.v1" &&
                proof.Ready && proof.GoalId == expectedGoalId &&
                proof.GameVersion == expectedGameVersion &&
                !proof.TrainingLabelEligible &&
                proof.BlockingReasons is { Length: 0 } &&
                proof.NativeDenominatorCount ==
                    MasterAnglerFreshSavePrerequisiteCompletionProofBuilder
                        .ExpectedNativeDenominatorCount &&
                proof.PrerequisiteFamilyCount ==
                    MasterAnglerFreshSavePrerequisiteCompletionProofBuilder
                        .ExpectedPrerequisiteFamilyCount &&
                proof.VerifiedGourmandTransitionCount == 3,
            "Completion proof is not ready for dependency closure.");
        Require(Path.GetFullPath(proof.DependencyExpansionPath) ==
                    dependencyFullPath &&
                string.Equals(proof.DependencyExpansionSha256,
                    Hash(File.ReadAllBytes(dependencyFullPath)),
                    StringComparison.OrdinalIgnoreCase),
            "Completion proof does not bind the supplied dependency expansion.");

        var auditFullPath = Path.GetFullPath(proof.AuditPath);
        var audit = JsonSerializer.Deserialize<
            MasterAnglerFreshSavePrerequisiteAuditReport>(
            File.ReadAllBytes(auditFullPath)) ??
            throw new InvalidDataException("Completion-proof audit is null.");
        var rebuilt =
            MasterAnglerFreshSavePrerequisiteCompletionProofBuilder.Build(
                audit,
                auditFullPath,
                dependencyFullPath,
                expectedGoalId,
                expectedGameVersion);
        Require(JsonNode.DeepEquals(
                JsonSerializer.SerializeToNode(rebuilt), proofNode),
            "Completion proof does not match an independent replay.");

        var root = ParseUniqueObject(
            File.ReadAllBytes(dependencyFullPath),
            "Dependency expansion");
        Require(root["schema_version"]?.GetValue<string>() ==
                    "goal_method_dependency_expansions.v1" &&
                root["goal_id"]?.GetValue<string>() == expectedGoalId,
            "Dependency expansion schema or goal identity mismatch.");
        var directions = root["directions"]?.AsArray() ??
            throw new InvalidDataException(
                "Dependency expansion directions are missing.");
        var matchingDirections = directions
            .Where(value => value?["direction_id"]?.GetValue<string>() ==
                            DirectionId)
            .Select(value => value!.AsObject())
            .ToArray();
        Require(matchingDirections.Length == 1,
            "Dependency expansion must contain exactly one Master Angler direction.");
        var direction = matchingDirections[0];
        Require(direction["completion_node_id"]?.GetValue<string>() ==
                    "dependency.complete_master_angler.all_native_species_caught",
            "Master Angler completion node identity drifted.");
        var nodes = direction["nodes"]?.AsArray() ??
            throw new InvalidDataException("Master Angler nodes are missing.");
        var requiredNodeIds = new[]
        {
            "dependency.complete_master_angler.fresh_save_prerequisite_inventory",
            "dependency.complete_master_angler.target_date_opportunity_producer",
            "dependency.complete_master_angler.trap_lifecycle_intent",
            "dependency.complete_master_angler.current_legal_rod_attempt",
            "dependency.complete_master_angler.current_ready_trap_collection",
            "dependency.complete_master_angler.acquisition_recurrence",
            "dependency.complete_master_angler.all_native_species_caught"
        };
        var nodesById = nodes
            .Select(value => value?.AsObject() ??
                throw new InvalidDataException("Master Angler node is null."))
            .ToDictionary(
                value => value["id"]?.GetValue<string>() ?? string.Empty,
                StringComparer.Ordinal);
        Require(requiredNodeIds.All(nodesById.ContainsKey),
            "Master Angler dependency closure nodes are incomplete.");

        direction["status"] = "complete";
        direction["blockers"] = new JsonArray();
        var inventoryRule = nodesById[requiredNodeIds[0]]["rule"]?.AsObject() ??
            throw new InvalidDataException(
                "Master Angler prerequisite inventory rule is missing.");
        inventoryRule["status"] =
            MasterAnglerFreshSavePrerequisiteCompletionProofBuilder
                .CompleteAuditStatus;
        inventoryRule["completion_proof_path"] = proofFullPath;
        inventoryRule["completion_proof_sha256"] = Hash(proofBytes);
        inventoryRule["training_label_eligible"] = false;
        return root;
    }

    private static JsonObject ParseUniqueObject(byte[] bytes, string label)
    {
        using var document = JsonDocument.Parse(bytes);
        RequireUniqueProperties(document.RootElement);
        Require(document.RootElement.ValueKind == JsonValueKind.Object,
            label + " must be an object.");
        return JsonNode.Parse(bytes)?.AsObject() ??
            throw new InvalidDataException(label + " is null.");
    }

    private static void RequireUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Require(names.Add(property.Name),
                    "Evidence contains duplicate properties.");
                RequireUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
                RequireUniqueProperties(item);
        }
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}
