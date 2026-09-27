using System.Text.Json;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionSettlementBuilder
{
    private const string SettlementReason =
        "verified_supporting_transition_consumption";

    private static SettlementContext Prepare(
        AcquisitionRouteSupportingTransitionRequestInputs inputs,
        string supportRequestPath,
        string supportCommitReceiptPath,
        string committedLedgerPath,
        string commitResultPath,
        string compilationPath,
        string executionReceiptPath,
        string afterSnapshotPath,
        string supportingTransitionReceiptPath,
        string runId,
        string executorVersion)
    {
        var requestPath = Path.GetFullPath(supportRequestPath);
        var commitReceiptPath = Path.GetFullPath(supportCommitReceiptPath);
        var ledgerPath = Path.GetFullPath(committedLedgerPath);
        var compiledPath = Path.GetFullPath(compilationPath);
        var afterPath = Path.GetFullPath(afterSnapshotPath);
        var transitionPath = Path.GetFullPath(supportingTransitionReceiptPath);
        var request = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionRequest>(
            requestPath,
            "Acquisition support settlement request source");
        Require(EqualJson(
                request,
                AcquisitionRouteSupportingTransitionRequestBuilder.Build(inputs)),
            "Support settlement request source drifted.");
        var commit = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionCommitReceipt>(
            commitReceiptPath,
            "Acquisition support settlement commit source");
        Require(EqualJson(
                commit,
                AcquisitionRouteSupportingTransitionCommitReceiptBuilder.Build(
                    inputs,
                    requestPath,
                    ledgerPath,
                    commitResultPath)),
            "Support settlement commit receipt drifted.");
        var compilation = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteDispatchCompilation>(
            compiledPath,
            "Acquisition support settlement compilation source");
        Require(EqualJson(
                compilation,
                AcquisitionRouteSupportingTransitionCompilationBuilder.Build(
                    inputs,
                    requestPath,
                    commitReceiptPath,
                    ledgerPath,
                    commitResultPath)),
            "Support settlement compilation drifted.");
        var transition = CurrentTeacherFrontierSupport.Read<
            AcquisitionRouteSupportingTransitionReceipt>(
            transitionPath,
            "Acquisition support settlement transition source");
        Require(EqualJson(
                transition,
                AcquisitionRouteSupportingTransitionReceiptBuilder.Build(
                    compiledPath,
                    Path.GetFullPath(inputs.SnapshotPath),
                    Path.GetFullPath(executionReceiptPath),
                    afterPath,
                    runId,
                    executorVersion)),
            "Support settlement transition receipt drifted.");
        var after = CurrentTeacherFrontierSupport.Read<SnapshotEnvelope>(
            afterPath,
            "Acquisition support settlement after snapshot");
        using var afterDocument = JsonDocument.Parse(File.ReadAllText(afterPath));
        var ledger = AcquisitionStrategyLedgerReader.Read(
            ledgerPath,
            afterDocument.RootElement).Ledger;
        return CreateContext(
            request,
            commit,
            compilation,
            transition,
            after,
            ledger,
            requestPath,
            commitReceiptPath,
            transitionPath,
            ledgerPath);
    }

    private static SettlementContext CreateContext(
        AcquisitionRouteSupportingTransitionRequest request,
        AcquisitionRouteSupportingTransitionCommitReceipt commit,
        AcquisitionRouteDispatchCompilation compilation,
        AcquisitionRouteSupportingTransitionReceipt transition,
        SnapshotEnvelope after,
        StrategyCommitmentLedger ledger,
        string requestPath = "",
        string commitReceiptPath = "",
        string transitionPath = "",
        string ledgerPath = "")
    {
        Require(request.SupportRequestReady &&
                commit.SupportReservationCommitVerified &&
                compilation.DispatchReady &&
                compilation.SupportReservationCommitVerified &&
                transition.SupportingTransitionVerified &&
                transition.FreshReplanRequired &&
                !transition.TerminalReceiptEligible &&
                !transition.FormalTrainingAuthorized,
            "Support settlement source chain is not verified.");
        Require(request.GoalId == commit.GoalId &&
                request.GoalId == compilation.GoalId &&
                request.GoalId == transition.GoalId &&
                request.RouteOccurrenceId == commit.RouteOccurrenceId &&
                request.RouteOccurrenceId == compilation.RouteOccurrenceId &&
                request.RouteOccurrenceId == transition.RouteOccurrenceId &&
                request.SelectedCandidateId == commit.SelectedCandidateId &&
                request.SelectedCandidateId == compilation.SourceCandidateId &&
                request.SelectedCandidateId == transition.SourceCandidateId &&
                compilation.SelectedCandidateId ==
                    transition.SelectedCandidateId &&
                commit.CommittedLedgerRevision == ledger.Revision,
            "Support settlement source identity drifted.");
        var evidence = VerifiedMaterialEvidence(request, transition);
        Require(request.SupportMaterialConsumptions.Length > 0 &&
                evidence.Length ==
                    request.SupportMaterialConsumptions.Length,
            "Support settlement material evidence is incomplete.");
        var consumptions = new List<SettlementConsumptionContext>();
        foreach (var planned in request.SupportMaterialConsumptions)
        {
            var observed = evidence.Where(value =>
                    value.ReservationId == planned.ReservationId &&
                    value.NodeId == planned.NodeId &&
                    value.SlotIndex == planned.SlotIndex &&
                    value.QualifiedItemId == planned.QualifiedItemId &&
                    value.ExpectedConsumedQuantity ==
                        planned.ConsumedQuantity &&
                    value.ObservedConsumedQuantity ==
                        planned.ConsumedQuantity &&
                    value.Verified)
                .ToArray();
            Require(observed.Length == 1,
                "Support settlement consumption evidence is not unique and exact.");
            var claims = request.ReservationMaterialClaims.Where(claim =>
                    claim.ReservationId == planned.ReservationId &&
                    claim.NodeId == planned.NodeId &&
                    claim.SlotIndex == planned.SlotIndex &&
                    claim.QualifiedItemId == planned.QualifiedItemId &&
                    claim.Quantity >= planned.ConsumedQuantity)
                .ToArray();
            Require(claims.Length == 1 &&
                    request.ReservationClaimIds.Contains(
                        claims[0].ReservationId,
                        StringComparer.Ordinal),
                "Support settlement consumed claim is not unique and exact.");
            var active = ledger.MaterialReservations.Where(row =>
                    row.ReservationId == claims[0].ReservationId &&
                    row.Status == StrategyCommitmentStatuses.Active &&
                    Exact(row, claims[0], ledger.PlayerId))
                .ToArray();
            Require(active.Length == 1,
                "Support settlement consumed claim is not active and exact.");
            consumptions.Add(new SettlementConsumptionContext(
                claims[0],
                planned.ConsumedQuantity));
        }
        Require(consumptions.Select(value => value.Claim.ReservationId)
                    .Distinct(StringComparer.Ordinal).Count() ==
                consumptions.Count &&
                consumptions.Select(value => value.Claim.SourceDecisionId)
                    .Distinct(StringComparer.Ordinal).Count() == 1,
            "Support settlement consumption set is ambiguous.");
        return new SettlementContext(
            request,
            commit,
            transition,
            after,
            ledger,
            consumptions.ToArray(),
            HashOrEmpty(requestPath),
            HashOrEmpty(commitReceiptPath),
            HashOrEmpty(transitionPath),
            HashOrEmpty(ledgerPath));
    }

    private static ReservationPortfolioSupportingTransitionSettlementRequest
        CanonicalRequest(SettlementContext context) => new()
        {
            StateHash = context.AfterSnapshot.StateHash,
            ExpectedLedgerRevision = context.BaseLedger.Revision,
            PortfolioId = context.Request.SupportRequestId,
            GoalId = context.Request.GoalId,
            RouteSourceDecisionId = context.Consumptions[0]
                .Claim.SourceDecisionId,
            SupportingTransitionReceiptSha256 =
                context.SupportingTransitionReceiptSha256,
            MaterialConsumptions = context.Consumptions.Select(value =>
                    new ReservationPortfolioMaterialConsumption
                    {
                        MaterialReservationId = value.Claim.ReservationId,
                        NodeId = value.Claim.NodeId,
                        SlotIndex = value.Claim.SlotIndex,
                        QualifiedItemId = value.Claim.QualifiedItemId,
                        ConsumedQuantity = value.ConsumedQuantity
                    })
                .ToArray(),
            Reason = SettlementReason
        };

    private static AcquisitionSupportMaterialConsumptionEvidence[]
        VerifiedMaterialEvidence(
            AcquisitionRouteSupportingTransitionRequest request,
            AcquisitionRouteSupportingTransitionReceipt transition)
    {
        if (request.SupportTransitionKind == "crop_planting")
        {
            var crop = transition.CropPlantingTransition ??
                throw new InvalidDataException(
                    "Support settlement crop evidence is missing.");
            Require(crop is { Verified: true, SeedQuantityDecrease: 1 } &&
                    request.SupportMaterialConsumptions.Length == 1,
                "Support settlement lacks exact crop-seed consumption evidence.");
            var planned = request.SupportMaterialConsumptions[0];
            Require(planned.SlotIndex == request.SeedSlotIndex &&
                    QualifiedItemMatches(
                        planned.QualifiedItemId,
                        request.SeedId) &&
                    planned.ConsumedQuantity == crop.SeedQuantityDecrease,
                "Support settlement crop consumption lineage drifted.");
            return new[]
            {
                new AcquisitionSupportMaterialConsumptionEvidence
                {
                    ReservationId = planned.ReservationId,
                    NodeId = planned.NodeId,
                    SlotIndex = planned.SlotIndex,
                    QualifiedItemId = planned.QualifiedItemId,
                    ExpectedConsumedQuantity = planned.ConsumedQuantity,
                    BeforeQuantity = crop.BeforeSeedQuantity,
                    AfterQuantity = crop.AfterSeedQuantity,
                    ObservedConsumedQuantity = crop.SeedQuantityDecrease,
                    Verified = true
                }
            };
        }
        if (request.SupportTransitionKind == "machine_input_load")
        {
            var machine = transition.MachineInputTransition ??
                throw new InvalidDataException(
                    "Support settlement machine evidence is missing.");
            Require(machine.Verified &&
                    machine.MaterialConsumptions.All(value => value.Verified),
                "Support settlement lacks exact machine consumption evidence.");
            return machine.MaterialConsumptions;
        }
        throw new InvalidDataException(
            "Support settlement transition kind is unsupported.");
    }

    private static bool QualifiedItemMatches(
        string qualifiedId,
        string itemId)
    {
        var separator = qualifiedId.LastIndexOf(')');
        return separator >= 0 && separator < qualifiedId.Length - 1 &&
            qualifiedId[(separator + 1)..] == itemId;
    }

    private static bool Exact(
        MaterialReservation row,
        MaterialReservationUpsertRequest claim,
        string playerId) =>
        long.TryParse(playerId, out var owner) &&
        row.ReservationId == claim.ReservationId &&
        row.SourceDecisionId == claim.SourceDecisionId &&
        row.SourceStateHash == claim.StateHash &&
        row.GoalId == claim.GoalId &&
        row.OwnerPlayerId == owner &&
        row.NodeId == claim.NodeId &&
        row.SlotIndex == claim.SlotIndex &&
        row.QualifiedItemId == claim.QualifiedItemId &&
        row.Quantity == claim.Quantity &&
        row.Purpose == claim.Purpose;

    private static string HashOrEmpty(string path) =>
        string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : CurrentTeacherFrontierSupport.HashFile(path);

    private sealed record SettlementContext(
        AcquisitionRouteSupportingTransitionRequest Request,
        AcquisitionRouteSupportingTransitionCommitReceipt CommitReceipt,
        AcquisitionRouteSupportingTransitionReceipt TransitionReceipt,
        SnapshotEnvelope AfterSnapshot,
        StrategyCommitmentLedger BaseLedger,
        SettlementConsumptionContext[] Consumptions,
        string SupportRequestSha256,
        string SupportCommitReceiptSha256,
        string SupportingTransitionReceiptSha256,
        string BaseLedgerSha256);

    private sealed record SettlementConsumptionContext(
        MaterialReservationUpsertRequest Claim,
        int ConsumedQuantity);
}
