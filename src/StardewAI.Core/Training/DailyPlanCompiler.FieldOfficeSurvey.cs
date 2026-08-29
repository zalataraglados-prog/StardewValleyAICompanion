using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

public sealed partial class DailyPlanCompiler
{
    private static IEnumerable<SmallModelPlanStep> FieldOfficeSurveySteps(PolicyEventCandidatePrediction candidate) =>
        new[]
        {
            new SmallModelPlanStep
            {
                StepId = StepId(candidate, "answer_field_office_survey", 0),
                Kind = "answer_field_office_survey",
                TargetLocation = candidate.LocationId,
                TargetTileX = candidate.TileX,
                TargetTileY = candidate.TileY,
                EstimatedMinutes = TicksToMinutes(candidate.EstimatedTicks),
                Preconditions = new[] { "candidate_id:" + candidate.CandidateId, "field_office_survey_projection_still_matches=true" },
                ExpectedEffects = new[] { candidate.ExpectedEffect },
                SafetyConstraints = new[]
                {
                    "native_FieldOfficeSurvey_action_only",
                    "native_Survey_Yes_response_only",
                    "locked_exact_numeric_Correct_response_only",
                    "no_direct_plant_failed_lock_nut_debris_mail_or_finale_mutation"
                },
                FailurePolicy = new[] { "close_survey_dialogue_refresh_snapshot_and_replan" },
                Parameters = candidate.Parameters
            }
        };
}
