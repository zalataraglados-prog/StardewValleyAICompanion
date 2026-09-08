using System.Text.Json;
using System.Text.Json.Serialization;

namespace StardewAI.GoalConditionedBootstrap;

public static class ClaimConflictAuditor
{
    private static readonly HashSet<string> VerifiedVerdicts = new(StringComparer.Ordinal)
    {
        "verified",
        "verified_scope_difference"
    };

    private static readonly HashSet<string> NonBlockingVerdicts = new(StringComparer.Ordinal)
    {
        "strategy_hypothesis_only"
    };

    private static readonly HashSet<string> RuntimePendingVerdicts = new(StringComparer.Ordinal)
    {
        "native_verified_runtime_pending"
    };

    public static ClaimConflictAuditReport Audit(
        string ledgerPath,
        string knowledgePath,
        string decompileRoot,
        string knowledgeRoot)
    {
        var ledgerFullPath = Path.GetFullPath(ledgerPath);
        var decompileFullPath = Path.GetFullPath(decompileRoot);
        var knowledgeFullRoot = Path.GetFullPath(knowledgeRoot);
        var knowledge = KnowledgeIndex.Load(knowledgePath);
        using var document = JsonDocument.Parse(File.ReadAllText(ledgerFullPath));
        var root = document.RootElement;
        var issues = new List<ClaimConflictAuditIssue>();

        if (RequiredString(root, "schema_version") != "goal_method_claim_conflict_ledger.v1")
            issues.Add(Block("unsupported_ledger_schema", ledgerFullPath));
        if (RequiredString(root, "goal_id") != knowledge.GoalId)
            issues.Add(Block("ledger_goal_mismatch", RequiredString(root, "goal_id")));

        var sourceIds = VerifySourceLocks(
            root.GetProperty("source_locks"),
            decompileFullPath,
            knowledgeFullRoot,
            issues);
        var criterionIds = knowledge.Criteria.Select(value => value.Id).ToHashSet(StringComparer.Ordinal);
        var verifiedCriteria = new HashSet<string>(StringComparer.Ordinal);
        var claimIds = new HashSet<string>(StringComparer.Ordinal);
        var unresolvedClaims = new List<string>();
        var strategyClaims = new List<string>();
        var runtimePendingClaims = new List<string>();

        foreach (var claim in root.GetProperty("claims").EnumerateArray())
        {
            var claimId = RequiredString(claim, "claim_id");
            if (!claimIds.Add(claimId))
                issues.Add(Block("duplicate_claim_id", claimId));

            var verdict = RequiredString(claim, "verdict");
            var covered = claim.GetProperty("criterion_ids").EnumerateArray()
                .Select(value => value.GetString() ?? string.Empty)
                .ToArray();
            foreach (var criterionId in covered.Where(value => !criterionIds.Contains(value)))
                issues.Add(Block("unknown_criterion_id", $"{claimId}:{criterionId}"));

            if (VerifiedVerdicts.Contains(verdict))
            {
                VerifyClaimEvidence(claim, claimId, sourceIds, requireRuntimeSource: true, issues);
                verifiedCriteria.UnionWith(covered.Where(criterionIds.Contains));
            }
            else if (RuntimePendingVerdicts.Contains(verdict))
            {
                VerifyClaimEvidence(claim, claimId, sourceIds, requireRuntimeSource: false, issues);
                runtimePendingClaims.Add(claimId);
            }
            else if (NonBlockingVerdicts.Contains(verdict))
            {
                strategyClaims.Add(claimId);
            }
            else
            {
                unresolvedClaims.Add(claimId);
                issues.Add(Block("unresolved_claim", $"{claimId}:{verdict}"));
            }
        }

        var missingCriteria = criterionIds.Except(verifiedCriteria).Order(StringComparer.Ordinal).ToArray();
        foreach (var criterionId in missingCriteria)
            issues.Add(Block("criterion_without_verified_claim", criterionId));

        return new ClaimConflictAuditReport
        {
            Status = issues.Any(value => value.Severity == "blocking") ? "blocked" : "pass",
            LedgerPath = ledgerFullPath,
            LedgerSha256 = ContentInventoryVerifier.HashFile(ledgerFullPath),
            KnowledgeSha256 = knowledge.Sha256,
            GoalId = knowledge.GoalId,
            CriterionCount = criterionIds.Count,
            VerifiedCriterionCount = verifiedCriteria.Count,
            VerifiedCriteria = verifiedCriteria.Order(StringComparer.Ordinal).ToArray(),
            MissingCriteria = missingCriteria,
            ClaimCount = claimIds.Count,
            UnresolvedClaims = unresolvedClaims.Order(StringComparer.Ordinal).ToArray(),
            RuntimePendingClaims = runtimePendingClaims.Order(StringComparer.Ordinal).ToArray(),
            StrategyHypothesisClaims = strategyClaims.Order(StringComparer.Ordinal).ToArray(),
            VerifiedSourceLockCount = sourceIds.Count,
            Issues = issues.ToArray(),
            AdmissionPolicy =
                "Only verified native/runtime claims cover criteria. Native-verified runtime-pending claims may constrain fail-closed implementation but cannot cover criteria or produce positive labels until runtime evidence exists. Strategy hypotheses never become hard facts without rollout evidence."
        };
    }

    private static HashSet<string> VerifySourceLocks(
        JsonElement locks,
        string decompileRoot,
        string knowledgeRoot,
        ICollection<ClaimConflictAuditIssue> issues)
    {
        var verified = new HashSet<string>(StringComparer.Ordinal);
        var declared = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in locks.EnumerateArray())
        {
            var sourceId = RequiredString(source, "source_id");
            if (!declared.Add(sourceId))
            {
                issues.Add(Block("duplicate_source_id", sourceId));
                continue;
            }
            var rootKind = RequiredString(source, "root_kind");
            var selectedRoot = rootKind switch
            {
                "decompile" => decompileRoot,
                "knowledge" => knowledgeRoot,
                _ => string.Empty
            };
            if (selectedRoot.Length == 0)
            {
                issues.Add(Block("unknown_source_root_kind", $"{sourceId}:{rootKind}"));
                continue;
            }
            var path = ResolveDescendant(selectedRoot, RequiredString(source, "relative_path"));
            if (!File.Exists(path))
            {
                issues.Add(Block("claim_source_missing", sourceId));
                continue;
            }
            var expected = RequiredString(source, "sha256");
            var actual = ContentInventoryVerifier.HashFile(path);
            if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                issues.Add(Block("claim_source_hash_mismatch", sourceId));
                continue;
            }
            verified.Add(sourceId);
        }
        return verified;
    }

    private static void VerifyClaimEvidence(
        JsonElement claim,
        string claimId,
        IReadOnlySet<string> sourceIds,
        bool requireRuntimeSource,
        ICollection<ClaimConflictAuditIssue> issues)
    {
        RequiredString(claim, "subject");
        RequiredString(claim, "native_evidence");
        RequiredString(claim, "runtime_evidence");
        RequiredString(claim, "wiki_evidence");
        RequiredString(claim, "downstream_effect");

        var native = claim.GetProperty("native_source_ids").EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty).ToArray();
        var runtime = claim.GetProperty("runtime_source_ids").EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty).ToArray();
        if (native.Length == 0)
            issues.Add(Block("verified_claim_without_native_source", claimId));
        if (requireRuntimeSource && runtime.Length == 0)
            issues.Add(Block("verified_claim_without_runtime_source", claimId));
        foreach (var sourceId in native.Concat(runtime).Where(value => !sourceIds.Contains(value)).Distinct())
            issues.Add(Block("claim_references_unverified_source", $"{claimId}:{sourceId}"));
    }

    private static string ResolveDescendant(string root, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidDataException("Claim source path is rooted: " + relativePath);
        var prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var result = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!result.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Claim source path escapes root: " + relativePath);
        return result;
    }

    private static string RequiredString(JsonElement value, string property)
    {
        var result = value.GetProperty(property).GetString();
        return string.IsNullOrWhiteSpace(result)
            ? throw new InvalidDataException("Required claim string is empty: " + property)
            : result;
    }

    private static ClaimConflictAuditIssue Block(string code, string subject) => new("blocking", code, subject);
}

public sealed class ClaimConflictAuditReport
{
    [JsonPropertyName("schema_version")]
    public string SchemaVersion { get; set; } = "goal_method_claim_conflict_audit.v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("ledger_path")]
    public string LedgerPath { get; set; } = string.Empty;

    [JsonPropertyName("ledger_sha256")]
    public string LedgerSha256 { get; set; } = string.Empty;

    [JsonPropertyName("knowledge_sha256")]
    public string KnowledgeSha256 { get; set; } = string.Empty;

    [JsonPropertyName("goal_id")]
    public string GoalId { get; set; } = string.Empty;

    [JsonPropertyName("criterion_count")]
    public int CriterionCount { get; set; }

    [JsonPropertyName("verified_criterion_count")]
    public int VerifiedCriterionCount { get; set; }

    [JsonPropertyName("verified_criteria")]
    public string[] VerifiedCriteria { get; set; } = Array.Empty<string>();

    [JsonPropertyName("missing_criteria")]
    public string[] MissingCriteria { get; set; } = Array.Empty<string>();

    [JsonPropertyName("claim_count")]
    public int ClaimCount { get; set; }

    [JsonPropertyName("unresolved_claims")]
    public string[] UnresolvedClaims { get; set; } = Array.Empty<string>();

    [JsonPropertyName("runtime_pending_claims")]
    public string[] RuntimePendingClaims { get; set; } = Array.Empty<string>();

    [JsonPropertyName("strategy_hypothesis_claims")]
    public string[] StrategyHypothesisClaims { get; set; } = Array.Empty<string>();

    [JsonPropertyName("verified_source_lock_count")]
    public int VerifiedSourceLockCount { get; set; }

    [JsonPropertyName("issues")]
    public ClaimConflictAuditIssue[] Issues { get; set; } = Array.Empty<ClaimConflictAuditIssue>();

    [JsonPropertyName("admission_policy")]
    public string AdmissionPolicy { get; set; } = string.Empty;
}

public sealed record ClaimConflictAuditIssue(
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("subject")] string Subject);
