using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueCandidateDenominator()
    {
        using var document = JsonDocument.Parse("""
            {
              "schema_version": "goal_method_dependency_expansions.v1",
              "directions": [
                {
                  "direction_id": "earn_money",
                  "status": "in_progress",
                  "completion_node_id": "dependency.earn_money.complete",
                  "blockers": ["fresh_save_schedule_pending"],
                  "nodes": [
                    {
                      "id": "dependency.earn_money.complete",
                      "kind": "terminal_fact"
                    },
                    {
                      "id": "dependency.earn_money.milestone",
                      "kind": "state_fact"
                    },
                    {
                      "id": "dependency.earn_money.forage",
                      "kind": "policy_option_transition",
                      "option_id": "foraging.collect_spawned_objects",
                      "candidate_kinds": ["collect_spawned_object"],
                      "required_field_paths": ["current_location.objects"]
                    },
                    {
                      "id": "dependency.earn_money.reserve",
                      "kind": "state_gate"
                    },
                    {
                      "id": "dependency.earn_money.sell",
                      "kind": "policy_option_transition",
                      "option_id": "economy.sell_items",
                      "candidate_kinds": ["sell_shop_item"],
                      "required_field_paths": ["player.inventory"]
                    },
                    {
                      "id": "dependency.earn_money.ship",
                      "kind": "policy_option_transition",
                      "option_id": "economy.ship_items",
                      "candidate_kinds": ["ship_inventory_item_to_bin"],
                      "required_field_paths": ["farm.shipping_bins"]
                    },
                    {
                      "id": "dependency.earn_money.sleep",
                      "kind": "deterministic_transition",
                      "option_id": "recovery.stabilize_day",
                      "candidate_kinds": ["recovery_sleep_immediately"],
                      "required_field_paths": ["time.total_days"]
                    }
                  ],
                  "edges": [
                    { "from": "dependency.earn_money.forage", "to": "dependency.earn_money.reserve", "kind": "may_produce" },
                    { "from": "dependency.earn_money.reserve", "to": "dependency.earn_money.sell", "kind": "authorizes_alternative" },
                    { "from": "dependency.earn_money.reserve", "to": "dependency.earn_money.ship", "kind": "authorizes_alternative" },
                    { "from": "dependency.earn_money.sell", "to": "dependency.earn_money.milestone", "kind": "advances_immediately" },
                    { "from": "dependency.earn_money.ship", "to": "dependency.earn_money.sleep", "kind": "requires_later_settlement" },
                    { "from": "dependency.earn_money.sleep", "to": "dependency.earn_money.milestone", "kind": "advances_after_day_transition" },
                    { "from": "dependency.earn_money.milestone", "to": "dependency.earn_money.complete", "kind": "terminates_at" }
                  ]
                }
              ]
            }
            """);
        var direction = MoneyRevenueCandidateDenominatorBuilder.ReadDirection(
            document.RootElement);
        var snapshot = new SnapshotEnvelope
        {
            StateHash = "revenue-denominator-state"
        };
        var availability = RevenueDenominatorAvailability(
            snapshot.StateHash,
            includeShipOption: true,
            shipKind: "route_connector_tile");
        var catalog = MoneyRevenueCandidateDenominatorBuilder.BuildCatalog(
            snapshot,
            "snapshot.json",
            new string('1', 64),
            "dependencies.json",
            new string('2', 64),
            direction,
            availability);
        Require(
            catalog.NodeCoverageComplete &&
            catalog.CurrentSnapshotDenominatorAuthoritative &&
            catalog.CurrentComparisonDenominatorReady &&
            !catalog.LongHorizonDenominatorAuthoritative &&
            !catalog.ProductionTeacherPreferenceLabelEligible &&
            !catalog.FormalProductTrainingAuthorized &&
            catalog.PolicyNodeCount == 3 &&
            catalog.DeterministicSupportNodeCount == 1 &&
            catalog.ComparisonCandidateCount == 3 &&
            catalog.ComparisonCandidateIds.Length == 3 &&
            catalog.Nodes.SelectMany(node => node.Candidates)
                .All(candidate => candidate.CandidateSha256.Length == 64) &&
            catalog.CatalogSha256.Length == 64,
            "Current Revenue candidate denominator did not preserve its bounded authority.");
        Require(
            catalog.Nodes.Single(node =>
                    node.OptionId == "economy.ship_items")
                .Candidates.Single().SemanticKindBinding ==
                    "staged_continuation",
            "Revenue catalog did not bind a rolling staged continuation.");
        var alternatives = MoneyRevenueAlternativeDenominatorBuilder
            .BuildCatalog(
                "candidate-catalog.json",
                new string('3', 64),
                catalog,
                direction);
        Require(
            alternatives.AlternativeSkeletonDenominatorComplete &&
            !alternatives.CurrentAlternativeDenominatorAuthoritative &&
            !alternatives.LongHorizonDenominatorAuthoritative &&
            !alternatives.ProductionTeacherPreferenceLabelEligible &&
            !alternatives.FormalProductTrainingAuthorized &&
            alternatives.SourceCandidateCount == 3 &&
            alternatives.AlternativeCount == 4 &&
            alternatives.AlternativeIds.Length == 4 &&
            alternatives.Alternatives.Count(row =>
                row.SourceOptionId ==
                    "foraging.collect_spawned_objects") == 2 &&
            alternatives.Alternatives.Count(row =>
                row.RequiresFreshTerminalCandidateRebind) == 2 &&
            alternatives.Alternatives.Any(row =>
                row.ProofShape == "direct_native_shop_sale") &&
            alternatives.Alternatives.Any(row =>
                row.ProofShape ==
                    "direct_native_shipping_deposit_and_day_settlement") &&
            alternatives.CatalogSha256.Length == 64,
            "Revenue source candidates did not expand through all settlement paths.");

        var disconnected = MoneyRevenueAlternativeDenominatorBuilder
            .BuildCatalog(
                "candidate-catalog.json",
                new string('3', 64),
                catalog,
                direction with
                {
                    Edges = direction.Edges.Where(edge =>
                            edge.FromNodeId !=
                                "dependency.earn_money.forage")
                        .ToArray()
                });
        Require(
            !disconnected.AlternativeSkeletonDenominatorComplete &&
            disconnected.BlockingReasons.Any(reason =>
                reason.StartsWith(
                    "source_candidate_has_no_settlement_path:",
                    StringComparison.Ordinal)),
            "A disconnected Revenue source candidate was omitted silently.");

        var missingOption = MoneyRevenueCandidateDenominatorBuilder.BuildCatalog(
            snapshot,
            "snapshot.json",
            new string('1', 64),
            "dependencies.json",
            new string('2', 64),
            direction,
            RevenueDenominatorAvailability(
                snapshot.StateHash,
                includeShipOption: false,
                shipKind: "route_connector_tile"));
        Require(
            !missingOption.CurrentSnapshotDenominatorAuthoritative &&
            missingOption.BlockingReasons.Contains(
                "revenue_node_enumeration_incomplete:" +
                "dependency.earn_money.ship",
                StringComparer.Ordinal),
            "A missing Revenue option evaluation was admitted as authoritative.");

        var unboundStage = MoneyRevenueCandidateDenominatorBuilder.BuildCatalog(
            snapshot,
            "snapshot.json",
            new string('1', 64),
            "dependencies.json",
            new string('2', 64),
            direction,
            RevenueDenominatorAvailability(
                snapshot.StateHash,
                includeShipOption: true,
                shipKind: "unrelated_stage"));
        Require(
            !unboundStage.CurrentSnapshotDenominatorAuthoritative &&
            !unboundStage.CurrentComparisonDenominatorReady,
            "An unbound Revenue candidate kind entered the denominator.");
    }

    private static OptionAvailabilityEnvelope RevenueDenominatorAvailability(
        string stateHash,
        bool includeShipOption,
        string shipKind)
    {
        var options = new List<OptionAvailability>
        {
            ReadyRevenueOption(
                "foraging.collect_spawned_objects",
                eventCandidates: new[]
                {
                    new EventCandidate
                    {
                        CandidateId = "forage:Town:12,8:16",
                        Kind = "collect_spawned_object",
                        Available = true,
                        LocationId = "Town",
                        TileX = 12,
                        TileY = 8,
                        QualifiedItemId = "(O)16",
                        Quantity = 1,
                        UnitPrice = 80,
                        TotalValue = 80
                    }
                }),
            ReadyRevenueOption(
                "economy.sell_items",
                economicCandidates: new[]
                {
                    new EconomicCandidate
                    {
                        CandidateId = "sell:0",
                        Kind = "sell_shop_item",
                        Available = true,
                        QualifiedItemId = "(O)24",
                        ShopId = "SeedShop",
                        SlotIndex = 0,
                        Quantity = 1,
                        UnitPrice = 35,
                        TotalValue = 35
                    }
                }),
            ReadyRevenueOption("recovery.stabilize_day")
        };
        if (includeShipOption)
        {
            options.Add(ReadyRevenueOption(
                "economy.ship_items",
                eventCandidates: new[]
                {
                    new EventCandidate
                    {
                        CandidateId = "ship:Farm:71,14:1:24:route:FarmHouse",
                        Kind = shipKind,
                        Available = true,
                        QualifiedItemId = "(O)24",
                        SlotIndex = 1,
                        Quantity = 1,
                        UnitPrice = 35,
                        TotalValue = 35,
                        Parameters = shipKind == "route_connector_tile"
                            ? new[]
                            {
                                new SmallModelActionParameter
                                {
                                    Name = "continuation.option_id",
                                    Value = "economy.ship_items"
                                }
                            }
                            : Array.Empty<SmallModelActionParameter>()
                    }
                }));
        }
        return new OptionAvailabilityEnvelope
        {
            StateHash = stateHash,
            Options = options.ToArray()
        };
    }

    private static OptionAvailability ReadyRevenueOption(
        string optionId,
        EventCandidate[]? eventCandidates = null,
        EconomicCandidate[]? economicCandidates = null) => new()
        {
            OptionId = optionId,
            Available = true,
            ReadEligible = true,
            Status = "available",
            EventCandidates = eventCandidates ?? Array.Empty<EventCandidate>(),
            EconomicCandidates = economicCandidates ??
                Array.Empty<EconomicCandidate>()
        };
}
