namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunFullShipmentRuntimeSampleEvidence()
    {
        var strata = Enumerable.Range(1, 3)
            .Select(index => new FullShipmentRuntimeSampleStratum(
                "stratum-" + index,
                "route-kind-" + index,
                "source-mode",
                new[] { "option-" + index },
                Array.Empty<string>(),
                Array.Empty<string>(),
                index,
                "route-" + index,
                "requirement-" + index,
                "(O)" + index))
            .ToArray();
        var inventory = new FullShipmentStaticCompilabilityInventoryReport
        {
            StaticCompilabilityComplete = true,
            RequiredGroupCount = 154,
            RuntimeSampleStratumCount = strata.Length,
            FullRecurrenceRequiredForTraining = false,
            FullRecurrenceRetainedForAcceptance = true,
            FormalProductTrainingAuthorized = false,
            RuntimeSampleStrata = strata
        };
        var shared = new FullShipmentVerifiedSharedShippingEvidence(
            new string('a', 64),
            new string('b', 64));
        var samples = strata.Select((stratum, index) =>
            new FullShipmentVerifiedRuntimeSample(
                stratum.StratumId,
                "route-" + (index + 1),
                "requirement-" + (index + 1),
                "(O)" + (index + 1),
                new string((char)('c' + index), 64),
                new string((char)('f' + index), 64),
                "rollout-" + (index + 1)))
            .ToArray();

        var partial = FullShipmentRuntimeSampleEvidenceIndexBuilder.BuildReport(
            inventory,
            new string('1', 64),
            new string('2', 64),
            shared,
            samples.Take(1).ToArray());
        Require(partial.Status == "partial_runtime_sample_evidence" &&
                partial.SharedShippingEvidenceVerified &&
                partial.RuntimeSampleStratumCount == 3 &&
                partial.VerifiedRuntimeSampleStratumCount == 1 &&
                partial.MissingRuntimeSampleStratumCount == 2 &&
                !partial.RuntimeSampleEvidenceComplete &&
                partial.RemainingStratumIds.SequenceEqual(
                    new[] { "stratum-2", "stratum-3" },
                    StringComparer.Ordinal) &&
                partial.RemainingEvidenceGaps.SequenceEqual(
                    new[] { "runtime_sample_strata_missing:2" },
                    StringComparer.Ordinal),
            "Partial Full Shipment runtime sample evidence was misreported.");

        var complete = FullShipmentRuntimeSampleEvidenceIndexBuilder.BuildReport(
            inventory,
            new string('1', 64),
            new string('2', 64),
            shared,
            samples);
        Require(complete.Status ==
                    "verified_complete_runtime_sample_evidence" &&
                complete.VerifiedRuntimeSampleStratumCount == 3 &&
                complete.MissingRuntimeSampleStratumCount == 0 &&
                complete.RuntimeSampleEvidenceComplete &&
                !complete.FullRecurrenceRequiredForTraining &&
                complete.FullRecurrenceRetainedForAcceptance &&
                !complete.FormalProductTrainingAuthorized &&
                complete.Strata.All(row => row.EvidenceStatus ==
                    "verified_exact_native_sample"),
            "Complete Full Shipment runtime sample evidence was misreported.");

        var missingShared =
            FullShipmentRuntimeSampleEvidenceIndexBuilder.BuildReport(
                inventory,
                new string('1', 64),
                new string('2', 64),
                null,
                samples);
        Require(!missingShared.RuntimeSampleEvidenceComplete &&
                missingShared.RemainingEvidenceGaps.SequenceEqual(new[]
                {
                    "shared_full_shipment_shipping_settlement_evidence_missing"
                }, StringComparer.Ordinal),
            "Missing shared shipping evidence was accepted.");

        var duplicateRejected = false;
        try
        {
            FullShipmentRuntimeSampleEvidenceIndexBuilder.BuildReport(
                inventory,
                new string('1', 64),
                new string('2', 64),
                shared,
                samples.Append(samples[0]).ToArray());
        }
        catch (InvalidDataException)
        {
            duplicateRejected = true;
        }
        Require(duplicateRejected,
            "Duplicate Full Shipment runtime sample evidence was accepted.");

        VerifySapEvidenceExport();
    }

    private static void VerifySapEvidenceExport()
    {
        var manifestPath = Path.GetFullPath("sap-recurrence-manifest.json");
        var checkpointPath = Path.GetFullPath("sap-prefix-checkpoint.json");
        var rolloutManifestPath = Path.GetFullPath("sap-rollout-manifest.json");
        var rolloutReceiptPath = Path.GetFullPath("sap-rollout-receipt.json");
        var manifestSha = new string('a', 64);
        var rolloutManifestSha = new string('b', 64);
        var manifest = new FullShipmentRecurrenceProofManifest
        {
            Iterations = new[]
            {
                new FullShipmentRecurrenceIterationProof
                {
                    RequirementId = "full_shipment:item:92",
                    QualifiedItemId = "(O)92",
                    AcquisitionRolloutProofManifestPath = rolloutManifestPath,
                    AcquisitionRolloutProofReceiptPath = rolloutReceiptPath
                }
            }
        };
        var checkpoint = new FullShipmentRecurrencePrefixCheckpoint
        {
            Status = "verified_recurrence_prefix",
            ManifestSha256 = manifestSha,
            RequiredItemCount = 154,
            VerifiedIterationCount = 1,
            RemainingItemCount = 153,
            FinalTotalDay = 1,
            PrefixProofVerified = true,
            ReadyForNextIteration = true,
            CompletedRequirementIds = new[] { "full_shipment:item:92" }
        };
        var rolloutManifest = new AcquisitionRoutePortfolioRolloutProofManifest
        {
            InitialCheckpointProof =
                new AcquisitionRoutePortfolioInitialCheckpointProof
                {
                    RunId = "sap-export-self-test",
                    ExecutionInputs = new AcquisitionRouteExecutionBindingInputs
                    {
                        RouteOccurrenceId =
                            "full_shipment:full_shipment:item:92:0:7"
                    }
                }
        };
        var rolloutReceipt = new AcquisitionRoutePortfolioRolloutProofReceipt
        {
            Status = "verified_complete_rollout_proof_chain",
            ManifestSha256 = rolloutManifestSha,
            ProofChainVerified = true,
            PortfolioCompletionVerified = true
        };

        var export = FullShipmentSapEvidenceExportBuilder.BuildReport(
            manifestPath,
            checkpointPath,
            rolloutManifestPath,
            rolloutReceiptPath,
            manifest,
            checkpoint,
            rolloutManifest,
            rolloutReceipt,
            manifestSha,
            new string('c', 64),
            rolloutManifestSha,
            new string('d', 64));
        Require(
            export.EvidenceVerified &&
            export.Status == "passed" &&
            export.Scenario == "sap_prefix" &&
            export.RequirementId == "full_shipment:item:92" &&
            export.QualifiedItemId == "(O)92" &&
            export.VerifiedIterationCount == 1 &&
            export.RemainingItemCount == 153 &&
            !export.FormalTrainingAuthorized,
            "Verified Sap evidence export was misreported.");

        checkpoint.ManifestSha256 = new string('e', 64);
        var hashDriftRejected = false;
        try
        {
            FullShipmentSapEvidenceExportBuilder.BuildReport(
                manifestPath,
                checkpointPath,
                rolloutManifestPath,
                rolloutReceiptPath,
                manifest,
                checkpoint,
                rolloutManifest,
                rolloutReceipt,
                manifestSha,
                new string('c', 64),
                rolloutManifestSha,
                new string('d', 64));
        }
        catch (InvalidDataException)
        {
            hashDriftRejected = true;
        }
        Require(hashDriftRejected,
            "Sap evidence export accepted a drifted recurrence manifest hash.");

        checkpoint.ManifestSha256 = manifestSha;
        manifest.Iterations[0].QualifiedItemId = "(O)93";
        var identityDriftRejected = false;
        try
        {
            FullShipmentSapEvidenceExportBuilder.BuildReport(
                manifestPath,
                checkpointPath,
                rolloutManifestPath,
                rolloutReceiptPath,
                manifest,
                checkpoint,
                rolloutManifest,
                rolloutReceipt,
                manifestSha,
                new string('c', 64),
                rolloutManifestSha,
                new string('d', 64));
        }
        catch (InvalidDataException)
        {
            identityDriftRejected = true;
        }
        Require(identityDriftRejected,
            "Sap evidence export accepted a drifted Sap identity.");
    }
}
