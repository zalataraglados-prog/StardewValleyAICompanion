using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;
using StardewAI.Core.Execution;
using StardewAI.Core.Infrastructure;
using StardewAI.Core.OptionRegistry;

namespace StardewAI.GoalConditionedBootstrap;

public sealed record CropTeacherSeedTransferExecutionPlan(string CandidateId, string LocationId,
    int StandTileX, int StandTileY, int EstimatedTicks, int RequiredMinutes, int AvailableMinutes);

public sealed partial class CropTeacherSeedTransferSupport
{
    public CropTeacherSeedTransferExecutionPlan PlanExecution()
    {
        ValidateInputsUnchanged();
        var available = new CandidateOptionAvailabilityEvaluator().Evaluate(source.Before, new[]
        {
            new OptionAvailabilityCandidate { OptionId = "inventory.transfer_item",
                Parameters = MaterialTransferIntentBinder.ToParameters(preparation.MaterialTransferPreparation!.Intent) }
        }, includeExecutorCalibrationOptions: true);
        var candidates = available.Options.Where(row => row.Available).SelectMany(row => row.EventCandidates).Where(row =>
            row.Kind == "transfer_inventory_item" && row.Available == true && row.AllowedNow == true).ToArray();
        Require(candidates.Length == 1, "Owned seed transfer source route is not uniquely executable: " + string.Join(",",
            available.Options.SelectMany(row => row.BlockingReasons.Concat(row.EventCandidates.SelectMany(candidate => candidate.BlockReasons))
                .Concat(row.PreconditionResults.Where(result => result.Status != "passed").Select(result => result.StateFactor + ":" + result.Message)))));
        var candidate = candidates[0];
        int Stand(string name)
        {
            var values = candidate.Parameters.Where(row => row.Name == name).ToArray();
            Require(values.Length == 1 && int.TryParse(values[0].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _),
                "Owned seed transfer route lacks its original adjacent stand.");
            return int.Parse(values[0].Value, CultureInfo.InvariantCulture);
        }
        var standX = Stand("stand_tile_x");
        var standY = Stand("stand_tile_y");
        // The generic probe supplies geometry; only the original owned primitive authorizes claims.
        var queue = new ActionQueueCompiler().Compile(new SmallModelActionEnvelope
        {
            ModelOutputId = preparation.PortfolioId, SourceModel = "crop_seed_transfer_preflight",
            StateHash = source.Before.StateHash, GoalId = "earn_money",
            ExecutionMode = ExecutionTargetProfiles.TrainingSingleplayer,
            Actor = ExecutionTargetProfiles.CreateActor(ExecutionTargetProfiles.TrainingSingleplayer),
            Actions = new[] { new CropTeacherSeedTransferMarker(this, preparationPreview).BuildAction(standX, standY) }
        }, source.Before, preparationPreview);
        Require(queue.Status == "pending" && queue.CompilerDiagnostics.Length == 0 && queue.Items is [var item] &&
                item.BlockingReasons.Length == 0 && item.MissingStateFactors.Length == 0 &&
                new ActionQueueDispatchReadinessService().Evaluate(queue, item, preparationPreview, source.Before.StateHash).Ready,
            "Owned seed transfer original prepared primitive preflight is blocked.");
        var minutes = SnapshotGameClockBudgetReader.AvailableMinutes(source.Before, GameClockBudgetPolicy.AutonomousRecoveryStartTime);
        Require(minutes.HasValue && candidate.EstimatedTicks > 0 && FieldEnvelopeValidator.IsReadableStatus(source.Before.InGameTime.Status) &&
                source.Before.InGameTime.Value == SnapshotGameClockBudgetReader.CurrentTime(source.Before),
            "Owned seed transfer source clock/budget is unavailable.");
        var required = GameClockBudgetPolicy.TicksToGameMinutes(candidate.EstimatedTicks);
        Require(required <= minutes, "Owned seed transfer cannot fit before the original recovery window.");
        return new(candidate.CandidateId, candidate.LocationId, standX, standY,
            candidate.EstimatedTicks, required, minutes!.Value);
    }
}
