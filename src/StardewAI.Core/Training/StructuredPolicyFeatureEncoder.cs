using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;

namespace StardewAI.Core.Training;

internal static class StructuredPolicyFeatureEncoder
{
    public static string[] DiscoverFeatureNames(
        IReadOnlyList<PolicyDecisionTrajectoryEnvelope> rows) =>
        rows.SelectMany(row => row.Candidates.SelectMany(candidate =>
                Raw(row.StateFeatures, CandidateView.From(candidate)).Keys))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    public static StructuredPolicyLinearModel FitModelShape(
        IReadOnlyList<PolicyDecisionTrajectoryEnvelope> rows,
        string[] featureNames)
    {
        var sums = new double[featureNames.Length];
        var squareSums = new double[featureNames.Length];
        var count = 0;
        foreach (var row in rows)
        {
            foreach (var candidate in row.Candidates)
            {
                var raw = Raw(row.StateFeatures, CandidateView.From(candidate));
                for (var index = 0; index < featureNames.Length; index++)
                {
                    var value = raw.TryGetValue(featureNames[index], out var observed) ? observed : 0;
                    sums[index] += value;
                    squareSums[index] += value * value;
                }
                count++;
            }
        }
        if (count == 0)
            throw new InvalidOperationException("Structured policy feature fitting requires candidates.");

        var means = new double[featureNames.Length];
        var scales = new double[featureNames.Length];
        for (var index = 0; index < featureNames.Length; index++)
        {
            means[index] = sums[index] / count;
            var variance = Math.Max(0, squareSums[index] / count - means[index] * means[index]);
            var scale = Math.Sqrt(variance);
            scales[index] = scale < 1e-9 ? 1 : scale;
        }
        return new StructuredPolicyLinearModel
        {
            FeatureNames = featureNames,
            FeatureMeans = means,
            FeatureScales = scales,
            Weights = new double[featureNames.Length]
        };
    }

    public static double[] Encode(
        FeatureVector state,
        PolicyTrajectoryCandidate candidate,
        StructuredPolicyLinearModel model) =>
        Encode(state, CandidateView.From(candidate), model);

    public static double[] Encode(
        FeatureVector state,
        PolicyEventCandidatePrediction candidate,
        StructuredPolicyLinearModel model) =>
        Encode(state, CandidateView.From(candidate), model);

    private static double[] Encode(
        FeatureVector state,
        CandidateView candidate,
        StructuredPolicyLinearModel model)
    {
        var raw = Raw(state, candidate);
        var result = new double[model.FeatureNames.Length];
        for (var index = 0; index < result.Length; index++)
        {
            var value = raw.TryGetValue(model.FeatureNames[index], out var observed) ? observed : 0;
            result[index] = (value - model.FeatureMeans[index]) / model.FeatureScales[index];
        }
        return result;
    }

    private static Dictionary<string, double> Raw(FeatureVector state, CandidateView candidate)
    {
        var values = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["candidate.numeric:prior_score"] = candidate.Score,
            ["candidate.numeric:expected_reward"] = candidate.ExpectedReward,
            ["candidate.numeric:estimated_ticks"] = candidate.EstimatedTicks,
            ["candidate.numeric:energy_cost"] = candidate.EnergyCost,
            ["candidate.numeric:exclusion_count"] = candidate.ExclusionReasons.Length,
            ["candidate.numeric:parameter_count"] = candidate.Parameters.Length,
            ["candidate.boolean:available"] = candidate.Available ? 1 : 0,
            ["candidate.boolean:admitted"] = candidate.Admitted ? 1 : 0,
            ["candidate.categorical:option_id=" + Safe(candidate.OptionId)] = 1,
            ["candidate.categorical:kind=" + Safe(candidate.Kind)] = 1
        };
        var option = Safe(candidate.OptionId);
        foreach (var feature in state.Numeric ?? Array.Empty<NumericFeature>())
        {
            values["state.numeric:" + feature.Name] = feature.Value;
            values["interaction.option=" + option + "|numeric:" + feature.Name] = feature.Value;
        }
        foreach (var feature in state.Categorical ?? Array.Empty<CategoricalFeature>())
        {
            var key = "categorical:" + feature.Name + "=" + Safe(feature.Value);
            values["state." + key] = 1;
            values["interaction.option=" + option + "|" + key] = 1;
        }
        foreach (var feature in state.Boolean ?? Array.Empty<BooleanFeature>())
        {
            var numeric = feature.Value ? 1d : 0d;
            values["state.boolean:" + feature.Name] = numeric;
            values["interaction.option=" + option + "|boolean:" + feature.Name] = numeric;
        }
        foreach (var parameter in candidate.Parameters)
        {
            if (!string.IsNullOrWhiteSpace(parameter.Name))
                values["candidate.parameter_name=" + Safe(parameter.Name)] = 1;
        }
        foreach (var reason in candidate.ExclusionReasons)
        {
            if (!string.IsNullOrWhiteSpace(reason))
                values["candidate.exclusion_reason=" + Safe(reason)] = 1;
        }
        AddSourceCandidateFeatures(values, candidate.Source);
        return values;
    }

    private static void AddSourceCandidateFeatures(
        IDictionary<string, double> values,
        PolicyEventCandidatePrediction source)
    {
        Number(values, "rank", source.Rank);
        Number(values, "quantity", source.Quantity);
        Number(values, "unit_price", source.UnitPrice);
        Number(values, "total_value", source.TotalValue);
        Number(values, "slot_index", source.SlotIndex);
        Number(values, "tile_x", source.TileX);
        Number(values, "tile_y", source.TileY);
        Number(values, "next_open_time", source.NextOpenTime);
        Number(values, "effective_open_time", source.EffectiveOpenTime);
        Number(values, "closes_at", source.ClosesAt);
        Number(values, "wait_cost", source.WaitCost);
        Number(values, "scheduled_start_time", source.ScheduledStartTime);
        Number(values, "scheduled_wait_cost", source.ScheduledWaitCost);
        Number(values, "full_shipment_current_shipped_count", source.FullShipmentCurrentShippedCount);
        Flag(values, "can_ship", source.CanShip);
        Flag(values, "can_shop_sell", source.CanShopSell);
        NullableFlag(values, "allowed_now", source.AllowedNow);
        NullableFlag(values, "allowed_today", source.AllowedToday);
        NullableFlag(values, "full_shipment_known", source.FullShipmentKnown);
        NullableFlag(values, "full_shipment_eligible", source.FullShipmentEligible);
        NullableFlag(values, "full_shipment_already_shipped", source.FullShipmentAlreadyShipped);
        NullableFlag(values, "full_shipment_contributes", source.FullShipmentContributes);
        Category(values, "item_id", source.ItemId);
        Category(values, "qualified_item_id", source.QualifiedItemId);
        Category(values, "display_name", source.DisplayName);
        Category(values, "shop_id", source.ShopId);
        Category(values, "location_id", source.LocationId);
        Category(values, "availability_class", source.AvailabilityClass);
        Category(values, "timeline_status", source.TimelineStatus);
        Reasons(values, "gate_reason", source.GateReasons);
        Reasons(values, "timeline_reason", source.TimelineReasons);
        Reasons(values, "block_reason", source.BlockReasons);
        Parameters(values, source.Parameters);
        ExpectedEffect(values, source.ExpectedEffect);
    }

    private static void Number(IDictionary<string, double> values, string name, double? value)
    {
        if (value.HasValue)
            values["candidate.source.numeric:" + name] = value.Value;
    }

    private static void Flag(IDictionary<string, double> values, string name, bool value) =>
        values["candidate.source.boolean:" + name] = value ? 1 : 0;

    private static void NullableFlag(IDictionary<string, double> values, string name, bool? value) =>
        values["candidate.source.categorical:" + name + "=" +
            (value.HasValue ? value.Value ? "true" : "false" : "unknown")] = 1;

    private static void Category(IDictionary<string, double> values, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            values["candidate.source.categorical:" + name + "=" + Safe(value)] = 1;
    }

    private static void Reasons(IDictionary<string, double> values, string name, IEnumerable<string>? reasons)
    {
        foreach (var reason in (reasons ?? Array.Empty<string>()).Where(value => !string.IsNullOrWhiteSpace(value)))
            values["candidate.source." + name + "=" + Safe(reason)] = 1;
    }

    private static void Parameters(
        IDictionary<string, double> values,
        IEnumerable<SmallModelActionParameter>? parameters)
    {
        foreach (var parameter in parameters ?? Array.Empty<SmallModelActionParameter>())
        {
            if (string.IsNullOrWhiteSpace(parameter.Name))
                continue;
            if (double.TryParse(parameter.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) &&
                !double.IsNaN(numeric) && !double.IsInfinity(numeric))
                values["candidate.parameter.numeric:" + Safe(parameter.Name)] = numeric;
            else
                values["candidate.parameter.categorical:" + Safe(parameter.Name) + "=" + Safe(parameter.Value)] = 1;
        }
    }

    private static void ExpectedEffect(IDictionary<string, double> values, string source)
    {
        if (string.IsNullOrWhiteSpace(source))
            return;
        foreach (var segment in source.Split(';').Where(value => !string.IsNullOrWhiteSpace(value)))
        {
            var separator = segment.IndexOf('=');
            var name = separator > 0 ? segment.Substring(0, separator).Trim() : "flag";
            var raw = separator > 0 ? segment.Substring(separator + 1).Trim() : segment.Trim();
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric) &&
                !double.IsNaN(numeric) && !double.IsInfinity(numeric))
                values["candidate.effect.numeric:" + Safe(name)] = numeric;
            else
                values["candidate.effect.categorical:" + Safe(name) + "=" + Safe(raw)] = 1;
        }
    }

    private static string Safe(string? value) => string.IsNullOrWhiteSpace(value)
        ? "unknown"
        : value.Trim().Replace("\r", "\\r").Replace("\n", "\\n");

    private sealed class CandidateView
    {
        public string OptionId { get; private set; } = string.Empty;
        public string Kind { get; private set; } = string.Empty;
        public double Score { get; private set; }
        public double ExpectedReward { get; private set; }
        public int EstimatedTicks { get; private set; }
        public int EnergyCost { get; private set; }
        public bool Available { get; private set; }
        public bool Admitted { get; private set; }
        public string[] ExclusionReasons { get; private set; } = Array.Empty<string>();
        public SmallModelActionParameter[] Parameters { get; private set; } = Array.Empty<SmallModelActionParameter>();
        public PolicyEventCandidatePrediction Source { get; private set; } = new();

        public static CandidateView From(PolicyTrajectoryCandidate candidate) => new()
        {
            OptionId = candidate.OptionId,
            Kind = candidate.Kind,
            Score = candidate.Score,
            ExpectedReward = candidate.ExpectedReward,
            EstimatedTicks = candidate.EstimatedTicks,
            EnergyCost = candidate.EnergyCost,
            Available = candidate.Available,
            Admitted = candidate.AdmittedForPolicy,
            ExclusionReasons = candidate.ExclusionReasons ?? Array.Empty<string>(),
            Parameters = candidate.Parameters ?? Array.Empty<SmallModelActionParameter>(),
            Source = candidate.SourceCandidate
        };

        public static CandidateView From(PolicyEventCandidatePrediction candidate) => new()
        {
            OptionId = candidate.OptionId,
            Kind = candidate.Kind,
            Score = candidate.Score,
            ExpectedReward = candidate.ExpectedReward,
            EstimatedTicks = candidate.EstimatedTicks,
            EnergyCost = candidate.EnergyCost,
            Available = candidate.Available,
            Admitted = true,
            ExclusionReasons = (candidate.GateReasons ?? Array.Empty<string>())
                .Concat(candidate.TimelineReasons ?? Array.Empty<string>())
                .Concat(candidate.BlockReasons ?? Array.Empty<string>())
                .Distinct(StringComparer.Ordinal)
                .ToArray(),
            Parameters = candidate.Parameters ?? Array.Empty<SmallModelActionParameter>(),
            Source = candidate
        };
    }
}
