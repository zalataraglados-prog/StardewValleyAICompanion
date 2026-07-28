using System.Text.RegularExpressions;
using StardewAI.Contracts.Capabilities;

namespace StardewAI.KnowledgeCompiler;

internal sealed record QuestActionCoverageResult(
    string SourceStatus,
    string QuestSourceDirectory,
    string ObjectiveSourceDirectory,
    string[] DiscoveredOrdinaryRuntimeTypes,
    string[] DiscoveredSpecialOrderObjectiveRuntimeTypes,
    string[] UncataloguedOrdinaryRuntimeTypes,
    string[] UncataloguedSpecialOrderObjectiveRuntimeTypes,
    string[] CatalogOrdinaryTypesMissingFromSource,
    string[] CatalogSpecialOrderTypesMissingFromSource);

internal sealed class QuestActionCoverageBuilder
{
    private static readonly Regex OrdinarySubclassPattern = new(
        @"\bclass\s+([A-Za-z0-9_]+)\s*:\s*Quest\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ObjectiveSubclassPattern = new(
        @"\bclass\s+([A-Za-z0-9_]+)\s*:\s*OrderObjective\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public QuestActionCoverageResult Build(string? decompileRoot)
    {
        if (string.IsNullOrWhiteSpace(decompileRoot) || !Directory.Exists(decompileRoot))
        {
            return Empty("decompile_source_not_supplied");
        }

        var questDirectory = ResolveDirectory(decompileRoot, "Quests");
        var objectiveDirectory = ResolveDirectory(decompileRoot, Path.Combine("SpecialOrders", "Objectives"));
        if (questDirectory is null || objectiveDirectory is null)
        {
            return Empty("native_quest_source_directories_not_found");
        }

        var ordinary = Discover(questDirectory, OrdinarySubclassPattern)
            .Concat(File.Exists(Path.Combine(questDirectory, "Quest.cs")) ? new[] { "Quest" } : Array.Empty<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var objectives = Discover(objectiveDirectory, ObjectiveSubclassPattern)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var expectedOrdinary = QuestActionCoverageCatalog.OrdinaryRuntimeTypes.ToHashSet(StringComparer.Ordinal);
        var expectedObjectives = QuestActionCoverageCatalog.SpecialOrderObjectiveRuntimeTypes.ToHashSet(StringComparer.Ordinal);

        return new QuestActionCoverageResult(
            "native_decompile_scanned",
            questDirectory,
            objectiveDirectory,
            ordinary,
            objectives,
            ordinary.Where(value => !expectedOrdinary.Contains(value)).ToArray(),
            objectives.Where(value => !expectedObjectives.Contains(value)).ToArray(),
            expectedOrdinary.Where(value => !ordinary.Contains(value, StringComparer.Ordinal)).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            expectedObjectives.Where(value => !objectives.Contains(value, StringComparer.Ordinal)).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static IEnumerable<string> Discover(string directory, Regex pattern)
    {
        foreach (var path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
        {
            var match = pattern.Match(File.ReadAllText(path));
            if (match.Success)
            {
                yield return match.Groups[1].Value;
            }
        }
    }

    private static string? ResolveDirectory(string root, string suffix)
    {
        var candidates = new[]
        {
            Path.Combine(root, suffix),
            Path.Combine(root, "StardewValley", suffix),
            Path.Combine(root, "StardewValley", "StardewValley", suffix)
        };
        return candidates.FirstOrDefault(Directory.Exists);
    }

    private static QuestActionCoverageResult Empty(string status)
    {
        return new QuestActionCoverageResult(
            status,
            string.Empty,
            string.Empty,
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>(),
            Array.Empty<string>());
    }
}
