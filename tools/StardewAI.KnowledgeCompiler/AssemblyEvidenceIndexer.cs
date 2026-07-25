using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;

namespace StardewAI.KnowledgeCompiler;

internal sealed class AssemblyEvidenceIndexer
{
    public AssemblyEvidenceIndex Build(string assemblyPath, string sourceRoot)
    {
        assemblyPath = Path.GetFullPath(assemblyPath);
        sourceRoot = Path.GetFullPath(sourceRoot);
        var sourceFiles = Directory.EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new DecompiledSourceFile(
                Path.GetRelativePath(sourceRoot, path).Replace('\\', '/'),
                new FileInfo(path).Length,
                HashFile(path)))
            .ToArray();
        var sourceByTypeName = sourceFiles
            .GroupBy(row => Path.GetFileNameWithoutExtension(row.RelativePath), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Select(row => row.RelativePath).ToArray(), StringComparer.Ordinal);

        using var stream = File.OpenRead(assemblyPath);
        using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
        var metadata = pe.GetMetadataReader();
        var module = metadata.GetModuleDefinition();
        var assembly = metadata.GetAssemblyDefinition();

        var types = new List<DecompiledTypeEvidence>();
        foreach (var typeHandle in metadata.TypeDefinitions)
        {
            var definition = metadata.GetTypeDefinition(typeHandle);
            var fullName = FullTypeName(metadata, typeHandle);
            var simpleName = StripGenericArity(metadata.GetString(definition.Name));
            sourceByTypeName.TryGetValue(simpleName, out var candidates);
            var methods = new List<DecompiledMethodEvidence>();

            foreach (var methodHandle in definition.GetMethods())
            {
                var method = metadata.GetMethodDefinition(methodHandle);
                var signature = metadata.GetBlobBytes(method.Signature);
                string? ilHash = null;
                var bodyStatus = "no_il_body";
                if (method.RelativeVirtualAddress != 0)
                {
                    try
                    {
                        var il = pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
                        if (il is not null)
                        {
                            ilHash = Hash(il);
                            bodyStatus = "il_hashed";
                        }
                    }
                    catch (BadImageFormatException)
                    {
                        bodyStatus = "invalid_il_body";
                    }
                }

                methods.Add(new DecompiledMethodEvidence(
                    metadata.GetString(method.Name),
                    $"0x{MetadataTokens.GetToken(methodHandle):X8}",
                    method.RelativeVirtualAddress,
                    method.Attributes.ToString(),
                    Hash(signature),
                    ilHash,
                    bodyStatus));
            }

            types.Add(new DecompiledTypeEvidence(
                fullName,
                $"0x{MetadataTokens.GetToken(typeHandle):X8}",
                definition.Attributes.ToString(),
                candidates ?? Array.Empty<string>(),
                methods));
        }

        return new AssemblyEvidenceIndex(
            assemblyPath,
            new FileInfo(assemblyPath).Length,
            HashFile(assemblyPath),
            metadata.GetString(assembly.Name),
            assembly.Version.ToString(),
            metadata.GetGuid(module.Mvid).ToString("D"),
            sourceRoot,
            sourceFiles,
            types);
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

    private static string StripGenericArity(string value)
    {
        var index = value.IndexOf('`');
        return index < 0 ? value : value[..index];
    }

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}

internal sealed record AssemblyEvidenceIndex(
    string AssemblyPath,
    long AssemblyBytes,
    string AssemblySha256,
    string AssemblyName,
    string AssemblyVersion,
    string ModuleVersionId,
    string DecompiledSourceRoot,
    IReadOnlyList<DecompiledSourceFile> SourceFiles,
    IReadOnlyList<DecompiledTypeEvidence> Types);

internal sealed record DecompiledSourceFile(string RelativePath, long Bytes, string Sha256);

internal sealed record DecompiledTypeEvidence(
    string FullName,
    string MetadataToken,
    string Attributes,
    IReadOnlyList<string> SourceCandidates,
    IReadOnlyList<DecompiledMethodEvidence> Methods);

internal sealed record DecompiledMethodEvidence(
    string Name,
    string MetadataToken,
    int RelativeVirtualAddress,
    string Attributes,
    string SignatureSha256,
    string? IlSha256,
    string BodyStatus);
