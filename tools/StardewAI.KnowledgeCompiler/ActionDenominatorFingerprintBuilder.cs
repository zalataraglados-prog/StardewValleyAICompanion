using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed record ActionDenominatorFingerprint(
    string GameVersion,
    string Sha256,
    int SurfaceCount,
    int BranchCount,
    int MapTokenCount,
    int SemanticActionCount);

internal sealed record ActionDenominatorFreezeResult(
    string Status,
    string ApprovalPath,
    string[] MismatchReasons);

internal static class ActionDenominatorFingerprintBuilder
{
    public static ActionDenominatorFingerprint Build(
        string gameVersion,
        NativeActionSurfaceCatalog surfaces,
        NativeActionBranchCatalog branches,
        NativeMapInteractionCoverageCatalog mapInteractions,
        IEnumerable<string> semanticActionIds)
    {
        var canonical = new StringBuilder();
        Append(canonical, "stardewai.native_action_denominator.v1");
        Append(canonical, gameVersion);

        foreach (var row in surfaces.Surfaces.OrderBy(row => row.SurfaceId, StringComparer.Ordinal))
        {
            Append(canonical, "surface");
            Append(canonical, row.SurfaceId);
            Append(canonical, row.Signature);
            Append(canonical, row.RelativeSourcePath);
            Append(canonical, row.BodySha256);
            AppendMany(canonical, row.MappedOptionIds);
        }

        foreach (var row in branches.Branches.OrderBy(row => row.BranchId, StringComparer.Ordinal))
        {
            Append(canonical, "branch");
            Append(canonical, row.BranchId);
            Append(canonical, row.SurfaceId);
            Append(canonical, row.SourceSha256);
            AppendMany(canonical, row.MappedActionIds);
        }

        foreach (var row in mapInteractions.Interactions
                     .OrderBy(row => row.PropertyName, StringComparer.Ordinal)
                     .ThenBy(row => row.ActionToken, StringComparer.Ordinal))
        {
            Append(canonical, "map_token");
            Append(canonical, row.PropertyName);
            Append(canonical, row.ActionToken);
            AppendMany(canonical, row.SourceBranchIds);
            AppendMany(canonical, row.MappedActionIds);
        }

        var actionIds = semanticActionIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (var actionId in actionIds)
        {
            Append(canonical, "semantic_action");
            Append(canonical, actionId);
        }

        var hash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())))
            .ToLowerInvariant();
        return new(
            gameVersion,
            hash,
            surfaces.Surfaces.Count,
            branches.Branches.Count,
            mapInteractions.Interactions.Count,
            actionIds.Length);
    }

    public static ActionDenominatorFreezeResult VerifyApproval(
        ActionDenominatorFingerprint actual,
        string? approvalPath)
    {
        if (string.IsNullOrWhiteSpace(approvalPath))
            return new("approval_not_supplied", string.Empty, Array.Empty<string>());

        var fullPath = Path.GetFullPath(approvalPath);
        if (!File.Exists(fullPath))
            return new("approval_file_missing", fullPath, new[] { "approval_file_missing" });

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(fullPath));
            var root = document.RootElement;
            var reasons = new List<string>();
            CompareString(root, "schema_version", "stardewai.native_action_denominator_freeze.v1", reasons);
            CompareString(root, "game_version", actual.GameVersion, reasons);
            CompareString(root, "fingerprint_sha256", actual.Sha256, reasons);
            CompareInt(root, "surface_count", actual.SurfaceCount, reasons);
            CompareInt(root, "branch_count", actual.BranchCount, reasons);
            CompareInt(root, "map_token_count", actual.MapTokenCount, reasons);
            CompareInt(root, "semantic_action_count", actual.SemanticActionCount, reasons);
            return new(
                reasons.Count == 0 ? "frozen" : "approval_mismatch",
                fullPath,
                reasons.ToArray());
        }
        catch (JsonException ex)
        {
            return new("approval_invalid_json", fullPath, new[] { "approval_invalid_json:" + ex.Message });
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
