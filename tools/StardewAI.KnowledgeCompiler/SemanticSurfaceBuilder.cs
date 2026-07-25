namespace StardewAI.KnowledgeCompiler;

internal sealed class SemanticSurfaceBuilder
{
    public IReadOnlyList<HandlerSemanticSurface> Build(HandlerOperationCatalog catalog) =>
        catalog.Rules.Select(row =>
        {
            var roles = row.Families.Select(FamilyRole)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var boundaries = new List<string>();
            if (row.DynamicDispatchIds.Count > 0)
                boundaries.Add("runtime_virtual_target_required");
            if (row.ReflectionCallIds.Count > 0)
                boundaries.Add("runtime_reflection_target_required");
            if (row.RandomSourceIds.Count > 0)
                boundaries.Add("runtime_random_state_and_outcome_required");

            return new HandlerSemanticSurface(
                row.RuleId,
                roles,
                row.Families,
                row.Keys,
                row.FieldReadIds,
                row.PropertyReadIds,
                row.FieldWriteIds,
                row.PropertyWriteIds,
                row.ExternalCallIds,
                row.RandomSourceIds,
                boundaries,
                boundaries.Count == 0
                    ? "complete_static_may_read_write_surface"
                    : "complete_static_surface_with_runtime_boundaries",
                roles.Contains("predicate", StringComparer.Ordinal)
                    ? "boolean_predicate_result"
                    : roles.Contains("command", StringComparer.Ordinal)
                        ? "command_completion_and_state_transition"
                        : "method_return_semantics_require_signature_and_branch_classification");
        }).ToArray();

    private static string FamilyRole(string family) => family switch
    {
        "game_state_query" => "predicate",
        "event_precondition" => "predicate",
        "event_command" => "command",
        "data_method" => "data_method",
        _ => "unknown"
    };
}

internal sealed record HandlerSemanticSurface(
    int OperationRuleId,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Families,
    IReadOnlyList<string> Keys,
    IReadOnlyList<int> MayReadFieldIds,
    IReadOnlyList<int> MayReadPropertyIds,
    IReadOnlyList<int> MayWriteFieldIds,
    IReadOnlyList<int> MayWritePropertyIds,
    IReadOnlyList<int> ExternalSideEffectCallIds,
    IReadOnlyList<int> RandomSourceIds,
    IReadOnlyList<string> RuntimeBoundaries,
    string StaticSurfaceStatus,
    string ResultContract);
