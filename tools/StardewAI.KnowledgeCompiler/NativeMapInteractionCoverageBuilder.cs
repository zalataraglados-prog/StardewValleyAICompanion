namespace StardewAI.KnowledgeCompiler;

internal sealed record NativeMapInteractionCoverage(
    string PropertyName,
    string ActionToken,
    int OccurrenceCount,
    string[] ExampleMapTiles,
    string[] SourceBranchIds,
    string[] MappedActionIds,
    string SourceBindingStatus,
    string SemanticCoverageStatus,
    string EvidenceBasis);

internal sealed class NativeMapInteractionCoverageCatalog
{
    public IReadOnlyList<NativeMapInteractionCoverage> Interactions { get; init; } =
        Array.Empty<NativeMapInteractionCoverage>();
}

internal sealed class NativeMapInteractionCoverageBuilder
{
    public NativeMapInteractionCoverageCatalog Build(
        MapTopologyIndex topology,
        NativeActionBranchCatalog branches)
    {
        var result = new List<NativeMapInteractionCoverage>();
        var effectiveInteractions = topology.Maps
            .SelectMany(map => map.Interactions
                .Where(row => row.EffectiveUnderNativePropertyPrecedence)
                .Select(row => new { Map = map.AssetName, Row = row }))
            .ToArray();

        foreach (var group in effectiveInteractions
                     .GroupBy(
                         row => (row.Row.PropertyName, ActionToken(row.Row.Value)),
                         StringTupleComparer.Ordinal)
                     .OrderBy(row => row.Key.PropertyName, StringComparer.Ordinal)
                     .ThenBy(row => row.Key.Item2, StringComparer.Ordinal))
        {
            var propertyName = group.Key.PropertyName;
            var token = group.Key.Item2;
            var expectedMembers = propertyName == "TouchAction"
                ? new[] { "performTouchAction" }
                : new[] { "performAction", "checkAction" };
            var candidateBranches = branches.Branches
                .Where(row =>
                    expectedMembers.Contains(row.Member, StringComparer.Ordinal) &&
                    row.BranchKind != "method_envelope")
                .ToArray();
            var matchingBranches = candidateBranches
                .Where(row => AnchorContainsToken(row.Anchor, token))
                .ToArray();
            if (matchingBranches.Length == 0)
            {
                var literalMatches = candidateBranches
                    .Where(row => row.StringLiterals.Contains(token, StringComparer.Ordinal))
                    .ToArray();
                if (literalMatches.Length > 0)
                {
                    var narrowestSpan = literalMatches.Min(row => row.EndLine - row.StartLine);
                    matchingBranches = literalMatches
                        .Where(row => row.EndLine - row.StartLine == narrowestSpan)
                        .ToArray();
                }
            }
            var actionIds = matchingBranches
                .SelectMany(row => row.MappedActionIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var evidenceBasis =
                "effective runtime-projected map property joined by exact first action token to locked native branch string evidence";
            var sourceBindingStatus = matchingBranches.Length > 0
                ? "matched_native_branch"
                : "native_source_binding_missing";

            if (matchingBranches.Length == 0 &&
                TryResolveCoordinateBinding(
                    propertyName,
                    token,
                    branches,
                    out var coordinateBranches,
                    out var coordinateActions,
                    out var coordinateEvidence))
            {
                matchingBranches = coordinateBranches;
                actionIds = coordinateActions;
                sourceBindingStatus = "matched_native_coordinate_branch";
                evidenceBasis = coordinateEvidence;
            }

            if (IsNativeNonSemanticToken(propertyName, token, out var noOpEvidence))
            {
                result.Add(new(
                    propertyName,
                    token,
                    group.Count(),
                    group.Select(row =>
                            $"{row.Map}:{row.Row.Layer}[{row.Row.X},{row.Row.Y}]={row.Row.Value}")
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .Take(16)
                        .ToArray(),
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    "classified_native_noop_static_token",
                    "classified_non_semantic",
                    noOpEvidence));
                continue;
            }

            if (actionIds.Length == 0 && matchingBranches.Length > 0)
                actionIds = NativeBranchSemanticClassifier.ClassifyActionToken(token);

            var semanticStatus = sourceBindingStatus.StartsWith("matched_native_", StringComparison.Ordinal) &&
                actionIds.Length > 0
                    ? "mapped_to_semantic_action"
                    : "requires_semantic_review";
            result.Add(new(
                propertyName,
                token,
                group.Count(),
                group.Select(row =>
                        $"{row.Map}:{row.Row.Layer}[{row.Row.X},{row.Row.Y}]={row.Row.Value}")
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .Take(16)
                    .ToArray(),
                matchingBranches.Select(row => row.BranchId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray(),
                actionIds,
                sourceBindingStatus,
                semanticStatus,
                evidenceBasis));
        }

        return new NativeMapInteractionCoverageCatalog
        {
            Interactions = result
        };
    }

    private static string ActionToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        var span = value.AsSpan().TrimStart();
        var end = span.IndexOfAny(" \t\r\n");
        return end < 0 ? span.ToString() : span[..end].ToString();
    }

    private static bool AnchorContainsToken(string anchor, string token)
    {
        if (anchor.Split(" | ", StringSplitOptions.RemoveEmptyEntries)
            .Any(label => string.Equals(label, $"case \"{token}\"", StringComparison.Ordinal)))
        {
            return true;
        }

        return anchor.Contains($"\"{token}\"", StringComparison.Ordinal);
    }

    private static bool TryResolveCoordinateBinding(
        string propertyName,
        string token,
        NativeActionBranchCatalog catalog,
        out NativeActionBranch[] branches,
        out string[] actionIds,
        out string evidence)
    {
        branches = Array.Empty<NativeActionBranch>();
        actionIds = Array.Empty<string>();
        evidence = string.Empty;
        if (propertyName == "Action" && token == "BrokenBeachBridge")
        {
            branches = catalog.Branches.Where(row =>
                    row.RuntimeType == "Beach" &&
                    row.Member == "checkAction" &&
                    row.Anchor.Contains("case 284", StringComparison.Ordinal))
                .ToArray();
            actionIds = new[] { "quest.advance" };
            evidence =
                "Maps/Beach token is intercepted by Beach.checkAction tile index 284 before base GameLocation.performAction";
            return branches.Length > 0;
        }
        if (propertyName == "Action" && token == "WitchCaveBlock")
        {
            branches = catalog.Branches.Where(row =>
                    row.RuntimeType == "Railroad" &&
                    row.Member == "checkAction" &&
                    row.Anchor.Contains("== 287", StringComparison.Ordinal))
                .ToArray();
            actionIds = new[] { "quest.advance" };
            evidence =
                "Maps/Railroad token tile is intercepted by Railroad.checkAction tile index 287 before base GameLocation.performAction";
            return branches.Length > 0;
        }
        return false;
    }

    private static bool IsNativeNonSemanticToken(
        string propertyName,
        string token,
        out string evidence)
    {
        evidence = (propertyName, token) switch
        {
            ("Action", "None") =>
                "locked native dispatcher has an exact no-op branch with no gameplay commitment",
            ("TouchAction", "FaceDirection") =>
                "locked native touch branch only changes an NPC orientation as a map-authored side effect",
            ("Action", "'The") or ("Action", "Emote") or ("Action", "General") or
                ("Action", "TownMailbox") =>
                $"locked native tile/Event dispatchers have no exact '{token}' branch and return false; static map property is legacy or inactive metadata",
            ("TouchAction", "MakeoverBox") or ("TouchAction", "Sleep2") =>
                $"locked native touch dispatcher has no exact '{token}' branch and falls through without a gameplay commitment",
            _ => string.Empty
        };
        return evidence.Length > 0;
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string PropertyName, string Item2)>
    {
        public static readonly StringTupleComparer Ordinal = new();

        public bool Equals(
            (string PropertyName, string Item2) x,
            (string PropertyName, string Item2) y) =>
            string.Equals(x.PropertyName, y.PropertyName, StringComparison.Ordinal) &&
            string.Equals(x.Item2, y.Item2, StringComparison.Ordinal);

        public int GetHashCode((string PropertyName, string Item2) obj) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(obj.PropertyName),
                StringComparer.Ordinal.GetHashCode(obj.Item2));
    }
}
