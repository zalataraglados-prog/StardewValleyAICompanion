using System.Text.Json;
using StardewAI.Core.Goals;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void VerifyMoneyShopReplayComparison()
    {
        var root = TempArtifactRoot("money-shop-replay-comparison");
        Directory.CreateDirectory(root);
        var before = MoneyRevenueTerminalSnapshot(100, 100, 49_990, 1);
        var sourcePath = Path.Combine(root, "source.json");
        Write(sourcePath, before);
        var ledgerPath = Path.Combine(root, "ledger.json");
        var ledger = MoneyRevenueTerminalLedger(before.StateHash);
        Write(ledgerPath, ledger);
        var fast = MoneyRevenueComparisonAdmissionAlternative(root, "replay-batch-fast", before, 142, sourcePath, ledgerPath, ledger);
        var slow = MoneyRevenueComparisonAdmissionAlternative(root, "replay-batch-slow", before, 184, sourcePath, ledgerPath, ledger);
        var referenceSlow = JsonSerializer.Deserialize<MoneyRevenueCounterfactualProofRequest>(JsonSerializer.Serialize(slow.Request), JsonDefaults.Options)!;
        referenceSlow.RequestId = "reference.request.slow";
        referenceSlow.ConcreteAlternativeId = "reference.concrete.slow";
        var requests = new[] { fast.Request, referenceSlow };
        var catalog = new MoneyRevenueCounterfactualProofRequestCatalog
        {
            SourceStateHash = before.StateHash, RequestCount = 2, Requests = requests,
            RequestIds = requests.Select(row => row.RequestId).ToArray(), RequestDenominatorAuthoritative = true,
            TerminalStrategyLedgerAnchorsVerified = true, CounterfactualProofCollectionReady = true
        };
        MoneyShopReplayComparisonAlternative Row(MoneyRevenueCounterfactualProofRequest reference, MoneyRevenueComparisonAdmissionFixture actual, string id)
        {
            var context = MoneyRevenueComparisonAdmissionBuilder.VerifyAlternativeWithContext(actual.Request, actual.Proof, actual.Evidence);
            // This tests the pure mapped comparer; Build separately verifies actual runtime source authority.
            return new(reference.RequestId, reference.ConcreteAlternativeId, actual.Request.RequestId, actual.Request.RequestSha256,
                actual.Request.SourceStateHash, "fixture.run." + id, "fixture.nonce." + id,
                id == "fast" ? "exact_reference_native_witness" : "verified_shop_replay_source", string.Empty, string.Empty,
                actual.EpisodePath, CurrentTeacherFrontierSupport.HashFile(actual.EpisodePath), context.Alternative);
        }
        var rows = new[] { Row(fast.Request, fast, "fast"), Row(referenceSlow, slow, "slow") };
        var sourceHashes = rows.Select(row => row.ActualComparisonEvidence.RevenuePrefix.InitialStateHash).ToArray();
        var comparisons = MoneyShopReplayComparisonBuilder.CompareVerifiedDenominator(catalog, rows);
        Require(comparisons.Select(row => row.CriterionId).SequenceEqual(GrandpaEvaluationGoalEvaluator.MoneyThresholds.Select(row => row.CriterionId)) &&
            comparisons.All(row => row.CandidateDenominatorComplete && row.CandidateDenominatorCount == 2 && row.TeacherPreferenceLabelEligible &&
                row.PairwisePreferences.Length == 1 && row.SelectedAlternativeId == fast.Request.ConcreteAlternativeId && !row.UsesLearnerScore && !row.FormalTrainingAuthorized) &&
            rows[1].ActualRequestId != rows[1].ReferenceRequestId && rows[1].ActualComparisonEvidence.ConcreteAlternativeId != rows[1].ReferenceConcreteAlternativeId &&
            rows.Select(row => row.ActualComparisonEvidence.RevenuePrefix.InitialStateHash).SequenceEqual(sourceHashes),
            "Mapped comparison lost authoritative thresholds, actual identity or original comparator semantics.");
        var checks = 0;
        void Reject(Action action, string reason)
        {
            var rejected = false;
            try { action(); } catch (InvalidDataException) { rejected = true; }
            Require(rejected, reason); checks++;
        }
        Reject(() => MoneyShopReplayComparisonBuilder.CompareVerifiedDenominator(catalog, rows.Take(1).ToArray()), "Incomplete mapped native denominator admitted.");
        foreach (var mutation in new[] { "reference", "actual", "run", "nonce", "concrete", "prefix", "evidence" })
        {
            var changed = rows[1];
            changed = mutation switch
            {
                "reference" => changed with { ReferenceRequestId = rows[0].ReferenceRequestId },
                "actual" => changed with { ActualRequestId = rows[0].ActualRequestId },
                "run" => changed with { ActualRunId = rows[0].ActualRunId },
                "nonce" => changed with { ActualRequestNonce = rows[0].ActualRequestNonce },
                "concrete" => changed with { ReferenceConcreteAlternativeId = "wrong-reference" },
                "prefix" => changed with { ActualSourceStateHash = "invented-source" },
                _ => changed with { ActualComparisonEvidence = changed.ActualComparisonEvidence with { RequestId = "wrong-actual" } }
            };
            Reject(() => MoneyShopReplayComparisonBuilder.CompareVerifiedDenominator(catalog, new[] { rows[0], changed }), "Mapped identity forgery admitted: " + mutation);
        }
        var tied = rows[1] with { ActualComparisonEvidence = rows[1].ActualComparisonEvidence with
            { AggregateOpportunityCost = rows[0].ActualComparisonEvidence.AggregateOpportunityCost } };
        Require(MoneyShopReplayComparisonBuilder.CompareVerifiedDenominator(catalog, new[] { rows[0], tied }).All(row =>
            !row.TeacherPreferenceLabelEligible && row.PairwisePreferences.Length == 0 && row.SelectedAlternativeId.Length == 0),
            "An incomparable native frontier fabricated a Teacher preference.");
        var binding = Path.Combine(root, "binding.json");
        var valid = new MoneyShopReplayComparisonManifest { SourceBindingReceiptPaths = new[] { binding } };
        MoneyShopReplayComparisonBuilder.ValidateManifest(valid);
        foreach (var mutation in new[] { "schema", "formal", "empty", "duplicate", "alias", "blank" })
        {
            var bad = new MoneyShopReplayComparisonManifest { SourceBindingReceiptPaths = new[] { binding } };
            switch (mutation)
            {
                case "schema": bad.SchemaVersion = "unknown"; break;
                case "formal": bad.FormalProductTrainingAuthorized = true; break;
                case "empty": bad.SourceBindingReceiptPaths = Array.Empty<string>(); break;
                case "duplicate": bad.SourceBindingReceiptPaths = new[] { binding, binding }; break;
                case "alias": bad.SourceBindingReceiptPaths = new[] { binding, Path.Combine(root, ".", "binding.json") }; break;
                case "blank": bad.SourceBindingReceiptPaths = new[] { " " }; break;
            }
            Reject(() => MoneyShopReplayComparisonBuilder.ValidateManifest(bad), "Invalid batch manifest admitted: " + mutation);
        }
        var hash = CurrentTeacherFrontierSupport.HashFile(sourcePath);
        var descriptor = new MoneyShopReplaySource { SourceSaveSlotPath = Path.Combine(root, "source-save"),
            SetupSteps = new[] { new MoneyShopReplaySetupStep("time", "request.json", "result.json") } };
        var equalDescriptor = JsonSerializer.Deserialize<MoneyShopReplaySource>(JsonSerializer.Serialize(descriptor), JsonDefaults.Options)!;
        MoneyShopReplaySourceBindingBuilder.VerifyReferenceDescriptor(descriptor, equalDescriptor);
        var foreignDescriptor = JsonSerializer.Deserialize<MoneyShopReplaySource>(JsonSerializer.Serialize(descriptor), JsonDefaults.Options)!;
        foreignDescriptor.SourceSaveSlotPath += ".foreign";
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyReferenceDescriptor(descriptor, foreignDescriptor), "Cached reference crossed to a foreign source descriptor.");
        foreignDescriptor.SourceSaveSlotPath = descriptor.SourceSaveSlotPath;
        foreignDescriptor.SetupSteps[0] = foreignDescriptor.SetupSteps[0] with { RequestPath = "other-request.json" };
        Reject(() => MoneyShopReplaySourceBindingBuilder.VerifyReferenceDescriptor(descriptor, foreignDescriptor), "Cached reference dropped changed setup provenance.");
        Reject(() => MoneyShopReplayComparisonCommands.Write(Arguments.Parse(new[] { "--output", sourcePath }),
            (_, _) => throw new Exception("Output guard did not run first."), false), "Comparison overwrote a source file.");
        Require(CurrentTeacherFrontierSupport.HashFile(sourcePath) == hash, "Mapped comparer changed its original source.");
        Console.WriteLine($"PASS: six authoritative mapped comparisons, incomparable frontier and {checks} rejection checks; no actual identity rewrite.");
    }
}
