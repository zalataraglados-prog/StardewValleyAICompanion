using System;
using System.Text.Json.Serialization;

namespace StardewAI.Contracts.Goals
{
    public static class GrandpaEvaluationGoalDefinition
    {
        public const string SchemaVersion = "grandpa_evaluation_goal.v2";
        public const string GoalId = "goal.grandpa_max_score_year3";
        public const string StrategicGoal = "grandpa_max_score_year3";
        public const int FourCandleScore = 12;
        public const int MaximumRuleScore = 21;
    }

    public sealed class GrandpaEvaluationGoalReport
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = GrandpaEvaluationGoalDefinition.SchemaVersion;

        [JsonPropertyName("goal_id")]
        public string GoalId { get; set; } = GrandpaEvaluationGoalDefinition.GoalId;

        [JsonPropertyName("target_score")]
        public int TargetScore { get; set; } = GrandpaEvaluationGoalDefinition.MaximumRuleScore;

        [JsonPropertyName("max_rule_score")]
        public int MaxRuleScore { get; set; } = GrandpaEvaluationGoalDefinition.MaximumRuleScore;

        [JsonPropertyName("four_candle_score_threshold")]
        public int FourCandleScoreThreshold { get; set; } = GrandpaEvaluationGoalDefinition.FourCandleScore;

        [JsonPropertyName("current_score")]
        public int CurrentScore { get; set; }

        [JsonPropertyName("current_candles")]
        public int CurrentCandles { get; set; }

        [JsonPropertyName("four_candle_milestone_met")]
        public bool FourCandleMilestoneMet { get; set; }

        [JsonPropertyName("points_needed")]
        public int PointsNeeded { get; set; }

        [JsonPropertyName("target_met")]
        public bool TargetMet { get; set; }

        [JsonPropertyName("evaluation_trigger")]
        public string EvaluationTrigger { get; set; } = "grandpa year-3 evaluation";

        [JsonPropertyName("required_fact_paths")]
        public string[] RequiredFactPaths { get; set; } = Array.Empty<string>();

        [JsonPropertyName("missing_fact_paths")]
        public string[] MissingFactPaths { get; set; } = Array.Empty<string>();

        [JsonPropertyName("factors")]
        public GrandpaEvaluationFactor[] Factors { get; set; } = Array.Empty<GrandpaEvaluationFactor>();

        [JsonPropertyName("evaluation_context")]
        public GrandpaEvaluationContext EvaluationContext { get; set; } = new();

        [JsonPropertyName("audit")]
        public GrandpaEvaluationAudit Audit { get; set; } = new();
    }

    public sealed class GrandpaEvaluationFactor
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("label")]
        public string Label { get; set; } = string.Empty;

        [JsonPropertyName("fact_path")]
        public string FactPath { get; set; } = string.Empty;

        [JsonPropertyName("points")]
        public int Points { get; set; }

        [JsonPropertyName("max_points")]
        public int MaxPoints { get; set; }

        [JsonPropertyName("satisfied")]
        public bool Satisfied { get; set; }

        [JsonPropertyName("known")]
        public bool Known { get; set; }

        [JsonPropertyName("current_value")]
        public string CurrentValue { get; set; } = string.Empty;

        [JsonPropertyName("target_value")]
        public string TargetValue { get; set; } = string.Empty;

        [JsonPropertyName("source_rule")]
        public string SourceRule { get; set; } = string.Empty;
    }

    public sealed class GrandpaEvaluationAudit
    {
        [JsonPropertyName("rule_source")]
        public string RuleSource { get; set; } = "StardewValley.Utility.getGrandpaScore()";

        [JsonPropertyName("candles_source")]
        public string CandlesSource { get; set; } = "StardewValley.Utility.getGrandpaCandlesFromScore(int)";

        [JsonPropertyName("policy")]
        public string Policy { get; set; } = "The optimization target is all 21 rule points; four candles at 12 points are only a milestone. Scores use readable world_model.v1 facts and never guess missing facts.";
    }

    public sealed class GrandpaEvaluationContext
    {
        [JsonPropertyName("year")]
        public int? Year { get; set; }

        [JsonPropertyName("initial_evaluation_available")]
        public bool? InitialEvaluationAvailable { get; set; }

        [JsonPropertyName("recorded_grandpa_candles")]
        public int? RecordedGrandpaCandles { get; set; }

        [JsonPropertyName("reevaluation_available")]
        public bool? ReevaluationAvailable { get; set; }

        [JsonPropertyName("active_object_qualified_id")]
        public string ActiveObjectQualifiedId { get; set; } = string.Empty;

        [JsonPropertyName("holding_reevaluation_item")]
        public bool? HoldingReevaluationItem { get; set; }

        [JsonPropertyName("notes")]
        public string[] Notes { get; set; } = Array.Empty<string>();
    }
}
