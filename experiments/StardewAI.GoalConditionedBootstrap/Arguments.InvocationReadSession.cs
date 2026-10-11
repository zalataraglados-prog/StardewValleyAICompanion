internal sealed partial class Arguments
{
    private static readonly StringComparer PathComparer =
        OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
    private static readonly string[] MutableArgumentMarkers =
    {
        "ledger",
        "lease",
        "ownership"
    };

    public InvocationReadPlan InvocationReadPlan()
    {
        var immutableInputs = new List<string>();
        var prohibitedPaths = new List<string>();
        var prohibitedRoots = new List<string>();
        foreach (var pair in values)
        {
            if (IsControlArgument(pair.Key))
                continue;

            if (IsOutputRootArgument(pair.Key))
            {
                AddFullPath(prohibitedRoots, pair.Value);
                continue;
            }
            if (IsMutableOrOutputArgument(pair.Key))
            {
                AddFullPath(prohibitedPaths, pair.Value);
                continue;
            }

            try
            {
                if (File.Exists(pair.Value))
                    immutableInputs.Add(Path.GetFullPath(pair.Value));
            }
            catch
            {
                // Non-path arguments are not read-session inputs.
            }
        }

        return new InvocationReadPlan(
            immutableInputs
                .Distinct(PathComparer)
                .OrderBy(value => value, PathComparer)
                .ToArray(),
            prohibitedPaths
                .Distinct(PathComparer)
                .OrderBy(value => value, PathComparer)
                .ToArray(),
            prohibitedRoots
                .Distinct(PathComparer)
                .OrderBy(value => value, PathComparer)
                .ToArray());
    }

    private static bool IsControlArgument(string name) =>
        string.Equals(name, "validation-trace", StringComparison.Ordinal) ||
        string.Equals(name, "validation-trace-scenario", StringComparison.Ordinal) ||
        string.Equals(name, "read-session", StringComparison.Ordinal) ||
        string.Equals(
            name,
            "deterministic-build-session",
            StringComparison.Ordinal);

    private static bool IsOutputRootArgument(string name) =>
        name.Contains("output-root", StringComparison.OrdinalIgnoreCase);

    private static bool IsMutableOrOutputArgument(string name) =>
        name.Contains("output", StringComparison.OrdinalIgnoreCase) ||
        name.Contains("checkpoint", StringComparison.OrdinalIgnoreCase) ||
        MutableArgumentMarkers.Any(marker => name.Contains(
            marker,
            StringComparison.OrdinalIgnoreCase));

    private static void AddFullPath(List<string> paths, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;
        try
        {
            paths.Add(Path.GetFullPath(value));
        }
        catch
        {
            // Non-path mutable arguments cannot collide with a file input.
        }
    }
}

internal sealed record InvocationReadPlan(
    string[] ImmutableInputPaths,
    string[] ProhibitedPaths,
    string[] ProhibitedRoots);
