using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

public static partial class AcquisitionRouteSupportingTransitionCompilationBuilder
{
    internal static AcquisitionRouteDispatchCompilation BuildCore(
        AcquisitionRouteSupportingTransitionRequest request,
        AcquisitionRouteSupportingTransitionCommitReceipt commit,
        AcquisitionRouteTargetDateUnlock requirement,
        AcquisitionRequirementRouteLowering lowered,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger committedLedger,
        AcquisitionRouteDispatchCandidateMatch[] matches,
        string rankingSha256,
        string supportRequestSha256,
        string supportCommitReceiptSha256,
        IEnumerable<string>? inheritedReasons = null)
    {
        var reasons = (inheritedReasons ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        ValidateArtifacts(
            request,
            commit,
            requirement,
            snapshot,
            committedLedger,
            rankingSha256,
            supportRequestSha256,
            supportCommitReceiptSha256,
            reasons);
        var selected = matches.Where(match =>
                match.RouteOptionRole == "supporting_transition" &&
                match.Candidate.CandidateId == request.SelectedCandidateId &&
                CandidateMatchesRequest(match, request))
            .ToArray();
        if (selected.Length != 1)
            reasons.Add("support_committed_candidate_not_rebuilt_exactly_once");
        if (reasons.Count > 0)
        {
            return Blocked(
                request,
                rankingSha256,
                supportRequestSha256,
                supportCommitReceiptSha256,
                reasons);
        }

        var compilation = AcquisitionRouteDispatchCompilationBuilder.Compile(
            request.GoalId,
            requirement,
            lowered,
            selected[0],
            snapshot,
            committedLedger,
            commit.SupportRequestId,
            commit.CommittedLedgerRevision,
            rankingSha256,
            new[]
            {
                Parameter(
                    "acquisition_support_request_sha256",
                    supportRequestSha256),
                Parameter(
                    "acquisition_support_commit_receipt_sha256",
                    supportCommitReceiptSha256),
                Parameter(
                    "acquisition_support_deadline_total_day",
                    request.SupportDeadlineTotalDay.ToString()),
                Parameter(
                    "acquisition_support_expected_ready_total_day",
                    request.ExpectedReadyTotalDay!.Value.ToString()),
                Parameter(
                    "acquisition_support_transition_kind",
                    request.SupportTransitionKind),
                Parameter(
                    "acquisition_support_material_consumptions_json",
                    JsonSerializer.Serialize(
                        request.SupportMaterialConsumptions,
                        JsonDefaults.Options)),
                Parameter(
                    "acquisition_support_machine_qualified_item_id",
                    request.MachineQualifiedItemId),
                Parameter(
                    "acquisition_support_input_qualified_item_id",
                    request.InputQualifiedItemId),
                Parameter(
                    "acquisition_support_input_slot_index",
                    request.InputSlotIndex?.ToString() ?? string.Empty),
                Parameter(
                    "acquisition_support_input_required_quantity",
                    request.InputRequiredQuantity?.ToString() ?? string.Empty),
                Parameter(
                    "acquisition_support_predicted_processing_minutes",
                    request.PredictedProcessingMinutes?.ToString() ??
                        string.Empty)
            });
        compilation.SupportRequestSha256 = supportRequestSha256;
        compilation.SupportCommitReceiptSha256 = supportCommitReceiptSha256;
        compilation.SupportDeadlineTotalDay =
            request.SupportDeadlineTotalDay;
        compilation.SupportExpectedReadyTotalDay =
            request.ExpectedReadyTotalDay;
        compilation.SupportReservationCommitVerified =
            commit.SupportReservationCommitVerified;
        return compilation;
    }

    private static void ValidateArtifacts(
        AcquisitionRouteSupportingTransitionRequest request,
        AcquisitionRouteSupportingTransitionCommitReceipt commit,
        AcquisitionRouteTargetDateUnlock requirement,
        SnapshotEnvelope snapshot,
        StrategyCommitmentLedger ledger,
        string rankingSha256,
        string requestSha256,
        string commitSha256,
        ICollection<string> reasons)
    {
        if (!request.SupportRequestReady ||
            request.AtomicCommitRequest is null ||
            request.ExpectedReadyTotalDay is null ||
            request.SupportTransitionKind is not (
                "crop_planting" or "machine_input_load") ||
            request.FormalTrainingAuthorized ||
            request.BlockingReasons.Length != 0)
        {
            reasons.Add("support_request_not_compilation_ready");
        }
        if (!commit.SupportReservationCommitVerified ||
            commit.FormalTrainingAuthorized ||
            commit.BlockingReasons.Length != 0 ||
            commit.SupportRequestId != request.SupportRequestId ||
            commit.RouteOccurrenceId != request.RouteOccurrenceId ||
            commit.SelectedCandidateId != request.SelectedCandidateId ||
            commit.SourceStateHash != request.SourceStateHash ||
            commit.SupportRequestSha256 != requestSha256)
        {
            reasons.Add("support_commit_receipt_not_compilation_ready");
        }
        if (request.SourceStateHash != snapshot.StateHash ||
            request.RouteOccurrenceId != requirement.RouteOccurrenceId ||
            request.RankingSha256 != rankingSha256 ||
            commit.CommittedLedgerRevision != ledger.Revision ||
            !IsSha256(requestSha256) ||
            !IsSha256(commitSha256))
        {
            reasons.Add("support_compilation_source_identity_mismatch");
        }
    }

    private static bool CandidateMatchesRequest(
        AcquisitionRouteDispatchCandidateMatch match,
        AcquisitionRouteSupportingTransitionRequest request)
    {
        var candidate = match.Candidate;
        if (candidate.LocationId != request.TargetLocationId)
            return false;
        if (candidate.TileX != request.TargetTileX ||
            candidate.TileY != request.TargetTileY)
        {
            return false;
        }
        if (request.SupportTransitionKind == "crop_planting")
        {
            return candidate.ItemId == request.SeedId &&
                candidate.SlotIndex == request.SeedSlotIndex &&
                candidate.QualifiedItemId == request.InputQualifiedItemId &&
                candidate.SlotIndex == request.InputSlotIndex &&
                request.InputRequiredQuantity == 1;
        }
        return request.SupportTransitionKind == "machine_input_load" &&
            candidate.QualifiedItemId == request.InputQualifiedItemId &&
            candidate.SlotIndex == request.InputSlotIndex &&
            CurrentTeacherFrontierSupport.TryReadUniqueParameter(
                candidate,
                "machine_qualified_item_id",
                out var machineQualifiedItemId) &&
            machineQualifiedItemId == request.MachineQualifiedItemId &&
            CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "machine_input_required_count",
                out var requiredQuantity) &&
            requiredQuantity == request.InputRequiredQuantity &&
            CurrentTeacherFrontierSupport.TryReadUniqueIntParameter(
                candidate,
                "predicted_processing_minutes",
                out var processingMinutes) &&
            processingMinutes == request.PredictedProcessingMinutes;
    }

    private static AcquisitionRouteDispatchCompilation Blocked(
        AcquisitionRouteSupportingTransitionRequest request,
        string rankingSha256,
        string requestSha256,
        string commitSha256,
        IEnumerable<string> reasons) => new()
        {
            Status = "blocked",
            GoalId = request.GoalId,
            RouteOccurrenceId = request.RouteOccurrenceId,
            RouteKind = request.RouteKind,
            SourceId = request.SourceId,
            QualifiedItemId = request.QualifiedItemId,
            SourceStateHash = request.SourceStateHash,
            RankingSha256 = rankingSha256,
            SourceCandidateId = request.SelectedCandidateId,
            SelectedRouteOptionRole = "supporting_transition",
            TerminalReceiptEligible = false,
            FreshReplanRequiredAfterSuccess = true,
            SupportRequestSha256 = requestSha256,
            SupportCommitReceiptSha256 = commitSha256,
            SupportDeadlineTotalDay = request.SupportDeadlineTotalDay,
            SupportExpectedReadyTotalDay = request.ExpectedReadyTotalDay,
            SupportReservationCommitVerified = false,
            DispatchReady = false,
            FormalTrainingAuthorized = false,
            BlockingReasons = reasons.Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal)
                .ToArray()
        };

    private static SmallModelActionParameter Parameter(
        string name,
        string value) => new()
        {
            Name = name,
            Value = value
        };
}
