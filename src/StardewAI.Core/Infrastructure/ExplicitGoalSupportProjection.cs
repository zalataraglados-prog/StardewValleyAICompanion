using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using StardewAI.Contracts.Execution;

namespace StardewAI.Core.Infrastructure;

internal sealed record ExplicitGoalSupportDemand(
    string Status,
    string SupportKind,
    string ParentGoalId,
    string EvidenceStatus,
    int GrossBenefit,
    int OpportunityCost,
    int NetBenefit,
    double Score,
    string Reason);

internal static class ExplicitGoalSupportProjection
{
    private const string EarnMoneyGoal =
        "goal.economy.earn_money";

    public const string EconomicSupportStatus =
        "supported_bounded_positive_net_benefit";

    public const string TaskSupportStatus =
        "supported_exact_active_collection_task";

    public static ExplicitGoalSupportDemand Read(
        string candidateKind,
        string expectedEffect,
        string goalId)
    {
        goalId = string.IsNullOrWhiteSpace(goalId)
            ? "daily.closed_loop"
            : goalId;
        if (!string.Equals(
                candidateKind,
                "craft_machine_item",
                StringComparison.Ordinal) &&
            !string.Equals(
                candidateKind,
                "place_machine_item",
                StringComparison.Ordinal))
        {
            return NotApplicable(goalId);
        }

        var demandClass = Parse(
            expectedEffect,
            "machine_demand_class=");
        if (string.Equals(
                demandClass,
                "priority_task_requirement",
                StringComparison.Ordinal))
        {
            return ReadTaskSupport(expectedEffect, goalId);
        }

        if (!string.Equals(goalId, EarnMoneyGoal, StringComparison.Ordinal))
        {
            return Neutral(
                goalId,
                "effective_goal_has_no_vetted_machine_support_rule");
        }

        var buildWindowOpen = Parse(
            expectedEffect,
            "machine_build_window_open=");
        var requiredMachines = ParseInt(
            expectedEffect,
            "required_additional_machine_count=");
        if (!string.Equals(
                demandClass,
                "production_capacity_requirement",
                StringComparison.Ordinal) ||
            !string.Equals(
                buildWindowOpen,
                "true",
                StringComparison.Ordinal) ||
            requiredMachines is null or <= 0)
        {
            return Neutral(
                goalId,
                "machine_has_no_current_open_capacity_deficit");
        }

        var economicStatus = Parse(
            expectedEffect,
            "machine_economic_value_status=");
        var materialStatus = Parse(
            expectedEffect,
            "machine_craft_material_opportunity_cost_status=");
        var grossBenefit = ParseInt(
            expectedEffect,
            "machine_capacity_deficit_processing_net_value=");
        var opportunityCost = ParseInt(
            expectedEffect,
            "machine_craft_material_opportunity_cost=");
        if (!string.Equals(
                economicStatus,
                "bounded_current_backlog_positive",
                StringComparison.Ordinal) ||
            !string.Equals(
                materialStatus,
                "complete_exact_native_consumption_sale_value",
                StringComparison.Ordinal) ||
            !grossBenefit.HasValue ||
            grossBenefit <= 0 ||
            !opportunityCost.HasValue ||
            opportunityCost < 0)
        {
            return new ExplicitGoalSupportDemand(
                "blocked_incomplete_economic_evidence",
                "machine_capacity_current_backlog",
                goalId,
                economicStatus + "|" + materialStatus,
                grossBenefit ?? 0,
                opportunityCost ?? 0,
                0,
                0,
                "machine_support_requires_exact_current_value_and_material_cost");
        }

        var netBenefit = grossBenefit.Value -
            opportunityCost.Value;
        if (netBenefit <= 0)
        {
            return new ExplicitGoalSupportDemand(
                "neutral_nonpositive_bounded_net_benefit",
                "machine_capacity_current_backlog",
                goalId,
                economicStatus + "|" + materialStatus,
                grossBenefit.Value,
                opportunityCost.Value,
                netBenefit,
                0,
                "bounded_processing_gain_does_not_repay_material_opportunity_cost");
        }

        var score = Math.Round(
            Math.Clamp(
                netBenefit * 0.0001,
                0.01,
                0.12),
            4);
        return new ExplicitGoalSupportDemand(
            EconomicSupportStatus,
            "machine_capacity_current_backlog",
            goalId,
            economicStatus + "|" + materialStatus,
            grossBenefit.Value,
            opportunityCost.Value,
            netBenefit,
            score,
            "current_capacity_deficit_processing_gain_exceeds_material_opportunity_cost");
    }

    public static bool IsSupported(ExplicitGoalSupportDemand? demand) =>
        demand is not null &&
        (string.Equals(
             demand.Status,
             EconomicSupportStatus,
             StringComparison.Ordinal) ||
         string.Equals(
             demand.Status,
             TaskSupportStatus,
             StringComparison.Ordinal));

    public static bool HasExactCollectionTaskSources(
        string expectedEffect) =>
        TryReadExactCollectionTaskSources(
            Parse(
                expectedEffect,
                "priority_task_sources_json="),
            out _);

    private static ExplicitGoalSupportDemand ReadTaskSupport(
        string expectedEffect,
        string goalId)
    {
        var sourcesJson = Parse(
            expectedEffect,
            "priority_task_sources_json=");
        var capacityActionRequired = string.Equals(
            Parse(
                expectedEffect,
                "machine_task_capacity_action_required="),
            "true",
            StringComparison.Ordinal) ||
            ParseInt(
                expectedEffect,
                "required_additional_machine_count=") is > 0;
        var materialPriority = ParseInt(
            expectedEffect,
            "material_reservation_request_priority=");
        var materialClass = Parse(
            expectedEffect,
            "material_reservation_request_class=");
        if (!capacityActionRequired ||
            materialPriority != 300 ||
            !string.Equals(
                materialClass,
                "active_collection_task",
                StringComparison.Ordinal) ||
            !TryReadExactCollectionTaskSources(
                sourcesJson,
                out var sources))
        {
            return new ExplicitGoalSupportDemand(
                "blocked_inexact_or_stale_task_capacity_demand",
                "machine_capacity_active_collection_task",
                goalId,
                sourcesJson,
                0,
                0,
                0,
                0,
                "task_machine_support_requires_exact_live_collection_source_and_priority");
        }

        return new ExplicitGoalSupportDemand(
            TaskSupportStatus,
            "machine_capacity_active_collection_task",
            goalId,
            JsonSerializer.Serialize(sources),
            0,
            0,
            0,
            0.12,
            "exact_active_collection_task_requires_one_machine_capacity_action");
    }

    private static bool TryReadExactCollectionTaskSources(
        string json,
        out string[] sources)
    {
        sources = Array.Empty<string>();
        try
        {
            var candidates = JsonSerializer.Deserialize<string[]>(json) ??
                Array.Empty<string>();
            if (candidates.Length == 0 ||
                candidates.Any(source =>
                    string.IsNullOrWhiteSpace(source) ||
                    (!source.StartsWith(
                         "ordinary_quest:ResourceCollectionQuest:",
                         StringComparison.Ordinal) &&
                     !source.StartsWith(
                         "special_order:",
                         StringComparison.Ordinal))))
            {
                return false;
            }

            sources = candidates
                .Distinct(StringComparer.Ordinal)
                .OrderBy(source => source, StringComparer.Ordinal)
                .ToArray();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    public static string ExpectedEffectSuffix(
        ExplicitGoalSupportDemand demand)
    {
        return
            ";goal_support_status=" + demand.Status +
            ";goal_support_kind=" + demand.SupportKind +
            ";goal_support_parent_goal_id=" +
            demand.ParentGoalId +
            ";goal_support_evidence_status=" +
            demand.EvidenceStatus +
            ";goal_support_gross_benefit=" +
            demand.GrossBenefit +
            ";goal_support_opportunity_cost=" +
            demand.OpportunityCost +
            ";goal_support_net_benefit=" +
            demand.NetBenefit +
            ";goal_support_score=" +
            Format(demand.Score) +
            ";goal_support_reason=" +
            demand.Reason;
    }

    public static SmallModelActionParameter[] Parameters(
        ExplicitGoalSupportDemand demand)
    {
        return new[]
        {
            Parameter("goal_support_status", demand.Status),
            Parameter("goal_support_kind", demand.SupportKind),
            Parameter(
                "goal_support_parent_goal_id",
                demand.ParentGoalId),
            Parameter(
                "goal_support_evidence_status",
                demand.EvidenceStatus),
            Parameter(
                "goal_support_gross_benefit",
                demand.GrossBenefit.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter(
                "goal_support_opportunity_cost",
                demand.OpportunityCost.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter(
                "goal_support_net_benefit",
                demand.NetBenefit.ToString(
                    CultureInfo.InvariantCulture)),
            Parameter(
                "goal_support_score",
                Format(demand.Score)),
            Parameter("goal_support_reason", demand.Reason)
        };
    }

    private static ExplicitGoalSupportDemand NotApplicable(
        string goalId)
    {
        return new(
            "not_applicable",
            "none",
            goalId ?? string.Empty,
            "not_applicable",
            0,
            0,
            0,
            0,
            "candidate_kind_has_no_support_projection");
    }

    private static ExplicitGoalSupportDemand Neutral(
        string goalId,
        string reason)
    {
        return new(
            "neutral",
            "machine_capacity_current_backlog",
            goalId ?? string.Empty,
            "not_evaluated",
            0,
            0,
            0,
            0,
            reason);
    }

    private static string Parse(
        string source,
        string prefix)
    {
        var start = source.IndexOf(
            prefix,
            StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += prefix.Length;
        var end = source.IndexOf(';', start);
        return end < 0
            ? source.Substring(start)
            : source.Substring(start, end - start);
    }

    private static int? ParseInt(
        string source,
        string prefix)
    {
        return int.TryParse(
            Parse(source, prefix),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var value)
                ? value
                : null;
    }

    private static string Format(double value)
    {
        return value.ToString(
            "0.####",
            CultureInfo.InvariantCulture);
    }

    private static SmallModelActionParameter Parameter(
        string name,
        string value)
    {
        return new()
        {
            Name = name,
            Value = value
        };
    }
}
