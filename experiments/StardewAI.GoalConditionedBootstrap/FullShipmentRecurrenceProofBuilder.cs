namespace StardewAI.GoalConditionedBootstrap;

public static partial class FullShipmentRecurrenceProofBuilder
{
    private const int StageOneDeadlineTotalDayExclusive = 224;

    public static FullShipmentRecurrenceProofReceipt Build(string manifestPath)
    {
        var verified = VerifyManifest(manifestPath, requireComplete: true);
        return new FullShipmentRecurrenceProofReceipt
        {
            Status = "verified_complete_recurrence",
            GoalId = verified.Inventory.GoalId,
            ManifestSha256 = verified.ManifestSha256,
            RequirementInventorySha256 = verified.InventorySha256,
            AcquisitionLoweringSha256 = verified.LoweringSha256,
            RequiredItemCount = verified.RequiredItemCount,
            VerifiedIterationCount = verified.Iterations.Length,
            InitialStateHash = verified.InitialSnapshot.StateHash,
            FinalStateHash = verified.FinalSnapshot.StateHash,
            InitialTotalDay = verified.InitialProgress.TotalDay,
            TerminalSettlementStartTotalDay =
                verified.Iterations[^1].SettlementStartTotalDay,
            FinalTotalDay = verified.FinalProgress.TotalDay,
            Achievement34Verified = verified.FinalProgress.Achievement34,
            RecurrenceProofVerified = true,
            FormalTrainingAuthorized = false,
            Iterations = verified.Iterations,
            BlockingReasons = Array.Empty<string>()
        };
    }

    public static FullShipmentRecurrencePrefixCheckpoint BuildPrefixCheckpoint(
        string manifestPath)
    {
        var verified = VerifyManifest(manifestPath, requireComplete: false);
        return new FullShipmentRecurrencePrefixCheckpoint
        {
            Status = verified.Complete
                ? "verified_complete_recurrence"
                : "verified_recurrence_prefix",
            GoalId = verified.Inventory.GoalId,
            ManifestSha256 = verified.ManifestSha256,
            RequirementInventorySha256 = verified.InventorySha256,
            AcquisitionLoweringSha256 = verified.LoweringSha256,
            RequiredItemCount = verified.RequiredItemCount,
            VerifiedIterationCount = verified.Iterations.Length,
            RemainingItemCount = verified.RemainingRequirementIds.Length,
            InitialStateHash = verified.InitialSnapshot.StateHash,
            FinalStateHash = verified.FinalSnapshot.StateHash,
            FinalSnapshotSha256 = CurrentTeacherFrontierSupport.HashFile(
                verified.FinalSnapshotPath),
            InitialTotalDay = verified.InitialProgress.TotalDay,
            FinalTotalDay = verified.FinalProgress.TotalDay,
            PrefixProofVerified = true,
            Complete = verified.Complete,
            Achievement34Verified = verified.FinalProgress.Achievement34,
            ReadyForNextIteration = !verified.Complete,
            FormalTrainingAuthorized = false,
            CompletedRequirementIds = verified.CompletedRequirementIds,
            RemainingRequirementIds = verified.RemainingRequirementIds,
            RemainingQualifiedItemIds =
                verified.FinalProgress.MissingQualifiedItemIds,
            Iterations = verified.Iterations,
            BlockingReasons = Array.Empty<string>()
        };
    }

    private static VerifiedRecurrence VerifyManifest(
        string manifestPath,
        bool requireComplete)
    {
        var manifestFullPath = Path.GetFullPath(manifestPath);
        var manifest = CurrentTeacherFrontierSupport.Read<
            FullShipmentRecurrenceProofManifest>(
            manifestFullPath,
            "Full Shipment recurrence proof manifest");
        Require(
            manifest.SchemaVersion ==
                "full_shipment_recurrence_proof_manifest.v1" &&
            !manifest.FormalTrainingAuthorized,
            "Full Shipment recurrence proof manifest is invalid.");
        var iterations = manifest.Iterations ??
            throw new InvalidDataException(
                "Full Shipment recurrence proof manifest has no iterations.");

        var inventoryPath = Path.GetFullPath(
            manifest.RequirementInventoryPath);
        var loweringPath = Path.GetFullPath(manifest.AcquisitionLoweringPath);
        var inventory = CurrentTeacherFrontierSupport.Read<
            AuthoritativeRequirementInventoryReport>(
            inventoryPath,
            "Full Shipment authoritative requirement inventory");
        var lowering = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteOptionLoweringReport>(
            loweringPath,
            "Full Shipment authoritative acquisition lowering");
        CurrentTeacherFrontierSupport.ValidateAuthority(
            inventoryPath,
            inventory,
            lowering,
            "Full Shipment recurrence proof");
        var requiredIds = FullShipmentSettlementSupport
            .RequiredQualifiedItemIds(inventory);
        var requirementSet = CurrentTeacherFrontierSupport.SingleSet(
            inventory.RequirementSets,
            value => value.RequirementSetId,
            "full_shipment",
            "requirement inventory");
        var requirements = requirementSet.Groups.ToDictionary(
            group => group.RequirementId,
            group => group.Alternatives.Single().QualifiedItemId,
            StringComparer.Ordinal);
        Require(iterations.Length > 0 &&
                iterations.Length <= requiredIds.Length &&
                (!requireComplete || iterations.Length == requiredIds.Length),
            requireComplete
                ? "Full Shipment recurrence must contain exactly one iteration per requirement."
                : "Full Shipment recurrence prefix must contain between one and all authoritative requirements.");

        var anchorSnapshotPath = Path.GetFullPath(
            manifest.InitialSnapshotPath);
        var anchorSnapshot = ReadSnapshot(
            anchorSnapshotPath,
            "initial snapshot");
        var initialSnapshot = anchorSnapshot;
        var initial = FullShipmentSettlementVerifier.Project(
            requiredIds,
            anchorSnapshot);
        Require(
            initial.ShippedItemCount == 0 &&
            initial.MissingQualifiedItemIds.Length == requiredIds.Length &&
            !initial.Complete &&
            !initial.Achievement34 &&
            initial.TotalDay == 0,
            "Full Shipment recurrence does not start from a fresh zero-shipment save.");

        var inventorySha = CurrentTeacherFrontierSupport.HashFile(inventoryPath);
        var loweringSha = CurrentTeacherFrontierSupport.HashFile(loweringPath);
        var seenRequirements = new HashSet<string>(StringComparer.Ordinal);
        var seenItems = new HashSet<string>(StringComparer.Ordinal);
        var rows = new List<FullShipmentRecurrenceIterationEvidence>();
        for (var index = 0; index < iterations.Length; index++)
        {
            var proof = iterations[index] ??
                throw new InvalidDataException(
                    "Full Shipment recurrence contains a null iteration.");
            Require(
                requirements.TryGetValue(proof.RequirementId, out var expected) &&
                expected == proof.QualifiedItemId &&
                seenRequirements.Add(proof.RequirementId) &&
                seenItems.Add(proof.QualifiedItemId),
                "Full Shipment recurrence requirement identity is missing, duplicated, or drifted.");

            var acquisition = VerifyAcquisition(
                proof,
                anchorSnapshot,
                anchorSnapshotPath,
                inventorySha,
                loweringSha,
                requiredIds);
            var deposit = VerifyDeposit(
                proof,
                inventoryPath,
                loweringPath,
                acquisition.AfterSnapshot,
                requiredIds);
            var terminalIteration =
                iterations.Length == requiredIds.Length &&
                index == requiredIds.Length - 1;
            var settlement = VerifySettlement(
                proof,
                inventoryPath,
                loweringPath,
                deposit.AfterSnapshot,
                requiredIds,
                terminalIteration);

            Require(
                settlement.BeforeShippedItemCount == index &&
                settlement.AfterShippedItemCount == index + 1,
                "Full Shipment recurrence shipped-count sequence is not contiguous.");
            rows.Add(new FullShipmentRecurrenceIterationEvidence(
                index,
                proof.RequirementId,
                proof.QualifiedItemId,
                acquisition.ProofReceiptSha256,
                deposit.TeacherReceiptSha256,
                settlement.SettlementReceiptSha256,
                settlement.BeforeShippedItemCount,
                settlement.AfterShippedItemCount,
                settlement.StartTotalDay,
                settlement.EndTotalDay,
                settlement.TerminalTransition)
            {
                SettlementRecoveryTransitions =
                    settlement.RecoveryTransitions
            });
            anchorSnapshot = settlement.AfterSnapshot;
            anchorSnapshotPath = settlement.AfterSnapshotPath;
        }

        var final = FullShipmentSettlementVerifier.Project(
            requiredIds,
            anchorSnapshot);
        var complete = iterations.Length == requiredIds.Length;
        VerifyPrefixSequence(
            rows,
            requiredIds.Length,
            initial.TotalDay,
            complete);
        var remainingRequirements = requirementSet.Groups
            .Where(group => !seenRequirements.Contains(group.RequirementId))
            .ToArray();
        var expectedMissingItems = remainingRequirements
            .Select(group => group.Alternatives.Single().QualifiedItemId)
            .ToHashSet(StringComparer.Ordinal);
        Require(final.ShippedItemCount == rows.Count &&
                final.MissingQualifiedItemIds.Length ==
                    requiredIds.Length - rows.Count &&
                final.MissingQualifiedItemIds.ToHashSet(StringComparer.Ordinal)
                    .SetEquals(expectedMissingItems) &&
                (complete
                    ? seenRequirements.SetEquals(requirements.Keys) &&
                      seenItems.SetEquals(requiredIds) &&
                      final.Complete && final.Achievement34
                    : !final.Complete && !final.Achievement34),
            complete
                ? "Full Shipment recurrence terminal denominator is incomplete."
                : "Full Shipment recurrence prefix denominator drifted.");

        return new VerifiedRecurrence(
            CurrentTeacherFrontierSupport.HashFile(manifestFullPath),
            inventory,
            inventorySha,
            loweringSha,
            requiredIds.Length,
            initialSnapshot,
            initial,
            anchorSnapshot,
            final,
            anchorSnapshotPath,
            rows.ToArray(),
            rows.Select(row => row.RequirementId).ToArray(),
            remainingRequirements.Select(group => group.RequirementId)
                .ToArray(),
            complete);
    }

    internal static void VerifySequence(
        IReadOnlyList<FullShipmentRecurrenceIterationEvidence> rows,
        int requiredItemCount,
        int initialTotalDay)
        => VerifyPrefixSequence(
            rows,
            requiredItemCount,
            initialTotalDay,
            expectComplete: true);

    internal static void VerifyPrefixSequence(
        IReadOnlyList<FullShipmentRecurrenceIterationEvidence> rows,
        int requiredItemCount,
        int initialTotalDay,
        bool expectComplete)
    {
        Require(requiredItemCount > 0 &&
                rows.Count > 0 &&
                rows.Count <= requiredItemCount &&
                initialTotalDay == 0 &&
                (expectComplete
                    ? rows.Count == requiredItemCount
                    : rows.Count < requiredItemCount),
            "Full Shipment recurrence sequence denominator is invalid.");
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var terminal = expectComplete && index == rows.Count - 1;
            var priorEndTotalDay = index == 0
                ? initialTotalDay
                : rows[index - 1].SettlementEndTotalDay;
            Require(
                row.IterationIndex == index &&
                row.BeforeShippedItemCount == index &&
                row.AfterShippedItemCount == index + 1 &&
                row.SettlementStartTotalDay >= priorEndTotalDay &&
                row.SettlementEndTotalDay == row.SettlementStartTotalDay + 1 &&
                row.TerminalTransition == terminal &&
                row.SettlementStartTotalDay <
                    StageOneDeadlineTotalDayExclusive &&
                (terminal
                    ? row.SettlementEndTotalDay <=
                        StageOneDeadlineTotalDayExclusive
                    : row.SettlementEndTotalDay <
                        StageOneDeadlineTotalDayExclusive),
                "Full Shipment recurrence sequence order or deadline drifted.");
        }
        if (!expectComplete)
        {
            var remainingSettlementCount = requiredItemCount - rows.Count;
            Require(
                rows[^1].SettlementEndTotalDay + remainingSettlementCount <=
                    StageOneDeadlineTotalDayExclusive,
                "Full Shipment recurrence prefix cannot finish before the Stage-1 deadline.");
        }
    }

    private sealed record VerifiedRecurrence(
        string ManifestSha256,
        AuthoritativeRequirementInventoryReport Inventory,
        string InventorySha256,
        string LoweringSha256,
        int RequiredItemCount,
        StardewAI.Contracts.State.SnapshotEnvelope InitialSnapshot,
        FullShipmentProgressCheckpoint InitialProgress,
        StardewAI.Contracts.State.SnapshotEnvelope FinalSnapshot,
        FullShipmentProgressCheckpoint FinalProgress,
        string FinalSnapshotPath,
        FullShipmentRecurrenceIterationEvidence[] Iterations,
        string[] CompletedRequirementIds,
        string[] RemainingRequirementIds,
        bool Complete);
}
