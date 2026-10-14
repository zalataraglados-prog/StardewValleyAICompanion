using System.Text.Json;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueTerminalCandidateRebind()
    {
        var before = RevenueRebindSnapshot(
            "rebind-before",
            new { slot_index = 0, qualified_item_id = "(O)16", quality = 0, stack = 3 });
        var after = RevenueRebindSnapshot(
            "rebind-after",
            new { slot_index = 0, qualified_item_id = "(O)16", quality = 0, stack = 3 },
            new { slot_index = 2, qualified_item_id = "(O)16", quality = 0, stack = 1 });
        var increased = MoneyRevenueTerminalCandidateRebindBuilder
            .IncreasedSlots(
                before,
                after,
                "(O)16",
                minimumQuality: 0,
                requiredAmount: 1,
                expectedNetIncrease: 1);
        Require(increased.Length == 1 &&
                increased[0].SlotIndex == 2 &&
                increased[0].QuantityIncrease == 1,
            "Revenue terminal rebind did not isolate the newly produced inventory slot.");

        var direction = RevenueRebindDirection();
        var freshCatalog = MoneyRevenueCandidateDenominatorBuilder.BuildCatalog(
            after,
            "rebind-after.json",
            new string('1', 64),
            "rebind-dependencies.json",
            new string('2', 64),
            direction,
            RevenueRebindAvailability(after.StateHash));
        var sale = MoneyRevenueTerminalCandidateRebindBuilder
            .ReboundCandidates(
                RevenueRebindSkeleton("sale", "economy.sell_items"),
                "(O)16",
                increased,
                freshCatalog);
        var shipping = MoneyRevenueTerminalCandidateRebindBuilder
            .ReboundCandidates(
                RevenueRebindSkeleton("ship", "economy.ship_items"),
                "(O)16",
                increased,
                freshCatalog);
        Require(sale.Length == 2 &&
                sale.All(row => row.TerminalCandidate.SlotIndex == 2) &&
                sale.Select(row => row.TerminalCandidate.ShopId)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(new[] { "FishShop", "SeedShop" }) &&
                shipping.Length == 1 &&
                shipping[0].TerminalCandidate.SlotIndex == 2 &&
                sale.Concat(shipping).All(row =>
                    row.ConcreteAlternativeId.StartsWith(
                        "revenue-concrete:",
                        StringComparison.Ordinal) &&
                    row.ConcreteAlternativeSha256.Length == 64),
            "Revenue terminal rebind omitted a shop path or admitted an unchanged inventory slot.");

        var rejected = false;
        try
        {
            _ = MoneyRevenueTerminalCandidateRebindBuilder.IncreasedSlots(
                before,
                after,
                "(O)16",
                minimumQuality: 0,
                requiredAmount: 1,
                expectedNetIncrease: 2);
        }
        catch (InvalidDataException)
        {
            rejected = true;
        }
        Require(rejected,
            "Revenue terminal rebind admitted a slot delta that contradicted its fresh receipt.");
    }

    private static SnapshotEnvelope RevenueRebindSnapshot(
        string stateHash,
        params object[] inventory) => new()
        {
            StateHash = stateHash,
            State = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["player"] = JsonSerializer.SerializeToElement(new
                {
                    inventory = new
                    {
                        status = "available",
                        value = inventory
                    }
                })
            }
        };

    private static MoneyRevenueCandidateDenominatorBuilder
        .RevenueDirectionDefinition RevenueRebindDirection()
    {
        using var document = JsonDocument.Parse("""
            {
              "schema_version": "goal_method_dependency_expansions.v1",
              "directions": [{
                "direction_id": "earn_money",
                "status": "complete",
                "completion_node_id": "complete",
                "blockers": [],
                "nodes": [
                  {"id":"complete","kind":"terminal_fact"},
                  {"id":"milestone","kind":"state_fact"},
                  {"id":"forage","kind":"policy_option_transition","option_id":"foraging.collect_spawned_objects","candidate_kinds":["collect_spawned_object"],"required_field_paths":[]},
                  {"id":"sell","kind":"policy_option_transition","option_id":"economy.sell_items","candidate_kinds":["sell_shop_item"],"required_field_paths":[]},
                  {"id":"ship","kind":"policy_option_transition","option_id":"economy.ship_items","candidate_kinds":["ship_inventory_item_to_bin"],"required_field_paths":[]},
                  {"id":"sleep","kind":"deterministic_transition","option_id":"recovery.stabilize_day","candidate_kinds":["recovery_sleep_immediately"],"required_field_paths":[]}
                ],
                "edges": [
                  {"from":"forage","to":"sell","kind":"authorizes_alternative"},
                  {"from":"forage","to":"ship","kind":"authorizes_alternative"},
                  {"from":"sell","to":"milestone","kind":"advances_immediately"},
                  {"from":"ship","to":"sleep","kind":"requires_later_settlement"},
                  {"from":"sleep","to":"milestone","kind":"advances_after_day_transition"},
                  {"from":"milestone","to":"complete","kind":"terminates_at"}
                ]
              }]
            }
            """);
        return MoneyRevenueCandidateDenominatorBuilder.ReadDirection(
            document.RootElement);
    }

    private static MoneyRevenueAlternativeSkeleton RevenueRebindSkeleton(
        string id,
        string terminalOptionId) => new()
        {
            AlternativeId = "rebind." + id,
            SourceCatalogCandidateId = "forage::fixture",
            SourceCandidateId = "forage:fixture",
            SourceCandidateKind = "collect_spawned_object",
            SourceOptionId = "foraging.collect_spawned_objects",
            TerminalOptionId = terminalOptionId,
            ProofShape = "acquisition_or_processing_rollout_then_terminal",
            RequiresAcquisitionOrProcessingRollout = true,
            RequiresFreshTerminalCandidateRebind = true,
            ConcreteAlternativeIdentityComplete = false,
            PathSha256 = id == "sale"
                ? new string('a', 64)
                : new string('b', 64)
        };
}
