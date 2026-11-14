using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Contracts.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.LiveTrainingLoop;

public sealed record CropTeacherDailyCompositionBinding(string Status, string SourceStateHash, string LedgerId,
    int LedgerRevision, CropTeacherRunStep OriginalNextStep, string[] RequiredCandidateIds,
    string[] BlockingReasons, DailyPlanCompileRequest? CompileRequest)
{
    public bool NativeOwnerInterleavingAuthorized => false;
    public bool FormalProductTrainingAuthorized => false;
}

public static partial class CropTeacherRunPlanner
{
    // Bind original obligations to the existing candidate surface; do not synthesize costs, actions or owner receipts.
    public static CropTeacherDailyCompositionBinding BindDailyComposition(
        MoneyRevenueCropNativeProductionRunPlan plan, MoneyRevenueCropNativeProductionRunPlanRequest request,
        SnapshotEnvelope snapshot, StrategyCommitmentLedger ledger, DailyPlanCompileRequest candidateRequest,
        IReadOnlySet<string>? preparedCommitmentIds = null, IReadOnlySet<string>? completedHarvestTileKeys = null,
        IReadOnlySet<string>? completedDayBoundaryKeys = null)
    {
        if (ledger.SourceStateHash != snapshot.StateHash || ledger.SaveId != snapshot.SaveId.Value || ledger.PlayerId != snapshot.PlayerId.Value)
            throw new InvalidDataException("Daily composition requires the exact current same-save/actor strategy ledger.");
        if (candidateRequest.StateHash != snapshot.StateHash)
            throw new InvalidDataException("Daily composition candidates belong to a different snapshot.");
        var candidates = candidateRequest.RankedEventCandidates;
        completedHarvestTileKeys ??= new HashSet<string>(StringComparer.Ordinal);
        var next = Evaluate(plan, request, snapshot, preparedCommitmentIds, completedHarvestTileKeys, completedDayBoundaryKeys, ledger);
        var required = candidateRequest.RequiredCandidateIds?.ToList() ?? new List<string>();
        if (required.Distinct(StringComparer.Ordinal).Count() != required.Count)
            throw new InvalidDataException("Daily composition base required bindings are duplicated.");
        var cropRequired = new HashSet<string>(StringComparer.Ordinal);
        CropTeacherDailyCompositionBinding Result(string status, params string[] reasons) => new(status, snapshot.StateHash,
            ledger.LedgerId, ledger.Revision, next, required.ToArray(), reasons,
            status == "ready" ? new DailyPlanCompileRequest
            {
                StateHash = snapshot.StateHash, UseJointCalendar = true, RequiredCandidateIds = required.ToArray(),
                RankedEventCandidates = candidates, MaxCandidates = candidateRequest.MaxCandidates,
                GoalId = candidateRequest.GoalId, ExecutionMode = candidateRequest.ExecutionMode,
                // A composed input is not authority to dispatch a mixed queue through the crop-only owner.
                CompileActionQueue = false
            } : null);
        if (next.Stage == CropTeacherRunStageKinds.ReplanRequired) return Result("blocked", next.Reason);
        if (next.Stage is CropTeacherRunStageKinds.Complete or CropTeacherRunStageKinds.RouteHome or CropTeacherRunStageKinds.NativeSleep)
            return Result("ready");
        if (next.Stage != CropTeacherRunStageKinds.ExecuteCropBatch)
            return Result("original_prefix_required", next.Reason);
        var day = plan.Days.Single(row => row.TotalDay == next.TotalDay);
        foreach (var batch in EvaluatePendingBatches(plan, day, ReadCropRows(snapshot), next.TotalDay, completedHarvestTileKeys))
        {
            // A later watering filter is not valid until its pending planting prefix has run.
            if (batch.Result.Batch.OptionId == PlantOptionId) return Result("original_prefix_required", "crop_teacher_pending_planting_prefix");
            if (batch.Result.ReplanReason.Length > 0) return Result("blocked", batch.Result.ReplanReason);
            var active = ledger.CropPlantingCommitments.Where(row => row.CommitmentId == batch.Result.Batch.CommitmentId &&
                row.Status == StrategyCommitmentStatuses.Active).ToArray();
            if (active.Length != 1) return Result("blocked", "crop_teacher_required_commitment_not_active");
            var kind = batch.Result.Batch.OptionId == WaterOptionId ? "water_crop_tile" : "harvest_crop_tile";
            foreach (var tile in batch.Result.Batch.Tiles)
            {
                var matches = candidates.Where(row => row.Kind == kind && row.LocationId == batch.Result.Batch.LocationId &&
                    row.TileX == tile.TileX && row.TileY == tile.TileY && row.TimelineStatus != "blocked").ToArray();
                if (matches.Length != 1 || string.IsNullOrWhiteSpace(matches[0].CandidateId) ||
                    candidates.Count(row => row.CandidateId == matches[0].CandidateId) != 1 || !cropRequired.Add(matches[0].CandidateId))
                    return Result("blocked", "crop_teacher_required_candidate_missing_or_ambiguous");
                if (!required.Contains(matches[0].CandidateId, StringComparer.Ordinal)) required.Add(matches[0].CandidateId);
            }
        }
        return Result("ready");
    }
}
