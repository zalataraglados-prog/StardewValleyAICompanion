using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void VerifyMoneyCounterfactualSourceAudit()
    {
        var root = TempArtifactRoot("money-counterfactual-source-audit");
        Directory.CreateDirectory(root);
        MoneyDirectConcreteFixture(root);
        var alternativePath = Path.Combine(root, "audit-alternatives.json");
        var sourcePath = Path.Combine(root, "audit-source.json");
        var dependencies = Path.Combine(root, "dependencies.json");
        var fixture = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(Path.Combine(root, "source.json"), "source fixture");
        var source = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(fixture), JsonDefaults.Options)!;
        SetActiveShopTestField(source, "player", "club_coins", 0);
        SetActiveShopTestField(source, "player", "shop_currency_balances", Array.Empty<object>());
        SetActiveShopTestField(source, "world_progress", "preserved_fact", true);
        source.StateHash = SnapshotHash.ComputeStateHash(source.State);
        Write(sourcePath, source);
        var candidatePath = Path.Combine(root, "audit-candidates.json");
        Write(candidatePath, MoneyRevenueCandidateDenominatorBuilder.Build(sourcePath, dependencies));
        Write(alternativePath, MoneyRevenueAlternativeDenominatorBuilder.Build(candidatePath));
        var concreteManifest = Path.Combine(root, "concrete-manifest.json");
        var concretePath = Path.Combine(root, "concrete.json");
        Write(concreteManifest, new MoneyRevenueConcreteAlternativeDenominatorManifest { AlternativeDenominatorCatalogPath = alternativePath });
        Write(concretePath, MoneyRevenueConcreteAlternativeDenominatorBuilder.Build(concreteManifest));
        var ledgerPath = Path.Combine(root, "source-ledger.json");
        Write(ledgerPath, new StrategyCommitmentLedger { LedgerId = "fixture-ledger", SaveId = source.SaveId.Value!,
            PlayerId = source.PlayerId.Value!, SourceStateHash = source.StateHash });
        var manifestPath = Path.Combine(root, "request-manifest.json");
        Write(manifestPath, new MoneyRevenueCounterfactualProofRequestManifest
            { ConcreteAlternativeCatalogPath = concretePath, SourceStrategyLedgerPath = ledgerPath });
        var requests = MoneyRevenueCounterfactualProofRequestBuilder.Build(manifestPath);
        var request = requests.Requests.Single(row => row.TerminalCandidate.SlotIndex == 1);
        var originalFiles = new[] { sourcePath, candidatePath, manifestPath, ledgerPath };
        var originalHashes = originalFiles.Select(CurrentTeacherFrontierSupport.HashFile).ToArray();

        MoneyCounterfactualSourceAudit Audit(string path) => MoneyCounterfactualSourceAuditBuilder.Build(manifestPath, request.RequestId, path);
        void NoAuthority(MoneyCounterfactualSourceAudit audit)
        {
            Require(!audit.ReplayBindingAuthorized && !audit.NativeOutcomeVerified &&
                    !audit.TeacherPreferenceLabelEligible && !audit.FormalProductTrainingAuthorized &&
                    audit.BlockingReasons.Contains("source_audit_is_not_native_proof_or_replay_binding_authority"),
                "Source audit invented replay, native or training authority.");
        }
        var exact = Audit(candidatePath);
        Require(exact.SameRawStateHash && exact.SameSnapshotBytes && exact.SameActorAndVersion &&
                exact.SameMoneyCheckpointFacts && exact.SamePersistentBoundary && exact.SameNativeShopContext &&
                exact.ChangedPersistentFields.Length == 0 && exact.MatchingReplayCandidateCount == 1 &&
                exact.ReplayCandidate?.SlotIndex == 1 && exact.ReplayObjectiveParameters.Length == 8,
            "Exact source audit lost the original complete source or below-threshold sale candidate.");
        NoAuthority(exact);
        var auditPath = Path.Combine(root, "audit.json");
        Write(auditPath, exact);
        Require(EqualJson(exact, MoneyCounterfactualSourceAuditBuilder.Verify(auditPath)), "Source audit rebuild differs.");

        string Replay(string name, Action<SnapshotEnvelope> change)
        {
            var snapshot = JsonSerializer.Deserialize<SnapshotEnvelope>(JsonSerializer.Serialize(source), JsonDefaults.Options)!;
            change(snapshot);
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
            var path = Path.Combine(root, name + "-snapshot.json");
            Write(path, snapshot);
            var catalogPath = Path.Combine(root, name + "-catalog.json");
            Write(catalogPath, MoneyRevenueCandidateDenominatorBuilder.Build(path, dependencies));
            return catalogPath;
        }
        void SetValue(SnapshotEnvelope snapshot, string section, string field, object value)
        {
            var fields = JsonNode.Parse(snapshot.State[section].GetRawText())!.AsObject();
            fields[field]!["value"] = JsonSerializer.SerializeToNode(value);
            snapshot.State[section] = JsonSerializer.SerializeToElement(fields);
        }
        var metadata = Audit(Replay("new-frame", snapshot =>
        {
            snapshot.GameTick += 10;
            foreach (var section in snapshot.State.Keys.ToArray())
            {
                var fields = JsonNode.Parse(snapshot.State[section].GetRawText())!.AsObject();
                foreach (var envelope in fields.Select(row => row.Value).OfType<JsonObject>().Where(row => row.ContainsKey("read_at_tick")))
                    envelope["read_at_tick"] = snapshot.GameTick;
                snapshot.State[section] = JsonSerializer.SerializeToElement(fields);
            }
        }));
        Require(!metadata.SameRawStateHash && !metadata.SameSnapshotBytes && metadata.SameMoneyCheckpointFacts &&
                metadata.SamePersistentBoundary && metadata.MatchingReplayCandidateCount == 1 &&
                metadata.BlockingReasons.Contains("original_request_requires_exact_frame_or_explicit_replay_binding"),
            "New frame metadata was silently treated as the original native source.");
        NoAuthority(metadata);
        var changed = Audit(Replay("unknown-obligation", snapshot => SetActiveShopTestField(snapshot, "player", "new_unknown_obligation", 1)));
        Require(!changed.SamePersistentBoundary && changed.ChangedPersistentFields.Contains("player.new_unknown_obligation"),
            "Unknown persistent evidence was dropped to force source equivalence.");
        NoAuthority(changed);
        var money = Audit(Replay("changed-money", snapshot => SetValue(snapshot, "player", "money", 999)));
        Require(!money.SameMoneyCheckpointFacts && !money.SamePersistentBoundary, "Changed money was called the same source.");
        var time = Audit(Replay("changed-time", snapshot => SetValue(snapshot, "time", "time", 1100)));
        Require(!time.SameNativeShopContext, "Changed service time was called the same shop context.");
        var actor = Audit(Replay("changed-actor", snapshot => snapshot.SaveId = MoneyProgressionIdentity("another-save")));
        Require(!actor.SameActorAndVersion && !actor.SameMoneyCheckpointFacts, "A different actor was called the same source.");
        var quote = Audit(Replay("changed-quote", snapshot =>
        {
            var fields = JsonNode.Parse(snapshot.State["player"].GetRawText())!.AsObject();
            fields["inventory"]!["value"]![1]!["sell_to_store_price"] = 18;
            snapshot.State["player"] = JsonSerializer.SerializeToElement(fields);
        }));
        Require(quote.MatchingReplayCandidateCount == 0 && quote.ReplayCandidate is null && quote.ReplayObjectiveParameters.Length == 0,
            "A changed exact quote retained the old request binding.");
        foreach (var report in new[] { money, time, actor, quote }) NoAuthority(report);

        void Reject(Action action, string message)
        {
            var rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, message);
        }
        Reject(() => MoneyCounterfactualSourceAuditBuilder.Build(manifestPath, "invented-id", candidatePath), "Unknown request ID admitted.");
        foreach (var flag in new[] { "binding", "native", "teacher", "formal", "digest", "schema" })
        {
            var forged = JsonSerializer.Deserialize<MoneyCounterfactualSourceAudit>(JsonSerializer.Serialize(exact), JsonDefaults.Options)!;
            switch (flag)
            {
                case "binding": forged.ReplayBindingAuthorized = true; break;
                case "native": forged.NativeOutcomeVerified = true; break;
                case "teacher": forged.TeacherPreferenceLabelEligible = true; break;
                case "formal": forged.FormalProductTrainingAuthorized = true; break;
                case "digest": forged.AuditSha256 = new string('f', 64); break;
                case "schema": forged.SchemaVersion = "unreviewed"; break;
            }
            var path = Path.Combine(root, "forged-" + flag + ".json");
            Write(path, forged);
            Reject(() => MoneyCounterfactualSourceAuditBuilder.Verify(path), "Forged source audit authority or digest admitted.");
        }
        Reject(() => MoneyCounterfactualSourceAuditCommands.Write(Arguments.Parse(new[] { "--output", manifestPath }),
            (_, _) => throw new Exception("Output guard did not run before write."), verify: false), "Audit overwrote its original manifest.");
        Require(originalFiles.Select(CurrentTeacherFrontierSupport.HashFile).SequenceEqual(originalHashes), "Source audit changed original inputs.");
        Console.WriteLine(root);
    }
}
