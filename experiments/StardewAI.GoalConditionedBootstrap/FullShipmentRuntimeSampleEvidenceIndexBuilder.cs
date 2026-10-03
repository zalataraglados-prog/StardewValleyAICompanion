namespace StardewAI.GoalConditionedBootstrap;

public static class FullShipmentRuntimeSampleEvidenceIndexBuilder
{
    public static FullShipmentRuntimeSampleEvidenceIndexReport Build(
        string staticInventoryPath,
        string evidenceManifestPath)
    {
        var inventoryFullPath = Path.GetFullPath(staticInventoryPath);
        var manifestFullPath = Path.GetFullPath(evidenceManifestPath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            FullShipmentStaticCompilabilityInventoryReport>(
            inventoryFullPath,
            "Full Shipment static compilability inventory");
        var manifest = CurrentTeacherFrontierSupport.Read<
            FullShipmentRuntimeSampleEvidenceManifest>(
            manifestFullPath,
            "Full Shipment runtime sample evidence manifest");

        ValidateStaticInventory(inventory);
        var sources = manifest.Samples ??
            throw new InvalidDataException(
                "Full Shipment runtime sample evidence manifest has no samples.");
        Require(manifest.SchemaVersion ==
                    "full_shipment_runtime_sample_evidence_manifest.v1" &&
                !manifest.FormalTrainingAuthorized &&
                sources.All(sample => sample is not null) &&
                sources.GroupBy(sample => sample.StratumId,
                        StringComparer.Ordinal)
                    .All(group => group.Count() == 1) &&
                sources.GroupBy(sample => sample.RouteOccurrenceId,
                        StringComparer.Ordinal)
                    .All(group => group.Count() == 1),
            "Full Shipment runtime sample evidence manifest is invalid.");

        var shared = VerifySharedShippingOrNull(inventory, manifest);
        var samples = sources
            .Select(sample => VerifySample(inventory, sample))
            .ToArray();
        return BuildReport(
            inventory,
            CurrentTeacherFrontierSupport.HashFile(inventoryFullPath),
            CurrentTeacherFrontierSupport.HashFile(manifestFullPath),
            shared,
            samples);
    }

    internal static FullShipmentRuntimeSampleEvidenceIndexReport BuildReport(
        FullShipmentStaticCompilabilityInventoryReport inventory,
        string staticInventorySha256,
        string evidenceManifestSha256,
        FullShipmentVerifiedSharedShippingEvidence? shared,
        IReadOnlyCollection<FullShipmentVerifiedRuntimeSample> samples)
    {
        Require(IsSha256(staticInventorySha256) &&
                IsSha256(evidenceManifestSha256),
            "Full Shipment runtime sample index input hash is invalid.");
        var strata = inventory.RuntimeSampleStrata ??
            throw new InvalidDataException(
                "Full Shipment static inventory has no runtime sample strata.");
        var stratumIds = strata
            .Select(stratum => stratum.StratumId)
            .ToHashSet(StringComparer.Ordinal);
        Require(strata.Length == inventory.RuntimeSampleStratumCount &&
                strata.Length > 0 &&
                stratumIds.Count == strata.Length,
            "Full Shipment runtime sample strata are incomplete or duplicated.");
        Require(samples.All(sample => stratumIds.Contains(sample.StratumId)) &&
                samples.GroupBy(sample => sample.StratumId, StringComparer.Ordinal)
                    .All(group => group.Count() == 1) &&
                samples.GroupBy(sample => sample.RouteOccurrenceId,
                        StringComparer.Ordinal)
                    .All(group => group.Count() == 1),
            "Full Shipment runtime sample evidence is unknown or duplicated.");

        var evidenceByStratum = samples.ToDictionary(
            sample => sample.StratumId,
            StringComparer.Ordinal);
        var rows = strata
            .OrderBy(stratum => stratum.StratumId, StringComparer.Ordinal)
            .Select(stratum =>
            {
                if (!evidenceByStratum.TryGetValue(
                        stratum.StratumId,
                        out var evidence))
                {
                    return new FullShipmentRuntimeSampleEvidenceRow(
                        stratum.StratumId,
                        stratum.RouteKind,
                        stratum.RouteOccurrenceCount,
                        "missing_exact_native_sample",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty);
                }

                return new FullShipmentRuntimeSampleEvidenceRow(
                    stratum.StratumId,
                    stratum.RouteKind,
                    stratum.RouteOccurrenceCount,
                    "verified_exact_native_sample",
                    evidence.RouteOccurrenceId,
                    evidence.RequirementId,
                    evidence.QualifiedItemId,
                    evidence.RolloutProofManifestSha256,
                    evidence.RolloutProofReceiptSha256,
                    evidence.RolloutId);
            })
            .ToArray();
        var missing = rows
            .Where(row => row.EvidenceStatus == "missing_exact_native_sample")
            .Select(row => row.StratumId)
            .ToArray();
        var complete = shared is not null && missing.Length == 0;
        var gaps = new List<string>();
        if (shared is null)
            gaps.Add("shared_full_shipment_shipping_settlement_evidence_missing");
        if (missing.Length > 0)
            gaps.Add("runtime_sample_strata_missing:" + missing.Length);

        return new FullShipmentRuntimeSampleEvidenceIndexReport
        {
            Status = complete
                ? "verified_complete_runtime_sample_evidence"
                : "partial_runtime_sample_evidence",
            StaticInventorySha256 = staticInventorySha256,
            EvidenceManifestSha256 = evidenceManifestSha256,
            SharedShippingEvidenceVerified = shared is not null,
            SharedShippingRecurrenceManifestSha256 =
                shared?.RecurrenceManifestSha256 ?? string.Empty,
            SharedShippingPrefixCheckpointSha256 =
                shared?.PrefixCheckpointSha256 ?? string.Empty,
            RuntimeSampleStratumCount = rows.Length,
            VerifiedRuntimeSampleStratumCount = rows.Length - missing.Length,
            MissingRuntimeSampleStratumCount = missing.Length,
            RuntimeSampleEvidenceComplete = complete,
            FullRecurrenceRequiredForTraining = false,
            FullRecurrenceRetainedForAcceptance = true,
            FormalProductTrainingAuthorized = false,
            Strata = rows,
            RemainingStratumIds = missing,
            RemainingEvidenceGaps = gaps.ToArray()
        };
    }

    private static FullShipmentVerifiedSharedShippingEvidence?
        VerifySharedShippingOrNull(
            FullShipmentStaticCompilabilityInventoryReport inventory,
            FullShipmentRuntimeSampleEvidenceManifest manifest)
    {
        var hasRecurrence = !string.IsNullOrWhiteSpace(
            manifest.SharedShippingRecurrenceManifestPath);
        var hasCheckpoint = !string.IsNullOrWhiteSpace(
            manifest.SharedShippingPrefixCheckpointPath);
        Require(hasRecurrence == hasCheckpoint,
            "Shared Full Shipment shipping evidence is only partially specified.");
        if (!hasRecurrence)
            return null;

        var recurrencePath = Path.GetFullPath(
            manifest.SharedShippingRecurrenceManifestPath);
        var checkpointPath = Path.GetFullPath(
            manifest.SharedShippingPrefixCheckpointPath);
        var expected = FullShipmentRecurrenceProofBuilder
            .BuildPrefixCheckpoint(recurrencePath);
        var actual = CurrentTeacherFrontierSupport.Read<
            FullShipmentRecurrencePrefixCheckpoint>(
            checkpointPath,
            "Full Shipment shared shipping prefix checkpoint");
        Require(EqualJson(actual, expected) &&
                actual.PrefixProofVerified &&
                actual.VerifiedIterationCount > 0 &&
                !actual.FormalTrainingAuthorized &&
                actual.RequirementInventorySha256 ==
                    inventory.RequirementInventorySha256 &&
                actual.AcquisitionLoweringSha256 ==
                    inventory.AcquisitionLoweringSha256,
            "Shared Full Shipment shipping evidence is stale or invalid.");
        return new FullShipmentVerifiedSharedShippingEvidence(
            CurrentTeacherFrontierSupport.HashFile(recurrencePath),
            CurrentTeacherFrontierSupport.HashFile(checkpointPath));
    }

    private static FullShipmentVerifiedRuntimeSample VerifySample(
        FullShipmentStaticCompilabilityInventoryReport inventory,
        FullShipmentRuntimeSampleEvidenceSource source)
    {
        Require(!string.IsNullOrWhiteSpace(source.StratumId) &&
                !string.IsNullOrWhiteSpace(source.RouteOccurrenceId) &&
                !string.IsNullOrWhiteSpace(
                    source.AcquisitionRolloutProofManifestPath) &&
                !string.IsNullOrWhiteSpace(
                    source.AcquisitionRolloutProofReceiptPath),
            "Full Shipment runtime sample source is incomplete.");
        var manifestPath = Path.GetFullPath(
            source.AcquisitionRolloutProofManifestPath);
        var receiptPath = Path.GetFullPath(
            source.AcquisitionRolloutProofReceiptPath);
        var expectedReceipt = AcquisitionRoutePortfolioRolloutProofBuilder
            .BuildReceipt(manifestPath);
        var actualReceipt = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofReceipt>(
            receiptPath,
            "Full Shipment acquisition rollout proof receipt");
        Require(EqualJson(actualReceipt, expectedReceipt) &&
                actualReceipt.ProofChainVerified &&
                actualReceipt.PortfolioCompletionVerified &&
                !actualReceipt.FormalTrainingAuthorized,
            "Full Shipment acquisition rollout proof is stale or incomplete.");

        var proof = CurrentTeacherFrontierSupport.Read<
            AcquisitionRoutePortfolioRolloutProofManifest>(
            manifestPath,
            "Full Shipment acquisition rollout proof manifest");
        var matchingInputs = EnumerateExecutionInputs(proof)
            .Where(inputs => inputs.RouteOccurrenceId ==
                source.RouteOccurrenceId)
            .ToArray();
        Require(matchingInputs.Length == 1,
            "Full Shipment runtime sample route occurrence is absent or ambiguous.");
        var executionInputs = matchingInputs[0];
        Require(CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(
                    executionInputs.RequirementInventoryPath)) ==
                    inventory.RequirementInventorySha256 &&
                CurrentTeacherFrontierSupport.HashFile(Path.GetFullPath(
                    executionInputs.AcquisitionLoweringPath)) ==
                    inventory.AcquisitionLoweringSha256,
            "Full Shipment runtime sample authority inputs drifted.");

        var route = inventory.Routes.SingleOrDefault(candidate =>
                candidate.RouteOccurrenceId == source.RouteOccurrenceId) ??
            throw new InvalidDataException(
                "Full Shipment runtime sample route is not statically admitted.");
        Require(route.StaticCompilationReady,
            "Full Shipment runtime sample route is not statically admitted.");
        var stratum = inventory.RuntimeSampleStrata.SingleOrDefault(candidate =>
                candidate.StratumId == source.StratumId) ??
            throw new InvalidDataException(
                "Full Shipment runtime sample stratum is unknown.");
        Require(Matches(stratum, route),
            "Full Shipment runtime sample does not match its declared stratum.");

        return new FullShipmentVerifiedRuntimeSample(
            source.StratumId,
            route.RouteOccurrenceId,
            route.RequirementId,
            route.QualifiedItemId,
            CurrentTeacherFrontierSupport.HashFile(manifestPath),
            CurrentTeacherFrontierSupport.HashFile(receiptPath),
            actualReceipt.RolloutId);
    }

    private static IEnumerable<AcquisitionRouteExecutionBindingInputs>
        EnumerateExecutionInputs(
            AcquisitionRoutePortfolioRolloutProofManifest proof)
    {
        yield return proof.InitialCheckpointProof.ExecutionInputs;
        foreach (var transition in proof.ContinuationTransitions ??
                 Array.Empty<AcquisitionRoutePortfolioContinuationTransitionProof>())
        {
            yield return transition.ExecutionInputs;
        }
    }

    private static bool Matches(
        FullShipmentRuntimeSampleStratum stratum,
        FullShipmentRouteCompilabilityRow route) =>
        stratum.RouteKind == route.RouteKind &&
        stratum.SourceEvidenceMode == route.SourceEvidenceMode &&
        stratum.EndpointOptionIds.SequenceEqual(
            route.EndpointOptionIds,
            StringComparer.Ordinal) &&
        stratum.SupportingOptionIds.SequenceEqual(
            route.SupportingOptionIds,
            StringComparer.Ordinal) &&
        stratum.InlineSupportTransitionKinds.SequenceEqual(
            route.InlineSupportTransitionKinds,
            StringComparer.Ordinal);

    private static void ValidateStaticInventory(
        FullShipmentStaticCompilabilityInventoryReport inventory)
    {
        var rebuilt = FullShipmentStaticCompilabilityInventoryBuilder
            .BuildRuntimeSampleStrata(inventory.Routes);
        Require(inventory.SchemaVersion ==
                    "full_shipment_static_compilability_inventory.v2" &&
                inventory.StaticCompilabilityComplete &&
                inventory.RequiredGroupCount == 154 &&
                inventory.RuntimeSampleStratumCount > 0 &&
                !inventory.FullRecurrenceRequiredForTraining &&
                inventory.FullRecurrenceRetainedForAcceptance &&
                !inventory.FormalProductTrainingAuthorized &&
                EqualJson(inventory.RuntimeSampleStrata, rebuilt),
            "Full Shipment static compilability inventory is invalid or stale.");
    }
}
