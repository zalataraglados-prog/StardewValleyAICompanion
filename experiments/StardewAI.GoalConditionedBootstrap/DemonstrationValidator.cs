namespace StardewAI.GoalConditionedBootstrap;

public sealed record DemonstrationValidationResult(
    bool Admitted,
    string[] Reasons);

public sealed class DemonstrationValidator
{
    public DemonstrationValidationResult Validate(
        GoalConditionedDemonstration value,
        GoalKnowledge knowledge)
    {
        var reasons = new List<string>();
        if (!string.Equals(value.SchemaVersion, BootstrapSchemas.Demonstration, StringComparison.Ordinal))
            reasons.Add("unsupported_schema");
        if (string.IsNullOrWhiteSpace(value.DemonstrationId))
            reasons.Add("demonstration_id_missing");
        if (!DemonstrationSourceKinds.CanTeach(value.Source.Kind))
            reasons.Add("source_not_admitted_as_expert");
        if (!string.Equals(value.Goal.GoalId, knowledge.GoalId, StringComparison.Ordinal))
            reasons.Add("goal_id_not_authoritative");
        if (value.Goal.TargetScore != knowledge.TargetScore)
            reasons.Add("target_score_not_authoritative");
        var authoritativeCriteria = knowledge.Criteria
            .Select(criterion => criterion.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (value.Goal.ActiveCriterionIds.Any(id => !authoritativeCriteria.Contains(id)))
            reasons.Add("criterion_id_not_authoritative");
        if (string.IsNullOrWhiteSpace(value.Context.StartStateHash) ||
            string.IsNullOrWhiteSpace(value.Context.EndStateHash))
            reasons.Add("state_hash_boundary_missing");
        if (value.Segments.Length == 0)
            reasons.Add("segments_empty");
        var orderedSequences = value.Segments.Select(segment => segment.Sequence).Order().ToArray();
        if (!orderedSequences.SequenceEqual(Enumerable.Range(1, value.Segments.Length)))
            reasons.Add("segment_sequence_not_contiguous");
        if (value.Segments.Any(segment =>
                string.IsNullOrWhiteSpace(segment.MethodId) ||
                string.IsNullOrWhiteSpace(segment.OptionId) ||
                string.IsNullOrWhiteSpace(segment.CandidateKind) ||
                string.IsNullOrWhiteSpace(segment.CandidateId) ||
                string.IsNullOrWhiteSpace(segment.StartStateHash) ||
                string.IsNullOrWhiteSpace(segment.EndStateHash) ||
                segment.ObservedEffects.ValueKind is System.Text.Json.JsonValueKind.Undefined or System.Text.Json.JsonValueKind.Null ||
                !segment.Verified ||
                segment.SemanticConfidence < 0.95))
            reasons.Add("segment_not_verified_or_semantically_ambiguous");
        var segmentMethods = value.Segments.Select(segment => segment.MethodId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var declaredMethods = value.Goal.MethodIds
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        if (!segmentMethods.SequenceEqual(declaredMethods, StringComparer.Ordinal))
            reasons.Add("declared_methods_do_not_match_segments");
        if (!value.Outcome.AllSegmentsVerified)
            reasons.Add("outcome_not_fully_verified");
        if (!value.Outcome.DayBoundaryObserved)
            reasons.Add("day_boundary_missing");
        if (!string.Equals(value.Outcome.Status, "completed", StringComparison.Ordinal))
            reasons.Add("demonstration_not_completed");
        if (value.Source.SourcePaths.Length == 0 ||
            value.Source.SourcePaths.Length != value.Source.SourceSha256.Length ||
            value.Source.SourceSha256.Any(hash => hash.Length != 64 || !hash.All(Uri.IsHexDigit)))
            reasons.Add("source_provenance_incomplete");

        value.Audit.ExpertAdmitted = reasons.Count == 0;
        value.Audit.AdmissionReasons = reasons.Count == 0
            ? new[] { "verified_goal_conditioned_expert_demonstration" }
            : reasons.ToArray();
        return new DemonstrationValidationResult(reasons.Count == 0, value.Audit.AdmissionReasons);
    }
}
