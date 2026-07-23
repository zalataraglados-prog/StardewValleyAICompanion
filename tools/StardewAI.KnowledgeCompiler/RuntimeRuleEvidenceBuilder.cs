using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class RuntimeRuleEvidenceBuilder
{
    public RuntimeRuleEvidence Build(
        IReadOnlyDictionary<string, PayloadAsset> assets,
        IReadOnlyList<AssemblyEvidenceIndex> assemblies)
    {
        var conditions = new List<RuntimeConditionEvidence>();
        var methods = new List<RuntimeMethodReferenceEvidence>();
        var eventScripts = new List<RuntimeEventScriptEvidence>();

        foreach (var asset in assets.Values.OrderBy(row => row.AssetName, StringComparer.OrdinalIgnoreCase))
        {
            Walk(asset.AssetName, asset.Payload, "payload", conditions, methods, assemblies);
            if (asset.AssetName.StartsWith("Data/Events/", StringComparison.OrdinalIgnoreCase) &&
                asset.Payload.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in asset.Payload.EnumerateObject())
                {
                    var script = entry.Value.GetRawText();
                    eventScripts.Add(new RuntimeEventScriptEvidence(
                        asset.AssetName,
                        entry.Name,
                        "payload." + Escape(entry.Name),
                        Encoding.UTF8.GetByteCount(script),
                        Hash(script),
                        "requires_event_precondition_and_command_semantic_review"));
                }
            }
        }

        return new RuntimeRuleEvidence(conditions, methods, eventScripts);
    }

    private static void Walk(
        string assetName,
        JsonElement element,
        string path,
        ICollection<RuntimeConditionEvidence> conditions,
        ICollection<RuntimeMethodReferenceEvidence> methods,
        IReadOnlyList<AssemblyEvidenceIndex> assemblies)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                var childPath = path + "." + Escape(property.Name);
                if (IsConditionProperty(property.Name) && property.Value.ValueKind == JsonValueKind.String)
                {
                    var raw = property.Value.GetRawText();
                    conditions.Add(new RuntimeConditionEvidence(
                        assetName,
                        childPath,
                        property.Name,
                        Encoding.UTF8.GetByteCount(raw),
                        Hash(raw),
                        "requires_condition_parser_and_decompiled_handler_review"));
                }

                if (property.Name.EndsWith("Method", StringComparison.OrdinalIgnoreCase) &&
                    property.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    methods.Add(ResolveMethod(assetName, childPath, property.Value.GetString()!, assemblies));
                }

                Walk(assetName, property.Value, childPath, conditions, methods, assemblies);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var child in element.EnumerateArray())
                Walk(assetName, child, path + "[" + index++ + "]", conditions, methods, assemblies);
        }
    }

    private static RuntimeMethodReferenceEvidence ResolveMethod(
        string assetName,
        string path,
        string raw,
        IReadOnlyList<AssemblyEvidenceIndex> assemblies)
    {
        var separator = raw.LastIndexOf(": ", StringComparison.Ordinal);
        var member = separator >= 0 ? raw[(separator + 2)..].Trim() : string.Empty;
        var owner = separator >= 0 ? raw[..separator].Trim() : raw.Trim();
        var assemblySeparator = owner.LastIndexOf(", ", StringComparison.Ordinal);
        var typeName = assemblySeparator >= 0 ? owner[..assemblySeparator].Trim() : owner;
        var assemblyName = assemblySeparator >= 0 ? owner[(assemblySeparator + 2)..].Trim() : string.Empty;

        var matches = assemblies
            .Where(assembly => string.IsNullOrWhiteSpace(assemblyName) ||
                               string.Equals(assembly.AssemblyName, assemblyName, StringComparison.Ordinal))
            .SelectMany(assembly => assembly.Types
                .Where(type => string.Equals(type.FullName, typeName, StringComparison.Ordinal))
                .SelectMany(type => type.Methods
                    .Where(method => string.Equals(method.Name, member, StringComparison.Ordinal))
                    .Select(method => new ResolvedMethodEvidence(
                        assembly.AssemblyName,
                        assembly.AssemblySha256,
                        type.FullName,
                        type.SourceCandidates,
                        method.MetadataToken,
                        method.SignatureSha256,
                        method.IlSha256,
                        method.BodyStatus))))
            .ToArray();

        var status = matches.Length switch
        {
            0 => "unresolved",
            1 when matches[0].BodyStatus == "il_hashed" => "resolved_binary_evidence_requires_semantic_review",
            1 => "resolved_without_il_body",
            _ => "ambiguous_overload_requires_signature_binding"
        };
        return new RuntimeMethodReferenceEvidence(assetName, path, raw, typeName, assemblyName, member, status, matches);
    }

    private static bool IsConditionProperty(string name) =>
        !string.Equals(name, "conditions", StringComparison.Ordinal) &&
        (name.Contains("Condition", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Query", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith("Queries", StringComparison.OrdinalIgnoreCase));

    private static string Escape(string value) => value.Replace(".", "\\.", StringComparison.Ordinal);

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

internal sealed record RuntimeRuleEvidence(
    IReadOnlyList<RuntimeConditionEvidence> Conditions,
    IReadOnlyList<RuntimeMethodReferenceEvidence> MethodReferences,
    IReadOnlyList<RuntimeEventScriptEvidence> EventScripts);

internal sealed record RuntimeConditionEvidence(
    string SourceAsset,
    string SourcePath,
    string PropertyName,
    int Utf8Bytes,
    string ValueSha256,
    string SemanticStatus);

internal sealed record RuntimeMethodReferenceEvidence(
    string SourceAsset,
    string SourcePath,
    string RawReference,
    string TypeName,
    string AssemblyName,
    string MemberName,
    string ResolutionStatus,
    IReadOnlyList<ResolvedMethodEvidence> Matches);

internal sealed record ResolvedMethodEvidence(
    string AssemblyName,
    string AssemblySha256,
    string TypeName,
    IReadOnlyList<string> SourceCandidates,
    string MetadataToken,
    string SignatureSha256,
    string? IlSha256,
    string BodyStatus);

internal sealed record RuntimeEventScriptEvidence(
    string SourceAsset,
    string EventKey,
    string SourcePath,
    int Utf8Bytes,
    string ScriptSha256,
    string SemanticStatus);
