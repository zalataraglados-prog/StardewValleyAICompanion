using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace StardewAI.KnowledgeCompiler;

internal sealed record DecompiledActionMethod(
    string RuntimeType,
    string Member,
    string Signature,
    string RelativeSourcePath,
    int StartLine,
    int EndLine,
    string BodySha256,
    MethodDeclarationSyntax Syntax);

internal sealed record DecompiledMinigameType(
    string RuntimeType,
    string RelativeSourcePath,
    int StartLine,
    int EndLine);

internal sealed class DecompiledActionSourceIndex
{
    private static readonly HashSet<string> PlayerEntryMembers = new(StringComparer.Ordinal)
    {
        "checkAction",
        "checkForAction",
        "performAction",
        "performTouchAction",
        "DoFunction",
        "beginUsing",
        "onRelease",
        "performUseAction",
        "placementAction",
        "receiveLeftClick",
        "receiveRightClick",
        "receiveKeyPress"
    };

    public IReadOnlyList<DecompiledActionMethod> Methods { get; init; } =
        Array.Empty<DecompiledActionMethod>();

    public IReadOnlyList<DecompiledMinigameType> MinigameTypes { get; init; } =
        Array.Empty<DecompiledMinigameType>();

    public static DecompiledActionSourceIndex Build(string root)
    {
        var methods = new List<DecompiledActionMethod>();
        var minigames = new List<DecompiledMinigameType>();
        foreach (var path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!IsActionBearingPath(relative))
                continue;

            var source = File.ReadAllText(path);
            var tree = CSharpSyntaxTree.ParseText(
                source,
                CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Latest),
                path);
            var rootNode = tree.GetRoot();
            foreach (var method in rootNode.DescendantNodes().OfType<MethodDeclarationSyntax>())
            {
                var member = method.Identifier.ValueText;
                if (!PlayerEntryMembers.Contains(member))
                    continue;

                var runtimeType = method.Ancestors()
                    .OfType<TypeDeclarationSyntax>()
                    .FirstOrDefault()?.Identifier.ValueText;
                if (string.IsNullOrWhiteSpace(runtimeType))
                    continue;

                var span = tree.GetLineSpan(method.Span);
                var bodyText = method.Body?.ToFullString() ??
                    method.ExpressionBody?.ToFullString() ??
                    string.Empty;
                methods.Add(new(
                    runtimeType,
                    member,
                    NormalizeSignature(method),
                    relative,
                    span.StartLinePosition.Line + 1,
                    span.EndLinePosition.Line + 1,
                    Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(bodyText)))
                        .ToLowerInvariant(),
                    method));
            }

            foreach (var type in rootNode.DescendantNodes().OfType<TypeDeclarationSyntax>())
            {
                if (type.BaseList is null ||
                    !type.BaseList.Types.Any(row => IsExactMinigameType(row.Type)))
                {
                    continue;
                }

                var span = tree.GetLineSpan(type.Span);
                minigames.Add(new(
                    type.Identifier.ValueText,
                    relative,
                    span.StartLinePosition.Line + 1,
                    span.EndLinePosition.Line + 1));
            }
        }

        return new DecompiledActionSourceIndex
        {
            Methods = methods
                .OrderBy(row => row.RelativeSourcePath, StringComparer.Ordinal)
                .ThenBy(row => row.StartLine)
                .ToArray(),
            MinigameTypes = minigames
                .OrderBy(row => row.RelativeSourcePath, StringComparer.Ordinal)
                .ThenBy(row => row.StartLine)
                .ToArray()
        };
    }

    private static bool IsExactMinigameType(TypeSyntax type) => type switch
    {
        IdentifierNameSyntax identifier =>
            identifier.Identifier.ValueText == "IMinigame",
        QualifiedNameSyntax qualified =>
            qualified.Right.Identifier.ValueText == "IMinigame",
        AliasQualifiedNameSyntax alias =>
            alias.Name.Identifier.ValueText == "IMinigame",
        _ => false
    };

    private static string NormalizeSignature(MethodDeclarationSyntax method)
    {
        var modifiers = string.Join(' ', method.Modifiers.Select(row => row.ValueText));
        var parameters = string.Join(
            ", ",
            method.ParameterList.Parameters.Select(row =>
                $"{row.Type?.WithoutTrivia().ToString() ?? "?"} {row.Identifier.ValueText}"));
        return string.Join(
            ' ',
            new[] { modifiers, method.ReturnType.WithoutTrivia().ToString(), method.Identifier.ValueText }
                .Where(value => !string.IsNullOrWhiteSpace(value))) +
            $"({parameters})";
    }

    private static bool IsActionBearingPath(string relative)
    {
        return relative.Contains("/Tools/", StringComparison.Ordinal) ||
            relative.Contains("/Menus/", StringComparison.Ordinal) ||
            relative.Contains("/Minigames/", StringComparison.Ordinal) ||
            relative.Contains("/Locations/", StringComparison.Ordinal) ||
            relative.Contains("/TerrainFeatures/", StringComparison.Ordinal) ||
            relative.EndsWith("/GameLocation.cs", StringComparison.Ordinal) ||
            relative.EndsWith("/Event.cs", StringComparison.Ordinal) ||
            relative.EndsWith("/Object.cs", StringComparison.Ordinal) ||
            relative.EndsWith("/Objects/Sign.cs", StringComparison.Ordinal) ||
            relative.EndsWith("/Item.cs", StringComparison.Ordinal);
    }
}
