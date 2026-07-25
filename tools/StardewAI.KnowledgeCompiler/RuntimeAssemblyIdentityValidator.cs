using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class RuntimeAssemblyIdentityValidator
{
    public RuntimeAssemblyIdentityValidation Validate(
        string runtimeSemanticsPath,
        IReadOnlyList<AssemblyEvidenceIndex> assemblies)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeSemanticsPath));
        var runtimeReferences = new Dictionary<string, RuntimeAssemblyReference>(StringComparer.OrdinalIgnoreCase);
        CollectHandlerReferences(document.RootElement, runtimeReferences);

        var declaredIdentities = ReadDeclaredIdentities(document.RootElement);
        var suppliedByName = assemblies
            .GroupBy(row => row.AssemblyName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.OrdinalIgnoreCase);
        var mismatches = new List<RuntimeAssemblyIdentityMismatch>();

        foreach (var reference in runtimeReferences.Values.OrderBy(row => row.AssemblyName, StringComparer.Ordinal))
        {
            if (!suppliedByName.TryGetValue(reference.AssemblyName, out var supplied))
            {
                mismatches.Add(new(
                    reference.AssemblyName,
                    reference.ModuleVersionId,
                    null,
                    "assembly_not_supplied"));
                continue;
            }

            if (!supplied.Any(row => string.Equals(
                    row.ModuleVersionId,
                    reference.ModuleVersionId,
                    StringComparison.OrdinalIgnoreCase)))
            {
                mismatches.Add(new(
                    reference.AssemblyName,
                    reference.ModuleVersionId,
                    string.Join(',', supplied.Select(row => row.ModuleVersionId)),
                    "module_version_id_mismatch"));
            }
        }

        foreach (var declared in declaredIdentities)
        {
            if (!suppliedByName.TryGetValue(declared.AssemblyName, out var supplied))
                continue;
            var exactMvid = supplied.FirstOrDefault(row => string.Equals(
                row.ModuleVersionId,
                declared.ModuleVersionId,
                StringComparison.OrdinalIgnoreCase));
            if (exactMvid is null)
                continue;
            if (!string.IsNullOrWhiteSpace(declared.Sha256) &&
                !string.Equals(declared.Sha256, exactMvid.AssemblySha256, StringComparison.OrdinalIgnoreCase))
            {
                mismatches.Add(new(
                    declared.AssemblyName,
                    declared.ModuleVersionId,
                    exactMvid.ModuleVersionId,
                    $"sha256_mismatch:runtime={declared.Sha256};supplied={exactMvid.AssemblySha256}"));
            }
            if (declared.Bytes is not null && declared.Bytes != exactMvid.AssemblyBytes)
            {
                mismatches.Add(new(
                    declared.AssemblyName,
                    declared.ModuleVersionId,
                    exactMvid.ModuleVersionId,
                    $"size_mismatch:runtime={declared.Bytes};supplied={exactMvid.AssemblyBytes}"));
            }
        }

        return new(
            runtimeReferences.Values.OrderBy(row => row.AssemblyName, StringComparer.Ordinal).ToArray(),
            declaredIdentities,
            assemblies.Select(row => new SuppliedAssemblyIdentity(
                row.AssemblyName,
                row.AssemblyVersion,
                row.ModuleVersionId,
                row.AssemblyBytes,
                row.AssemblySha256,
                row.AssemblyPath)).ToArray(),
            mismatches);
    }

    private static void CollectHandlerReferences(
        JsonElement element,
        IDictionary<string, RuntimeAssemblyReference> result)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("assemblyName", out var assemblyNameProperty) &&
                element.TryGetProperty("moduleVersionId", out var mvidProperty) &&
                element.TryGetProperty("metadataToken", out _) &&
                assemblyNameProperty.ValueKind == JsonValueKind.String &&
                mvidProperty.ValueKind == JsonValueKind.String)
            {
                var assemblyName = assemblyNameProperty.GetString() ?? string.Empty;
                var mvid = mvidProperty.GetString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(assemblyName) && !string.IsNullOrWhiteSpace(mvid))
                    result[assemblyName + ":" + mvid] = new(assemblyName, mvid);
            }

            foreach (var property in element.EnumerateObject())
                CollectHandlerReferences(property.Value, result);
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var child in element.EnumerateArray())
                CollectHandlerReferences(child, result);
        }
    }

    private static IReadOnlyList<DeclaredRuntimeAssemblyIdentity> ReadDeclaredIdentities(JsonElement root)
    {
        if (!root.TryGetProperty("runtime_assemblies", out var rows) ||
            rows.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<DeclaredRuntimeAssemblyIdentity>();
        }

        return rows.EnumerateArray().Select(row => new DeclaredRuntimeAssemblyIdentity(
            row.GetProperty("assemblyName").GetString() ?? string.Empty,
            row.TryGetProperty("assemblyVersion", out var version) ? version.GetString() ?? string.Empty : string.Empty,
            row.GetProperty("moduleVersionId").GetString() ?? string.Empty,
            row.TryGetProperty("bytes", out var bytes) && bytes.ValueKind == JsonValueKind.Number
                ? bytes.GetInt64()
                : null,
            row.TryGetProperty("sha256", out var hash) ? hash.GetString() : null)).ToArray();
    }
}

internal sealed record RuntimeAssemblyIdentityValidation(
    IReadOnlyList<RuntimeAssemblyReference> HandlerAssemblyReferences,
    IReadOnlyList<DeclaredRuntimeAssemblyIdentity> DeclaredRuntimeAssemblies,
    IReadOnlyList<SuppliedAssemblyIdentity> SuppliedAssemblies,
    IReadOnlyList<RuntimeAssemblyIdentityMismatch> Mismatches)
{
    public bool IsCompatible => Mismatches.Count == 0;
}

internal sealed record RuntimeAssemblyReference(string AssemblyName, string ModuleVersionId);

internal sealed record DeclaredRuntimeAssemblyIdentity(
    string AssemblyName,
    string AssemblyVersion,
    string ModuleVersionId,
    long? Bytes,
    string? Sha256);

internal sealed record SuppliedAssemblyIdentity(
    string AssemblyName,
    string AssemblyVersion,
    string ModuleVersionId,
    long Bytes,
    string Sha256,
    string Path);

internal sealed record RuntimeAssemblyIdentityMismatch(
    string AssemblyName,
    string RuntimeModuleVersionId,
    string? SuppliedModuleVersionId,
    string Reason);
