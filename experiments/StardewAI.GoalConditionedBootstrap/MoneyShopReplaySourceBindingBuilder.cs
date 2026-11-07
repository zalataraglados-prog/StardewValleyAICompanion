using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyShopReplaySourceBindingBuilder
{
    public static MoneyShopReplaySourceBindingReceipt Build(string manifestPath)
    {
        manifestPath = Path.GetFullPath(manifestPath);
        var manifest = Read<MoneyShopReplaySourceBindingManifest>(manifestPath);
        Require(manifest.SchemaVersion == "money_shop_replay_source_binding_manifest.v1" &&
            !manifest.FormalProductTrainingAuthorized && manifest.Reference is not null && manifest.Replay is not null,
            "Money shop replay binding manifest is invalid.");
        var reference = LoadSource(manifest.Reference!, manifest.ReferenceRequestId);
        var replay = LoadSource(manifest.Replay!, manifest.ReplayRequestId);
        Require(reference.Native.Request.RunId != replay.Native.Request.RunId &&
            reference.Inputs.Snapshot.StateHash != replay.Inputs.Snapshot.StateHash &&
            replay.Witness.RequestId == manifest.ReplayRequestId,
            "Replay binding needs distinct actual sources and the requested replay's own native proof.");
        var audit = MoneyCounterfactualSourceAuditBuilder.CompareSources(reference.Request, reference.Inputs.Snapshot, replay.Inputs);
        VerifySourceFacts(audit);
        var mapping = BindDenominator(reference.Catalog, replay.Catalog, reference.Inputs, replay.Inputs);
        Require(mapping.Any(row => row.ReferenceRequestId == manifest.ReferenceRequestId && row.ReplayRequestId == manifest.ReplayRequestId),
            "Money replay target does not follow the complete request mapping.");
        Require(reference.SaveHash == replay.SaveHash && reference.SetupSignature == replay.SetupSignature &&
            reference.ModHash == replay.ModHash && reference.LedgerSignature == replay.LedgerSignature,
            "Money replay origin, declared setup, exact Mods or resource obligations differ.");
        VerifyExecutables(manifest.Executables, reference.Identity, replay.Identity);
        var artifacts = reference.Artifacts.Concat(replay.Artifacts).Concat(manifest.Executables.Select(row => Artifact("executable:" + row.Role, row.Path)))
            .ToArray();
        var receipt = new MoneyShopReplaySourceBindingReceipt
        {
            ManifestPath = manifestPath, ManifestSha256 = Hash(manifestPath), ReferenceRequestId = manifest.ReferenceRequestId,
            ReplayRequestId = manifest.ReplayRequestId, ReferenceStateHash = reference.Inputs.Snapshot.StateHash,
            ReplayStateHash = replay.Inputs.Snapshot.StateHash, SourceSaveSha256 = reference.SaveHash,
            DeclaredSetupSha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(reference.SetupSignature),
            PersistentBoundaryContract = FullShipmentRecoverySettlementBoundaryProjector.ContractVersion,
            BoundExecutionMetadataDifferences = audit.ChangedPersistentFields,
            FullCandidateCount = reference.Inputs.Catalog.EnumeratedCandidateCount, RequestMapping = mapping,
            ReferenceNativeWitness = reference.Evidence, ReplayNativeEvidence = replay.Evidence,
            SourceArtifacts = artifacts, SourceReplayBindingVerified = true
        };
        receipt.ReceiptSha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(receipt);
        return receipt;
    }

    public static MoneyShopReplaySourceBindingReceipt Verify(string receiptPath)
    {
        var actual = Read<MoneyShopReplaySourceBindingReceipt>(receiptPath);
        Require(actual.SchemaVersion == "money_shop_replay_source_binding_receipt.v1", "Money replay binding receipt schema differs.");
        var expected = Build(actual.ManifestPath);
        Require(EqualJson(actual, expected), "Money replay binding receipt differs from its full source rebuild.");
        return expected;
    }

    private static BoundSource LoadSource(MoneyShopReplaySource source, string requestId)
    {
        var catalog = MoneyRevenueCounterfactualProofRequestBuilder.Build(source.RequestManifestPath);
        var requests = catalog.Requests.Where(row => row.RequestId == requestId).ToArray();
        Require(requests.Length == 1, "Money replay needs an exact original request ID.");
        VerifyCandidateLineage(catalog, source.CandidateCatalogPath);
        var inputs = MoneyThresholdNativeOutcomeSelectionBuilder.LoadInputs(source.CandidateCatalogPath);
        Require(inputs.Snapshot.StateHash == catalog.SourceStateHash && inputs.Catalog.SnapshotSha256 == catalog.SourceSnapshotSha256,
            "Money replay request and original candidate catalog have different sources.");
        var proof = Read<MoneyRevenueTerminalAlternativeProof>(source.NativeWitnessProofPath);
        var witnesses = catalog.Requests.Where(row => row.RequestId == proof.RequestId).ToArray();
        Require(witnesses.Length == 1 && IsDirect(witnesses[0]) && (proof.RouteTransitions?.Length ?? 0) == 0,
            "Money replay native witness is not an own-source direct request.");
        var evidence = MoneyRevenueTerminalExecutionProofBuilder.VerifyAlternative(witnesses[0], proof);
        VerifyLocalRecurrence(Read<JsonElement>(proof.RevenueRecurrenceManifestPath), proof.ShopSaleEpisodePath);
        var episode = Read<PlanExecutionEpisodeEnvelope>(proof.ShopSaleEpisodePath);
        var native = NativeExecutionActionSourceReader.Read(episode.CompiledQueuePath, episode.ExecutionResultPath, episode.AfterSnapshotPath);
        Require(native.Before.StateHash == catalog.SourceStateHash && evidence.StrategyLedgerExecutionRunId == native.Request.RunId,
            "Money replay witness does not bind its actual native source.");
        VerifyExecutionIdentity(native, source.IsolatedSaveSlotPath);
        var identity = Read<JsonElement>(source.RunIdentityPath);
        Require(Text(identity, "run_id") == native.Request.RunId, "Money replay recorded run identity differs.");
        var saveHash = MoneyThresholdRuntimeModIsolationVerifier.HashDirectory(source.SourceSaveSlotPath);
        Require(saveHash == Text(identity, "source_save_sha256") &&
            saveHash == MoneyThresholdRuntimeModIsolationVerifier.HashDirectory(source.IsolatedSaveSlotPath) &&
            !SamePath(source.SourceSaveSlotPath, source.IsolatedSaveSlotPath), "Money replay save origin or isolated clone drifted.");
        var measurement = Read<MoneyShopReplayMeasurementIdentity>(source.MeasurementIdentityPath);
        Require(measurement.AcceptanceScope == "criterion_local_declared_held_stock" &&
            measurement.PreMeasurementNativeConfigInitializationExcluded && measurement.SourceStateHash == catalog.SourceStateHash &&
            SamePath(measurement.SourceSnapshotPath, catalog.SourceSnapshotPath) && measurement.SourceSnapshotSha256 == catalog.SourceSnapshotSha256 &&
            Timestamp(measurement.RecordedBeforeDispatchUtc) < Timestamp(native.Request.CreatedAt),
            "Money replay lacks exact pre-dispatch measurement provenance.");
        // Reuse only the original Mod verifier, not the threshold-run admission or a fabricated passed summary.
        MoneyThresholdRuntimeModIsolationVerifier.Verify(new MoneyThresholdNativeOutcomeRunSummary
        {
            IsolatedModsRootPath = source.ModsRootPath, IsolatedModsSha256Before = measurement.MeasuredModRootSha256,
            IsolatedModsSha256After = measurement.MeasuredModRootSha256, RuntimeModAllowlist = measurement.MeasuredModAllowlist
        });
        var setup = VerifySetup(source.SetupSteps, native.Request.RunId, native.Request.SaveIsolationPath,
            Timestamp(measurement.RecordedBeforeDispatchUtc));
        var ledger = VerifyInitialLedger(catalog.SourceStrategyLedgerPath, inputs.Snapshot);
        var artifacts = new[] { ("request_manifest", source.RequestManifestPath), ("candidate_catalog", source.CandidateCatalogPath),
            ("snapshot", catalog.SourceSnapshotPath), ("strategy_ledger", catalog.SourceStrategyLedgerPath),
            ("native_witness_proof", source.NativeWitnessProofPath), ("native_episode", proof.ShopSaleEpisodePath),
            ("run_identity", source.RunIdentityPath), ("measurement_identity", source.MeasurementIdentityPath) }
            .Select(row => Artifact(native.Request.RunId + ":" + row.Item1, row.Item2))
            .Concat(source.SetupSteps.SelectMany(row => new[] { Artifact(native.Request.RunId + ":setup_request:" + row.Role, row.RequestPath),
                Artifact(native.Request.RunId + ":setup_result:" + row.Role, row.ResultPath) })).ToArray();
        return new(source, catalog, requests[0], inputs, proof, evidence, native, identity, saveHash, setup,
            measurement.MeasuredModRootSha256, ledger, artifacts);
    }

    private static bool IsDirect(MoneyRevenueCounterfactualProofRequest row) => row.NativeShopSaleProofRequired &&
        !row.AcquisitionRolloutProofRequired && !row.NativeShippingDepositProofRequired && !row.NativeShippingSettlementProofRequired &&
        row.TerminalOptionId == "economy.sell_items" && row.TerminalStateHash == row.SourceStateHash;
    private static T Read<T>(string path) => CurrentTeacherFrontierSupport.Read<T>(Path.GetFullPath(path), "Money shop replay source");
    private static string Hash(string path) => CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(path));
    private static string Text(JsonElement row, string field) => CurrentTeacherFrontierSupport.RequiredString(row, field);
    private static bool SamePath(string left, string right) => string.Equals(Path.GetFullPath(left), Path.GetFullPath(right),
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static MoneyRevenueArtifactBinding Artifact(string role, string path) => new(role, Path.GetFullPath(path), Hash(path));
    private static DateTimeOffset Timestamp(string value) => DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture,
        System.Globalization.DateTimeStyles.RoundtripKind, out var parsed) ? parsed : throw new InvalidDataException("Money replay timestamp is invalid.");

    private sealed record BoundSource(MoneyShopReplaySource Source, MoneyRevenueCounterfactualProofRequestCatalog Catalog,
        MoneyRevenueCounterfactualProofRequest Request, MoneyThresholdNativeOutcomeSelectionBuilder.SelectionInputs Inputs,
        MoneyRevenueTerminalAlternativeProof Witness, MoneyRevenueTerminalExecutionEvidence Evidence, VerifiedNativeExecutionAction Native,
        JsonElement Identity, string SaveHash, string SetupSignature, string ModHash, string LedgerSignature, MoneyRevenueArtifactBinding[] Artifacts);
}
