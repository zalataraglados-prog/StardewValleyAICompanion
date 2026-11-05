using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public static class CurrentSkillLocalTimeAdmission
{
    public static bool TryCertify(
        PolicyEventCandidatePrediction candidate,
        SmallModelPlanEnvelope plan,
        ActionQueueEnvelope queue,
        string stateHash,
        string currentLocation,
        int availableMinutes)
    {
        if (string.IsNullOrWhiteSpace(stateHash) || string.IsNullOrWhiteSpace(currentLocation) ||
            candidate.LocationId != currentLocation || !candidate.Available ||
            candidate.AllowedNow == false || candidate.AllowedToday == false ||
            candidate.TimelineStatus != "ready_now" || candidate.GateReasons.Length > 0 ||
            candidate.BlockReasons.Length > 0 || candidate.NextOpenTime.HasValue ||
            candidate.EffectiveOpenTime.HasValue || candidate.ClosesAt.HasValue ||
            (candidate.WaitCost ?? 0) != 0 || (candidate.ScheduledWaitCost ?? 0) != 0 ||
            candidate.EstimatedTicks <= 0 || candidate.EnergyCost < 0 ||
            candidate.Parameters.Any(parameter => parameter.Name.StartsWith("continuation.", StringComparison.Ordinal) ||
                parameter.Name.StartsWith("skill_current_admission_", StringComparison.Ordinal)) ||
            plan.StateHash != stateHash || queue.StateHash != stateHash || queue.Status != "pending" ||
            plan.Steps.Length == 0 || plan.Steps.Any(step => step.Kind == "traverse_connector" ||
                (!string.IsNullOrWhiteSpace(step.TargetLocation) && step.TargetLocation != currentLocation)) ||
            queue.Items.Length == 0 || queue.Items.Any(item => item.Status != "pending" ||
                item.OptionId == "executor.traverse_connector" ||
                item.NormalizedCommand.StateHash != stateHash || item.NormalizedCommand.Steps.Length == 0) ||
            CurrentStageOneCollectionTeacherPreferenceBuilder.CompilationReasons(plan, queue).Length > 0 ||
            plan.CandidateAudit.Length != 1 || plan.CandidateAudit[0].CandidateId != candidate.CandidateId ||
            plan.CandidateAudit[0].Decision != "accepted" || plan.CandidateAudit[0].CandidateMinutes <= 0 ||
            plan.CandidateAudit[0].CandidateMinutes > availableMinutes)
            return false;

        // This proves only an immediate same-location action, not a future opening window.
        candidate.AllowedNow = true;
        candidate.AllowedToday = true;
        candidate.Parameters = candidate.Parameters.Concat(new[]
        {
            new SmallModelActionParameter { Name = "skill_current_admission_basis", Value = "original_same_location_plan_and_queue_pending" },
            new SmallModelActionParameter { Name = "skill_current_admission_state_hash", Value = stateHash },
            new SmallModelActionParameter { Name = "skill_current_admission_minutes",
                Value = plan.CandidateAudit[0].CandidateMinutes.ToString(System.Globalization.CultureInfo.InvariantCulture) }
        }).ToArray();
        return true;
    }
}
