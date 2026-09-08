using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class EvidenceAuditor
{
    public static EvidenceAuditReport Audit(
        string lockPath,
        string knowledgeRoot,
        string contentRoot)
    {
        var lockFullPath = Path.GetFullPath(lockPath);
        var root = Path.GetFullPath(knowledgeRoot);
        using var lockDocument = JsonDocument.Parse(File.ReadAllText(lockFullPath));
        var evidenceLock = lockDocument.RootElement;
        Require(RequiredString(evidenceLock, "schema_version") == "goal_conditioned_teacher_evidence_lock.v1",
            "Unsupported evidence lock schema.");
        var profile = RequiredString(evidenceLock, "profile");
        var artifactRoot = Path.Combine(root, "derived", profile);
        var issues = new List<EvidenceAuditIssue>();
        var verifiedArtifacts = 0;

        var sourceDescriptor = evidenceLock.GetProperty("source_manifest");
        var sourceManifestPath = ResolveDescendant(root, RequiredString(sourceDescriptor, "relative_path"));
        VerifyFile(sourceManifestPath, sourceDescriptor, "source_manifest", issues, ref verifiedArtifacts);
        foreach (var descriptor in evidenceLock.GetProperty("artifacts").EnumerateArray())
        {
            var file = RequiredString(descriptor, "file");
            if (Path.IsPathRooted(file) || Path.GetFileName(file) != file)
            {
                issues.Add(Block("invalid_artifact_name", file));
                continue;
            }
            VerifyFile(Path.Combine(artifactRoot, file), descriptor, file, issues, ref verifiedArtifacts);
        }

        VerifyBuildManifest(Path.Combine(artifactRoot, "build-manifest.json"), sourceDescriptor, issues);
        VerifyRuntimeIdentity(Path.Combine(artifactRoot, "runtime-assembly-identity.json"),
            evidenceLock.GetProperty("runtime_assembly"), issues);
        VerifyGoal(Path.Combine(artifactRoot, "goal-dependency-index.json"),
            evidenceLock.GetProperty("goal_contract"), issues);
        VerifyGraph(Path.Combine(artifactRoot, "authoritative-dependency-graph.json"),
            evidenceLock.GetProperty("graph_contract"), issues);
        VerifyWiki(Path.Combine(artifactRoot, "wiki-verification-registry.json"),
            evidenceLock.GetProperty("required_wiki_source_ids"), issues);
        VerifySourceValidation(Path.Combine(artifactRoot, "source-validation.json"), issues);

        var content = ContentInventoryVerifier.Verify(sourceManifestPath, contentRoot);
        if (content.ManifestSha256 != RequiredString(sourceDescriptor, "sha256"))
            issues.Add(Block("content_rehash_manifest_mismatch", content.ManifestSha256));
        if (content.ExpectedFiles != sourceDescriptor.GetProperty("expected_content_files").GetInt32())
            issues.Add(Block("content_rehash_count_mismatch", content.ExpectedFiles.ToString()));
        if (content.Status != "pass")
            issues.Add(Block("content_rehash_failed", content.ContentRoot));

        return new EvidenceAuditReport
        {
            Status = issues.Any(issue => issue.Severity == "blocking") ? "blocked" : "pass",
            LockPath = lockFullPath,
            LockSha256 = ContentInventoryVerifier.HashFile(lockFullPath),
            KnowledgeRoot = root,
            Profile = profile,
            ArtifactRoot = artifactRoot,
            VerifiedArtifactCount = verifiedArtifacts,
            ContentRehash = content,
            Issues = issues.ToArray(),
            AuditPolicy =
                "Exact hashes and native/runtime contracts are mandatory. A direct content rehash closes the historical content_root_not_supplied warning without mutating the immutable v24 source artifact."
        };
    }

    private static void VerifyFile(
        string path,
        JsonElement descriptor,
        string subject,
        ICollection<EvidenceAuditIssue> issues,
        ref int verified)
    {
        if (!File.Exists(path))
        {
            issues.Add(Block("locked_file_missing", subject));
            return;
        }
        var expectedBytes = descriptor.GetProperty("bytes").GetInt64();
        var actualBytes = new FileInfo(path).Length;
        if (actualBytes != expectedBytes)
        {
            issues.Add(Block("locked_file_size_mismatch", $"{subject}:expected={expectedBytes};actual={actualBytes}"));
            return;
        }
        var expectedHash = RequiredString(descriptor, "sha256");
        var actualHash = ContentInventoryVerifier.HashFile(path);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(Block("locked_file_hash_mismatch", subject));
            return;
        }
        verified++;
    }

    private static void VerifyBuildManifest(
        string path,
        JsonElement sourceDescriptor,
        ICollection<EvidenceAuditIssue> issues)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (RequiredString(root, "status") != "complete_authoritative_identity_graph_stage")
            issues.Add(Block("knowledge_build_incomplete", RequiredString(root, "status")));
        if (RequiredString(root, "source_manifest_sha256") != RequiredString(sourceDescriptor, "sha256"))
            issues.Add(Block("build_source_manifest_mismatch", path));
        if (RequiredString(root, "runtime_assembly_identity_status") != "exact_match")
            issues.Add(Block("build_runtime_identity_not_exact", path));
    }

    private static void VerifyRuntimeIdentity(
        string path,
        JsonElement contract,
        ICollection<EvidenceAuditIssue> issues)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (RequiredString(root, "status") != "exact_match" || root.GetProperty("mismatches").GetArrayLength() != 0)
            issues.Add(Block("runtime_assembly_identity_failed", path));
        var assembly = root.GetProperty("declared_runtime_assemblies").EnumerateArray()
            .SingleOrDefault(value => RequiredString(value, "assemblyName") == RequiredString(contract, "assembly_name"));
        if (assembly.ValueKind == JsonValueKind.Undefined ||
            RequiredString(assembly, "assemblyVersion") != RequiredString(contract, "assembly_version") ||
            RequiredString(assembly, "moduleVersionId") != RequiredString(contract, "module_version_id") ||
            assembly.GetProperty("bytes").GetInt64() != contract.GetProperty("bytes").GetInt64() ||
            RequiredString(assembly, "sha256") != RequiredString(contract, "sha256"))
            issues.Add(Block("runtime_assembly_contract_mismatch", path));
    }

    private static void VerifyGoal(
        string path,
        JsonElement contract,
        ICollection<EvidenceAuditIssue> issues)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        var goal = root.GetProperty("grandpa_goal");
        var summary = root.GetProperty("summary");
        if (RequiredString(goal, "goalId") != RequiredString(contract, "goal_id") ||
            goal.GetProperty("targetScore").GetInt32() != contract.GetProperty("target_score").GetInt32() ||
            goal.GetProperty("criteria").GetArrayLength() != contract.GetProperty("criterion_count").GetInt32() ||
            summary.GetProperty("bundleCount").GetInt32() != contract.GetProperty("bundle_count").GetInt32() ||
            summary.GetProperty("recipeOutputCount").GetInt32() != contract.GetProperty("recipe_output_count").GetInt32() ||
            root.GetProperty("issues").GetArrayLength() != 0)
            issues.Add(Block("goal_contract_mismatch", path));
    }

    private static void VerifyGraph(
        string path,
        JsonElement contract,
        ICollection<EvidenceAuditIssue> issues)
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var root = document.RootElement;
        if (RequiredString(root, "schema_version") != RequiredString(contract, "schema_version") ||
            root.GetProperty("node_count").GetInt32() != contract.GetProperty("node_count").GetInt32() ||
            root.GetProperty("edge_count").GetInt32() != contract.GetProperty("edge_count").GetInt32())
            issues.Add(Block("dependency_graph_contract_mismatch", path));
    }

    private static void VerifyWiki(
        string path,
        JsonElement requiredIds,
        ICollection<EvidenceAuditIssue> issues)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (!RequiredString(root, "authority_policy").Contains("secondary", StringComparison.OrdinalIgnoreCase))
            issues.Add(Block("wiki_authority_policy_invalid", path));
        var actual = root.GetProperty("sources").EnumerateArray()
            .Select(value => RequiredString(value, "id"))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var required in requiredIds.EnumerateArray().Select(value => value.GetString() ?? string.Empty))
        {
            if (!actual.Contains(required))
                issues.Add(Block("required_wiki_source_missing", required));
        }
    }

    private static void VerifySourceValidation(string path, ICollection<EvidenceAuditIssue> issues)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var root = document.RootElement;
        if (root.GetProperty("blocking_issue_count").GetInt32() != 0)
            issues.Add(Block("source_validation_blocking", path));
        var warnings = root.GetProperty("issues").EnumerateArray()
            .Where(value => RequiredString(value, "severity") == "warning")
            .Select(value => RequiredString(value, "code"))
            .Where(code => code != "content_root_not_supplied")
            .ToArray();
        foreach (var warning in warnings)
            issues.Add(new EvidenceAuditIssue("warning", "source_validation_warning", warning));
    }

    private static string ResolveDescendant(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidDataException("Evidence lock path is rooted: " + relativePath);
        var result = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(root.TrimEnd('\\', '/') + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Evidence lock path escapes root: " + relativePath);
        return result;
    }

    private static EvidenceAuditIssue Block(string code, string subject) => new("blocking", code, subject);

    private static string RequiredString(JsonElement value, string property)
    {
        var result = value.GetProperty(property).GetString();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidDataException("Required evidence string is empty: " + property)
            : result;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidDataException(message);
    }
}

public sealed class EvidenceAuditReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "goal_conditioned_teacher_evidence_audit.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("lock_path")]
    public string LockPath { get; set; } = string.Empty;

    [JsonPropertyName("lock_sha256")]
    public string LockSha256 { get; set; } = string.Empty;

    [JsonPropertyName("knowledge_root")]
    public string KnowledgeRoot { get; set; } = string.Empty;

    [JsonPropertyName("profile")]
    public string Profile { get; set; } = string.Empty;

    [JsonPropertyName("artifact_root")]
    public string ArtifactRoot { get; set; } = string.Empty;

    [JsonPropertyName("verified_artifact_count")]
    public int VerifiedArtifactCount { get; set; }

    [JsonPropertyName("content_rehash")]
    public ContentInventoryRehashReport ContentRehash { get; set; } = new();

    [JsonPropertyName("issues")]
    public EvidenceAuditIssue[] Issues { get; set; } = Array.Empty<EvidenceAuditIssue>();

    [JsonPropertyName("audit_policy")]
    public string AuditPolicy { get; set; } = string.Empty;
}

public sealed record EvidenceAuditIssue(
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("subject")] string Subject);
