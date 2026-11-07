using System.Text.Json;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    internal static void VerifyMoneyDirectConcrete(string? outputRoot = null)
    {
        var root = outputRoot is null ? TempArtifactRoot("money-direct-concrete") : Path.GetFullPath(outputRoot);
        if (outputRoot is not null && (Directory.Exists(root) || File.Exists(root)))
            throw new InvalidDataException("Direct-concrete fixture output already exists.");
        Directory.CreateDirectory(root);
        var alternativePath = MoneyDirectConcreteFixture(root);
        var alternatives = CurrentTeacherFrontierSupport.Read<MoneyRevenueAlternativeDenominatorCatalog>(alternativePath, "fixture alternatives");
        var source = CurrentTeacherFrontierSupport.Read<MoneyRevenueCandidateDenominatorCatalog>(alternatives.SourceCandidateCatalogPath, "fixture candidates");
        var manifestPath = Path.Combine(root, "direct-manifest.json");
        Write(manifestPath, new MoneyRevenueConcreteAlternativeDenominatorManifest { AlternativeDenominatorCatalogPath = alternativePath });
        var before = JsonSerializer.Serialize(alternatives) + JsonSerializer.Serialize(source);
        var files = new[] { alternativePath, alternatives.SourceCandidateCatalogPath, source.SnapshotPath };
        var hashes = files.Select(CurrentTeacherFrontierSupport.HashFile).ToArray();
        var result = MoneyRevenueConcreteAlternativeDenominatorBuilder.Build(manifestPath);
        Require(result.CurrentConcreteDenominatorAuthoritative && result.SkeletonDenominatorComplete &&
                result.TerminalRebindDenominatorComplete && result.ConcreteAlternativeCount == 2 &&
                result.DirectAlternativeCount == 2 && result.ReboundAlternativeCount == 0 &&
                result.TerminalRebindManifestPath == "" && result.TerminalRebindReceiptPath == "" &&
                result.TerminalRebindManifestSha256 == "" && result.TerminalRebindReceiptSha256 == "" &&
                !result.LongHorizonDenominatorAuthoritative && !result.ProductionTeacherPreferenceLabelEligible &&
                !result.FormalProductTrainingAuthorized && result.Alternatives.All(row =>
                    !row.UsesFreshTerminalRebind && !row.RequiresAcquisitionOrProcessingRollout &&
                    row.TerminalStateHash == source.SourceStateHash && row.SourceCandidate == row.TerminalCandidate) &&
                before == JsonSerializer.Serialize(alternatives) + JsonSerializer.Serialize(source),
            "Direct-only concrete union invented evidence, changed its source or promoted proof authority.");
        Require(files.Select(CurrentTeacherFrontierSupport.HashFile).SequenceEqual(hashes),
            "Direct-only public rebuild changed an original source file.");
        Write(Path.Combine(root, "direct-catalog.json"), result);

        static T Copy<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value), JsonDefaults.Options)!;
        void Reject(Action<MoneyRevenueAlternativeDenominatorCatalog>? change = null,
            Action<MoneyRevenueCandidateDenominatorCatalog>? changeSource = null,
            MoneyRevenueTerminalCandidateRebindManifest? rebind = null,
            MoneyRevenueTerminalCandidateRebindReceipt? receipt = null,
            string metadata = "")
        {
            var rows = Copy(alternatives);
            var origin = Copy(source);
            change?.Invoke(rows); changeSource?.Invoke(origin);
            var rejected = false;
            try
            {
                MoneyRevenueConcreteAlternativeDenominatorBuilder.BuildCatalog(manifestPath, new string('a', 64),
                    alternativePath, new string('b', 64), rows, origin, metadata, metadata, rebind, metadata, metadata, receipt);
            }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Direct-only concrete union admitted incomplete source, hidden rollout or invented evidence.");
        }
        Reject(metadata: "dangling");
        Reject(rebind: new MoneyRevenueTerminalCandidateRebindManifest());
        Reject(receipt: new MoneyRevenueTerminalCandidateRebindReceipt());
        Reject(rebind: new MoneyRevenueTerminalCandidateRebindManifest(), receipt: new MoneyRevenueTerminalCandidateRebindReceipt());
        Reject(rows => { rows.Alternatives = Array.Empty<MoneyRevenueAlternativeSkeleton>(); rows.AlternativeCount = 0; });
        Reject(rows => rows.AlternativeSkeletonDenominatorComplete = false);
        Reject(rows => rows.CurrentAlternativeDenominatorAuthoritative = false);
        Reject(rows => rows.FormalProductTrainingAuthorized = true);
        Reject(changeSource: value => value.CurrentSnapshotDenominatorAuthoritative = false);
        Reject(changeSource: value => value.SourceStateHash = new string('f', 64));
        Reject(changeSource: value => value.ComparisonCandidateIds = value.ComparisonCandidateIds.Take(1).ToArray());
        Reject(rows => rows.Alternatives[0].ConcreteAlternativeIdentityComplete = false);
        Reject(rows => rows.Alternatives[0].RequiresAcquisitionOrProcessingRollout = true);
        Reject(rows => rows.Alternatives[0].RequiresFreshTerminalCandidateRebind = true);
        Reject(rows => rows.Alternatives[0].SourceCandidateSha256 = new string('f', 64));
        Reject(rows => rows.AlternativeCount++);
        Reject(rows => rows.Alternatives[1].AlternativeId = rows.Alternatives[0].AlternativeId);

        void RejectManifest(string name, string rebindPath, string receiptPath)
        {
            var path = Path.Combine(root, name + ".json");
            Write(path, new MoneyRevenueConcreteAlternativeDenominatorManifest
            {
                AlternativeDenominatorCatalogPath = alternativePath,
                TerminalRebindManifestPath = rebindPath, TerminalRebindReceiptPath = receiptPath
            });
            var rejected = false;
            try { MoneyRevenueConcreteAlternativeDenominatorBuilder.Build(path); }
            catch (InvalidDataException) { rejected = true; }
            Require(rejected, "Direct-only public consumer ignored unexpected or partial rebind metadata.");
        }
        RejectManifest("unexpected-rebind", "unneeded-manifest.json", "unneeded-receipt.json");
        RejectManifest("partial-manifest", "unneeded-manifest.json", "");
        RejectManifest("partial-receipt", "", "unneeded-receipt.json");
        var producerManifest = Path.Combine(root, "empty-rebind-manifest.json");
        Write(producerManifest, new MoneyRevenueTerminalCandidateRebindManifest { AlternativeDenominatorCatalogPath = alternativePath });
        var producerRejected = false;
        try { MoneyRevenueTerminalCandidateRebindBuilder.Build(producerManifest); }
        catch (InvalidDataException error) { producerRejected = error.Message.Contains("no terminal rebind skeletons", StringComparison.Ordinal); }
        Require(producerRejected, "The original nonempty rollout-rebind producer contract was weakened.");
        Console.WriteLine(root);
    }
}
