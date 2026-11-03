using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Infrastructure;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private sealed class RustyKeyPortfolioFixture
    {
        public SnapshotEnvelope Snapshot { get; }
        public AuthoritativeRequirementInventoryReport Inventory { get; }
        public AcquisitionRoutePortfolioProposal Proposal { get; }
        public AcquisitionRouteTargetDateOpportunityCostReport Opportunity { get; }
        public StrategyCommitmentLedger Ledger { get; }

        public RustyKeyPortfolioFixture(int donated = 59, int? selectedCount = null)
        {
            var groups = Enumerable.Range(0, 95).Select(index => new GoalRequirementGroup
            {
                RequirementId = "museum:item:fixture_" + index, SelectionRule = "all_required",
                RequiredAlternativeCount = 1, RouteCovered = true,
                Alternatives = new[] { new GoalRequirementAlternative
                {
                    ItemId = "fixture_" + index, QualifiedItemId = "(O)fixture_" + index,
                    MatchKind = "item_id", Amount = 1
                } }
            }).ToArray();
            Inventory = new()
            {
                GoalId = GoalMethodTeacherCoverageGoalIds.AcquisitionCorpus, GameVersion = "1.6.15",
                DenominatorComplete = true, AcquisitionRoutesComplete = true,
                RequirementSets = new[] { new GoalRequirementSet
                {
                    RequirementSetId = "museum_collection", DenominatorStatus = "complete",
                    RequiredGroupCount = 95, RouteCoveredGroupCount = 95,
                    AcquisitionRoutesComplete = true, Groups = groups
                } }
            };
            Snapshot = new()
            {
                GameVersion = Inventory.GameVersion,
                SaveId = new() { Value = "threshold-fixture", Status = FieldStatus.Available },
                PlayerId = new() { Value = "1", Status = FieldStatus.Available },
                State = new()
                {
                    ["time"] = JsonSerializer.SerializeToElement(new
                    {
                        total_days = new { value = 0, status = "available" }
                    }),
                    ["identity"] = JsonSerializer.SerializeToElement(new
                    {
                        save_id = new { value = "threshold-fixture", status = "available" },
                        player_id = new { value = "1", status = "available" }
                    }),
                    ["world_progress"] = JsonSerializer.SerializeToElement(new
                    {
                        museum = new { status = "available", value = new
                        {
                            donated_count = donated, total_donatable_items = 95, missing_item_count = 95 - donated,
                            collection_complete = donated == 95,
                            missing_item_ids = groups.Skip(donated).Select(group => group.Alternatives[0].ItemId),
                            donatable_items = groups.Select((group, index) => new
                            {
                                item_id = group.Alternatives[0].ItemId, qualified_item_id = group.Alternatives[0].QualifiedItemId,
                                donated = index < donated
                            }),
                            rusty_key_donation_threshold = 60, rusty_key_reward_id = "museum60",
                            rusty_key_reward_action = "MarkEventSeen Host 295672"
                        } }
                    }),
                    ["farm"] = JsonSerializer.SerializeToElement(new
                    {
                        material_inventory_graph = new { status = "available", value = new
                        {
                            schema_version = "material_inventory_graph.v1", status = "available", player_id = 1,
                            inventory_nodes = new[] { new
                            {
                                node_id = "player:1", inventory_kind = "player_inventory", supply_state = "available",
                                owner_player_id = 1, ownership_class = "actor_owned", actor_use_authorized = true,
                                slots = new[] { new { slot_index = 0, item_id = "388", qualified_item_id = "(O)388", stack = 1 } }
                            } }
                        } }
                    })
                }
            };
            Opportunity = new()
            {
                GoalId = Inventory.GoalId, GameVersion = Inventory.GameVersion, RouteOccurrenceInventoryComplete = true,
                Routes = groups.Select((group, index) => Route(group, index)).ToArray(), RouteOccurrenceCount = 95
            };
            var count = selectedCount ?? Math.Max(0, 60 - donated);
            Proposal = new()
            {
                ProposalId = "fixture-threshold", GoalId = Inventory.GoalId,
                ScopedRequirements = groups.Skip(donated).Take(count)
                    .Select(group => new AcquisitionRoutePortfolioRequirementScope("museum_collection", group.RequirementId)).ToArray(),
                SelectedRouteOccurrenceIds = Opportunity.Routes.Skip(donated).Take(count).Select(route => route.RouteOccurrenceId).ToArray()
            };
            Ledger = new() { LedgerId = "fixture-ledger", SaveId = "threshold-fixture", PlayerId = "1" };
            Rehash();
        }

        public AcquisitionRoutePortfolioBuilder.AcquisitionRoutePortfolioBuildContext Context => new(
            Inventory, Opportunity, Snapshot, new AcquisitionStrategyLedgerState(Ledger, 1), new(),
            "fixture-inventory-sha", "fixture-opportunity-sha", "fixture-ledger-sha", "fixture-snapshot-sha");

        public RustyKeyAcquisitionPortfolioAdmission Run() => RustyKeyAcquisitionPortfolioBuilder.Build(Context, Proposal, "fixture-proposal-sha");

        public void Rehash()
        {
            Snapshot.StateHash = SnapshotHash.ComputeStateHash(Snapshot.State);
            Proposal.SnapshotStateHash = Snapshot.StateHash;
            Opportunity.SnapshotStateHash = Snapshot.StateHash;
            Ledger.SourceStateHash = Snapshot.StateHash;
        }

        public void ChangeMuseum(string field, JsonNode? value)
        {
            var world = JsonNode.Parse(Snapshot.State["world_progress"].GetRawText())!;
            world["museum"]!["value"]![field] = value;
            Snapshot.State["world_progress"] = JsonSerializer.SerializeToElement(world);
            Rehash();
        }

        public void AddMaterialClaim(int index)
        {
            var node = JsonSerializer.SerializeToNode(Opportunity.Routes[index], JsonDefaults.Options)!;
            var reservation = node;
            for (var layer = 0; layer < 4; layer++) reservation = reservation["upstream_route"]!;
            reservation["claim_disposition"] = "claim_proposed";
            reservation["claim_set"] = JsonSerializer.SerializeToNode(new AcquisitionRouteReservationClaimSet(
                "fixture-claim", Snapshot.StateHash, Ledger.Revision, true, false, Array.Empty<string>(),
                new[] { new MaterialReservationUpsertRequest
                {
                    StateHash = Snapshot.StateHash, ReservationId = "claim-" + index,
                    SourceDecisionId = "fixture-route-" + index, GoalId = Inventory.GoalId,
                    NodeId = "player:1", QualifiedItemId = "(O)388", Quantity = 1, Purpose = "fixture-input"
                } }, Array.Empty<CurrencyReservationUpsertRequest>()), JsonDefaults.Options);
            Opportunity.Routes[index] = node.Deserialize<AcquisitionRouteTargetDateOpportunityCost>(JsonDefaults.Options)!;
        }

        private static AcquisitionRouteTargetDateOpportunityCost Route(GoalRequirementGroup group, int index)
        {
            // Synthetic axis chain tests portfolio composition, not native acquisition.
            JsonNode node = JsonSerializer.SerializeToNode(new
            {
                route_occurrence_id = "route-" + index, requirement_set_id = "museum_collection",
                requirement_id = group.RequirementId, alternative_index = 0, required_amount = 1,
                qualified_item_id = group.Alternatives[0].QualifiedItemId
            })!;
            for (var layer = 9; layer >= 0; layer--)
            {
                node = new JsonObject { ["upstream_route"] = node };
                if (layer == 4) node["claim_disposition"] = "not_required";
            }
            node["route_occurrence_id"] = "route-" + index;
            node["opportunity_cost_axis_resolved"] = true;
            node["opportunity_cost_matches_target_date"] = true;
            node["opportunity_cost_axis_status"] = "resolved_opportunity_cost_pareto_frontier";
            node["dominated_by_route_occurrence_ids"] = new JsonArray();
            node["non_matching_reasons"] = new JsonArray();
            node["blocking_reasons"] = new JsonArray();
            node["cost_vector"] = JsonSerializer.SerializeToNode(new AcquisitionOpportunityCostVector(
                2, 0, 0, Array.Empty<AcquisitionOpportunityMaterialCost>(), Array.Empty<AcquisitionOpportunityCurrencyCost>(),
                new[] { "fixture-cost-only" }), JsonDefaults.Options);
            return node.Deserialize<AcquisitionRouteTargetDateOpportunityCost>(JsonDefaults.Options)!;
        }
    }
}
