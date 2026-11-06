using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class FriendshipLocalDependencyViewBuilder
{
    public const string DirectionId = "raise_friendships";
    public const string CertificateNodeId = "dependency.raise_friendships.current_source_certificate";
    private const string MetadataKey = "friendship_local_method_view";
    private const string Prefix = "dependency.raise_friendships.";

    public static JsonObject Build(string corpusPath, string dependenciesPath) =>
        Rebuild(FriendshipTeacherCorpusBuilder.Verify(RequiredFile(corpusPath)), corpusPath, dependenciesPath);

    public static JsonObject Verify(string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Friendship local view metadata is missing.");
        var rebuilt = Build(RequiredString(metadata, "corpus_path"), RequiredString(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Friendship local dependency view differs from its original sources.");
        return rebuilt;
    }

    internal static void VerifyForCoverage(FriendshipTeacherCorpus verifiedCorpus, string corpusPath, string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Executable Friendship method lacks its local source certificate.");
        Require(Path.GetFullPath(RequiredString(metadata, "corpus_path")).Equals(Path.GetFullPath(corpusPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Friendship method certificate belongs to another corpus.");
        // Coverage already replayed the corpus, including every native action and nonce.
        var rebuilt = Rebuild(verifiedCorpus, corpusPath, RequiredString(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Executable Friendship method is not the independently rebuilt local view.");
    }

    private static JsonObject Rebuild(FriendshipTeacherCorpus corpus, string corpusPath, string dependenciesPath)
    {
        corpusPath = RequiredFile(corpusPath);
        dependenciesPath = RequiredFile(dependenciesPath);
        var routes = corpus.Rows.Select(source =>
        {
            Require(CurrentTeacherFrontierSupport.HashFile(source.TeacherRowPath) == source.TeacherRowSha256,
                "Friendship source row changed after replay.");
            var row = ReadTyped<PolicyDecisionTrajectoryEnvelope>(source.TeacherRowPath, PolicyTrajectoryVersionPins.TrajectorySchema);
            Require(GrandpaDirectionCatalog.Entries.Single(entry => entry.DirectionId == DirectionId)
                .PermittedOptionIds.Contains(row.Selection.OptionId) && row.SourceStateHash == source.BeforeStateHash &&
                row.Selection.CandidateId == source.SelectedCandidateId &&
                row.Audit.TeacherSupervision!.SelectionPolicyId == new CurrentFriendshipTeacherPreference().ComparisonPolicyId,
                "Friendship source route is not the original independently compared direction.");
            return new { source_id = source.SourceId, option_id = row.Selection.OptionId,
                selected_candidate_id = row.Selection.CandidateId, source_state_hash = row.SourceStateHash };
        }).ToArray();
        return Project(ReadObject(dependenciesPath), corpus, corpusPath, CurrentTeacherFrontierSupport.HashFile(corpusPath),
            dependenciesPath, CurrentTeacherFrontierSupport.HashFile(dependenciesPath), JsonSerializer.SerializeToNode(routes)!.AsArray());
    }

    private static JsonObject ReadObject(string path) => JsonNode.Parse(
        CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(path)))!.AsObject();

    private static string RequiredString(JsonObject value, string name) => value[name]?.GetValue<string>() ??
        throw new InvalidDataException("Friendship local view is missing " + name + ".");

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
