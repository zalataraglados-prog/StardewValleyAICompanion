using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

/// <summary>Binds an upstream prerequisite inventory, not catch outcomes or training admission.</summary>
public static class MasterAnglerFreshSavePrerequisiteCompletionProofBuilder
{
    public const string CompleteAuditStatus = "fresh_save_prerequisite_inventory_complete";
    public const int ExpectedNativeDenominatorCount = 72;
    public const int ExpectedPrerequisiteFamilyCount = 14;

    public static MasterAnglerFreshSavePrerequisiteCompletionProof Build(
        MasterAnglerFreshSavePrerequisiteAuditReport audit, string auditPath,
        string dependencyExpansionPath, string expectedGoalId, string expectedGameVersion) =>
        BuildCore(audit ?? throw new ArgumentNullException(nameof(audit)), auditPath,
            dependencyExpansionPath, expectedGoalId, expectedGameVersion);

    private static MasterAnglerFreshSavePrerequisiteCompletionProof BuildCore(
        MasterAnglerFreshSavePrerequisiteAuditReport suppliedAudit, string auditPath,
        string dependencyExpansionPath, string expectedGoalId, string expectedGameVersion)
    {
        Require(!string.IsNullOrWhiteSpace(expectedGoalId) && !string.IsNullOrWhiteSpace(expectedGameVersion),
            "Explicit goal and game identities are required.");
        var auditFullPath = Path.GetFullPath(auditPath);
        var dependencyFullPath = Path.GetFullPath(dependencyExpansionPath);
        var auditBytes = File.ReadAllBytes(auditFullPath);
        using var document = JsonDocument.Parse(auditBytes);
        var root = document.RootElement;
        RequireUniqueProperties(root);
        Require(root.ValueKind == JsonValueKind.Object, "Audit must be an object.");
        foreach (var name in RequiredAuditFields)
            Require(root.TryGetProperty(name, out var field) && field.ValueKind != JsonValueKind.Null,
                "Audit is missing " + name + ".");
        var audit = root.Deserialize<MasterAnglerFreshSavePrerequisiteAuditReport>()
            ?? throw new InvalidDataException("Audit is null.");
        Require(JsonNode.DeepEquals(JsonSerializer.SerializeToNode(suppliedAudit), JsonNode.Parse(auditBytes)),
            "Supplied rebuilt audit does not match its file.");
        Require(audit.SchemaVersion == "master_angler_fresh_save_prerequisite_audit.v2" &&
                audit.GoalId == expectedGoalId && audit.GameVersion == expectedGameVersion,
            "Audit schema, goal or game identity mismatch.");
        var dependencyBytes = File.ReadAllBytes(dependencyFullPath);
        var dependencyHash = Hash(dependencyBytes);
        Require(!string.IsNullOrWhiteSpace(audit.DependencyExpansionPath) &&
                Path.GetFullPath(audit.DependencyExpansionPath) == dependencyFullPath &&
                string.Equals(audit.DependencyExpansionSha256, dependencyHash, StringComparison.OrdinalIgnoreCase),
            "Audit dependency expansion path or SHA256 mismatch.");
        using var dependencies = JsonDocument.Parse(dependencyBytes);
        RequireUniqueProperties(dependencies.RootElement);
        Require(dependencies.RootElement.GetProperty("schema_version").GetString() ==
                    "goal_method_dependency_expansions.v1" &&
                dependencies.RootElement.GetProperty("goal_id").GetString() == expectedGoalId,
            "Dependency expansion schema or goal identity mismatch.");

        var blockers = new List<string>();
        void Check(bool condition, string reason) { if (!condition) blockers.Add(reason); }
        Check(audit.Status == CompleteAuditStatus, "audit_not_complete");
        Check(!audit.TrainingLabelEligible, "audit_training_label_eligible");
        Check(audit.StaticOpportunityInventoryComplete, "static_opportunity_inventory_incomplete");
        Check(audit.PrerequisiteFamilyInventoryComplete, "prerequisite_family_inventory_incomplete");
        Check(audit.MissingOrUnboundProducerFamilies is { Length: 0 }, "missing_or_unbound_producer_families");
        Check(audit.UnknownLocationIds is { Length: 0 }, "unknown_location_ids");
        var species = audit.Species ?? Array.Empty<MasterAnglerFreshSaveSpeciesPrerequisite>();
        var speciesIds = species.Where(row => row is not null).Select(row => row.QualifiedItemId)
            .ToHashSet(StringComparer.Ordinal);
        Check(audit.NativeDenominatorCount == ExpectedNativeDenominatorCount &&
              species.Length == ExpectedNativeDenominatorCount &&
              speciesIds.Count == species.Length && speciesIds.All(id => !string.IsNullOrWhiteSpace(id)) &&
              species.All(row => row is not null && row.StaticOpportunityDayCount >= 2) &&
              audit.SpeciesWithTwoOrMoreStaticOpportunityDays == ExpectedNativeDenominatorCount,
            "static_species_inventory_inconsistent");
        var families = audit.PrerequisiteFamilies ?? Array.Empty<MasterAnglerFreshSavePrerequisiteFamily>();
        Check(families.Length == ExpectedPrerequisiteFamilyCount && families.All(row => row is not null &&
                  !string.IsNullOrWhiteSpace(row.FamilyId) && row.ProducerStatus == "implemented_and_bound" &&
                  row.RequiredBySpeciesIds is { Length: > 0 } &&
                  row.RequiredBySpeciesIds.All(speciesIds.Contains)) &&
              families.Where(row => row is not null).Select(row => row.FamilyId)
                  .Distinct(StringComparer.Ordinal).Count() == families.Length &&
              families.Any(row => row?.FamilyId == "ginger_island_access"),
            "prerequisite_family_inventory_inconsistent");

        Require(root.TryGetProperty(
                "ginger_island_access_capability_certificate", out var certificateNode),
            "Audit is missing ginger_island_access_capability_certificate.");
        var certificateIds = Array.Empty<string>();
        if (audit.GingerIslandAccessCapabilityCertificate is not null &&
            !string.IsNullOrWhiteSpace(
                audit.GingerIslandAccessCapabilityCertificatePath))
        {
            var certificatePath = Path.GetFullPath(
                audit.GingerIslandAccessCapabilityCertificatePath);
            var certificateHash = Hash(File.ReadAllBytes(certificatePath));
            Require(string.Equals(
                    certificateHash,
                    audit.GingerIslandAccessCapabilityCertificateSha256,
                    StringComparison.OrdinalIgnoreCase),
                "Shared-capability certificate SHA256 mismatch.");
            var replayed = MasterAnglerSharedCapabilityCertificateBuilder.ReadAndReplay(
                certificatePath);
            Require(JsonNode.DeepEquals(
                    JsonSerializer.SerializeToNode(replayed),
                    JsonSerializer.SerializeToNode(
                        audit.GingerIslandAccessCapabilityCertificate)) &&
                    JsonNode.DeepEquals(
                        JsonNode.Parse(certificateNode.GetRawText()),
                        JsonSerializer.SerializeToNode(replayed)),
                "Shared-capability certificate file does not exactly match the audit certificate.");
            Check(replayed.Ready &&
                  replayed.GameVersion == expectedGameVersion &&
                  replayed.CapabilityId ==
                    MasterAnglerSharedCapabilityCertificateBuilder
                        .GingerIslandAccessCapabilityId &&
                  replayed.ConsumerDirectionId ==
                    MasterAnglerSharedCapabilityCertificateBuilder.ConsumerDirectionId &&
                  replayed.BlockingReasons is { Length: 0 } &&
                  !replayed.TrainingLabelEligible,
                "ginger_island_access_capability_certificate_invalid");
            certificateIds = new[] { replayed.CapabilityId };
        }
        else
            blockers.Add("ginger_island_access_capability_certificate_missing");

        return new MasterAnglerFreshSavePrerequisiteCompletionProof(blockers.Count == 0,
            audit.GoalId, audit.GameVersion, auditFullPath, Hash(auditBytes), dependencyFullPath,
            dependencyHash, audit.NativeDenominatorCount, families.Length, certificateIds,
            blockers.ToArray());
    }

    private static readonly string[] RequiredAuditFields =
    {
        "schema_version", "status", "goal_id", "game_version", "training_label_eligible",
        "dependency_expansion_path", "dependency_expansion_sha256", "native_denominator_count",
        "species_with_two_or_more_static_opportunity_days", "static_opportunity_inventory_complete",
        "prerequisite_family_inventory_complete", "species", "prerequisite_families",
        "missing_or_unbound_producer_families", "unknown_location_ids",
        "ginger_island_access_capability_certificate_path",
        "ginger_island_access_capability_certificate_sha256"
    };

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static void RequireUniqueProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                Require(names.Add(property.Name), "Evidence contains duplicate properties.");
                RequireUniqueProperties(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray()) RequireUniqueProperties(item);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidDataException(message);
    }
}
