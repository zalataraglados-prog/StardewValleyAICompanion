using System.Text.Json;
using System.Text.Json.Nodes;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyLocalDependencyViewBuilder
{
    public const string DirectionId = "earn_money";
    public const string CertificateNodeId = "dependency.earn_money.current_source_certificate";
    private const string Prefix = "dependency.earn_money.";
    private const string MetadataKey = "money_local_method_view";

    public static JsonObject Build(string corpusPath, string dependenciesPath) =>
        Rebuild(MoneyThresholdTeacherCorpusBuilder.Verify(RequiredFile(corpusPath)), corpusPath, dependenciesPath);

    public static JsonObject Verify(string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Money local method metadata is missing.");
        var rebuilt = Build(Text(metadata, "corpus_path"), Text(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Money local dependency view differs from its complete original sources.");
        return rebuilt;
    }

    internal static void VerifyForCoverageIfScoped(MoneyThresholdTeacherCorpus verifiedCorpus, string corpusPath,
        string dependenciesPath, string frontierDependenciesHash)
    {
        var stored = ReadObject(dependenciesPath);
        if (stored[MetadataKey] is null)
        {
            var hasCertificate = stored["directions"] is JsonArray directions && directions.OfType<JsonObject>().Any(direction =>
                direction["nodes"] is JsonArray nodes && nodes.OfType<JsonObject>().Any(node => node["id"]?.GetValue<string>() == CertificateNodeId));
            Require(!hasCertificate, "Money local certificate lost its source metadata.");
            return; // Original unscoped Money coverage keeps its existing contract.
        }
        Require(CurrentTeacherFrontierSupport.HashFile(dependenciesPath) == frontierDependenciesHash,
            "Money local method view changed after frontier construction.");
        var metadata = stored[MetadataKey]!.AsObject();
        Require(Path.GetFullPath(Text(metadata, "corpus_path")).Equals(Path.GetFullPath(corpusPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Money method certificate belongs to another corpus.");
        var rebuilt = Rebuild(verifiedCorpus, corpusPath, Text(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Money method is not its independently rebuilt conditional source view.");
    }

    private static JsonObject Rebuild(MoneyThresholdTeacherCorpus corpus, string corpusPath, string dependenciesPath)
    {
        corpusPath = RequiredFile(corpusPath);
        dependenciesPath = RequiredFile(dependenciesPath);
        Require(JsonNode.DeepEquals(ReadObject(corpusPath), JsonSerializer.SerializeToNode(corpus, JsonDefaults.Options)),
            "Money corpus changed after its complete source replay.");
        VerifyDependencyAuthority(corpus, dependenciesPath);
        return ProjectVerifiedCorpus(ReadObject(dependenciesPath), corpus, corpusPath, CurrentTeacherFrontierSupport.HashFile(corpusPath),
            dependenciesPath, CurrentTeacherFrontierSupport.HashFile(dependenciesPath));
    }

    private static void VerifyDependencyAuthority(MoneyThresholdTeacherCorpus verifiedCorpus, string dependenciesPath)
    {
        var pathComparer = OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
        var dependenciesHash = CurrentTeacherFrontierSupport.HashFile(dependenciesPath);
        var comparisons = verifiedCorpus.Rows.SelectMany(row => row.CrossedThresholds)
            .Select(value => value.TeacherComparison).OfType<MoneyThresholdTeacherComparisonEvidence>()
            .Where(value => value.ShopReplayJoin is not null).GroupBy(value => Path.GetFullPath(value.AdmissionReceiptPath), pathComparer);
        foreach (var group in comparisons)
        {
            var comparisonHash = CurrentTeacherFrontierSupport.HashFile(group.Key);
            Require(group.All(value => comparisonHash == value.AdmissionReceiptSha256),
                "Money comparison changed after corpus replay.");
            var comparison = CurrentTeacherFrontierSupport.Read<MoneyShopReplayComparisonReceipt>(group.Key, "Verified Money comparison");
            foreach (var binding in comparison.SourceBindings)
            {
                Require(CurrentTeacherFrontierSupport.HashFile(binding.Path) == binding.Sha256,
                    "Money source binding changed after corpus replay.");
                var source = CurrentTeacherFrontierSupport.Read<MoneyShopReplaySourceBindingReceipt>(binding.Path, "Verified Money source binding");
                var catalogs = source.SourceArtifacts.Where(value => value.Role.EndsWith(":candidate_catalog", StringComparison.Ordinal)).ToArray();
                Require(catalogs.Length == 2, "Money source authority lacks both original candidate catalogs.");
                foreach (var artifact in catalogs)
                {
                    Require(CurrentTeacherFrontierSupport.HashFile(artifact.Path) == artifact.Sha256,
                        "Money candidate catalog changed after corpus replay.");
                    var catalog = CurrentTeacherFrontierSupport.Read<MoneyRevenueCandidateDenominatorCatalog>(artifact.Path, "Verified Money candidate catalog");
                    Require(pathComparer.Equals(Path.GetFullPath(catalog.DependencyExpansionPath), dependenciesPath) &&
                        catalog.DependencyExpansionSha256 == dependenciesHash,
                        "Money local method does not share its native comparison's original dependency authority.");
                }
            }
        }
    }

    private static JsonObject ReadObject(string path) => JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(path)))!.AsObject();
    private static string Text(JsonNode value, string name) => value[name]?.GetValue<string>() ??
        throw new InvalidDataException("Money local view is missing " + name + ".");
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
