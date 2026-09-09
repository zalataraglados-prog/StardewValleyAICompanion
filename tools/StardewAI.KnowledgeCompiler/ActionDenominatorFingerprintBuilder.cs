using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed record NativeActionSurfaceFingerprint(
    string GameVersion,
    string Sha256,
    int SurfaceCount,
    int BranchCount,
    int MapTokenCount);

internal sealed record PlanningSemanticActionIdentity(
    string ActionId,
    string Domain,
    string SemanticKind,
    string PrimaryEngineId,
    string CatalogStatus,
    string BlockReason,
    IReadOnlyList<string> NativeRuntimeTypes);

internal sealed record PlanningSemanticCatalogFingerprint(
    string Sha256,
    int ActionCount);

internal sealed record ActionDenominatorFreezeResult(
    string Status,
    string ApprovalPath,
    string[] MismatchReasons,
    string LatestEvidenceId);

internal static class NativeActionSurfaceFingerprintBuilder
{
    public static NativeActionSurfaceFingerprint Build(
        string gameVersion,
        NativeActionSurfaceCatalog surfaces,
        NativeActionBranchCatalog branches,
        NativeMapInteractionCoverageCatalog mapInteractions)
    {
        var canonical = new StringBuilder();
        Append(canonical, "stardewai.native_action_surface.v1");
        Append(canonical, gameVersion);

        foreach (var row in surfaces.Surfaces.OrderBy(row => row.SurfaceId, StringComparer.Ordinal))
        {
            Append(canonical, "surface");
            Append(canonical, row.SurfaceId);
            Append(canonical, row.Signature);
            Append(canonical, row.RelativeSourcePath);
            Append(canonical, row.BodySha256);
        }

        foreach (var row in branches.Branches.OrderBy(row => row.BranchId, StringComparer.Ordinal))
        {
            Append(canonical, "branch");
            Append(canonical, row.BranchId);
            Append(canonical, row.SurfaceId);
            Append(canonical, row.SourceSha256);
        }

        foreach (var row in mapInteractions.Interactions
                     .OrderBy(row => row.PropertyName, StringComparer.Ordinal)
                     .ThenBy(row => row.ActionToken, StringComparer.Ordinal))
        {
            Append(canonical, "map_token");
            Append(canonical, row.PropertyName);
            Append(canonical, row.ActionToken);
            AppendMany(canonical, row.SourceBranchIds);
        }

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
        return new(
            gameVersion,
            hash,
            surfaces.Surfaces.Count,
            branches.Branches.Count,
            mapInteractions.Interactions.Count);
    }

    public static ActionDenominatorFreezeResult VerifyApproval(
        NativeActionSurfaceFingerprint actual,
        string? approvalPath)
    {
        if (string.IsNullOrWhiteSpace(approvalPath))
            return new("approval_not_supplied", string.Empty, Array.Empty<string>(), string.Empty);

        var fullPath = Path.GetFullPath(approvalPath);
        if (!File.Exists(fullPath))
            return new("approval_file_missing", fullPath, new[] { "approval_file_missing" }, string.Empty);

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            var root = document.RootElement;
            var reasons = new List<string>();
            CompareString(root, "schema_version", "stardewai.native_action_denominator_freeze.v2", reasons);
            CompareString(root, "game_version", actual.GameVersion, reasons);
            CompareString(root, "fingerprint_sha256", actual.Sha256, reasons);
            CompareInt(root, "surface_count", actual.SurfaceCount, reasons);
            CompareInt(root, "branch_count", actual.BranchCount, reasons);
            CompareInt(root, "map_token_count", actual.MapTokenCount, reasons);
            var latestEvidenceId = ReadString(root, "latest_evidence_id");
            if (latestEvidenceId.Length < 5 ||
                !latestEvidenceId.StartsWith("EVD-", StringComparison.Ordinal) ||
                !latestEvidenceId[4..].All(char.IsAsciiDigit))
            {
                reasons.Add("latest_evidence_id_invalid");
            }
            return new(
                reasons.Count == 0 ? "frozen" : "approval_mismatch",
                fullPath,
                reasons.ToArray(),
                latestEvidenceId);
        }
        catch (JsonException ex)
        {
            return new("approval_invalid_json", fullPath, new[] { "approval_invalid_json:" + ex.Message }, string.Empty);
        }
    }

    private static void CompareString(
        JsonElement root,
        string name,
        string expected,
        ICollection<string> reasons)
    {
        if (!root.TryGetProperty(name, out var value) ||
            value.ValueKind != JsonValueKind.String ||
            !string.Equals(value.GetString(), expected, StringComparison.Ordinal))
        {
            reasons.Add(name + "_mismatch");
        }
    }

    private static void CompareInt(
        JsonElement root,
        string name,
        int expected,
        ICollection<string> reasons)
    {
        if (!root.TryGetProperty(name, out var value) ||
            !value.TryGetInt32(out var actual) ||
            actual != expected)
        {
            reasons.Add(name + "_mismatch");
        }
    }

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static void AppendMany(StringBuilder target, IEnumerable<string> values)
    {
        foreach (var value in values.OrderBy(value => value, StringComparer.Ordinal))
            Append(target, value);
        Append(target, "#end");
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length).Append(':').Append(value).Append(';');
    }
}

internal static class PlanningSemanticCatalogFingerprintBuilder
{
    public static PlanningSemanticCatalogFingerprint Build(
        IEnumerable<PlanningSemanticActionIdentity> actions)
    {
        var canonical = new StringBuilder();
        Append(canonical, "stardewai.planning_semantic_catalog.v1");

        var rows = actions
            .OrderBy(row => row.ActionId, StringComparer.Ordinal)
            .ToArray();
        foreach (var row in rows)
        {
            Append(canonical, "planning_semantic");
            Append(canonical, row.ActionId);
            Append(canonical, row.Domain);
            Append(canonical, row.SemanticKind);
            Append(canonical, row.PrimaryEngineId);
            Append(canonical, row.CatalogStatus);
            Append(canonical, row.BlockReason);
            AppendMany(canonical, row.NativeRuntimeTypes);
        }

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
        return new(hash, rows.Length);
    }

    private static void AppendMany(StringBuilder target, IEnumerable<string> values)
    {
        foreach (var value in values.OrderBy(value => value, StringComparer.Ordinal))
            Append(target, value);
        Append(target, "#end");
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length).Append(':').Append(value).Append(';');
    }
}
