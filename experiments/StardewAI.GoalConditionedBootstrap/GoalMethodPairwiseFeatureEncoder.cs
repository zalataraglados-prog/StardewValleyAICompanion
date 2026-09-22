namespace StardewAI.GoalConditionedBootstrap;

internal static class GoalMethodPairwiseFeatureEncoder
{
    public static string[] DiscoverFeatureNames(
        IReadOnlyList<AcquisitionRoutePortfolioSupervisionCorpusRow> rows) =>
        rows.SelectMany(row => AdmittedCandidates(row).SelectMany(candidate =>
                Raw(row, candidate).Keys))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

    public static GoalMethodPairwiseLinearModel FitModelShape(
        IReadOnlyList<AcquisitionRoutePortfolioSupervisionCorpusRow> rows,
        string[] featureNames)
    {
        var sums = new double[featureNames.Length];
        var squareSums = new double[featureNames.Length];
        var count = 0;
        foreach (var row in rows)
        {
            foreach (var candidate in AdmittedCandidates(row))
            {
                var raw = Raw(row, candidate);
                for (var index = 0; index < featureNames.Length; index++)
                {
                    var value = raw.TryGetValue(
                        featureNames[index],
                        out var observed)
                        ? observed
                        : 0;
                    sums[index] += value;
                    squareSums[index] += value * value;
                }
                count++;
            }
        }
        if (count == 0)
            throw new InvalidDataException(
                "Goal-method feature fitting requires admitted candidates.");

        var means = new double[featureNames.Length];
        var scales = new double[featureNames.Length];
        for (var index = 0; index < featureNames.Length; index++)
        {
            means[index] = sums[index] / count;
            var variance = Math.Max(
                0,
                squareSums[index] / count - means[index] * means[index]);
            var scale = Math.Sqrt(variance);
            scales[index] = scale < 1e-9 ? 1 : scale;
        }
        return new GoalMethodPairwiseLinearModel
        {
            FeatureNames = featureNames,
            FeatureMeans = means,
            FeatureScales = scales,
            Weights = new double[featureNames.Length]
        };
    }

    public static int InitializeWeights(
        GoalMethodPairwiseLinearModel target,
        GoalMethodPairwiseLinearModel source)
    {
        var sourceIndexes = source.FeatureNames
            .Select((name, index) => new { name, index })
            .ToDictionary(
                value => value.name,
                value => value.index,
                StringComparer.Ordinal);
        var inherited = 0;
        for (var targetIndex = 0;
             targetIndex < target.FeatureNames.Length;
             targetIndex++)
        {
            if (!sourceIndexes.TryGetValue(
                    target.FeatureNames[targetIndex],
                    out var sourceIndex))
            {
                continue;
            }
            target.Weights[targetIndex] = source.Weights[sourceIndex] *
                target.FeatureScales[targetIndex] /
                source.FeatureScales[sourceIndex];
            inherited++;
        }
        return inherited;
    }

    public static double[] Encode(
        AcquisitionRoutePortfolioSupervisionCorpusRow row,
        AcquisitionRoutePortfolioTeacherCandidateEvaluation candidate,
        GoalMethodPairwiseLinearModel model)
    {
        var raw = Raw(row, candidate);
        var encoded = new double[model.FeatureNames.Length];
        for (var index = 0; index < encoded.Length; index++)
        {
            var value = raw.TryGetValue(
                model.FeatureNames[index],
                out var observed)
                ? observed
                : 0;
            encoded[index] = (value - model.FeatureMeans[index]) /
                model.FeatureScales[index];
        }
        return encoded;
    }

    private static IEnumerable<
        AcquisitionRoutePortfolioTeacherCandidateEvaluation>
        AdmittedCandidates(
            AcquisitionRoutePortfolioSupervisionCorpusRow row) =>
        row.SupervisionRow.Payload.TeacherPreference.CandidateEvaluations
            .Where(candidate =>
                candidate.AdmissionReady &&
                candidate.AggregateCostVector is not null);

    private static Dictionary<string, double> Raw(
        AcquisitionRoutePortfolioSupervisionCorpusRow row,
        AcquisitionRoutePortfolioTeacherCandidateEvaluation candidate)
    {
        var payload = row.SupervisionRow.Payload;
        var context = payload.DecisionContext;
        var teacher = payload.TeacherPreference;
        var cost = candidate.AggregateCostVector ??
            throw new InvalidDataException(
                "Admitted goal-method candidate has no cost vector.");
        var values = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["state.numeric:year"] = context.Year,
            ["state.numeric:day"] = context.Day,
            ["state.numeric:time"] = context.Time,
            ["state.numeric:total_day"] = context.TotalDay,
            ["state.numeric:transition_index"] =
                row.SupervisionRow.TransitionIndex,
            ["state.numeric:ledger_revision"] =
                payload.DecisionLedgerRevision,
            ["state.numeric:candidate_denominator_count"] =
                teacher.CandidateDenominatorCount,
            ["state.numeric:admitted_candidate_count"] =
                teacher.AdmittedCandidateCount,
            ["state.numeric:pareto_frontier_count"] =
                teacher.ParetoFrontierCount,
            ["state.categorical:goal=" + Safe(row.GoalId)] = 1,
            ["state.categorical:season=" + Safe(context.Season)] = 1,
            ["state.categorical:selection_policy=" +
                Safe(teacher.SelectionPolicyId)] = 1,
            ["candidate.numeric:route_count"] =
                candidate.SelectedRouteOccurrenceIds.Length,
            ["candidate.numeric:elapsed_game_minutes"] =
                cost.GuaranteedElapsedGameMinutes,
            ["candidate.numeric:required_energy"] = cost.RequiredEnergy,
            ["candidate.numeric:material_total_sale_value"] =
                cost.MaterialTotalSaleValue,
            ["candidate.numeric:material_kind_count"] =
                cost.MaterialCosts.Length,
            ["candidate.numeric:currency_kind_count"] =
                cost.CurrencyCosts.Length
        };
        foreach (var routeId in candidate.SelectedRouteOccurrenceIds)
        {
            values["candidate.route=" + Safe(routeId)] = 1;
            values["interaction.goal=" + Safe(row.GoalId) +
                "|route=" + Safe(routeId)] = 1;
        }
        foreach (var material in cost.MaterialCosts)
        {
            var key = Safe(material.QualifiedItemId) +
                "|quality=" + material.Quality;
            values["candidate.material.quantity:" + key] =
                material.Quantity;
            values["candidate.material.unit_sale_price:" + key] =
                material.UnitSalePrice;
            values["candidate.material.total_sale_value:" + key] =
                material.TotalSaleValue;
        }
        foreach (var currency in cost.CurrencyCosts)
        {
            values["candidate.currency.amount:" + currency.CurrencyId +
                "=" + Safe(currency.CurrencyKey)] = currency.Amount;
        }
        return values;
    }

    private static string Safe(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? "unknown"
            : value.Trim()
                .Replace("\r", "\\r", StringComparison.Ordinal)
                .Replace("\n", "\\n", StringComparison.Ordinal);
}
