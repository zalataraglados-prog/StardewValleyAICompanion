namespace StardewAI.GoalConditionedBootstrap;

public static partial class FullShipmentRecurrenceProofBuilder
{
    private const int StageOneDeadlineTotalDayExclusive = 224;

    public static FullShipmentRecurrenceProofReceipt Build(string manifestPath)
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
        Require(
            iterations.Length == requiredIds.Length,
            "Full Shipment recurrence must contain exactly one iteration per requirement.");

        var initialPath = Path.GetFullPath(manifest.InitialSnapshotPath);
        var anchorSnapshot = ReadSnapshot(initialPath, "initial snapshot");
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
                initialPath,
                inventorySha,
                loweringSha,
                requiredIds);
            var deposit = VerifyDeposit(
                proof,
                inventoryPath,
                loweringPath,
                acquisition.AfterSnapshot,
                requiredIds);
            var settlement = VerifySettlement(
                proof,
                inventoryPath,
                loweringPath,
                deposit.AfterSnapshot,
                requiredIds,
                index == requiredIds.Length - 1);

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
                settlement.TerminalTransition));
            anchorSnapshot = settlement.AfterSnapshot;
            initialPath = settlement.AfterSnapshotPath;
        }

        var final = FullShipmentSettlementVerifier.Project(
            requiredIds,
            anchorSnapshot);
        VerifySequence(rows, requiredIds.Length, initial.TotalDay);
        Require(
            seenRequirements.SetEquals(requirements.Keys) &&
            seenItems.SetEquals(requiredIds) &&
            final.Complete &&
            final.ShippedItemCount == requiredIds.Length &&
            final.MissingQualifiedItemIds.Length == 0 &&
            final.Achievement34,
            "Full Shipment recurrence terminal denominator is incomplete.");

        return new FullShipmentRecurrenceProofReceipt
        {
            Status = "verified_complete_recurrence",
            GoalId = inventory.GoalId,
            ManifestSha256 = CurrentTeacherFrontierSupport.HashFile(
                manifestFullPath),
            RequirementInventorySha256 = inventorySha,
            AcquisitionLoweringSha256 = loweringSha,
            RequiredItemCount = requiredIds.Length,
            VerifiedIterationCount = rows.Count,
            InitialStateHash = ReadSnapshot(
                Path.GetFullPath(manifest.InitialSnapshotPath),
                "initial snapshot").StateHash,
            FinalStateHash = anchorSnapshot.StateHash,
            InitialTotalDay = initial.TotalDay,
            TerminalSettlementStartTotalDay = rows[^1].SettlementStartTotalDay,
            FinalTotalDay = final.TotalDay,
            Achievement34Verified = final.Achievement34,
            RecurrenceProofVerified = true,
            FormalTrainingAuthorized = false,
            Iterations = rows.ToArray(),
            BlockingReasons = Array.Empty<string>()
        };
    }

    internal static void VerifySequence(
        IReadOnlyList<FullShipmentRecurrenceIterationEvidence> rows,
        int requiredItemCount,
        int initialTotalDay)
    {
        Require(
            requiredItemCount > 0 &&
            rows.Count == requiredItemCount &&
            initialTotalDay == 0,
            "Full Shipment recurrence sequence denominator is invalid.");
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var terminal = index == rows.Count - 1;
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
    }
}
