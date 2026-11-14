using StardewAI.Contracts.Execution;

namespace StardewAI.LiveTrainingLoop;

public static partial class CropTeacherRunPlanner
{
    public static CropTeacherRunStep? SelectOriginalDailyBatch(CropTeacherDailyCompositionBinding binding, SmallModelPlanEnvelope plan,
        StardewAI.GoalConditionedBootstrap.MoneyRevenueCropNativeProductionRunPlan originalPlan)
    {
        var first = plan.CandidateAudit.FirstOrDefault(row => row.Decision == "accepted");
        if (first is null || binding.CompileRequest is null) return SelectOriginalDailyBatch(binding, plan);
        var candidate = binding.CompileRequest.RankedEventCandidates.SingleOrDefault(row => row.CandidateId == first.CandidateId);
        if (candidate is null || !binding.RequiredCandidateIds.Contains(candidate.CandidateId, StringComparer.Ordinal)) return null;
        var option = candidate.Kind == "water_crop_tile" ? WaterOptionId : candidate.Kind == "harvest_crop_tile" ? HarvestOptionId : string.Empty;
        var matches = originalPlan.Days.Where(day => day.TotalDay == binding.OriginalNextStep.TotalDay).SelectMany(day => day.ActionBatches)
            .Where(batch => batch.OptionId == option && batch.LocationId == candidate.LocationId &&
                batch.Tiles.Any(tile => tile.TileX == candidate.TileX && tile.TileY == candidate.TileY)).ToArray();
        if (matches.Length != 1) throw new InvalidDataException("Selected daily candidate has no unique original owned batch.");
        var source = matches[0];
        var required = binding.CompileRequest.RankedEventCandidates.Where(row => binding.RequiredCandidateIds.Contains(row.CandidateId, StringComparer.Ordinal) &&
            row.Kind == candidate.Kind && row.LocationId == source.LocationId).ToArray();
        var pending = source.Tiles.Where(tile => required.Any(row => row.TileX == tile.TileX && row.TileY == tile.TileY)).ToArray();
        return SelectOriginalDailyBatch(binding with { OriginalNextStep = binding.OriginalNextStep with
        {
            CommitmentId = source.CommitmentId, TargetLocationId = source.LocationId,
            ActionBatch = source with { Tiles = pending }, RemainingQuantity = pending.Length
        } }, plan);
    }

    public static CropTeacherRunStep? SelectOriginalDailyBatch(CropTeacherDailyCompositionBinding binding, SmallModelPlanEnvelope plan)
    {
        var request = binding.CompileRequest;
        var step = binding.OriginalNextStep;
        var batch = step.ActionBatch;
        if (binding.Status != "ready" || request is null || request.CompileActionQueue || plan.StateHash != binding.SourceStateHash ||
            plan.SourceModel != "StardewAI.Core.Training.DailyPlanCompiler" || plan.PlanType != "daily_candidate_plan" ||
            step.Stage != CropTeacherRunStageKinds.ExecuteCropBatch || batch?.OptionId is not (WaterOptionId or HarvestOptionId))
            throw new InvalidDataException("Daily dispatch requires the matching composed plan and an original maintenance batch.");

        var kind = batch.OptionId == WaterOptionId ? "water_crop_tile" : "harvest_crop_tile";
        if (plan.CandidateAudit.Select(row => row.CandidateId).Distinct(StringComparer.Ordinal).Count() != plan.CandidateAudit.Length)
            throw new InvalidDataException("Daily dispatch plan has ambiguous candidate audit rows.");
        var bindings = batch.Tiles.Select(tile =>
        {
            var matches = request.RankedEventCandidates.Where(row => binding.RequiredCandidateIds.Contains(row.CandidateId, StringComparer.Ordinal) &&
                row.Kind == kind && row.LocationId == batch.LocationId && row.TileX == tile.TileX && row.TileY == tile.TileY).ToArray();
            if (matches.Length != 1) throw new InvalidDataException("Original maintenance tile lost its required candidate binding.");
            return (Tile: tile, Candidate: matches[0]);
        }).ToArray();
        var ownedIds = bindings.Where(row => row.Candidate.TimelineStatus != "deferred")
            .Select(row => row.Candidate.CandidateId).ToHashSet(StringComparer.Ordinal);
        // Never jump over a different owner/batch or a bounded wait in the accepted plan.
        var accepted = plan.CandidateAudit.Where(row => row.Decision == "accepted")
            .TakeWhile(row => ownedIds.Contains(row.CandidateId)).Select(row => row.CandidateId).ToHashSet(StringComparer.Ordinal);
        var tiles = bindings.Where(row => accepted.Contains(row.Candidate.CandidateId)).Select(row => row.Tile).ToArray();
        // Original owners already accept an ordered subset of their own immutable batch.
        return tiles.Length == 0 ? null : step with { ActionBatch = batch with { Tiles = tiles }, RemainingQuantity = tiles.Length };
    }
}
