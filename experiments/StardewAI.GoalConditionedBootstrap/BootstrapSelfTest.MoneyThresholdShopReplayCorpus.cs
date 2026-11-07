using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void VerifyMoneyThresholdShopReplayCorpus()
    {
        var root = TempArtifactRoot("money-threshold-shop-replay-corpus");
        Directory.CreateDirectory(root);
        var before = MoneyRevenueTerminalSnapshot(100, 100, 49_990, 1);
        var source = Path.Combine(root, "source.json");
        var ledgerPath = Path.Combine(root, "ledger.json");
        Write(source, before);
        var ledger = MoneyRevenueTerminalLedger(before.StateHash);
        ledger.Revision = 0;
        Write(ledgerPath, ledger);
        var fast = MoneyRevenueComparisonAdmissionAlternative(root, "replay-corpus-fast", before, 142, source, ledgerPath, ledger);
        var slow = MoneyRevenueComparisonAdmissionAlternative(root, "replay-corpus-slow", before, 184, source, ledgerPath, ledger);
        MoneyShopReplayComparisonAlternative Row(MoneyRevenueComparisonAdmissionFixture fixture, string kind) => new(
            fixture.Request.RequestId, fixture.Request.ConcreteAlternativeId, fixture.Request.RequestId, new string('a', 64), before.StateHash,
            fixture.Request.RequestId + ".run", fixture.Request.RequestId + ".nonce", kind, string.Empty, string.Empty,
            fixture.EpisodePath, CurrentTeacherFrontierSupport.HashFile(fixture.EpisodePath),
            MoneyRevenueComparisonAdmissionBuilder.VerifyAlternativeWithContext(fixture.Request, fixture.Proof, fixture.Evidence).Alternative);
        var rows = new[] { Row(fast, "exact_reference_native_witness"), Row(slow, "verified_shop_replay_source") };
        var catalog = new MoneyRevenueCounterfactualProofRequestCatalog
        {
            SourceStateHash = before.StateHash, RequestCount = 2, Requests = new[] { fast.Request, slow.Request },
            RequestIds = rows.Select(row => row.ReferenceRequestId).ToArray(), RequestDenominatorAuthoritative = true,
            TerminalStrategyLedgerAnchorsVerified = true, CounterfactualProofCollectionReady = true
        };
        var comparisons = MoneyShopReplayComparisonBuilder.CompareVerifiedDenominator(catalog, rows);
        var manifestPath = Path.Combine(root, "comparison-manifest.json");
        Write(manifestPath, new { fixture_only = true });
        var batch = new MoneyShopReplayComparisonReceipt
        {
            ManifestPath = manifestPath, ManifestSha256 = CurrentTeacherFrontierSupport.HashFile(manifestPath),
            ReferenceSourceStateHash = before.StateHash, RequestIds = catalog.RequestIds,
            ConcreteAlternativeIds = rows.Select(row => row.ReferenceConcreteAlternativeId).ToArray(),
            Alternatives = rows, Comparisons = comparisons, CandidateDenominatorAuthoritative = true, AllActualNativeOutcomesVerified = true,
            TeacherEligibleCriterionIds = comparisons.Where(row => row.TeacherPreferenceLabelEligible).Select(row => row.CriterionId).ToArray()
        };
        var receiptPath = Path.Combine(root, "comparison.json");
        Write(receiptPath, batch);
        var row = MoneyThresholdTeacherCorpusBuilder.BuildShopReplayRowForTest("joined-native", fast.EpisodePath, receiptPath, batch);
        Require(row.TeacherComparisonCount == 1 && row.CrossedThresholds.Single().TeacherComparison?.ShopReplayJoin is { } joined &&
            joined.ActualSourceStateHash == row.BeforeStateHash && joined.ActualRequestId == fast.Request.RequestId &&
            row.TotalMoneyEarnedBefore == 49_990 && row.TotalMoneyEarnedAfter == 50_010,
            "Replay criterion join lost its actual source or crossed threshold.");
        var checks = 0;
        void Reject(Action action, string reason)
        {
            var rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, reason); checks++;
        }
        MoneyShopReplayComparisonReceipt Clone() => JsonSerializer.Deserialize<MoneyShopReplayComparisonReceipt>(JsonSerializer.Serialize(batch), JsonDefaults.Options)!;
        var onlyCrossedCriterion = Clone();
        onlyCrossedCriterion.Comparisons = onlyCrossedCriterion.Comparisons.Select(value => value.CriterionId == "money_50000" ? value :
            value with { TeacherPreferenceLabelEligible = false }).ToArray();
        onlyCrossedCriterion.TeacherEligibleCriterionIds = new[] { "money_50000" };
        Require(MoneyThresholdTeacherCorpusBuilder.BuildShopReplayRowForTest("criterion-local", fast.EpisodePath, receiptPath, onlyCrossedCriterion)
            .TeacherComparisonCount == 1, "A local crossed criterion was blocked by unrelated threshold eligibility.");
        foreach (var mutation in new[] { "schema", "formal", "native", "index", "corpus", "eligibility", "mapping", "pair", "prefix", "target" })
        {
            var bad = Clone();
            var selected = Array.FindIndex(bad.Alternatives, value => value.ActualRequestId == fast.Request.RequestId);
            switch (mutation)
            {
                case "schema": bad.SchemaVersion = "unknown"; break;
                case "formal": bad.FormalProductTrainingAuthorized = true; break;
                case "native": bad.AllActualNativeOutcomesVerified = false; break;
                case "index": bad.OriginalExactFrameIndexCompleted = true; break;
                case "corpus": bad.CriterionCorpusAdmissionVerified = true; break;
                case "eligibility": bad.TeacherEligibleCriterionIds = Array.Empty<string>(); break;
                case "mapping": bad.Alternatives[selected] = bad.Alternatives[selected] with { ActualRequestId = "wrong-native-request" }; break;
                case "pair": bad.Comparisons[0] = bad.Comparisons[0] with { PairwisePreferences = Array.Empty<MoneyRevenueAlternativePairwisePreference>() }; break;
                case "prefix": bad.Alternatives[selected] = bad.Alternatives[selected] with { ActualComparisonEvidence = bad.Alternatives[selected].ActualComparisonEvidence with
                    { RevenuePrefix = bad.Alternatives[selected].ActualComparisonEvidence.RevenuePrefix with { FinalTotalMoneyEarned = 50_011 } } }; break;
                case "target": bad.Comparisons[0] = bad.Comparisons[0] with { TargetTotalMoneyEarned = 99_999 }; break;
            }
            Reject(() => MoneyThresholdTeacherCorpusBuilder.BuildShopReplayRowForTest("forged", fast.EpisodePath, receiptPath, bad), "Replay join admitted a forged condition: " + mutation);
        }
        Reject(() => MoneyThresholdTeacherCorpusBuilder.BuildShopReplayRowForTest("wrong-native", slow.EpisodePath, receiptPath, batch), "Unselected native outcome joined the preferred result.");
        var empty = Clone();
        empty.Comparisons = empty.Comparisons.Select(value => value with { TeacherPreferenceLabelEligible = false }).ToArray();
        empty.TeacherEligibleCriterionIds = Array.Empty<string>();
        Reject(() => MoneyThresholdTeacherCorpusBuilder.BuildShopReplayRowForTest("no-preference", fast.EpisodePath, receiptPath, empty), "An incomparable source fabricated a corpus join.");

        var legacySource = new MoneyThresholdTeacherCorpusSource("legacy", fast.EpisodePath);
        Require(!JsonSerializer.Serialize(legacySource, JsonDefaults.Options).Contains("shop_replay_comparison_receipt_path", StringComparison.Ordinal), "Old source serialization gained new fields.");
        var nativeOnly = MoneyThresholdTeacherCorpusBuilder.Build(new MoneyThresholdTeacherCorpusRequest { CorpusId = "old-wire", Sources = new[] { legacySource } });
        Require(!JsonSerializer.Serialize(nativeOnly, JsonDefaults.Options).Contains("shop_replay_join", StringComparison.Ordinal), "Old corpus serialization gained replay metadata.");
        var oldPath = Path.Combine(root, "old-corpus.json");
        Write(oldPath, nativeOnly);
        Require(EqualJson(nativeOnly, MoneyThresholdTeacherCorpusBuilder.Verify(oldPath)), "Old corpus verification changed.");
        var tagged = legacySource with { ShopReplayComparisonReceiptPath = receiptPath };
        Reject(() => MoneyThresholdTeacherCorpusBuilder.Build(new MoneyThresholdTeacherCorpusRequest { CorpusId = "v4-forged", Sources = new[] { tagged } }), "v4 silently accepted replay-source metadata.");
        Reject(() => MoneyThresholdTeacherCorpusBuilder.Build(new MoneyThresholdTeacherCorpusRequest { SchemaVersion = MoneyThresholdTeacherCorpusVersions.ShopReplayRequest,
            CorpusId = "v5-without-new-source", Sources = new[] { legacySource } }), "v5 upgraded an old source without explicit replay evidence.");
        var oldWithNewMetadata = new MoneyThresholdTeacherCorpus { CorpusId = "wrong-version", RowCount = 1, Rows = new[] { row } };
        Reject(() => MoneyThresholdTeacherCorpusBuilder.ValidateInMemory(oldWithNewMetadata), "v4 corpus accepted a new replay join.");
        var newCorpus = new MoneyThresholdTeacherCorpus { SchemaVersion = MoneyThresholdTeacherCorpusVersions.ShopReplayCorpus,
            CorpusId = "new-version", RowCount = 1, Rows = new[] { row } };
        MoneyThresholdTeacherCorpusBuilder.ValidateInMemory(newCorpus);
        foreach (var mutation in new[] { "native-hash", "metadata-schema", "binding-kind", "partition" })
        {
            var criterion = row.CrossedThresholds.Single();
            var evidence = criterion.TeacherComparison!;
            var metadata = evidence.ShopReplayJoin!;
            var badRow = mutation == "partition" ? row with { DatasetPartition = "invented" } : row with
            {
                CrossedThresholds = new[] { criterion with { TeacherComparison = evidence with { ShopReplayJoin = mutation switch
                    { "native-hash" => metadata with { ActualSourceStateHash = "other-source" }, "metadata-schema" => metadata with { SchemaVersion = "unknown" },
                        _ => metadata with { SourceKind = "unverified" } } } } }
            };
            Reject(() => MoneyThresholdTeacherCorpusBuilder.ValidateInMemory(new MoneyThresholdTeacherCorpus { SchemaVersion = newCorpus.SchemaVersion,
                CorpusId = "forged-metadata", RowCount = 1, Rows = new[] { badRow } }), "Forged replay corpus metadata admitted: " + mutation);
        }
        Reject(() => MoneyThresholdTeacherCorpusBuilder.ValidateInMemory(new MoneyThresholdTeacherCorpus { SchemaVersion = newCorpus.SchemaVersion,
            CorpusId = "same-native-twice", RowCount = 2, Rows = new[] { row, row with { RowId = "other-row" } } }), "Repeated native split identity was counted twice.");
        Reject(() => MoneyThresholdShopReplayCorpusCommands.Write(Arguments.Parse(new[] { "--output", source }),
            (_, _) => throw new Exception("Output guard did not run first."), false), "Replay corpus overwrote its own source.");
        Console.WriteLine($"PASS: explicit replay corpus join, original v4 wire/verification, criterion/partition identity and {checks} rejection checks.");
    }
}
