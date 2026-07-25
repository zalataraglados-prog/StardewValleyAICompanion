using System.Security.Cryptography;
using System.Text;

namespace StardewAI.KnowledgeCompiler;

internal sealed class AuthoritativeDependencyGraphBuilder
{
    public AuthoritativeDependencyGraph Build(
        RuntimeDependencyGraph runtimeGraph,
        HandlerOperationCatalog operationCatalog,
        IReadOnlyList<HandlerSemanticSurface> semanticSurfaces,
        ExecutableRuleIndex executableRules,
        MapTopologyIndex mapTopology,
        AccessConstraintIndex accessConstraints,
        GoalDependencyIndex goalDependencies)
    {
        var nodes = runtimeGraph.Nodes.ToDictionary(row => row.Id, StringComparer.Ordinal);
        var edges = runtimeGraph.Edges.ToList();
        var surfaceByRule = semanticSurfaces.ToDictionary(row => row.OperationRuleId);

        foreach (var asset in runtimeGraph.Nodes.Select(row => row.SourceAsset)
                     .Concat(executableRules.Conditions.Select(row => row.SourceAsset))
                     .Concat(executableRules.Events.Select(row => row.SourceAsset))
                     .Concat(executableRules.TriggerActions.Select(_ => "Data/TriggerActions"))
                     .Concat(executableRules.DataMethods.Select(row => row.SourceAsset))
                     .Where(value => !string.IsNullOrWhiteSpace(value))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddNode(nodes, new(
                AssetId(asset),
                "runtime_asset",
                asset,
                string.Empty,
                new Dictionary<string, object?>()));
        }

        foreach (var rule in operationCatalog.Rules)
        {
            var surface = surfaceByRule[rule.RuleId];
            AddNode(nodes, new(
                OperationRuleId(rule.RuleId),
                "native_operation_rule",
                "exact_platform_assembly",
                rule.Identity,
                new Dictionary<string, object?>
                {
                    ["operation_rule_id"] = rule.RuleId,
                    ["families"] = rule.Families,
                    ["keys"] = rule.Keys,
                    ["roles"] = surface.Roles,
                    ["static_surface_status"] = surface.StaticSurfaceStatus,
                    ["result_contract"] = surface.ResultContract,
                    ["runtime_boundaries"] = surface.RuntimeBoundaries,
                    ["operation_catalog_source"] = "handler-operation-rules.json"
                }));
        }

        AddConditions(nodes, edges, executableRules.Conditions, surfaceByRule);
        AddEvents(nodes, edges, executableRules.Events, surfaceByRule);
        AddTriggerActions(nodes, edges, executableRules.TriggerActions, surfaceByRule);
        AddDataMethods(nodes, edges, executableRules.DataMethods, surfaceByRule);
        AddMapTopology(nodes, edges, mapTopology);
        AddAccessConstraints(nodes, edges, accessConstraints);
        AddGoalDependencies(nodes, edges, goalDependencies);
        EnsureClosedGraph(nodes, edges);

        var orderedNodes = nodes.Values.OrderBy(row => row.Id, StringComparer.Ordinal).ToArray();
        var orderedEdges = edges.OrderBy(row => row.From, StringComparer.Ordinal)
            .ThenBy(row => row.To, StringComparer.Ordinal)
            .ThenBy(row => row.Kind, StringComparer.Ordinal)
            .ThenBy(row => row.SourcePath, StringComparer.Ordinal)
            .ToArray();
        return new(
            orderedNodes,
            orderedEdges,
            orderedNodes.GroupBy(row => row.Kind, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal),
            orderedEdges.GroupBy(row => row.Kind, StringComparer.Ordinal)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal));
    }

    private static void EnsureClosedGraph(
        IReadOnlyDictionary<string, GraphNode> nodes,
        IEnumerable<GraphEdge> edges)
    {
        var dangling = edges.FirstOrDefault(edge =>
            !nodes.ContainsKey(edge.From) || !nodes.ContainsKey(edge.To));
        if (dangling is not null)
        {
            throw new InvalidDataException(
                $"Authoritative graph has a dangling edge: {dangling.Kind} {dangling.From} -> {dangling.To} " +
                $"at {dangling.SourceAsset}:{dangling.SourcePath}");
        }
    }

    private static void AddGoalDependencies(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        GoalDependencyIndex dependencies)
    {
        var goal = dependencies.GrandpaGoal;
        var goalId = "goal:" + goal.GoalId;
        AddNode(nodes, new(
            goalId,
            "strategic_goal",
            "StardewValley.Utility",
            goal.GoalId,
            new Dictionary<string, object?>
            {
                ["target_score"] = goal.TargetScore,
                ["four_candle_threshold"] = goal.FourCandleThreshold,
                ["maximum_candles"] = goal.MaximumCandles,
                ["target_policy"] = goal.TargetPolicy,
                ["goal_index_source"] = "goal-dependency-index.json"
            }));

        foreach (var criterion in goal.Criteria)
        {
            var criterionId = goalId + ":criterion:" + criterion.Id;
            AddNode(nodes, new(
                criterionId,
                "goal_score_criterion",
                "StardewValley.Utility",
                criterion.Id,
                new Dictionary<string, object?>
                {
                    ["points"] = criterion.Points,
                    ["operation"] = criterion.Operation,
                    ["target"] = criterion.Target,
                    ["native_expression"] = criterion.NativeExpression
                }));
            AddEdge(edges, criterionId, goalId, "contributes_goal_score",
                "StardewValley.Utility", "getGrandpaScore",
                new Dictionary<string, object?> { ["points"] = criterion.Points });

            foreach (var path in criterion.StatePaths)
            {
                var fieldId = "transparent_state_field:" + path;
                AddNode(nodes, new(
                    fieldId,
                    "transparent_state_field",
                    "live_snapshot",
                    path,
                    new Dictionary<string, object?>()));
                AddEdge(edges, fieldId, criterionId, "provides_goal_score_input",
                    "live_snapshot", path);
            }
        }

        foreach (var method in goal.MethodEvidence)
        {
            var methodId = "native_method_identity:" + Hash(
                method.AssemblySha256 + "\n" + method.TypeName + "\n" + method.MetadataToken);
            AddNode(nodes, new(
                methodId,
                "native_method_identity",
                method.AssemblyName,
                method.TypeName + "." + method.MethodName,
                new Dictionary<string, object?>
                {
                    ["assembly_sha256"] = method.AssemblySha256,
                    ["module_version_id"] = method.ModuleVersionId,
                    ["metadata_token"] = method.MetadataToken,
                    ["signature_sha256"] = method.SignatureSha256,
                    ["il_sha256"] = method.IlSha256,
                    ["body_status"] = method.BodyStatus,
                    ["source_candidate"] = method.SourceCandidate,
                    ["source_sha256"] = method.SourceSha256
                }));
            AddEdge(edges, methodId, goalId, "defines_goal_formula",
                method.AssemblyName, method.SourceCandidate);
        }

        foreach (var achievement in goal.AchievementEvidence)
        {
            var achievementId = "achievement:" + achievement.Id;
            AddNode(nodes, new(
                achievementId,
                "achievement",
                "Data/Achievements",
                achievement.Id.ToString(),
                new Dictionary<string, object?>
                {
                    ["name"] = achievement.Name,
                    ["raw_sha256"] = achievement.RawSha256
                }));
            AddEdge(edges, AssetId("Data/Achievements"), achievementId, "declares_achievement",
                "Data/Achievements", "payload." + achievement.Id);
        }
    }

    private static void AddMapTopology(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        MapTopologyIndex topology)
    {
        foreach (var map in topology.Maps)
        {
            var mapId = "map:" + map.AssetName;
            AddNode(nodes, new(
                mapId,
                "runtime_map",
                map.AssetName,
                map.MapId,
                new Dictionary<string, object?>
                {
                    ["width"] = map.Width,
                    ["height"] = map.Height,
                    ["layer_count"] = map.Layers.Count,
                    ["static_passability_status"] = map.BasePassability.Status,
                    ["static_blocked_tile_count"] = map.BasePassability.BlockedTileCount,
                    ["topology_source"] = "map-topology-index.json"
                }));
            AddEdge(edges, AssetId(map.AssetName), mapId, "projects_runtime_map",
                map.AssetName, "payload.Map");

            for (var index = 0; index < map.Warps.Count; index++)
            {
                var warp = map.Warps[index];
                var destinationId = "map_location_reference:" + warp.DestinationLocation;
                AddNode(nodes, new(
                    destinationId,
                    "map_location_reference",
                    map.AssetName,
                    warp.DestinationLocation,
                    new Dictionary<string, object?>()));
                AddEdge(edges, mapId, destinationId, "native_map_warp",
                    map.AssetName,
                    $"payload.Map.Properties.{warp.PropertyName}[{index}]",
                    new Dictionary<string, object?>
                    {
                        ["npc_only"] = warp.NpcOnly,
                        ["from_x"] = warp.FromX,
                        ["from_y"] = warp.FromY,
                        ["destination_x"] = warp.DestinationX,
                        ["destination_y"] = warp.DestinationY
                    });
            }

            for (var index = 0; index < map.Interactions.Count; index++)
            {
                var interaction = map.Interactions[index];
                var interactionId = mapId + ":interaction:" + index;
                AddNode(nodes, new(
                    interactionId,
                    "map_interaction_property",
                    map.AssetName,
                    $"{interaction.Layer}:{interaction.X},{interaction.Y}:{interaction.PropertyName}",
                    new Dictionary<string, object?>
                    {
                        ["layer"] = interaction.Layer,
                        ["x"] = interaction.X,
                        ["y"] = interaction.Y,
                        ["property_name"] = interaction.PropertyName,
                        ["value"] = interaction.Value,
                        ["source"] = interaction.Source,
                        ["effective_under_native_property_precedence"] = interaction.EffectiveUnderNativePropertyPrecedence
                    }));
                AddEdge(edges, mapId, interactionId, "declares_map_interaction",
                    map.AssetName,
                    $"payload.Map.Layers.{interaction.Layer}[{interaction.X},{interaction.Y}]");
            }
        }
    }

    private static void AddAccessConstraints(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        AccessConstraintIndex access)
    {
        for (var index = 0; index < access.DoorWindows.Count; index++)
        {
            var door = access.DoorWindows[index];
            var id = "door_access:" + Hash(
                door.MapAsset + "\n" + door.X + "\n" + door.Y + "\n" + door.RawAction);
            AddNode(nodes, new(
                id,
                "door_access_window",
                door.MapAsset,
                $"{door.X},{door.Y}",
                new Dictionary<string, object?>
                {
                    ["destination_location"] = door.DestinationLocation,
                    ["destination_x"] = door.DestinationX,
                    ["destination_y"] = door.DestinationY,
                    ["open_time"] = door.OpenTime,
                    ["close_time"] = door.CloseTime,
                    ["required_npc"] = door.RequiredNpc,
                    ["minimum_friendship"] = door.MinimumFriendship,
                    ["raw_action"] = door.RawAction
                }));
            AddEdge(edges, "map:" + door.MapAsset, id, "declares_door_access_window",
                door.MapAsset, $"Action[{door.X},{door.Y}]");
        }

        for (var index = 0; index < access.ShopEndpoints.Count; index++)
        {
            var endpoint = access.ShopEndpoints[index];
            var id = "shop_endpoint:" + Hash(
                endpoint.MapAsset + "\n" + endpoint.X + "\n" + endpoint.Y + "\n" + endpoint.RawAction);
            AddNode(nodes, new(
                id,
                "shop_interaction_endpoint",
                endpoint.MapAsset,
                $"{endpoint.X},{endpoint.Y}",
                new Dictionary<string, object?>
                {
                    ["shop_id"] = endpoint.ShopId,
                    ["handler_key"] = endpoint.HandlerKey,
                    ["tokens"] = endpoint.Tokens,
                    ["raw_action"] = endpoint.RawAction,
                    ["resolution"] = endpoint.Resolution
                }));
            AddEdge(edges, "map:" + endpoint.MapAsset, id, "declares_shop_endpoint",
                endpoint.MapAsset, $"Action[{endpoint.X},{endpoint.Y}]");
            if (endpoint.ShopId is not null)
            {
                AddEdge(edges, id, "shop:" + endpoint.ShopId, "opens_shop",
                    endpoint.MapAsset, $"Action[{endpoint.X},{endpoint.Y}]");
            }
        }

        foreach (var schedule in access.NpcSchedules)
        {
            foreach (var entry in schedule.Entries)
            {
                var entryId = "npc_schedule:" + Hash(
                    schedule.AssetName + "\n" + entry.ScheduleKey + "\n" + entry.RawSha256);
                AddNode(nodes, new(
                    entryId,
                    "npc_schedule_entry",
                    schedule.AssetName,
                    entry.ScheduleKey,
                    new Dictionary<string, object?>
                    {
                        ["npc_name"] = schedule.NpcName,
                        ["raw_sha256"] = entry.RawSha256,
                        ["segment_count"] = entry.Segments.Count,
                        ["selection_authority"] = schedule.SelectionAuthority
                    }));
                AddEdge(edges, AssetId(schedule.AssetName), entryId, "declares_npc_schedule",
                    schedule.AssetName, entry.ScheduleKey);
                foreach (var segment in entry.Segments)
                {
                    var segmentId = entryId + ":segment:" + segment.Index;
                    AddNode(nodes, new(
                        segmentId,
                        "npc_schedule_segment",
                        schedule.AssetName,
                        entry.ScheduleKey,
                        new Dictionary<string, object?>
                        {
                            ["index"] = segment.Index,
                            ["kind"] = segment.Kind,
                            ["raw"] = segment.Raw,
                            ["tokens"] = segment.Tokens,
                            ["time"] = segment.Time,
                            ["arrival_time"] = segment.ArrivalTime,
                            ["location"] = segment.Location,
                            ["x"] = segment.X,
                            ["y"] = segment.Y,
                            ["facing"] = segment.Facing
                        }));
                    AddEdge(edges, entryId, segmentId, "has_npc_schedule_segment",
                        schedule.AssetName, entry.ScheduleKey);
                }
            }
        }
    }

    private static void AddConditions(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        IReadOnlyList<ExecutableConditionRule> conditions,
        IReadOnlyDictionary<int, HandlerSemanticSurface> surfaces)
    {
        foreach (var condition in conditions)
        {
            var conditionId = "condition:" + Hash(
                condition.SourceAsset + "\n" + condition.SourcePath + "\n" + condition.RawSha256);
            AddNode(nodes, new(
                conditionId,
                "runtime_condition",
                condition.SourceAsset,
                condition.SourcePath,
                new Dictionary<string, object?>
                {
                    ["raw_sha256"] = condition.RawSha256,
                    ["clause_count"] = condition.Clauses.Count,
                    ["evaluation_contract"] = "native_game_state_query"
                }));
            AddEdge(edges, AssetId(condition.SourceAsset), conditionId, "declares_condition",
                condition.SourceAsset, condition.SourcePath);

            for (var index = 0; index < condition.Clauses.Count; index++)
            {
                var clause = condition.Clauses[index];
                var clauseId = conditionId + ":clause:" + index;
                var ruleId = clause.Handler?.OperationRuleId;
                var classification = Classify(ruleId, surfaces, "native_runtime_evaluable");
                AddNode(nodes, new(
                    clauseId,
                    "runtime_condition_clause",
                    condition.SourceAsset,
                    condition.SourcePath,
                    new Dictionary<string, object?>
                    {
                        ["index"] = index,
                        ["negated"] = clause.Negated,
                        ["tokens"] = clause.Tokens,
                        ["handler_family"] = clause.Handler?.Family,
                        ["handler_key"] = clause.Handler?.Key,
                        ["operation_rule_id"] = ruleId,
                        ["authority_classification"] = classification
                    }));
                AddEdge(edges, conditionId, clauseId, "has_condition_clause",
                    condition.SourceAsset, condition.SourcePath);
                if (ruleId is not null)
                    AddEdge(edges, clauseId, OperationRuleId(ruleId.Value), "evaluated_by_native_rule",
                        condition.SourceAsset, condition.SourcePath);
            }
        }
    }

    private static void AddEvents(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        IReadOnlyList<ExecutableEventRule> events,
        IReadOnlyDictionary<int, HandlerSemanticSurface> surfaces)
    {
        foreach (var eventRule in events)
        {
            var eventId = "event:" + Hash(
                eventRule.SourceAsset + "\n" + eventRule.EventKey + "\n" + eventRule.ScriptSha256);
            AddNode(nodes, new(
                eventId,
                "runtime_event",
                eventRule.SourceAsset,
                eventRule.EventKey,
                new Dictionary<string, object?>
                {
                    ["event_id"] = eventRule.EventId,
                    ["script_sha256"] = eventRule.ScriptSha256,
                    ["precondition_count"] = eventRule.Preconditions.Count,
                    ["command_count"] = eventRule.Commands.Count,
                    ["execution_contract"] = "native_event_engine"
                }));
            AddEdge(edges, AssetId(eventRule.SourceAsset), eventId, "declares_event",
                eventRule.SourceAsset, eventRule.EventKey);

            AddEventTokens(nodes, edges, eventRule, eventId, eventRule.Preconditions, "precondition",
                "has_event_precondition", "evaluated_by_native_rule", surfaces);
            AddEventTokens(nodes, edges, eventRule, eventId, eventRule.Commands, "command",
                "has_event_command", "executed_by_native_rule", surfaces);
        }
    }

    private static void AddEventTokens(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        ExecutableEventRule eventRule,
        string eventId,
        IReadOnlyList<ExecutableEventToken> tokens,
        string tokenKind,
        string membershipKind,
        string bindingKind,
        IReadOnlyDictionary<int, HandlerSemanticSurface> surfaces)
    {
        for (var index = 0; index < tokens.Count; index++)
        {
            var token = tokens[index];
            var tokenId = eventId + ":" + tokenKind + ":" + index;
            var ruleId = token.Handler?.OperationRuleId;
            AddNode(nodes, new(
                tokenId,
                "runtime_event_" + tokenKind,
                eventRule.SourceAsset,
                eventRule.EventKey,
                new Dictionary<string, object?>
                {
                    ["index"] = index,
                    ["raw"] = token.Raw,
                    ["key"] = token.Key,
                    ["negated"] = token.Negated,
                    ["tokens"] = token.Tokens,
                    ["operation_rule_id"] = ruleId,
                    ["authority_classification"] = Classify(
                        ruleId,
                        surfaces,
                        tokenKind == "command" ? "native_event_engine_executable" : "native_runtime_evaluable")
                }));
            AddEdge(edges, eventId, tokenId, membershipKind, eventRule.SourceAsset, eventRule.EventKey);
            if (ruleId is not null)
                AddEdge(edges, tokenId, OperationRuleId(ruleId.Value), bindingKind,
                    eventRule.SourceAsset, eventRule.EventKey);
        }
    }

    private static void AddTriggerActions(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        IReadOnlyList<ExecutableTriggerActionRule> triggerActions,
        IReadOnlyDictionary<int, HandlerSemanticSurface> surfaces)
    {
        const string sourceAsset = "Data/TriggerActions";
        foreach (var entry in triggerActions)
        {
            var entryId = "trigger_action:" + Hash(entry.SourcePath + "\n" + entry.Id);
            AddNode(nodes, new(
                entryId,
                "runtime_trigger_action_entry",
                sourceAsset,
                entry.SourcePath,
                new Dictionary<string, object?>
                {
                    ["id"] = entry.Id,
                    ["triggers"] = entry.TriggerTokens,
                    ["host_only"] = entry.HostOnly,
                    ["mark_action_applied"] = entry.MarkActionApplied,
                    ["action_count"] = entry.Actions.Count,
                    ["execution_contract"] = "native_trigger_action_manager"
                }));
            AddEdge(edges, AssetId(sourceAsset), entryId, "declares_trigger_action",
                sourceAsset, entry.SourcePath);

            for (var index = 0; index < entry.Actions.Count; index++)
            {
                var action = entry.Actions[index];
                var actionId = entryId + ":action:" + index;
                var ruleId = action.Handler?.OperationRuleId;
                AddNode(nodes, new(
                    actionId,
                    "runtime_trigger_action",
                    sourceAsset,
                    action.SourcePath,
                    new Dictionary<string, object?>
                    {
                        ["index"] = index,
                        ["raw"] = action.Raw,
                        ["tokens"] = action.Tokens,
                        ["parse_error"] = action.Error,
                        ["handler_family"] = action.Handler?.Family,
                        ["handler_key"] = action.Handler?.Key,
                        ["operation_rule_id"] = ruleId,
                        ["authority_classification"] = Classify(
                            ruleId,
                            surfaces,
                            "native_trigger_action_executable")
                    }));
                AddEdge(edges, entryId, actionId, "has_trigger_action",
                    sourceAsset, action.SourcePath);
                if (ruleId is not null)
                {
                    AddEdge(edges, actionId, OperationRuleId(ruleId.Value), "executed_by_native_rule",
                        sourceAsset, action.SourcePath);
                }
            }
        }
    }

    private static void AddDataMethods(
        IDictionary<string, GraphNode> nodes,
        ICollection<GraphEdge> edges,
        IReadOnlyList<ExecutableDataMethodRule> methods,
        IReadOnlyDictionary<int, HandlerSemanticSurface> surfaces)
    {
        foreach (var method in methods)
        {
            var methodId = "data_method_reference:" + Hash(
                method.SourceAsset + "\n" + method.SourcePath + "\n" + method.RawReference);
            AddNode(nodes, new(
                methodId,
                "runtime_data_method_reference",
                method.SourceAsset,
                method.SourcePath,
                new Dictionary<string, object?>
                {
                    ["raw_reference"] = method.RawReference,
                    ["resolution_status"] = method.ResolutionStatus,
                    ["binding_count"] = method.Bindings.Count
                }));
            AddEdge(edges, AssetId(method.SourceAsset), methodId, "declares_data_method",
                method.SourceAsset, method.SourcePath);
            foreach (var binding in method.Bindings)
            {
                if (binding.OperationRuleId is not int ruleId)
                    continue;
                AddEdge(edges, methodId, OperationRuleId(ruleId), "implemented_by_native_rule",
                    method.SourceAsset,
                    method.SourcePath,
                    new Dictionary<string, object?>
                    {
                        ["authority_classification"] = Classify(
                            ruleId,
                            surfaces,
                            "native_data_method_bound")
                    });
            }
        }
    }

    private static string Classify(
        int? ruleId,
        IReadOnlyDictionary<int, HandlerSemanticSurface> surfaces,
        string closedClassification)
    {
        if (ruleId is null || !surfaces.TryGetValue(ruleId.Value, out var surface))
            return "blocking_unresolved_native_rule";
        return surface.RuntimeBoundaries.Count == 0
            ? closedClassification + "_static_surface_closed"
            : closedClassification + "_runtime_context_required";
    }

    private static void AddNode(IDictionary<string, GraphNode> nodes, GraphNode node) =>
        nodes.TryAdd(node.Id, node);

    private static void AddEdge(
        ICollection<GraphEdge> edges,
        string from,
        string to,
        string kind,
        string asset,
        string path,
        IReadOnlyDictionary<string, object?>? attributes = null) =>
        edges.Add(new(from, to, kind, asset, path, attributes ?? new Dictionary<string, object?>()));

    private static string AssetId(string asset) => "asset:" + asset;
    private static string OperationRuleId(int ruleId) => "native_operation_rule:" + ruleId;
    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}

internal sealed record AuthoritativeDependencyGraph(
    IReadOnlyList<GraphNode> Nodes,
    IReadOnlyList<GraphEdge> Edges,
    IReadOnlyDictionary<string, int> NodeKinds,
    IReadOnlyDictionary<string, int> EdgeKinds);
