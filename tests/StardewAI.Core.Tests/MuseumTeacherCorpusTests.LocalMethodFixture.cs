using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumTeacherCorpusTests
{
    private const string MuseumPrefix = "dependency.complete_museum_collection.";
    private static readonly JsonSerializerOptions MuseumJson = new(JsonSerializerDefaults.Web);
    private sealed record MuseumViewFiles(MuseumTeacherCorpus Corpus, string CorpusPath, string Dependencies, string View, GoalMethodFrontierReport Frontier);

    private static MuseumTeacherCorpus LocalMuseumCorpus() => new("request", "hash", "fixture", new[] { Row("train", "a") });

    // Verified-source protocol port only: public Build additionally proves the real corpus/native sources.
    private static MuseumViewFiles MuseumViewFixture()
    {
        var root = Path.Combine(Path.GetTempPath(), "museum-method-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var dependencies = Path.Combine(root, "dependencies.json");
        File.WriteAllText(dependencies, MuseumGraph().ToJsonString());
        var join = Path.Combine(root, "join.json");
        File.WriteAllText(join, "{\"test\":\"already_verified_source_port_not_a_native_proof\"}");
        var corpus = LocalMuseumCorpus(); var row = corpus.Rows[0];
        corpus = corpus with { Rows = new[] { row with { JoinPath = join, JoinSha256 = StructuredPolicyCheckpointStore.HashFile(join),
            Evidence = row.Evidence with { SourceSha256 = new Dictionary<string, string>(row.Evidence.SourceSha256)
                { ["comparison.dependencies"] = StructuredPolicyCheckpointStore.HashFile(dependencies) } } } } };
        var corpusPath = Path.Combine(root, "corpus.json");
        File.WriteAllText(corpusPath, JsonSerializer.Serialize(corpus, MuseumJson));
        var viewPath = Path.Combine(root, "view.json");
        var view = (JsonObject)typeof(MuseumLocalDependencyViewBuilder).GetMethod("Rebuild", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { corpus, corpusPath, dependencies })!;
        File.WriteAllText(viewPath, view.ToJsonString());
        var frontier = Frontier();
        frontier.Methods[0] = frontier.Methods[0] with { Status = "executable_frontier" };
        frontier.DependencyExpansionSha256 = StructuredPolicyCheckpointStore.HashFile(viewPath);
        return new(corpus, corpusPath, dependencies, viewPath, frontier);
    }

    private static void ApplyMuseumView(MuseumViewFiles f, System.Collections.IDictionary evidence) =>
        typeof(GoalMethodTeacherCoverageBuilder).GetMethod("ApplyMuseumCorpus", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null,
            new object[] { f.Corpus, GoalMethodTeacherCoverageSourceKinds.MuseumTerminalCorpus, f.Frontier, evidence, f.CorpusPath, f.View });

    private static JsonObject ProjectMuseum(JsonObject graph, MuseumTeacherCorpus corpus, string dependencyHash = "dependencies") =>
        (JsonObject)typeof(MuseumLocalDependencyViewBuilder).GetMethod("Project", BindingFlags.Static | BindingFlags.NonPublic)!
            .Invoke(null, new object[] { graph, corpus, "corpus", "corpus-hash", "dependencies", dependencyHash })!;

    private static JsonObject MuseumDirection(JsonObject graph) => graph["directions"]!.AsArray()
        .Single(row => row!["direction_id"]!.GetValue<string>() == MuseumTeacherCorpus.Direction)!.AsObject();

    private static JsonObject MuseumNode(JsonObject direction, string suffix) => direction["nodes"]!.AsArray()
        .Single(row => row!["id"]!.GetValue<string>() == MuseumPrefix + suffix)!.AsObject();

    private static JsonObject MuseumGraph()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "experiments", "goal-method-dependency-expansions.v1.json");
            if (File.Exists(path)) return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        }
        throw new FileNotFoundException("Original dependency graph not found.");
    }
}
