using System.Globalization;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Training;
using StardewAI.Core.Training;

namespace StardewAI.GoalConditionedBootstrap;

public sealed class TeacherPlanBuilder
{
    private const int TicksPerTile = 60;
    private readonly int maxItems;

    public TeacherPlanBuilder(int maxItems = 24)
    {
        if (maxItems is < 1 or > 256)
            throw new ArgumentOutOfRangeException(nameof(maxItems));
        this.maxItems = maxItems;
    }

    public TeacherPlan Build(
        AvailabilityAwarePolicyPredictionEnvelope ranking,
        GoalKnowledge knowledge,
        DemonstrationRetrieval? guidance = null)
    {
        if (!string.Equals(ranking.SchemaVersion, "availability_policy_prediction.v1", StringComparison.Ordinal))
            throw new InvalidDataException("Unsupported ranking schema.");
        if (string.IsNullOrWhiteSpace(ranking.Availability.StateHash))
            throw new InvalidDataException("Ranking has no source state hash.");

        var boundIds = new HashSet<string>(
            ranking.GoalResolution.BoundCandidateIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        var expert = guidance?.Matches.FirstOrDefault();
        var eligible = ranking.RankedEventCandidates
            .Where(IsEligible)
            .Select(candidate => Describe(candidate, boundIds, knowledge, expert))
            .Where(candidate => !candidate.IsControlPlaneSave)
            .ToArray();

        var deferred = new List<TeacherDeferredCandidate>();
        var selected = new List<DescribedCandidate>();
        foreach (var item in eligible
                     .OrderByDescending(value => value.Priority)
                     .ThenBy(value => value.GuidanceSequence ?? int.MaxValue)
                     .ThenByDescending(value => value.GoalCriterionIds.Length)
                     .ThenByDescending(value => value.ValueSignal)
                     .ThenBy(value => value.Candidate.CandidateId, StringComparer.Ordinal))
        {
            if (item.IsOptionalRemoteRoute)
            {
                deferred.Add(Deferred(item, "optional_remote_route_not_a_day_obligation"));
                continue;
            }
            if (selected.Count >= maxItems)
            {
                deferred.Add(Deferred(item, "teacher_day_item_budget_reached"));
                continue;
            }
            selected.Add(item);
        }

        var bundles = selected
            .GroupBy(item => item.DestinationLocation, StringComparer.Ordinal)
            .Select(group => BuildBundle(group.Key, group.ToArray()))
            .OrderByDescending(bundle => PriorityWeight(bundle.PriorityClass))
            .ThenBy(bundle => bundle.EstimatedTicks)
            .ThenBy(bundle => bundle.LocationId, StringComparer.Ordinal)
            .ToArray();

        var sequence = 1;
        foreach (var item in bundles.SelectMany(bundle => bundle.Items))
            item.Sequence = sequence++;

        return new TeacherPlan
        {
            PlanId = "teacher." + ranking.Availability.StateHash[..Math.Min(16, ranking.Availability.StateHash.Length)],
            GoalId = knowledge.GoalId,
            SourceStateHash = ranking.Availability.StateHash,
            KnowledgeSha256 = knowledge.Sha256,
            CandidateCount = ranking.RankedEventCandidates.Length,
            Bundles = bundles,
            DeferredCandidates = deferred
                .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                .ToArray(),
            Audit = new TeacherPlanAudit
            {
                UsesModelScoreAsLabel = false,
                UsesExpertDemonstrationGuidance = expert is not null &&
                    eligible.Any(value => value.GuidanceSequence.HasValue),
                ExpertDemonstrationId = expert?.DemonstrationId ?? string.Empty,
                ExpertDemonstrationSimilarity = expert?.Similarity
            }
        };
    }

    private static TeacherBundle BuildBundle(
        string locationId,
        DescribedCandidate[] candidates)
    {
        var remaining = candidates.ToList();
        var ordered = new List<TeacherPlanItem>();
        int? x = null;
        int? y = null;
        var first = true;
        while (remaining.Count > 0)
        {
            var next = remaining
                .OrderByDescending(value => value.Priority)
                .ThenBy(value => value.GuidanceSequence ?? int.MaxValue)
                .ThenBy(value => RouteDistance(x, y, value.Candidate.TileX, value.Candidate.TileY))
                .ThenByDescending(value => value.ValueSignal)
                .ThenBy(value => value.Candidate.CandidateId, StringComparer.Ordinal)
                .First();
            remaining.Remove(next);

            var routeTicks = first
                ? Math.Max(60, next.Candidate.EstimatedTicks)
                : Math.Max(60, RouteDistance(x, y, next.Candidate.TileX, next.Candidate.TileY) * TicksPerTile + 60);
            var reasons = new List<string>
            {
                "priority_class=" + next.PriorityClass,
                "shared_location_route=" + locationId,
                first ? "destination_entry_cost_paid_once" : "marginal_same_location_route_cost"
            };
            reasons.AddRange(next.GoalCriterionIds.Select(id => "supports_goal_criterion=" + id));
            if (next.IsBoundToCurrentGoal)
                reasons.Add("direct_current_goal_binding");
            if (next.GuidanceSequence.HasValue)
                reasons.Add("expert_demonstration_guidance=" + next.GuidanceMatch);

            ordered.Add(new TeacherPlanItem
            {
                CandidateId = next.Candidate.CandidateId,
                OptionId = next.Candidate.OptionId,
                Kind = next.Candidate.Kind,
                MethodId = next.MethodId,
                LocationId = locationId,
                TileX = next.Candidate.TileX,
                TileY = next.Candidate.TileY,
                MarginalRouteTicks = routeTicks,
                TeacherScore = Math.Round(
                    next.Priority + next.GoalCriterionIds.Length * 10 + next.ValueSignal - routeTicks * 0.0001,
                    4),
                Reasons = reasons.ToArray()
            });
            x = next.Candidate.TileX ?? x;
            y = next.Candidate.TileY ?? y;
            first = false;
        }

        var priorityClass = candidates
            .OrderByDescending(candidate => candidate.Priority)
            .First().PriorityClass;
        return new TeacherBundle
        {
            BundleId = "bundle:" + SafeId(locationId) + ":" + priorityClass,
            LocationId = locationId,
            PriorityClass = priorityClass,
            MethodIds = candidates.Select(value => value.MethodId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            EstimatedTicks = ordered.Sum(value => value.MarginalRouteTicks),
            Items = ordered.ToArray()
        };
    }

    private static DescribedCandidate Describe(
        PolicyEventCandidatePrediction candidate,
        ISet<string> boundIds,
        GoalKnowledge knowledge,
        DemonstrationMatch? guidance)
    {
        var direction = DirectionFor(candidate);
        var method = MethodFor(candidate, direction);
        var criteria = CriteriaFor(direction, candidate, knowledge);
        var priorityClass = PriorityClassFor(candidate, method, direction);
        var destination = EffectiveDestination(candidate);
        var value = Math.Clamp(candidate.TotalValue * 0.001, 0, 25);
        if (TryEffectNumber(candidate.ExpectedEffect, "output_total_value", out var outputValue))
            value = Math.Max(value, Math.Clamp(outputValue * 0.001, 0, 25));
        var direct = boundIds.Contains(candidate.CandidateId) ||
            Parameter(candidate, "grandpa_direction_id") is not null;
        var exactGuidance = guidance?.Segments
            .Where(segment => string.Equals(segment.OptionId, candidate.OptionId, StringComparison.Ordinal))
            .OrderBy(segment => segment.Sequence)
            .FirstOrDefault();
        var methodGuidance = exactGuidance ?? guidance?.Segments
            .Where(segment => string.Equals(segment.MethodId, method, StringComparison.Ordinal))
            .OrderBy(segment => segment.Sequence)
            .FirstOrDefault();
        return new DescribedCandidate(
            candidate,
            method,
            criteria,
            priorityClass,
            PriorityWeight(priorityClass) + (direct ? 50 : 0),
            value,
            destination,
            direct,
            candidate.Kind == "route_connector_tile" && priorityClass == "optional" && !direct,
            candidate.OptionId == "recovery.stabilize_day" &&
            string.Equals(Parameter(candidate, "control_plane.native_save_boundary"), "true", StringComparison.OrdinalIgnoreCase),
            methodGuidance?.Sequence,
            exactGuidance is not null ? "option" : methodGuidance is not null ? "method" : string.Empty);
    }

    private static GrandpaDirectionCatalogEntry? DirectionFor(
        PolicyEventCandidatePrediction candidate)
    {
        var explicitDirectionId = Parameter(candidate, "grandpa_direction_id");
        if (!string.IsNullOrWhiteSpace(explicitDirectionId))
        {
            var explicitDirection = GrandpaDirectionCatalog.Entries.SingleOrDefault(
                value => string.Equals(
                    value.DirectionId,
                    explicitDirectionId,
                    StringComparison.Ordinal));
            if (explicitDirection is null)
                throw new InvalidDataException(
                    "Candidate references an unknown Grandpa direction: " + explicitDirectionId);
            if (!explicitDirection.PermittedOptionIds.Contains(
                    candidate.OptionId,
                    StringComparer.Ordinal))
            {
                throw new InvalidDataException(
                    $"Candidate option {candidate.OptionId} is not permitted for Grandpa direction {explicitDirectionId}.");
            }
            return explicitDirection;
        }

        var matches = GrandpaDirectionCatalog.Entries
            .Where(value => value.PermittedOptionIds.Contains(
                candidate.OptionId,
                StringComparer.Ordinal))
            .ToArray();
        if (candidate.FullShipmentContributes == true)
        {
            var fullShipment = matches.SingleOrDefault(value => string.Equals(
                value.DirectionId,
                "complete_full_shipment",
                StringComparison.Ordinal));
            if (fullShipment is not null)
                return fullShipment;
        }

        return matches.Length == 1 ? matches[0] : null;
    }

    private static string MethodFor(
        PolicyEventCandidatePrediction value,
        GrandpaDirectionCatalogEntry? direction)
    {
        var option = value.OptionId;
        var kind = value.Kind;
        if (option == "recovery.stabilize_day" || kind.StartsWith("recovery_", StringComparison.Ordinal))
            return "method.day.recover_and_close";
        if (option == "mail.process_letter" || kind.Contains("mail", StringComparison.Ordinal))
            return "method.day.process_mail";
        if (direction is not null)
            return direction.BindingRuleId;
        if (kind.Contains("water_crop", StringComparison.Ordinal) ||
            kind.Contains("load_machine", StringComparison.Ordinal) ||
            kind.Contains("animal", StringComparison.Ordinal))
            return "method.day.maintain_production";
        if (option.StartsWith("quest.", StringComparison.Ordinal) || kind.Contains("quest", StringComparison.Ordinal))
            return "method.day.advance_task";
        return "method.day.available_action";
    }

    private static string[] CriteriaFor(
        GrandpaDirectionCatalogEntry? direction,
        PolicyEventCandidatePrediction candidate,
        GoalKnowledge knowledge)
    {
        var ids = new HashSet<string>(
            direction?.CriterionIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        if (candidate.FullShipmentContributes == true)
            ids.Add("achievement_full_shipment");

        return ids.Where(id => knowledge.Criteria.Any(value => value.Id == id))
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
    }

    private static string PriorityClassFor(
        PolicyEventCandidatePrediction value,
        string method,
        GrandpaDirectionCatalogEntry? direction)
    {
        if (method == "method.day.recover_and_close")
            return "control";
        if (method is "method.day.process_mail" or "method.day.advance_task" ||
            value.AvailabilityClass.Contains("deadline", StringComparison.Ordinal))
            return "obligation";
        if (value.Kind == "route_connector_tile" &&
            value.OptionId.StartsWith("social.", StringComparison.Ordinal))
            return "optional";
        if (method.StartsWith("method.day.maintain", StringComparison.Ordinal) ||
            string.Equals(direction?.DirectionId, "earn_pet_love", StringComparison.Ordinal) ||
            value.Kind.Contains("harvest_crop", StringComparison.Ordinal) ||
            value.Kind.Contains("machine_output", StringComparison.Ordinal))
            return "maintenance";
        if (direction is not null)
            return "strategic";
        if (value.OptionId.StartsWith("social.", StringComparison.Ordinal))
            return "optional";
        return "opportunistic";
    }

    private static int PriorityWeight(string value) => value switch
    {
        "control" => 1000,
        "obligation" => 900,
        "maintenance" => 800,
        "strategic" => 700,
        "opportunistic" => 500,
        "optional" => 200,
        _ => 0
    };

    private static bool IsEligible(PolicyEventCandidatePrediction value) =>
        value.Available &&
        value.AllowedToday != false &&
        !string.Equals(value.TimelineStatus, "blocked", StringComparison.Ordinal) &&
        (value.BlockReasons?.Length ?? 0) == 0;

    private static string EffectiveDestination(PolicyEventCandidatePrediction value) =>
        Parameter(value, "expected_target_location") ??
        Parameter(value, "continuation.target_location") ??
        Parameter(value, "target_location") ??
        Parameter(value, "route.target_location_id") ??
        (string.IsNullOrWhiteSpace(value.LocationId) ? "unknown" : value.LocationId);

    private static string? Parameter(PolicyEventCandidatePrediction value, string name) =>
        (value.Parameters ?? Array.Empty<SmallModelActionParameter>())
        .FirstOrDefault(parameter => string.Equals(parameter.Name, name, StringComparison.Ordinal))?.Value;

    private static bool TryEffectNumber(string source, string name, out double value)
    {
        value = 0;
        var prefix = name + "=";
        var raw = source.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault(item => item.StartsWith(prefix, StringComparison.Ordinal));
        return raw is not null && double.TryParse(
            raw[prefix.Length..],
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static int RouteDistance(int? x1, int? y1, int? x2, int? y2) =>
        x1.HasValue && y1.HasValue && x2.HasValue && y2.HasValue
            ? Math.Abs(x1.Value - x2.Value) + Math.Abs(y1.Value - y2.Value)
            : 0;

    private static TeacherDeferredCandidate Deferred(DescribedCandidate item, string reason) => new()
    {
        CandidateId = item.Candidate.CandidateId,
        Reason = reason
    };

    private static string SafeId(string value) => string.Concat(value.Select(character =>
        char.IsLetterOrDigit(character) || character is '_' or '-' ? character : '_'));

    private sealed record DescribedCandidate(
        PolicyEventCandidatePrediction Candidate,
        string MethodId,
        string[] GoalCriterionIds,
        string PriorityClass,
        double Priority,
        double ValueSignal,
        string DestinationLocation,
        bool IsBoundToCurrentGoal,
        bool IsOptionalRemoteRoute,
        bool IsControlPlaneSave,
        int? GuidanceSequence,
        string GuidanceMatch);
}
