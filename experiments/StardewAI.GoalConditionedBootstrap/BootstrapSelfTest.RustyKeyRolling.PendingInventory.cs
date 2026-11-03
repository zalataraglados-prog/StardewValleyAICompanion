using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyRollingPendingInventory(List<string> passed)
    {
        foreach (var donated in new[] { 58, 59 })
        {
            var fixture = new RustyKeyPortfolioFixture(donated);
            fixture.EnableHeldDonation();
            fixture.ChangeMuseum("museum_mutex_locked", JsonValue.Create(true));
            var result = fixture.RunRolling();
            Require(result.SelectedCandidateId != "route-59" && !result.ThresholdSatisfied &&
                result.CompleteMuseumItemDenominator.Length == 95 &&
                (donated == 58 ? result.RollingStepReady : !result.RollingStepReady),
                "A safely owned pending donation must not be acquired again while donation is unavailable.");
            passed.Add("pending_donation_not_reacquired_deficit_" + (60 - donated));
        }
        foreach (var reserved in new[] { 1, 2 })
        {
            var fixture = new RustyKeyPortfolioFixture();
            fixture.EnableHeldDonation();
            fixture.ChangeMuseum("museum_mutex_locked", JsonValue.Create(true));
            fixture.Ledger.MaterialReservations = new[] { new MaterialReservation
            {
                ReservationId = "pending-other", SourceDecisionId = "other", GoalId = "other.goal", Revision = 1,
                OwnerPlayerId = 1, SourceStateHash = fixture.Snapshot.StateHash, NodeId = "player:1", SlotIndex = 0,
                QualifiedItemId = "(O)fixture_59", Quantity = reserved, Purpose = "protected other target"
            } };
            var result = fixture.RunRolling();
            Require(result.RollingStepReady == (reserved == 2) && !result.ThresholdSatisfied &&
                (reserved == 2 ? result.SelectedCandidateId == "route-59" : result.SelectedAcquisitionProposal is null),
                "Pending supply must subtract other-goal claims without cancelling them or counting protected units.");
            passed.Add("pending_donation_other_goal_reserved_" + reserved);
        }
        var unreadable = new RustyKeyPortfolioFixture();
        unreadable.Snapshot.State.Remove("farm");
        unreadable.Rehash();
        var blocked = unreadable.RunRolling();
        Require(!blocked.RollingStepReady && blocked.SelectedAcquisitionProposal is null,
            "Unknown material inventory cannot prove missing supply and authorize duplicate acquisition.");
        passed.Add("pending_inventory_unknown_fails_closed");
        foreach (var authorized in new[] { false, true })
        {
            var fixture = new RustyKeyPortfolioFixture();
            fixture.EnableHeldDonation();
            fixture.ChangeMuseum("museum_mutex_locked", JsonValue.Create(true));
            var farm = JsonSerializer.SerializeToNode(fixture.Snapshot.State["farm"])!;
            var node = farm["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!;
            node["node_id"] = "chest:fixture";
            node["inventory_kind"] = "chest";
            node["actor_use_authorized"] = authorized;
            fixture.Snapshot.State["farm"] = JsonSerializer.SerializeToElement(farm);
            fixture.Rehash();
            var result = fixture.RunRolling();
            Require(result.RollingStepReady == !authorized && !result.ThresholdSatisfied &&
                (result.PendingDonationRequirementIds is { Length: 1 }) == authorized,
                "Only accessible storage may count as pending supply, never as already donated or immediately executable.");
            passed.Add("pending_storage_actor_authorized_" + authorized);
        }
    }
}
