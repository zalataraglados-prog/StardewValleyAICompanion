using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void VerifyMoneyLocalDependencyView(MoneyThresholdTeacherEvidenceRow evidence)
    {
        var root = TempArtifactRoot("money-local-method-view");
        Directory.CreateDirectory(root);
        var dependenciesPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../goal-method-dependency-expansions.v1.json"));
        var original = JsonNode.Parse(File.ReadAllText(dependenciesPath))!.AsObject();
        var originalText = original.ToJsonString();
        var source = Path.Combine(root, "source.json");
        Write(source, new { fixture_only = true });
        var native = new MoneyThresholdTeacherCorpus { CorpusId = "projection-only", Rows = new[] { evidence } };
        var checks = 0;
        void Reject(Action action, string reason)
        {
            var rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, reason); checks++;
        }
        Reject(() => MoneyLocalDependencyViewBuilder.ProjectVerifiedCorpus(original, native, "fixture", "hash", dependenciesPath, "hash"),
            "Native-only or legacy evidence claimed the conditional method.");
        var corpus = JsonSerializer.Deserialize<MoneyThresholdTeacherCorpus>(JsonSerializer.Serialize(native), JsonDefaults.Options)!;
        corpus.SchemaVersion = MoneyThresholdTeacherCorpusVersions.ShopReplayCorpus;
        var row = corpus.Rows[0];
        // This exercises only the pure projection contract, not native/corpus authorization.
        var template = evidence.CrossedThresholds.Single().TeacherComparison!;
        corpus.Rows = new[] { row with { CrossedThresholds = GrandpaEvaluationGoalEvaluator.MoneyThresholds.Select(target =>
            new MoneyThresholdTeacherCriterion(target.CriterionId, target.Target, target.Points, "projection-only", "projection-only",
                template, true)).ToArray() } };
        JsonObject Project(JsonObject graph, MoneyThresholdTeacherCorpus input) =>
            MoneyLocalDependencyViewBuilder.ProjectVerifiedCorpus(graph, input, "fixture", "hash", dependenciesPath, "hash");
        var projected = Project(original, corpus);
        var originalMoney = original["directions"]!.AsArray().Single(value => value!["direction_id"]!.GetValue<string>() == "earn_money")!;
        var projectedMoney = projected["directions"]!.AsArray().Single(value => value!["direction_id"]!.GetValue<string>() == "earn_money")!;
        Require(original.ToJsonString() == originalText && projectedMoney["status"]!.GetValue<string>() == "complete" &&
            JsonNode.DeepEquals(originalMoney, projected["money_local_method_view"]!["global_direction_contract"]) &&
            !projected["money_local_method_view"]!["formal_product_training_authorized"]!.GetValue<bool>(),
            "Money projection rewrote its input, dropped global responsibilities or authorized training.");
        foreach (var other in original["directions"]!.AsArray().Where(value => value!["direction_id"]!.GetValue<string>() != "earn_money"))
            Require(JsonNode.DeepEquals(other, projected["directions"]!.AsArray().Single(value =>
                value!["direction_id"]!.GetValue<string>() == other!["direction_id"]!.GetValue<string>())), "Money projection changed another root.");
        foreach (var node in originalMoney["nodes"]!.AsArray())
            Require(JsonNode.DeepEquals(node, projectedMoney["nodes"]!.AsArray().Single(value =>
                value!["id"]!.GetValue<string>() == node!["id"]!.GetValue<string>())), "Money projection changed an original rule or executor.");
        foreach (var mutation in new[] { "formal", "kind", "acquisition", "native", "missing-criterion", "target", "points", "comparison" })
        {
            var bad = JsonSerializer.Deserialize<MoneyThresholdTeacherCorpus>(JsonSerializer.Serialize(corpus), JsonDefaults.Options)!;
            switch (mutation)
            {
                case "formal": bad.FormalProductTrainingAuthorized = true; break;
                case "kind": bad.Rows[0] = bad.Rows[0] with { NativeOutcomeKind = MoneyThresholdNativeOutcomeKinds.RevenueTrajectory }; break;
                case "acquisition": bad.Rows[0] = bad.Rows[0] with { AcquisitionRolloutRequired = true }; break;
                case "native": bad.Rows[0] = bad.Rows[0] with { NativeTerminalOutcomeVerified = false }; break;
                case "missing-criterion": bad.Rows[0] = bad.Rows[0] with { CrossedThresholds = bad.Rows[0].CrossedThresholds.Skip(1).ToArray() }; break;
                case "target": bad.Rows[0].CrossedThresholds[0] = bad.Rows[0].CrossedThresholds[0] with { Target = 1 }; break;
                case "points": bad.Rows[0].CrossedThresholds[0] = bad.Rows[0].CrossedThresholds[0] with { Points = 7 }; break;
                case "comparison": bad.Rows[0].CrossedThresholds[0] = bad.Rows[0].CrossedThresholds[0] with { TeacherComparison = null }; break;
            }
            Reject(() => Project(original, bad), "Money projection accepted: " + mutation);
        }
        foreach (var mutation in new[] { "wallet", "terminal", "ladder", "reservation", "missing-production", "executor-role", "global-obligation" })
        {
            var graph = original.DeepClone().AsObject();
            var direction = graph["directions"]!.AsArray().Single(value => value!["direction_id"]!.GetValue<string>() == "earn_money")!;
            JsonNode Node(string suffix) => direction["nodes"]!.AsArray().Single(value => value!["id"]!.GetValue<string>() == "dependency.earn_money." + suffix)!;
            switch (mutation)
            {
                case "wallet": Node("total_money_earned_one_million")["rule"]!["path"] = "player.money"; break;
                case "terminal": Node("total_money_earned_one_million")["rule"]!["value"] = 50_000u; break;
                case "ladder": Node("settled_milestone_ladder")["rule"]!["points"]![0] = 7; break;
                case "reservation": Node("reserve_safe_positive_inventory")["rule"]!["missing_reservation_or_price_evidence"] = "ignore"; break;
                case "missing-production": direction["nodes"]!.AsArray().Remove(Node("maintain_crop_production")); break;
                case "executor-role": Node("maintain_crop_production")["kind"] = "policy_option_transition"; break;
                case "global-obligation": Node("revenue_portfolio_recurrence")["rule"]!["admission_requirement"] = ""; break;
            }
            Reject(() => Project(graph, corpus), "Money projection weakened an original role/rule: " + mutation);
        }
        Reject(() => Project(projected, corpus), "Money projection stacked a scoped graph.");
        MoneyLocalDependencyViewBuilder.VerifyForCoverageIfScoped(native, source, dependenciesPath, "legacy-hash-not-used");
        var legacyPath = Path.Combine(root, "legacy-unscoped.json");
        Write(legacyPath, new { directions = new[] { new { direction_id = "earn_money" } } });
        MoneyLocalDependencyViewBuilder.VerifyForCoverageIfScoped(native, source, legacyPath, "legacy-hash-not-used");
        var erased = projected.DeepClone().AsObject();
        erased.Remove("money_local_method_view");
        var erasedPath = Path.Combine(root, "erased-metadata.json");
        Write(erasedPath, erased);
        Reject(() => MoneyLocalDependencyViewBuilder.VerifyForCoverageIfScoped(corpus, source, erasedPath,
            CurrentTeacherFrontierSupport.HashFile(erasedPath)), "Money local certificate downgraded itself to the legacy coverage path.");
        Reject(() => MoneyLocalDependencyViewCommands.Write(Arguments.Parse(new[] { "--output", source }),
            (_, _) => throw new Exception("Output alias guard was not first."), false), "Money view overwrote a source.");
        Console.WriteLine($"PASS: conditional Money projection, unchanged original graph/rules/other roots and {checks} rejection checks.");
    }
}
