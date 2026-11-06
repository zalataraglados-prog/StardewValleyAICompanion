using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Training;
using static StardewAI.GoalConditionedBootstrap.NativeExecutionSourceSupport;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MarriageLocalDependencyViewBuilder
{
    public const string DirectionId = MarriageTeacherCorpus.CurrentDirectionId;
    public const string CertificateNodeId = "dependency.marriage_and_house_upgrade.current_source_certificate";
    private const string MetadataKey = "marriage_local_method_view";
    private const string Prefix = "dependency.marriage_and_house_upgrade.";

    public static JsonObject Build(string corpusPath, string dependenciesPath) =>
        Rebuild(MarriageTeacherCorpusBuilder.Verify(RequiredFile(corpusPath)), corpusPath, dependenciesPath);

    public static JsonObject Verify(string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Marriage local view metadata is missing.");
        var rebuilt = Build(Text(metadata, "corpus_path"), Text(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Marriage local dependency view differs from its complete original sources.");
        return rebuilt;
    }

    internal static void VerifyForCoverage(MarriageTeacherCorpus verifiedCorpus, string corpusPath, string viewPath)
    {
        var stored = ReadObject(viewPath);
        var metadata = stored[MetadataKey]?.AsObject() ?? throw new InvalidDataException("Executable Marriage method lacks its local source certificate.");
        Require(Path.GetFullPath(Text(metadata, "corpus_path")).Equals(Path.GetFullPath(corpusPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal),
            "Marriage method certificate belongs to another corpus.");
        // The caller already verified every original row and the entire native joint chain.
        var rebuilt = Rebuild(verifiedCorpus, corpusPath, Text(metadata, "original_dependencies_path"));
        Require(JsonNode.DeepEquals(stored, rebuilt), "Executable Marriage method is not the independently rebuilt conditional view.");
    }

    private static JsonObject Rebuild(MarriageTeacherCorpus corpus, string corpusPath, string dependenciesPath)
    {
        corpusPath = RequiredFile(corpusPath);
        dependenciesPath = RequiredFile(dependenciesPath);
        var routes = new JsonArray(corpus.Runs.Select(run => (JsonNode)ReadBinding(run)).ToArray());
        return Project(ReadObject(dependenciesPath), corpus, corpusPath, CurrentTeacherFrontierSupport.HashFile(corpusPath),
            dependenciesPath, CurrentTeacherFrontierSupport.HashFile(dependenciesPath), routes);
    }

    private static JsonObject ReadBinding(MarriageTeacherCorpusRun run)
    {
        Require(CurrentTeacherFrontierSupport.HashFile(run.NativeReceiptPath) == run.NativeReceiptSha256,
            "Marriage native source changed after corpus replay.");
        var native = ReadTyped<JsonElement>(run.NativeReceiptPath, "marriage_native_joint_receipt.v1");
        var proposal = Proposal(run);
        var preparations = new JsonArray(run.PreparationDecisions.Select(decision =>
        {
            Require(CurrentTeacherFrontierSupport.HashFile(decision.TeacherRowPath) == decision.TeacherRowSha256,
                "Marriage preparation row changed after corpus replay.");
            var row = ReadTyped<PolicyDecisionTrajectoryEnvelope>(decision.TeacherRowPath, PolicyTrajectoryVersionPins.TrajectorySchema);
            Require(row.Selection.OptionId == "social.gift_npc" && row.SourceStateHash == decision.BeforeStateHash &&
                row.Selection.CandidateId == decision.SelectedCandidateId && row.Outcome.QueueId == decision.QueueId &&
                row.Audit.TeacherSupervision!.SelectionPolicyId == new CurrentMarriageTeacherPreference().ComparisonPolicyId &&
                row.Audit.TeacherSupervision.RequirementTransitions.Single().RequirementId == Prefix + "gift_partner",
                "Marriage preparation is not the original gift dependency and independent comparison.");
            return (JsonNode)new JsonObject
            {
                ["preparation_index"] = decision.PreparationIndex, ["option_id"] = row.Selection.OptionId,
                ["dependency_node_id"] = Prefix + "gift_partner", ["selected_candidate_id"] = decision.SelectedCandidateId,
                ["source_state_hash"] = decision.BeforeStateHash, ["queue_id"] = decision.QueueId
            };
        }).ToArray());
        return new()
        {
            ["source_id"] = run.SourceId, ["option_id"] = "social.advance_partnership",
            ["selected_candidate_id"] = proposal.GetProperty("candidate_id").GetString(),
            ["source_state_hash"] = proposal.GetProperty("before_state_hash").GetString(),
            ["queue_id"] = proposal.GetProperty("queue_id").GetString(), ["preparation_bindings"] = preparations,
            ["declared_native_prerequisites"] = new JsonObject
            {
                ["native_receipt_sha256"] = run.NativeReceiptSha256,
                ["before_state_hash"] = native.GetProperty("before_state_hash").GetString(),
                ["after_state_hash"] = native.GetProperty("after_state_hash").GetString(),
                ["npc_name"] = native.GetProperty("npc_name").GetString(),
                ["initial_house_level"] = native.GetProperty("initial_house_level").GetInt32(),
                ["initial_house_pending_days"] = native.GetProperty("initial_house_pending_days").GetInt32(),
                ["final_house_level"] = native.GetProperty("final_house_level").GetInt32(),
                ["native_wedding_total_day"] = native.GetProperty("native_wedding_total_day").GetInt32()
            }
        };
    }

    private static JsonElement Proposal(MarriageTeacherCorpusRun run)
    {
        var proposals = run.QueueChain.EnumerateArray().Where(queue =>
            queue.GetProperty("role").GetString() == "mechanical_control_not_preference_label" &&
            queue.GetProperty("candidate_kind").GetString() == "partnership_propose_marriage_current" &&
            queue.GetProperty("primitive_options").EnumerateArray().Last().GetString() == "executor.social_interact").ToArray();
        Require(proposals.Length == 1, "Marriage local source lacks its unique original native proposal control.");
        return proposals[0];
    }

    private static JsonObject ReadObject(string path) => JsonNode.Parse(CurrentTeacherFrontierSupport.ReadArtifactText(RequiredFile(path)))!.AsObject();
    private static string Text(JsonNode value, string name) => value[name]?.GetValue<string>() ??
        throw new InvalidDataException("Marriage local view is missing " + name + ".");
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidDataException(reason);
    }
}
