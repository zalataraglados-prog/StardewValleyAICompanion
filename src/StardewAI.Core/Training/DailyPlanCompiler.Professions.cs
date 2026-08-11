using System.Collections.Generic;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training
{
    public sealed partial class DailyPlanCompiler
    {
        private static IEnumerable<SmallModelPlanStep> ProfessionChoiceSteps(
            PolicyEventCandidatePrediction candidate)
        {
            return new[]
            {
                new SmallModelPlanStep
                {
                    StepId = StepId(candidate, "choose_profession", 0),
                    Kind = "close_menu",
                    EstimatedMinutes = 1,
                    Preconditions = new[]
                    {
                        "candidate_id:" + candidate.CandidateId,
                        "menus.active_menu.type=LevelUpMenu",
                        "offered_profession_identity_matches=true"
                    },
                    ExpectedEffects = new[]
                    {
                        candidate.ExpectedEffect,
                        "menus.active_menu.is_open=false",
                        "fresh_snapshot_replan_required=true"
                    },
                    SafetyConstraints = new[]
                    {
                        "compile_only_exact_transparent_profession_choice",
                        "reuse_executor.close_menu_level_up_path",
                        "never_create_second_profession_executor"
                    },
                    FailurePolicy = new[] { "stop_refresh_snapshot_and_replan" },
                    Parameters = ContinuationParameters(candidate)
                }
            };
        }
    }
}
