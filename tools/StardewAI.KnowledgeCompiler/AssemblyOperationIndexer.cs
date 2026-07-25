using System.Collections;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;

namespace StardewAI.KnowledgeCompiler;

internal sealed class AssemblyOperationIndexer
{
    private static readonly IReadOnlyDictionary<ushort, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(field => field.FieldType == typeof(OpCode))
        .Select(field => (OpCode)field.GetValue(null)!)
        .ToDictionary(OpCodeKey);

    public HandlerOperationIndex Build(
        string runtimeSemanticsPath,
        IReadOnlyList<AssemblyEvidenceIndex> evidence,
        IEnumerable<string> assemblyPaths,
        IReadOnlyList<RuntimeMethodReferenceEvidence> dataMethodReferences)
    {
        var assemblies = assemblyPaths.Select(IndexAssembly).ToArray();
        var methods = assemblies.SelectMany(row => row.Methods.Values)
            .ToDictionary(row => row.Identity, StringComparer.OrdinalIgnoreCase);
        var usages = LoadHandlerUsages(runtimeSemanticsPath, assemblies);

        foreach (var reference in dataMethodReferences)
        {
            foreach (var match in reference.Matches)
            {
                var identity = Identity(match.AssemblySha256, match.MetadataToken);
                usages.AddOrUpdate(identity,
                    () => new MutableHandlerUsage(
                        identity,
                        "data_method",
                        match.AssemblyName,
                        match.TypeName,
                        reference.MemberName,
                        match.MetadataToken),
                    row => row.Add("data_method", reference.MemberName, reference.SourceAsset + ":" + reference.SourcePath));
            }
        }

        var rules = new List<HandlerOperationRule>();
        var unresolved = new List<string>();
        foreach (var usage in usages.Values.Values.OrderBy(row => row.Identity, StringComparer.Ordinal))
        {
            if (!methods.TryGetValue(usage.Identity, out var method))
            {
                unresolved.Add(usage.Identity);
                continue;
            }

            var closure = Closure(method, methods);
            var methodEvidence = evidence
                .SelectMany(row => row.Types)
                .SelectMany(type => type.Methods
                    .Where(item => string.Equals(item.MetadataToken, method.MetadataToken, StringComparison.OrdinalIgnoreCase))
                    .Select(item => new { type.SourceCandidates, Method = item }))
                .FirstOrDefault(item => string.Equals(item.Method.IlSha256, method.IlSha256, StringComparison.OrdinalIgnoreCase));

            var completeness = closure.DecodeFailures.Count > 0
                ? "blocked_il_decode_failure"
                : closure.ReflectionCalls.Count > 0
                    ? "static_operations_with_reflection_boundary"
                    : closure.DynamicDispatches.Count > 0
                        ? "static_operations_with_dynamic_dispatch_boundary"
                        : "complete_static_operation_closure";

            rules.Add(new HandlerOperationRule(
                usage.Identity,
                usage.Families.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                usage.Keys.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                usage.UseCount,
                usage.SourceExamples.OrderBy(value => value, StringComparer.Ordinal).Take(32).ToArray(),
                method.AssemblyName,
                method.AssemblySha256,
                method.ModuleVersionId,
                method.DeclaringType,
                method.Name,
                method.MetadataToken,
                method.IlSha256,
                methodEvidence?.SourceCandidates ?? Array.Empty<string>(),
                completeness,
                closure.Methods.Count,
                closure.FieldReads.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.FieldWrites.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.PropertyReads.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.PropertyWrites.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.DirectCalls.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.DynamicDispatches.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.ExternalCalls.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.ReflectionCalls.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.RandomSources.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                closure.DecodeFailures.OrderBy(value => value, StringComparer.Ordinal).ToArray()));
        }

        return new HandlerOperationIndex(rules, unresolved);
    }

    private static IndexedAssemblyOperations IndexAssembly(string assemblyPath)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        var moduleVersionId = metadata.GetGuid(metadata.GetModuleDefinition().Mvid).ToString("D");
        var assemblyDefinition = metadata.GetAssemblyDefinition();
        var assemblyName = metadata.GetString(assemblyDefinition.Name);
        var assemblySha256 = HashFile(assemblyPath);
        var methods = new Dictionary<string, IndexedMethodOperations>(StringComparer.OrdinalIgnoreCase);

        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var type = metadata.GetTypeDefinition(typeHandle);
            var declaringType = FullTypeName(metadata, typeHandle);
            foreach (var methodHandle in type.GetMethods())
            {
                var definition = metadata.GetMethodDefinition(methodHandle);
                var token = $"0x{MetadataTokens.GetToken(methodHandle):X8}";
                var ilHash = string.Empty;
                var fieldReads = new HashSet<string>(StringComparer.Ordinal);
                var fieldWrites = new HashSet<string>(StringComparer.Ordinal);
                var propertyReads = new HashSet<string>(StringComparer.Ordinal);
                var propertyWrites = new HashSet<string>(StringComparer.Ordinal);
                var calls = new List<MethodCallOperation>();
                var decodeFailures = new List<string>();

                if (definition.RelativeVirtualAddress != 0)
                {
                    try
                    {
                        var il = pe.GetMethodBody(definition.RelativeVirtualAddress).GetILBytes() ?? Array.Empty<byte>();
                        ilHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(il)).ToLowerInvariant();
                        Decode(metadata, moduleVersionId, assemblySha256, il, fieldReads, fieldWrites,
                            propertyReads, propertyWrites, calls, decodeFailures);
                    }
                    catch (Exception ex) when (ex is BadImageFormatException or ArgumentOutOfRangeException or InvalidOperationException)
                    {
                        decodeFailures.Add(ex.GetType().Name + ": " + ex.Message);
                    }
                }

                var method = new IndexedMethodOperations(
                    Identity(assemblySha256, token),
                    assemblyName,
                    assemblySha256,
                    moduleVersionId,
                    declaringType,
                    metadata.GetString(definition.Name),
                    token,
                    ilHash,
                    fieldReads,
                    fieldWrites,
                    propertyReads,
                    propertyWrites,
                    calls,
                    decodeFailures);
                methods[method.Identity] = method;
            }
        }

        return new IndexedAssemblyOperations(methods);
    }

    private static void Decode(
        MetadataReader metadata,
        string moduleVersionId,
        string assemblySha256,
        byte[] il,
        ISet<string> fieldReads,
        ISet<string> fieldWrites,
        ISet<string> propertyReads,
        ISet<string> propertyWrites,
        ICollection<MethodCallOperation> calls,
        ICollection<string> failures)
    {
        var offset = 0;
        while (offset < il.Length)
        {
            var instructionOffset = offset;
            var first = il[offset++];
            var key = first == 0xFE && offset < il.Length
                ? (ushort)(0xFE00 | il[offset++])
                : first;
            if (!OpCodesByValue.TryGetValue(key, out var opcode))
            {
                failures.Add($"unknown_opcode_0x{key:X4}_at_{instructionOffset}");
                return;
            }

            var operandOffset = offset;
            var operandSize = OperandSize(opcode.OperandType, il, operandOffset, failures);
            if (operandSize < 0 || operandOffset + operandSize > il.Length)
            {
                failures.Add($"invalid_operand_{opcode.Name}_at_{instructionOffset}");
                return;
            }

            if (opcode.OperandType is OperandType.InlineField or OperandType.InlineMethod or
                OperandType.InlineTok or OperandType.InlineType && operandSize >= 4)
            {
                var token = BitConverter.ToInt32(il, operandOffset);
                var member = ResolveMember(metadata, token, moduleVersionId, assemblySha256);
                if (opcode == OpCodes.Ldfld || opcode == OpCodes.Ldsfld || opcode == OpCodes.Ldflda || opcode == OpCodes.Ldsflda)
                    fieldReads.Add(member.DisplayName);
                else if (opcode == OpCodes.Stfld || opcode == OpCodes.Stsfld)
                    fieldWrites.Add(member.DisplayName);

                if (opcode == OpCodes.Call || opcode == OpCodes.Callvirt || opcode == OpCodes.Newobj ||
                    opcode == OpCodes.Ldftn || opcode == OpCodes.Ldvirtftn)
                {
                    var dispatch = opcode == OpCodes.Callvirt || opcode == OpCodes.Ldvirtftn
                        ? "dynamic_dispatch"
                        : opcode == OpCodes.Newobj
                            ? "constructor"
                            : opcode == OpCodes.Ldftn
                                ? "function_pointer"
                                : "direct";
                    calls.Add(new MethodCallOperation(member.DisplayName, member.MethodIdentity, dispatch));
                    if (member.MemberName.StartsWith("get_", StringComparison.Ordinal))
                        propertyReads.Add(member.DeclaringType + "." + member.MemberName[4..]);
                    else if (member.MemberName.StartsWith("set_", StringComparison.Ordinal))
                        propertyWrites.Add(member.DeclaringType + "." + member.MemberName[4..]);
                }
            }

            offset += operandSize;
        }
    }

    private static int OperandSize(OperandType operandType, byte[] il, int offset, ICollection<string> failures)
    {
        switch (operandType)
        {
            case OperandType.InlineNone:
                return 0;
            case OperandType.ShortInlineBrTarget:
            case OperandType.ShortInlineI:
            case OperandType.ShortInlineVar:
                return 1;
            case OperandType.InlineVar:
                return 2;
            case OperandType.InlineBrTarget:
            case OperandType.InlineField:
            case OperandType.InlineI:
            case OperandType.InlineMethod:
            case OperandType.InlineSig:
            case OperandType.InlineString:
            case OperandType.InlineTok:
            case OperandType.InlineType:
            case OperandType.ShortInlineR:
                return 4;
            case OperandType.InlineI8:
            case OperandType.InlineR:
                return 8;
            case OperandType.InlineSwitch:
                if (offset + 4 > il.Length)
                    return -1;
                var count = BitConverter.ToInt32(il, offset);
                if (count < 0 || count > (il.Length - offset - 4) / 4)
                {
                    failures.Add($"invalid_switch_count_{count}_at_{offset}");
                    return -1;
                }
                return 4 + count * 4;
            default:
                failures.Add("unknown_operand_type_" + operandType);
                return -1;
        }
    }

    private static ResolvedMember ResolveMember(
        MetadataReader metadata,
        int token,
        string moduleVersionId,
        string assemblySha256)
    {
        try
        {
            var handle = MetadataTokens.EntityHandle(token);
            return handle.Kind switch
            {
                HandleKind.MethodDefinition => ResolveMethodDefinition(metadata, (MethodDefinitionHandle)handle, moduleVersionId, assemblySha256),
                HandleKind.MemberReference => ResolveMemberReference(metadata, (MemberReferenceHandle)handle),
                HandleKind.MethodSpecification => ResolveMethodSpecification(metadata, (MethodSpecificationHandle)handle, moduleVersionId, assemblySha256),
                HandleKind.FieldDefinition => ResolveFieldDefinition(metadata, (FieldDefinitionHandle)handle),
                HandleKind.TypeDefinition => TypeMember(FullTypeName(metadata, (TypeDefinitionHandle)handle)),
                HandleKind.TypeReference => TypeMember(FullTypeName(metadata, (TypeReferenceHandle)handle)),
                _ => new ResolvedMember($"token:0x{token:X8}", string.Empty, string.Empty, null)
            };
        }
        catch (Exception)
        {
            return new ResolvedMember($"token:0x{token:X8}", string.Empty, string.Empty, null);
        }
    }

    private static ResolvedMember ResolveMethodDefinition(
        MetadataReader metadata,
        MethodDefinitionHandle handle,
        string moduleVersionId,
        string assemblySha256)
    {
        var definition = metadata.GetMethodDefinition(handle);
        var type = FullTypeName(metadata, definition.GetDeclaringType());
        var name = metadata.GetString(definition.Name);
        var token = $"0x{MetadataTokens.GetToken(handle):X8}";
        return new ResolvedMember(type + "." + name, type, name, Identity(assemblySha256, token));
    }

    private static ResolvedMember ResolveMemberReference(MetadataReader metadata, MemberReferenceHandle handle)
    {
        var reference = metadata.GetMemberReference(handle);
        var type = ResolveParentType(metadata, reference.Parent);
        var name = metadata.GetString(reference.Name);
        return new ResolvedMember(type + "." + name, type, name, null);
    }

    private static ResolvedMember ResolveMethodSpecification(
        MetadataReader metadata,
        MethodSpecificationHandle handle,
        string moduleVersionId,
        string assemblySha256)
    {
        var method = metadata.GetMethodSpecification(handle).Method;
        return method.Kind == HandleKind.MethodDefinition
            ? ResolveMethodDefinition(metadata, (MethodDefinitionHandle)method, moduleVersionId, assemblySha256)
            : method.Kind == HandleKind.MemberReference
                ? ResolveMemberReference(metadata, (MemberReferenceHandle)method)
                : new ResolvedMember("method_specification", string.Empty, string.Empty, null);
    }

    private static ResolvedMember ResolveFieldDefinition(MetadataReader metadata, FieldDefinitionHandle handle)
    {
        var field = metadata.GetFieldDefinition(handle);
        var type = FullTypeName(metadata, field.GetDeclaringType());
        var name = metadata.GetString(field.Name);
        return new ResolvedMember(type + "." + name, type, name, null);
    }

    private static string ResolveParentType(MetadataReader metadata, EntityHandle parent) => parent.Kind switch
    {
        HandleKind.TypeDefinition => FullTypeName(metadata, (TypeDefinitionHandle)parent),
        HandleKind.TypeReference => FullTypeName(metadata, (TypeReferenceHandle)parent),
        HandleKind.TypeSpecification => "type_specification",
        HandleKind.MethodDefinition => ResolveMethodDefinition(metadata, (MethodDefinitionHandle)parent, string.Empty, string.Empty).DisplayName,
        _ => parent.Kind.ToString()
    };

    private static ResolvedMember TypeMember(string type) => new(type, type, string.Empty, null);

    private static MutableHandlerUsageMap LoadHandlerUsages(
        string runtimeSemanticsPath,
        IReadOnlyList<IndexedAssemblyOperations> assemblies)
    {
        using var document = JsonDocument.Parse(File.ReadAllBytes(runtimeSemanticsPath));
        var root = document.RootElement;
        var result = new MutableHandlerUsageMap();
        var assemblyShaByMvid = assemblies
            .SelectMany(row => row.Methods.Values)
            .GroupBy(row => row.ModuleVersionId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().AssemblySha256, StringComparer.OrdinalIgnoreCase);
        foreach (var condition in root.GetProperty("parsed_conditions").EnumerateArray())
        {
            var source = condition.GetProperty("sourceAsset").GetString() + ":" + condition.GetProperty("sourcePath").GetString();
            foreach (var clause in condition.GetProperty("clauses").EnumerateArray())
                AddUsage(result, assemblyShaByMvid, clause.GetProperty("handler"), source);
        }
        foreach (var eventRow in root.GetProperty("parsed_events").EnumerateArray())
        {
            var source = eventRow.GetProperty("sourceAsset").GetString() + ":" + eventRow.GetProperty("eventKey").GetString();
            foreach (var token in eventRow.GetProperty("preconditions").EnumerateArray())
                AddUsage(result, assemblyShaByMvid, token.GetProperty("handler"), source);
            foreach (var token in eventRow.GetProperty("commands").EnumerateArray())
                AddUsage(result, assemblyShaByMvid, token.GetProperty("handler"), source);
        }
        if (root.TryGetProperty("parsed_trigger_actions", out var triggerRows) &&
            triggerRows.ValueKind == JsonValueKind.Array)
        {
            foreach (var triggerRow in triggerRows.EnumerateArray())
            {
                var source = "Data/TriggerActions:" +
                             (triggerRow.GetProperty("id").GetString() ?? string.Empty);
                foreach (var action in triggerRow.GetProperty("actions").EnumerateArray())
                    AddUsage(result, assemblyShaByMvid, action.GetProperty("handler"), source);
            }
        }
        return result;
    }

    private static void AddUsage(
        MutableHandlerUsageMap result,
        IReadOnlyDictionary<string, string> assemblyShaByMvid,
        JsonElement handler,
        string source)
    {
        if (handler.ValueKind != JsonValueKind.Object)
            return;
        var mvid = handler.GetProperty("moduleVersionId").GetString() ?? string.Empty;
        var token = handler.GetProperty("metadataToken").GetString() ?? string.Empty;
        var assembly = handler.GetProperty("assemblyName").GetString() ?? string.Empty;
        var assemblySha = assemblyShaByMvid.TryGetValue(mvid, out var hash) ? hash : string.Empty;
        var family = handler.GetProperty("family").GetString() ?? string.Empty;
        var key = handler.GetProperty("key").GetString() ?? string.Empty;
        var identity = Identity(assemblySha, token);
        result.AddOrUpdate(identity,
            () => new MutableHandlerUsage(
                identity,
                family,
                assembly,
                handler.GetProperty("declaringType").GetString() ?? string.Empty,
                handler.GetProperty("methodName").GetString() ?? string.Empty,
                token),
            row => row.Add(family, key, source));
    }

    private static OperationClosure Closure(IndexedMethodOperations root, IReadOnlyDictionary<string, IndexedMethodOperations> methods)
    {
        var closure = new OperationClosure();
        var pending = new Stack<IndexedMethodOperations>();
        pending.Push(root);
        while (pending.TryPop(out var method))
        {
            if (!closure.Methods.Add(method.Identity))
                continue;
            closure.FieldReads.UnionWith(method.FieldReads);
            closure.FieldWrites.UnionWith(method.FieldWrites);
            closure.PropertyReads.UnionWith(method.PropertyReads);
            closure.PropertyWrites.UnionWith(method.PropertyWrites);
            foreach (var failure in method.DecodeFailures)
                closure.DecodeFailures.Add(method.Identity + ":" + failure);
            foreach (var call in method.Calls)
            {
                closure.DirectCalls.Add(call.DisplayName);
                if (call.Dispatch == "dynamic_dispatch")
                    closure.DynamicDispatches.Add(call.DisplayName);
                if (IsReflection(call.DisplayName))
                    closure.ReflectionCalls.Add(call.DisplayName);
                if (IsRandom(call.DisplayName))
                    closure.RandomSources.Add(call.DisplayName);
                if (call.MethodIdentity is not null && methods.TryGetValue(call.MethodIdentity, out var target))
                    pending.Push(target);
                else
                    closure.ExternalCalls.Add(call.DisplayName);
            }
        }
        foreach (var field in closure.FieldReads.Concat(closure.FieldWrites).Where(IsRandom))
            closure.RandomSources.Add(field);
        return closure;
    }

    private static bool IsReflection(string value) =>
        value.Contains("System.Reflection", StringComparison.Ordinal) ||
        value.EndsWith(".Invoke", StringComparison.Ordinal) ||
        value.EndsWith(".GetMethod", StringComparison.Ordinal) ||
        value.EndsWith(".GetField", StringComparison.Ordinal) ||
        value.EndsWith(".GetProperty", StringComparison.Ordinal);

    private static bool IsRandom(string value) =>
        value.Contains("System.Random", StringComparison.Ordinal) ||
        value.Contains("Game1.random", StringComparison.Ordinal) ||
        value.Contains("Random.Shared", StringComparison.Ordinal) ||
        value.Contains("CreateRandom", StringComparison.Ordinal);

    private static ushort OpCodeKey(OpCode opcode) => unchecked((ushort)opcode.Value);

    private static string Identity(string assemblySha256, string metadataToken) =>
        assemblySha256 + ":" + metadataToken.ToLowerInvariant();

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string FullTypeName(MetadataReader metadata, TypeDefinitionHandle handle)
    {
        var definition = metadata.GetTypeDefinition(handle);
        var name = metadata.GetString(definition.Name);
        var declaring = definition.GetDeclaringType();
        if (!declaring.IsNil)
            return FullTypeName(metadata, declaring) + "+" + name;
        var ns = metadata.GetString(definition.Namespace);
        return string.IsNullOrWhiteSpace(ns) ? name : ns + "." + name;
    }

    private static string FullTypeName(MetadataReader metadata, TypeReferenceHandle handle)
    {
        var reference = metadata.GetTypeReference(handle);
        var name = metadata.GetString(reference.Name);
        var ns = metadata.GetString(reference.Namespace);
        return string.IsNullOrWhiteSpace(ns) ? name : ns + "." + name;
    }
}

internal sealed record HandlerOperationIndex(
    IReadOnlyList<HandlerOperationRule> Rules,
    IReadOnlyList<string> UnresolvedMethodIdentities);

internal sealed record HandlerOperationRule(
    string Identity,
    IReadOnlyList<string> Families,
    IReadOnlyList<string> Keys,
    int UsageCount,
    IReadOnlyList<string> SourceExamples,
    string AssemblyName,
    string AssemblySha256,
    string ModuleVersionId,
    string DeclaringType,
    string MethodName,
    string MetadataToken,
    string IlSha256,
    IReadOnlyList<string> SourceCandidates,
    string Completeness,
    int ClosureMethodCount,
    IReadOnlyList<string> FieldReads,
    IReadOnlyList<string> FieldWrites,
    IReadOnlyList<string> PropertyReads,
    IReadOnlyList<string> PropertyWrites,
    IReadOnlyList<string> DirectCalls,
    IReadOnlyList<string> DynamicDispatches,
    IReadOnlyList<string> ExternalCalls,
    IReadOnlyList<string> ReflectionCalls,
    IReadOnlyList<string> RandomSources,
    IReadOnlyList<string> DecodeFailures);

internal sealed record IndexedAssemblyOperations(IReadOnlyDictionary<string, IndexedMethodOperations> Methods);

internal sealed record IndexedMethodOperations(
    string Identity,
    string AssemblyName,
    string AssemblySha256,
    string ModuleVersionId,
    string DeclaringType,
    string Name,
    string MetadataToken,
    string IlSha256,
    IReadOnlySet<string> FieldReads,
    IReadOnlySet<string> FieldWrites,
    IReadOnlySet<string> PropertyReads,
    IReadOnlySet<string> PropertyWrites,
    IReadOnlyList<MethodCallOperation> Calls,
    IReadOnlyList<string> DecodeFailures);

internal sealed record MethodCallOperation(string DisplayName, string? MethodIdentity, string Dispatch);

internal sealed record ResolvedMember(
    string DisplayName,
    string DeclaringType,
    string MemberName,
    string? MethodIdentity);

internal sealed class MutableHandlerUsage
{
    public MutableHandlerUsage(string identity, string family, string assemblyName, string declaringType, string methodName, string metadataToken)
    {
        Identity = identity;
        AssemblyName = assemblyName;
        DeclaringType = declaringType;
        MethodName = methodName;
        MetadataToken = metadataToken;
        if (!string.IsNullOrWhiteSpace(family))
            Families.Add(family);
    }

    public string Identity { get; }
    public string AssemblyName { get; }
    public string DeclaringType { get; }
    public string MethodName { get; }
    public string MetadataToken { get; }
    public HashSet<string> Families { get; } = new(StringComparer.Ordinal);
    public HashSet<string> Keys { get; } = new(StringComparer.Ordinal);
    public HashSet<string> SourceExamples { get; } = new(StringComparer.Ordinal);
    public int UseCount { get; private set; }

    public void Add(string family, string key, string source)
    {
        if (!string.IsNullOrWhiteSpace(family))
            Families.Add(family);
        if (!string.IsNullOrWhiteSpace(key))
            Keys.Add(key);
        if (!string.IsNullOrWhiteSpace(source))
            SourceExamples.Add(source);
        UseCount++;
    }
}

internal sealed class MutableHandlerUsageMap
{
    public Dictionary<string, MutableHandlerUsage> Values { get; } = new(StringComparer.OrdinalIgnoreCase);

    public void AddOrUpdate(string key, Func<MutableHandlerUsage> create, Action<MutableHandlerUsage> update)
    {
        if (!Values.TryGetValue(key, out var value))
        {
            value = create();
            Values[key] = value;
        }
        update(value);
    }
}

internal sealed class OperationClosure
{
    public HashSet<string> Methods { get; } = new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> FieldReads { get; } = new(StringComparer.Ordinal);
    public HashSet<string> FieldWrites { get; } = new(StringComparer.Ordinal);
    public HashSet<string> PropertyReads { get; } = new(StringComparer.Ordinal);
    public HashSet<string> PropertyWrites { get; } = new(StringComparer.Ordinal);
    public HashSet<string> DirectCalls { get; } = new(StringComparer.Ordinal);
    public HashSet<string> DynamicDispatches { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ExternalCalls { get; } = new(StringComparer.Ordinal);
    public HashSet<string> ReflectionCalls { get; } = new(StringComparer.Ordinal);
    public HashSet<string> RandomSources { get; } = new(StringComparer.Ordinal);
    public HashSet<string> DecodeFailures { get; } = new(StringComparer.Ordinal);
}
