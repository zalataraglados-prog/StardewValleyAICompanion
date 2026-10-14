namespace StardewAI.GoalConditionedBootstrap;

public static partial class MoneyRevenueAlternativeDenominatorBuilder
{
    private static string SettledMilestoneNodeId(
        MoneyRevenueCandidateDenominatorBuilder.RevenueDirectionDefinition
            direction)
    {
        var predecessors = direction.Edges
            .Where(edge => edge.ToNodeId == direction.CompletionNodeId)
            .Select(edge => edge.FromNodeId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        Require(predecessors.Length == 1,
            "Revenue completion must have one settled-milestone predecessor.");
        var node = direction.AllNodes.SingleOrDefault(row =>
            row.NodeId == predecessors[0]);
        Require(node is not null && node.NodeKind == "state_fact",
            "Revenue completion predecessor is not a unique state fact.");
        return predecessors[0];
    }

    private static RevenuePath[] FindPaths(
        MoneyRevenueCandidateDenominatorBuilder.RevenueDirectionDefinition
            direction,
        string sourceNodeId,
        string targetNodeId)
    {
        const int maximumPathCount = 4096;
        Require(direction.AllNodes.Any(node =>
                    node.NodeId == sourceNodeId) &&
                direction.AllNodes.Any(node =>
                    node.NodeId == targetNodeId),
            "Revenue path endpoint is not present in the dependency graph.");
        var results = new List<RevenuePath>();
        Visit(
            sourceNodeId,
            new HashSet<string>(StringComparer.Ordinal) { sourceNodeId },
            new List<MoneyRevenueCandidateDenominatorBuilder
                .RevenueDependencyEdgeDefinition>());
        return results
            .OrderBy(path => string.Join("\u001f", path.NodeIds),
                StringComparer.Ordinal)
            .ToArray();

        void Visit(
            string nodeId,
            HashSet<string> visited,
            List<MoneyRevenueCandidateDenominatorBuilder
                .RevenueDependencyEdgeDefinition> pathEdges)
        {
            if (nodeId == targetNodeId)
            {
                Require(results.Count < maximumPathCount,
                    "Revenue dependency path count exceeded the bounded catalog limit.");
                results.Add(new RevenuePath(
                    new[] { sourceNodeId }
                        .Concat(pathEdges.Select(edge => edge.ToNodeId))
                        .ToArray(),
                    pathEdges.ToArray()));
                return;
            }
            foreach (var edge in direction.Edges
                         .Where(edge => edge.FromNodeId == nodeId)
                         .OrderBy(edge => edge.ToNodeId,
                             StringComparer.Ordinal)
                         .ThenBy(edge => edge.EdgeKind,
                             StringComparer.Ordinal))
            {
                if (!visited.Add(edge.ToNodeId))
                    continue;
                pathEdges.Add(edge);
                Visit(edge.ToNodeId, visited, pathEdges);
                pathEdges.RemoveAt(pathEdges.Count - 1);
                visited.Remove(edge.ToNodeId);
            }
        }
    }

    private static MoneyRevenueAlternativeSkeleton? BuildSkeleton(
        MoneyRevenueCandidateDenominatorCatalog source,
        MoneyRevenueCandidateDenominatorBuilder.RevenueDirectionDefinition
            direction,
        MoneyRevenueCandidateNodeCoverage sourceNode,
        MoneyRevenueCandidateCatalogEntry sourceCandidate,
        RevenuePath path,
        ICollection<string> blockers)
    {
        var pathNodeIds = path.NodeIds.ToHashSet(StringComparer.Ordinal);
        var terminalNodes = direction.Nodes
            .Where(node => pathNodeIds.Contains(node.NodeId))
            .Where(node => node.PolicyChoice)
            .Where(node => node.OptionId is
                "economy.sell_items" or "economy.ship_items")
            .ToArray();
        if (terminalNodes.Length != 1)
        {
            blockers.Add("settlement_path_terminal_count_not_one:" +
                sourceCandidate.CatalogCandidateId);
            return null;
        }
        var terminalOptionId = terminalNodes[0].OptionId;
        var shopSale = terminalOptionId == "economy.sell_items";
        var shipping = terminalOptionId == "economy.ship_items";
        var requiresRollout = sourceNode.OptionId is not
            "economy.sell_items" and not "economy.ship_items";
        var proofShape = requiresRollout
            ? shopSale
                ? "acquisition_or_processing_rollout_then_native_shop_sale"
                : "acquisition_or_processing_rollout_then_native_shipping_deposit_and_day_settlement"
            : shopSale
                ? "direct_native_shop_sale"
                : "direct_native_shipping_deposit_and_day_settlement";
        var pathEdges = path.Edges.Select(edge =>
                new MoneyRevenueAlternativePathEdge(
                    edge.FromNodeId,
                    edge.ToNodeId,
                    edge.EdgeKind))
            .ToArray();
        var pathSha256 =
            AcquisitionRoutePortfolioTeacherPreferenceBuilder.ArtifactSha256(
                new
                {
                    source.SourceStateHash,
                    source.DependencyExpansionSha256,
                    sourceCandidate.CatalogCandidateId,
                    sourceCandidate.CandidateSha256,
                    path.NodeIds,
                    pathEdges,
                    terminalOptionId,
                    proofShape
                });
        return new MoneyRevenueAlternativeSkeleton
        {
            AlternativeId = "revenue-alternative:" + pathSha256[..24],
            SourceCatalogCandidateId =
                sourceCandidate.CatalogCandidateId,
            SourceCandidateSha256 = sourceCandidate.CandidateSha256,
            SourceCandidateId = sourceCandidate.CandidateId,
            SourceCandidateKind = sourceCandidate.CandidateKind,
            SourceNodeId = sourceNode.NodeId,
            SourceOptionId = sourceNode.OptionId,
            TerminalOptionId = terminalOptionId,
            ProofShape = proofShape,
            RequiresAcquisitionOrProcessingRollout = requiresRollout,
            RequiresNativeShopSaleProof = shopSale,
            RequiresNativeShippingDepositProof = shipping,
            RequiresNativeShippingSettlementProof = shipping,
            RequiresFreshTerminalCandidateRebind = requiresRollout,
            ConcreteAlternativeIdentityComplete = !requiresRollout,
            PathNodeIds = path.NodeIds,
            PathEdges = pathEdges,
            PathSha256 = pathSha256
        };
    }

    private sealed record RevenuePath(
        string[] NodeIds,
        MoneyRevenueCandidateDenominatorBuilder
            .RevenueDependencyEdgeDefinition[] Edges);
}
