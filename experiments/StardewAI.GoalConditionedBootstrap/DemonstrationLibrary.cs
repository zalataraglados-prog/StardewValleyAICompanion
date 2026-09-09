using System.Text.Json;
using StardewAI.Contracts.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class DemonstrationLibrary
{
    private readonly GoalConditionedDemonstration[] allRows;
    private readonly GoalConditionedDemonstration[] expertRows;

    private DemonstrationLibrary(
        GoalConditionedDemonstration[] allRows,
        GoalConditionedDemonstration[] expertRows)
    {
        this.allRows = allRows;
        this.expertRows = expertRows;
    }

    public static DemonstrationLibrary Load(
        string path,
        GoalKnowledge knowledge)
    {
        var rows = File.ReadLines(Path.GetFullPath(path))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => JsonSerializer.Deserialize<GoalConditionedDemonstration>(line, JsonOptions)
                ?? throw new InvalidDataException("Demonstration row is null."))
            .ToArray();
        return FromRows(rows, knowledge);
    }

    public static DemonstrationLibrary FromRows(
        IEnumerable<GoalConditionedDemonstration> rows,
        GoalKnowledge knowledge)
    {
        var materialized = rows.ToArray();
        var validator = new DemonstrationValidator();
        var experts = materialized
            .Where(row => validator.Validate(row, knowledge).Admitted)
            .ToArray();
        return new DemonstrationLibrary(materialized, experts);
    }

    public DemonstrationRetrieval Retrieve(
        string goalId,
        string queryStateHash,
        FeatureVector query,
        int limit = 3,
        double minimumSimilarity = 0.20)
    {
        if (limit is < 1 or > 100)
            throw new ArgumentOutOfRangeException(nameof(limit));
        if (minimumSimilarity is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(minimumSimilarity));

        var matches = expertRows
            .Where(row => string.Equals(row.Goal.GoalId, goalId, StringComparison.Ordinal))
            .Select(row =>
            {
                var similarity = Similarity(query, row.StateFeatures);
                return new DemonstrationMatch
                {
                    DemonstrationId = row.DemonstrationId,
                    SourceKind = row.Source.Kind,
                    Similarity = Math.Round(similarity.Score, 6),
                    SharedFeatureCount = similarity.Shared,
                    Segments = row.Segments
                        .OrderBy(segment => segment.Sequence)
                        .ToArray()
                };
            })
            .Where(match => match.SharedFeatureCount > 0 && match.Similarity >= minimumSimilarity)
            .OrderByDescending(match => match.Similarity)
            .ThenBy(match => match.DemonstrationId, StringComparer.Ordinal)
            .Take(limit)
            .ToArray();

        return new DemonstrationRetrieval
        {
            GoalId = goalId,
            QueryStateHash = queryStateHash,
            LibraryRows = allRows.Length,
            ExpertRows = expertRows.Length,
            Matches = matches
        };
    }

    private static SimilarityResult Similarity(FeatureVector left, FeatureVector right)
    {
        var leftNumeric = left.Numeric.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        var rightNumeric = right.Numeric.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        var leftCategorical = left.Categorical.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        var rightCategorical = right.Categorical.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        var leftBoolean = left.Boolean.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);
        var rightBoolean = right.Boolean.ToDictionary(value => value.Name, value => value.Value, StringComparer.Ordinal);

        var score = 0d;
        var shared = 0;
        var union = 0;
        CompareNumeric(leftNumeric, rightNumeric, ref score, ref shared, ref union);
        CompareExact(leftCategorical, rightCategorical, ref score, ref shared, ref union);
        CompareExact(leftBoolean, rightBoolean, ref score, ref shared, ref union);
        return new SimilarityResult(union == 0 ? 0 : score / union, shared);
    }

    private static void CompareNumeric(
        IReadOnlyDictionary<string, double> left,
        IReadOnlyDictionary<string, double> right,
        ref double score,
        ref int shared,
        ref int union)
    {
        var names = left.Keys.Union(right.Keys, StringComparer.Ordinal).ToArray();
        union += names.Length;
        foreach (var name in names)
        {
            if (!left.TryGetValue(name, out var a) || !right.TryGetValue(name, out var b))
                continue;
            shared++;
            score += 1d - Math.Min(1d, Math.Abs(a - b) / (1d + Math.Max(Math.Abs(a), Math.Abs(b))));
        }
    }

    private static void CompareExact<T>(
        IReadOnlyDictionary<string, T> left,
        IReadOnlyDictionary<string, T> right,
        ref double score,
        ref int shared,
        ref int union)
        where T : notnull
    {
        var names = left.Keys.Union(right.Keys, StringComparer.Ordinal).ToArray();
        union += names.Length;
        foreach (var name in names)
        {
            if (!left.TryGetValue(name, out var a) || !right.TryGetValue(name, out var b))
                continue;
            shared++;
            if (EqualityComparer<T>.Default.Equals(a, b))
                score++;
        }
    }

    private sealed record SimilarityResult(double Score, int Shared);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
}
