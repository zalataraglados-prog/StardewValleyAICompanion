using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

public static class MoneyCounterfactualSourceAuditBuilder
{
    public static MoneyCounterfactualSourceAudit Build(string referenceManifestPath, string requestId, string replayCatalogPath)
    {
        referenceManifestPath = Path.GetFullPath(referenceManifestPath);
        var catalog = MoneyRevenueCounterfactualProofRequestBuilder.Build(referenceManifestPath);
        var requests = catalog.Requests.Where(row => row.RequestId == requestId).ToArray();
        Require(requests.Length == 1, "Money counterfactual source audit needs one exact original request ID.");
        var request = requests[0];
        Require(request.NativeShopSaleProofRequired && !request.AcquisitionRolloutProofRequired &&
            request.TerminalOptionId == "economy.sell_items" && request.TerminalStateHash == request.SourceStateHash,
            "Money counterfactual source audit supports direct native shop-sale requests only.");
        var reference = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(request.TerminalSnapshotPath, "Money reference source");
        Require(CurrentTeacherFrontierSupport.HashFile(request.TerminalSnapshotPath) == request.TerminalSnapshotSha256 &&
            reference.StateHash == request.TerminalStateHash, "Money counterfactual reference snapshot drifted.");
        var replay = MoneyThresholdNativeOutcomeSelectionBuilder.LoadInputs(replayCatalogPath);
        var report = CompareSources(request, reference, replay);
        report.ReferenceManifestPath = referenceManifestPath;
        report.ReferenceManifestSha256 = CurrentTeacherFrontierSupport.HashFile(referenceManifestPath);
        report.AuditSha256 = AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(report);
        return report;
    }

    public static MoneyCounterfactualSourceAudit Verify(string auditPath)
    {
        var actual = CurrentTeacherFrontierSupport.Read<MoneyCounterfactualSourceAudit>(auditPath, "Money counterfactual source audit");
        Require(actual.SchemaVersion == "money_counterfactual_source_audit.v1", "Money counterfactual source audit schema differs.");
        var expected = Build(actual.ReferenceManifestPath, actual.ReferenceRequestId, actual.ReplayCatalogPath);
        Require(EqualJson(actual, expected), "Money counterfactual source audit differs from its source rebuild.");
        return expected;
    }

    internal static MoneyCounterfactualSourceAudit CompareSources(MoneyRevenueCounterfactualProofRequest request,
        SnapshotEnvelope reference, MoneyThresholdNativeOutcomeSelectionBuilder.SelectionInputs replay)
    {
        var before = MoneyRevenueProgressionVerifier.Project(reference);
        var next = replay.Checkpoint;
        var matches = MoneyThresholdNativeOutcomeSelectionBuilder.BindShopSales(replay.Catalog, replay.Snapshot)
            .Where(row => SameCandidate(request.TerminalCandidate, row.Candidate)).ToArray();
        var referenceBoundaryAvailable = FullShipmentRecoverySettlementBoundaryProjector.TryProject(reference, out var referenceBoundary);
        var replayBoundaryAvailable = FullShipmentRecoverySettlementBoundaryProjector.TryProject(replay.Snapshot, out var replayBoundary);
        var sameActor = before.SaveId == next.SaveId && before.PlayerId == next.PlayerId &&
            reference.GameVersion == replay.Snapshot.GameVersion && reference.BridgeVersion == replay.Snapshot.BridgeVersion;
        var sameFacts = sameActor && before.TotalDay == next.TotalDay && before.Money == next.Money &&
            before.TotalMoneyEarned == next.TotalMoneyEarned && before.ShippingScopeId == next.ShippingScopeId &&
            before.PendingShippingSettlementValue == next.PendingShippingSettlementValue &&
            before.ShippingContentsSignature == next.ShippingContentsSignature;
        var sameContext = EqualJson(MoneyRevenueProgressionVerifier.ReadValue(reference, "menus", "sell_context"),
                MoneyRevenueProgressionVerifier.ReadValue(replay.Snapshot, "menus", "sell_context")) &&
            EqualJson(MoneyRevenueProgressionVerifier.ReadValue(reference, "menus", "active_menu"),
                MoneyRevenueProgressionVerifier.ReadValue(replay.Snapshot, "menus", "active_menu")) &&
            EqualJson(MoneyRevenueProgressionVerifier.ReadValue(reference, "player", "location_id"),
                MoneyRevenueProgressionVerifier.ReadValue(replay.Snapshot, "player", "location_id")) &&
            EqualJson(MoneyRevenueProgressionVerifier.ReadValue(reference, "time", "time"),
                MoneyRevenueProgressionVerifier.ReadValue(replay.Snapshot, "time", "time"));
        var sameBoundary = referenceBoundaryAvailable && replayBoundaryAvailable && referenceBoundary == replayBoundary;
        var blockers = new List<string> { "source_audit_is_not_native_proof_or_replay_binding_authority" };
        if (request.TerminalStateHash != replay.Snapshot.StateHash) blockers.Add("original_request_requires_exact_frame_or_explicit_replay_binding");
        if (!sameActor) blockers.Add("replay_actor_or_version_differs");
        if (!sameFacts) blockers.Add("replay_money_checkpoint_facts_differ");
        if (!referenceBoundaryAvailable || !replayBoundaryAvailable) blockers.Add("persistent_boundary_projection_unavailable");
        else if (!sameBoundary) blockers.Add("original_persistent_boundary_differs");
        if (!sameContext) blockers.Add("native_shop_context_differs");
        if (matches.Length != 1) blockers.Add("no_unique_exact_native_replay_quote");
        return new MoneyCounterfactualSourceAudit
        {
            ReferenceRequestId = request.RequestId, ReferenceRequestSha256 = request.RequestSha256,
            ReferenceSnapshotPath = request.TerminalSnapshotPath, ReferenceSnapshotSha256 = request.TerminalSnapshotSha256,
            ReferenceStateHash = request.TerminalStateHash, ReferenceCandidate = request.TerminalCandidate,
            ReplayCatalogPath = replay.CandidateDenominatorPath, ReplayCatalogSha256 = replay.CandidateDenominatorSha256,
            ReplaySnapshotPath = replay.Catalog.SnapshotPath, ReplaySnapshotSha256 = replay.Catalog.SnapshotSha256,
            ReplayStateHash = replay.Snapshot.StateHash, ReplayCandidate = matches.Length == 1 ? matches[0].Candidate : null,
            ReplayObjectiveParameters = matches.Length == 1 ? matches[0].ObjectiveParameters : Array.Empty<StardewAI.Contracts.Execution.SmallModelActionParameter>(),
            SameRawStateHash = request.TerminalStateHash == replay.Snapshot.StateHash,
            SameSnapshotBytes = request.TerminalSnapshotSha256 == replay.Catalog.SnapshotSha256,
            SameActorAndVersion = sameActor, SameMoneyCheckpointFacts = sameFacts,
            ReferencePersistentBoundaryAvailable = referenceBoundaryAvailable, ReplayPersistentBoundaryAvailable = replayBoundaryAvailable,
            SamePersistentBoundary = sameBoundary,
            ChangedPersistentFields = FullShipmentRecoverySettlementBoundaryProjector.ChangedPersistentFields(reference, replay.Snapshot),
            SameNativeShopContext = sameContext, MatchingReplayCandidateCount = matches.Length,
            BlockingReasons = blockers.ToArray()
        };
    }

    private static bool SameCandidate(MoneyRevenueCandidateCatalogEntry reference, MoneyRevenueCandidateCatalogEntry replay) =>
        reference.CandidateChannel == replay.CandidateChannel && reference.CandidateKind == replay.CandidateKind &&
        reference.SemanticKindBinding == replay.SemanticKindBinding && reference.ShopId == replay.ShopId &&
        reference.ItemId == replay.ItemId && reference.QualifiedItemId == replay.QualifiedItemId &&
        reference.SlotIndex == replay.SlotIndex && reference.Quantity == replay.Quantity &&
        reference.UnitPrice == replay.UnitPrice && reference.TotalValue == replay.TotalValue;
}
