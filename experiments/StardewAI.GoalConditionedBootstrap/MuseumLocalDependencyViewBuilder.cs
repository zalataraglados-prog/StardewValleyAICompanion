using System.Text.Json;
using System.Text.Json.Nodes;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MuseumLocalDependencyViewBuilder
{
    public const string DirectionId = MuseumTeacherCorpus.Direction;
    public const string CertificateNodeId = "dependency.complete_museum_collection.current_source_certificate";
    private const string Prefix = "dependency.complete_museum_collection.";
    private const string MetadataKey = "museum_local_method_view";

    public static JsonObject Build(string corpusPath, string dependenciesPath) =>
        Rebuild(MuseumTeacherCorpusBuilder.Verify(RequiredFile(corpusPath)), corpusPath, dependenciesPath);

    public static JsonObject Verify(string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Museum local method metadata is missing.");
        var rebuilt = Build(Text(metadata, "corpus_path"), Text(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Museum local dependency view differs from its complete original sources.");
        return rebuilt;
    }

    internal static void VerifyForCoverage(MuseumTeacherCorpus verifiedCorpus, string corpusPath, string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Executable Museum method lacks its local source certificate.");
        Require(Path.GetFullPath(Text(metadata, "corpus_path")).Equals(Path.GetFullPath(corpusPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Museum method certificate belongs to another corpus.");
        // Coverage already replayed the complete corpus and every original native source.
        var rebuilt = Rebuild(verifiedCorpus, corpusPath, Text(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Executable Museum method is not the independently rebuilt conditional view.");
    }

    private static JsonObject Rebuild(MuseumTeacherCorpus corpus, string corpusPath, string dependenciesPath)
    {
        corpusPath = RequiredFile(corpusPath);
        dependenciesPath = RequiredFile(dependenciesPath);
        Require(JsonNode.DeepEquals(ReadObject(corpusPath), JsonSerializer.SerializeToNode(corpus, JsonDefaults.Options)),
            "Museum corpus changed after its complete source replay.");
        foreach (var row in corpus.Rows)
            Require(CurrentTeacherFrontierSupport.HashFile(row.JoinPath) == row.JoinSha256, "Museum native join changed after corpus replay.");
        return Project(ReadObject(dependenciesPath), corpus, corpusPath, CurrentTeacherFrontierSupport.HashFile(corpusPath),
            dependenciesPath, CurrentTeacherFrontierSupport.HashFile(dependenciesPath));
    }

    private static JsonObject ReadObject(string path) => JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(path)))!.AsObject();
    private static string Text(JsonNode value, string name) => value[name]?.GetValue<string>() ??
        throw new InvalidDataException("Museum local view is missing " + name + ".");
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
