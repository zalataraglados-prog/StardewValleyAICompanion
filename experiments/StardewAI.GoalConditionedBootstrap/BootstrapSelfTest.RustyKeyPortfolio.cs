using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunRustyKeyPortfolio(string outputRoot)
    {
        var passed = new List<string>();
        foreach (var donated in Enumerable.Range(0, 60))
        {
            var fixture = new RustyKeyPortfolioFixture(donated);
            var originalLedger = JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options);
            var report = fixture.Run();
            Require(report.AcquisitionPortfolioReady && report.RemainingDonationCount == 60 - donated &&
                report.CompleteMuseumItemDenominator.Length == 95 &&
                report.CompleteMuseumItemDenominator.Count(item => item.SelectedForAcquisition) == 60 - donated &&
                report.CompleteMuseumItemDenominator.Count(item => item.AlreadyDonated) == donated &&
                report.AcquisitionPortfolio!.SelectedRouteOccurrenceIds.Length == 60 - donated &&
                !report.JointExecutionScheduleVerified && !report.DonationOutputCustodyVerified && !report.TeacherPreferenceVerified &&
                !report.NativeDonationAndKeyHandoffVerified && !report.FormalTrainingAuthorized &&
                originalLedger == JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options),
                "Rusty Key threshold composition or read-only ownership drifted.");
            passed.Add("exact_deficit_" + donated);
        }
        foreach (var donated in new[] { 60, 61, 95 })
        {
            var result = new RustyKeyPortfolioFixture(donated).Run();
            Require(result.ThresholdSatisfied && !result.AcquisitionPortfolioReady && result.AcquisitionPortfolio is null &&
                !result.NativeDonationAndKeyHandoffVerified && result.BlockingReasons.Length == 0,
                "Satisfied museum threshold must continue to native key handoff, not more acquisition.");
            passed.Add("threshold_only_not_key_" + donated);
        }
        Reject("undersupply", fixture => fixture.Proposal.ScopedRequirements = Array.Empty<AcquisitionRoutePortfolioRequirementScope>(),
            "rusty_key_selected_distinct_items_do_not_equal_live_deficit");
        Reject("oversupply", fixture => fixture.Proposal.ScopedRequirements = fixture.Proposal.ScopedRequirements.Append(
            new("museum_collection", "museum:item:fixture_60")).ToArray(), "rusty_key_selected_distinct_items_do_not_equal_live_deficit");
        Reject("duplicate_requirement", fixture => fixture.Proposal.ScopedRequirements = fixture.Proposal.ScopedRequirements
            .Concat(fixture.Proposal.ScopedRequirements).ToArray(), "rusty_key_scope_not_unique_museum_requirement");
        Reject("already_donated", fixture => fixture.Proposal.ScopedRequirements = new[] {
            new AcquisitionRoutePortfolioRequirementScope("museum_collection", "museum:item:fixture_0") },
            "rusty_key_selected_item_already_donated:");
        Reject("foreign_scope", fixture => fixture.Proposal.ScopedRequirements = new[] {
            new AcquisitionRoutePortfolioRequirementScope("full_shipment", "museum:item:fixture_59") },
            "rusty_key_scope_not_unique_museum_requirement");
        Reject("unrelated_release", fixture => fixture.Proposal.ReplacedRouteOccurrenceIds = new[] { "route-60" },
            "rusty_key_cannot_release_other_requirement_reservations:");
        Reject("selected_route_outside_scope", fixture => fixture.Proposal.SelectedRouteOccurrenceIds = new[] { "route-60" },
            "selected_route_outside_requirement_scope:");
        Reject("unknown_route", fixture => fixture.Proposal.SelectedRouteOccurrenceIds = new[] { "unknown-route" },
            "selected_route_occurrence_not_found:");
        Reject("stale_identity", fixture => fixture.Snapshot.SaveId.Status = FieldStatus.Stale,
            "rusty_key_snapshot_identity_or_hash_invalid");
        Reject("changed_hash", fixture => fixture.Snapshot.StateHash = "modified",
            "rusty_key_snapshot_identity_or_hash_invalid");
        Reject("changed_proposal_state", fixture => fixture.Proposal.SnapshotStateHash = "modified",
            "rusty_key_proposal_identity_invalid");
        Reject("missing_date", fixture => { fixture.Snapshot.State.Remove("time"); fixture.Rehash(); },
            "rusty_key_current_total_day_unavailable");
        Reject("past_supply", fixture => fixture.Opportunity.TargetTotalDay = -1,
            "rusty_key_supply_date_leaves_no_pre_year3_key_handoff");
        Reject("late_supply", fixture => fixture.Opportunity.TargetTotalDay = 223,
            "rusty_key_supply_date_leaves_no_pre_year3_key_handoff");
        Reject("modified_threshold", fixture => fixture.ChangeMuseum("rusty_key_donation_threshold", JsonValue.Create(59)),
            "rusty_key_live_reward_authority_invalid");
        Reject("modified_reward", fixture => fixture.ChangeMuseum("rusty_key_reward_action", JsonValue.Create("fake")),
            "rusty_key_live_reward_authority_invalid");
        var lateAllowed = new RustyKeyPortfolioFixture();
        lateAllowed.Opportunity.TargetTotalDay = 222;
        Require(lateAllowed.Run().AcquisitionPortfolioReady, "Supply must retain the last legal following-morning bound.");
        passed.Add("following_morning_reserved");
        var canonicalGoal = new RustyKeyPortfolioFixture();
        canonicalGoal.Inventory.GoalId = GoalMethodTeacherCoverageGoalIds.Authoritative;
        canonicalGoal.Proposal.GoalId = canonicalGoal.Inventory.GoalId;
        canonicalGoal.Opportunity.GoalId = canonicalGoal.Inventory.GoalId;
        Require(canonicalGoal.Run().AcquisitionPortfolioReady, "Canonical Grandpa and historical acquisition aliases must both work.");
        passed.Add("existing_acquisition_goal_alias_and_canonical_goal");
        var fullMuseumOverreach = new RustyKeyPortfolioFixture(59, 36).Run();
        Require(!fullMuseumOverreach.AcquisitionPortfolioReady && fullMuseumOverreach.BlockingReasons.Contains(
            "rusty_key_selected_distinct_items_do_not_equal_live_deficit"), "The 95-item goal must not replace the 60-item goal.");
        passed.Add("full_museum_not_a_key_prerequisite");
        var thresholdWithRoute = new RustyKeyPortfolioFixture(60);
        thresholdWithRoute.Proposal.SelectedRouteOccurrenceIds = new[] { "route-60" };
        Require(thresholdWithRoute.Run().BlockingReasons.Contains("rusty_key_threshold_satisfied_cannot_schedule_more_acquisition"),
            "An already satisfied threshold cannot authorize unused routes.");
        passed.Add("satisfied_threshold_rejects_route");
        var availableMaterial = new RustyKeyPortfolioFixture();
        availableMaterial.AddMaterialClaim(59);
        Require(availableMaterial.Run().AcquisitionPortfolioReady && availableMaterial.Ledger.MaterialReservations.Length == 0,
            "One available input must pass the shared preflight without committing the real ledger.");
        passed.Add("available_material_read_only_preflight");
        var conflicting = new RustyKeyPortfolioFixture(58);
        conflicting.AddMaterialClaim(58);
        conflicting.AddMaterialClaim(59);
        var original = JsonSerializer.Serialize(conflicting.Ledger, JsonDefaults.Options);
        var conflict = conflicting.Run();
        Require(!conflict.AcquisitionPortfolioReady && conflict.BlockingReasons.Any(reason =>
            reason.Contains("insufficient_unreserved_quantity", StringComparison.Ordinal)) &&
            original == JsonSerializer.Serialize(conflicting.Ledger, JsonDefaults.Options),
            "Joint input overbooking must fail in the shared atomic preflight without ledger mutation.");
        passed.Add("joint_material_overbooking");
        var protectedItem = new RustyKeyPortfolioFixture();
        protectedItem.Ledger.MaterialReservations = new[] { new MaterialReservation
        {
            ReservationId = "craft-protection", Revision = 1, SourceDecisionId = "craft-task",
            SourceStateHash = protectedItem.Snapshot.StateHash, GoalId = "task.craft",
            OwnerPlayerId = 1, NodeId = "player:1", QualifiedItemId = "(O)388", Quantity = 1, Purpose = "crafting"
        } };
        protectedItem.AddMaterialClaim(59);
        var protectionResult = protectedItem.Run();
        Require(!protectionResult.AcquisitionPortfolioReady && protectionResult.BlockingReasons.Any(reason =>
            reason.Contains("insufficient_unreserved_quantity", StringComparison.Ordinal)) &&
            protectedItem.Ledger.MaterialReservations[0].Status ==
            StrategyCommitmentStatuses.Active, "Existing task/crafting reservations cannot be spent by the threshold portfolio.");
        passed.Add("active_other_goal_material_protected");
        var malformed = new RustyKeyPortfolioFixture();
        malformed.ChangeMuseum("donated_count", JsonValue.Create(58));
        var rejected = false;
        try { malformed.Run(); } catch (InvalidDataException) { rejected = true; }
        Require(rejected, "Aggregate museum progress must agree with every retained item row.");
        passed.Add("counter_row_disagreement");

        Write(Path.Combine(Path.GetFullPath(outputRoot), "rusty-key-portfolio-self-test.json"), new
        {
            status = "passed", passed_count = passed.Count, cases = passed,
            evidence_scope = "synthetic_threshold_composition_and_shared_resource_preflight_only",
            native_game_run = false, criterion_promoted = false, formal_training_authorized = false
        });

        void Reject(string name, Action<RustyKeyPortfolioFixture> mutate, string expected)
        {
            var fixture = new RustyKeyPortfolioFixture();
            mutate(fixture);
            var report = fixture.Run();
            Require(!report.AcquisitionPortfolioReady && report.BlockingReasons.Any(reason =>
                reason.StartsWith(expected, StringComparison.Ordinal)), "Threshold rejection missing: " + name + ": " +
                string.Join(",", report.BlockingReasons));
            passed.Add(name);
        }
    }
}
