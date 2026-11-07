using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void VerifyMoneyShopReplaySourceBinding()
    {
        var root = TempArtifactRoot("money-shop-replay-source-binding");
        Directory.CreateDirectory(root);
        var alternativePath = MoneyDirectConcreteFixture(root);
        var snapshot = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(Path.Combine(root, "source.json"), "Binding fixture");
        var ledgerPath = Path.Combine(root, "ledger.json");
        var ledger = new StrategyCommitmentLedger { LedgerId = "binding-ledger", SaveId = snapshot.SaveId.Value!,
            PlayerId = snapshot.PlayerId.Value!, SourceStateHash = snapshot.StateHash, UpdatedAt = "2026-10-31T00:00:00+00:00" };
        Write(ledgerPath, ledger);
        var ledgerSignature = MoneyShopReplaySourceBindingBuilder.VerifyInitialLedger(ledgerPath, snapshot);
        var changedTime = JsonNode.Parse(File.ReadAllText(ledgerPath))!.AsObject();
        changedTime["updated_at"] = "2026-11-01T00:00:00+00:00";
        var otherLedger = Path.Combine(root, "other-ledger.json");
        Write(otherLedger, changedTime);
        Require(ledgerSignature == MoneyShopReplaySourceBindingBuilder.VerifyInitialLedger(otherLedger, snapshot), "Bound ledger timestamps changed resource identity.");
        var checks = 0;
        void Reject(Action action, string reason)
        {
            var rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, reason);
            checks++;
        }
        var localEpisodePath = Path.Combine(root, "own-native-episode.json");
        var localRecurrence = JsonSerializer.SerializeToElement(new { transitions = new[] {
            new { proof_kind = MoneyRevenueRecurrenceProofKinds.NativeShopSale, shop_sale_episode_path = localEpisodePath } } });
        MoneyShopReplaySourceBindingBuilder.VerifyLocalRecurrence(localRecurrence, localEpisodePath);
        var futureRecurrence = JsonSerializer.SerializeToElement(new { transitions = new[] {
            new { proof_kind = MoneyRevenueRecurrenceProofKinds.NativeShopSale, shop_sale_episode_path = localEpisodePath },
            new { proof_kind = MoneyRevenueRecurrenceProofKinds.NativeShopSale, shop_sale_episode_path = Path.Combine(root, "future-episode.json") } } });
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyLocalRecurrence(futureRecurrence, localEpisodePath), "Local shop-source binding authorized a future recurrence outside its scope.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyLocalRecurrence(localRecurrence, Path.Combine(root, "other-episode.json")), "Local source binding authorized an unrelated native episode.");
        foreach (var mutation in new[] { "unknown", "revision", "history", "reservation", "source", "actor", "missing", "schema" })
        {
            var node = JsonNode.Parse(File.ReadAllText(ledgerPath))!.AsObject();
            switch (mutation)
            {
                case "unknown": node["future_obligations"] = new JsonArray(); break;
                case "revision": node["revision"] = 1; break;
                case "history": node["history"] = new JsonArray(new JsonObject()); break;
                case "reservation": node["currency_reservations"] = new JsonArray(new JsonObject()); break;
                case "source": node["source_state_hash"] = "other-source"; break;
                case "actor": node["player_id"] = "other-player"; break;
                case "missing": node.Remove("material_reservations"); break;
                case "schema": node["schema_version"] = "unknown"; break;
            }
            var path = Path.Combine(root, mutation + "-ledger.json");
            Write(path, node);
            Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyInitialLedger(path, snapshot), "Replay discarded a ledger obligation: " + mutation);
        }

        var facts = new MoneyCounterfactualSourceAudit { SameActorAndVersion = true, SameMoneyCheckpointFacts = true,
            SameNativeShopContext = true, MatchingReplayCandidateCount = 1, ReferencePersistentBoundaryAvailable = true,
            ReplayPersistentBoundaryAvailable = true,
            ChangedPersistentFields = new[] { "environment.training_run_id", "environment.save_isolation_path" } };
        MoneyShopReplaySourceBindingBuilder.VerifySourceFacts(facts);
        foreach (var field in new[] { "actor", "money", "context", "quote", "boundary", "unknown" })
        {
            var bad = JsonSerializer.Deserialize<MoneyCounterfactualSourceAudit>(JsonSerializer.Serialize(facts), JsonDefaults.Options)!;
            switch (field)
            {
                case "actor": bad.SameActorAndVersion = false; break;
                case "money": bad.SameMoneyCheckpointFacts = false; break;
                case "context": bad.SameNativeShopContext = false; break;
                case "quote": bad.MatchingReplayCandidateCount = 0; break;
                case "boundary": bad.ReplayPersistentBoundaryAvailable = false; break;
                case "unknown": bad.ChangedPersistentFields = new[] { "player.future_obligation" }; break;
            }
            Reject(() => MoneyShopReplaySourceBindingBuilder.VerifySourceFacts(bad), "Replay accepted changed source facts: " + field);
        }

        var concreteManifest = Path.Combine(root, "concrete-manifest.json");
        var concretePath = Path.Combine(root, "concrete.json");
        Write(concreteManifest, new MoneyRevenueConcreteAlternativeDenominatorManifest { AlternativeDenominatorCatalogPath = alternativePath });
        Write(concretePath, MoneyRevenueConcreteAlternativeDenominatorBuilder.Build(concreteManifest));
        var requestManifest = Path.Combine(root, "request-manifest.json");
        Write(requestManifest, new MoneyRevenueCounterfactualProofRequestManifest { ConcreteAlternativeCatalogPath = concretePath, SourceStrategyLedgerPath = ledgerPath });
        var catalog = MoneyRevenueCounterfactualProofRequestBuilder.Build(requestManifest);
        var inputs = MoneyThresholdNativeOutcomeSelectionBuilder.LoadInputs(Path.Combine(root, "candidate-catalog.json"));
        MoneyShopReplaySourceBindingBuilder.VerifyCandidateLineage(catalog, inputs.CandidateDenominatorPath);
        var otherDependencies = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "dependencies.json")))!.AsObject();
        var direction = otherDependencies["directions"]![0]!;
        foreach (var node in direction["nodes"]!.AsArray().OfType<JsonObject>())
            if (node["id"]?.GetValue<string>() == "sale") node["id"] = "other-sale";
        foreach (var edge in direction["edges"]!.AsArray().OfType<JsonObject>())
            if (edge["from"]?.GetValue<string>() == "sale") edge["from"] = "other-sale";
        var otherDependencyPath = Path.Combine(root, "other-dependencies.json");
        Write(otherDependencyPath, otherDependencies);
        var otherCandidatePath = Path.Combine(root, "other-candidates.json");
        var otherCandidates = MoneyRevenueCandidateDenominatorBuilder.Build(Path.Combine(root, "source.json"), otherDependencyPath);
        Write(otherCandidatePath, otherCandidates);
        var otherInputs = MoneyThresholdNativeOutcomeSelectionBuilder.LoadInputs(otherCandidatePath);
        Require(otherInputs.Snapshot.StateHash == inputs.Snapshot.StateHash && otherInputs.Catalog.CurrentComparisonDenominatorReady &&
            otherInputs.Catalog.ComparisonCandidateCount == 2 && !EqualJson(inputs.Catalog.Nodes, otherInputs.Catalog.Nodes),
            "Foreign-lineage fixture is not a valid same-snapshot catalog with different nodes.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyCandidateLineage(catalog, otherCandidatePath), "Replay substituted another valid same-snapshot dependency/candidate lineage.");
        var aliasPath = Path.Combine(root, "candidate-copy.json");
        File.Copy(inputs.CandidateDenominatorPath, aliasPath);
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyCandidateLineage(catalog, aliasPath), "Replay substituted an unbound catalog path with identical bytes.");
        var mapping = MoneyShopReplaySourceBindingBuilder.BindDenominator(catalog, catalog, inputs, inputs);
        Require(mapping.Length == 2 && mapping.Select(row => row.ReplayRequestId).Distinct(StringComparer.Ordinal).Count() == 2,
            "Complete original direct denominator did not map bijectively.");
        var missingCatalog = JsonSerializer.Deserialize<MoneyRevenueCounterfactualProofRequestCatalog>(JsonSerializer.Serialize(catalog), JsonDefaults.Options)!;
        missingCatalog.Requests = missingCatalog.Requests.Take(1).ToArray();
        missingCatalog.RequestCount = 1;
        Reject(() => MoneyShopReplaySourceBindingBuilder.BindDenominator(catalog, missingCatalog, inputs, inputs), "Replay admitted selected-only denominator.");
        var duplicate = JsonSerializer.Deserialize<MoneyRevenueCounterfactualProofRequestCatalog>(JsonSerializer.Serialize(catalog), JsonDefaults.Options)!;
        duplicate.Requests[1].TerminalCandidate = duplicate.Requests[0].TerminalCandidate;
        Reject(() => MoneyShopReplaySourceBindingBuilder.BindDenominator(catalog, duplicate, inputs, inputs), "Replay admitted duplicate semantic request mapping.");

        var setup = ReplaySetupFixture(root, "run.binding", Path.Combine(root, "isolated-saves"));
        var measurement = DateTimeOffset.Parse("2026-11-01T00:01:00+00:00");
        var signature = MoneyShopReplaySourceBindingBuilder.VerifySetup(setup, "run.binding", Path.Combine(root, "isolated-saves"), measurement);
        var otherSetup = ReplaySetupFixture(Path.Combine(root, "other-setup"), "run.binding.other", Path.Combine(root, "other-saves"));
        Require(signature == MoneyShopReplaySourceBindingBuilder.VerifySetup(otherSetup, "run.binding.other", Path.Combine(root, "other-saves"), measurement),
            "Declared setup lost semantic equality across independently bound execution identities.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifySetup(setup, "wrong-run", Path.Combine(root, "isolated-saves"), measurement), "Wrong setup run admitted.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifySetup(setup, "run.binding", Path.Combine(root, "wrong-root"), measurement), "Wrong setup root admitted.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifySetup(setup.Take(3).ToArray(), "run.binding", root, measurement), "Missing setup step admitted.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifySetup(setup.Reverse().ToArray(), "run.binding", root, measurement), "Unordered setup admitted.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifySetup(setup, "run.binding", Path.Combine(root, "isolated-saves"), measurement.AddMinutes(-1)), "In-measurement debug setup admitted.");
        var guarded = false;
        Reject(() => MoneyShopReplaySourceBindingCommands.Write(Arguments.Parse(new[] { "--output", ledgerPath }), (_, _) => guarded = true, false), "Replay output overwrote its own input.");
        Require(!guarded, "Replay output guard ran after building/writing.");
        var binaries = new[] { ("game", "Stardew Valley.dll"), ("backend", "StardewAI.Backend.dll"), ("loop", "StardewAI.LiveTrainingLoop.dll") }
            .Select(row => new MoneyShopReplayExecutable(row.Item1, Path.Combine(root, row.Item2))).ToArray();
        foreach (var binary in binaries) File.WriteAllText(binary.Path, "binding-producer:" + binary.Role);
        var identity = JsonSerializer.SerializeToElement(new { binary_sha256 = binaries.ToDictionary(row => row.Path, row => CurrentTeacherFrontierSupport.HashFile(row.Path)) });
        MoneyShopReplaySourceBindingBuilder.VerifyExecutables(binaries, identity, identity);
        var mislabeled = binaries.Select(row => row with { Path = binaries[0].Path }).ToArray();
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyExecutables(mislabeled, identity, identity), "Replay skipped producer identity by relabeling one DLL three times.");
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyExecutables(binaries.Take(2).ToArray(), identity, identity), "Replay omitted game identity.");
        var forgedIdentity = JsonNode.Parse(identity.GetRawText())!.AsObject();
        forgedIdentity["binary_sha256"]![binaries[0].Path] = new string('f', 64);
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyExecutables(binaries, identity, JsonSerializer.SerializeToElement(forgedIdentity)), "Replay admitted changed game producer.");
        Console.WriteLine($"PASS: {checks} replay-boundary rejection checks; original candidate, ledger and setup contracts preserved.");
    }

    private static MoneyShopReplaySetupStep[] ReplaySetupFixture(string root, string run, string isolation)
    {
        Directory.CreateDirectory(root);
        return new[] { "time", "held_stock", "alternative_stock", "menu_input_settle" }.Select((role, index) =>
        {
            var option = role switch { "time" => "debug.advance_time_to", "menu_input_settle" => "executor.wait_ticks", _ => "debug.setup_sale_target" };
            var request = new JsonObject { ["schema_version"] = "training_execution_request.v1", ["run_id"] = run,
                ["queue_id"] = run + ".setup", ["queue_item_id"] = run + "." + role, ["before_state_hash"] = new string('a', 64),
                ["option_id"] = option, ["execution_mode"] = "training_singleplayer", ["actor"] = "training_farmer.main",
                ["save_isolation_path"] = isolation, ["request_nonce"] = run + ".nonce." + index,
                ["created_at"] = "2026-11-01T00:00:0" + index + "+00:00", ["declared_fixture_parameter"] = index };
            var result = new JsonObject { ["schema_version"] = "training_execution_result.v1", ["status"] = "applied",
                ["feedback_available"] = true, ["primitive_verification_status"] = "verified", ["completed_at"] = "2026-11-01T00:00:0" + index + "+00:00" };
            foreach (var name in new[] { "run_id", "queue_id", "queue_item_id", "before_state_hash", "option_id" }) result[name] = request[name]!.DeepClone();
            var requestPath = Path.Combine(root, role + "-request.json");
            var resultPath = Path.Combine(root, role + "-result.json");
            Write(requestPath, request); Write(resultPath, result);
            return new MoneyShopReplaySetupStep(role, requestPath, resultPath);
        }).ToArray();
    }
}
