using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    public static void RunRustyKeyRollingSupply(string outputRoot)
    {
        var passed = new List<string>();
        foreach (var donated in new[] { 0, 58, 59 })
        {
            var fixture = new RustyKeyPortfolioFixture(donated);
            var ledgerBefore = JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options);
            var result = fixture.RunRolling();
            Require(result.RollingStepReady && result.RemainingDonationCount == 60 - donated &&
                result.CompleteMuseumItemDenominator.Length == 95 && result.SelectedAcquisitionProposal!.ScopedRequirements.Length == 1 &&
                result.CompleteMuseumItemDenominator.Count(item => item.AlreadyDonated) == donated &&
                result.CompleteMuseumItemDenominator.Count(item => item.SelectedForAcquisition) == 1 &&
                result.SelectedAcquisitionAdmission!.PortfolioAdmissionReady && result.SelectedAcquisitionAdmission.AtomicCommitRequest is not null &&
                !result.FullThresholdSupplyVerified && !result.TeacherPreferenceLabelEligible && !result.FormalTrainingAuthorized &&
                ledgerBefore == JsonSerializer.Serialize(fixture.Ledger, JsonDefaults.Options),
                "A legal partial supply step must not require the complete threshold or mutate the ledger.");
            passed.Add("partial_one_of_deficit_" + (60 - donated));
        }
        var partial = new RustyKeyPortfolioFixture(0);
        var partialProposal = partial.RunRolling().SelectedAcquisitionProposal!;
        Require(RustyKeyAcquisitionPortfolioBuilder.Build(partial.Context, partialProposal, "synthetic-proposal").BlockingReasons
            .Contains("rusty_key_selected_distinct_items_do_not_equal_live_deficit"),
            "Rolling admission must not weaken the separate full-threshold certificate.");
        passed.Add("partial_not_a_full_threshold_certificate");
        var sparse = new RustyKeyPortfolioFixture(0);
        for (var index = 1; index < 95; index++) sparse.ChangeRoute(index, route =>
        {
            route["opportunity_cost_axis_resolved"] = false;
            route["blocking_reasons"] = new JsonArray("synthetic_axis_unavailable");
        });
        var sparseResult = sparse.RunRolling();
        Require(sparseResult.RollingStepReady && sparseResult.SelectedCandidateId == "route-0" &&
            sparseResult.CompleteMuseumItemDenominator.Length == 95 && sparseResult.CandidateEvaluations.Length == 95,
            "Unavailable unrelated item routes must not block legal rolling progress or disappear from evidence.");
        passed.Add("one_available_item_does_not_require_other_59_today");
        var cheaper = new RustyKeyPortfolioFixture();
        cheaper.ChangeRoute(60, route => route["cost_vector"]!["guaranteed_elapsed_game_minutes"] = 1);
        Require(cheaper.RunRolling().SelectedCandidateId == "route-60", "Controller must use declared elapsed-time ordering, not caller order.");
        passed.Add("elapsed_time_control_selection");
        var reversed = new RustyKeyPortfolioFixture();
        reversed.Opportunity.Routes = reversed.Opportunity.Routes.Reverse().ToArray();
        Require(reversed.RunRolling().SelectedCandidateId == "route-59", "Equal control candidates require a stable identity tie-break.");
        passed.Add("equal_cost_stable_not_a_preference_label");
        var protectedInput = new RustyKeyPortfolioFixture();
        protectedInput.AddMaterialClaim(59);
        protectedInput.Ledger.MaterialReservations = new[] { new MaterialReservation
        {
            ReservationId = "other-target", SourceDecisionId = "other", GoalId = "other.goal", Revision = 1,
            OwnerPlayerId = 1, SourceStateHash = protectedInput.Snapshot.StateHash, NodeId = "player:1",
            QualifiedItemId = "(O)388", Quantity = 1, Purpose = "other target"
        } };
        var protectedResult = protectedInput.RunRolling();
        Require(protectedResult.RollingStepReady && protectedResult.SelectedCandidateId != "route-59" &&
            protectedResult.CandidateEvaluations.Single(candidate => candidate.CandidateId == "route-59").BlockingReasons
                .Any(reason => reason.Contains("insufficient_unreserved_quantity", StringComparison.Ordinal)),
            "Rolling acquisition cannot spend protected inputs or release another goal.");
        passed.Add("shared_atomic_preflight_protects_other_target");
        var held = new RustyKeyPortfolioFixture();
        held.EnableHeldDonation();
        var contextCalls = 0;
        var heldResult = held.RunRolling(() => { contextCalls++; return held.Context; });
        if (contextCalls != 0 || heldResult.CompiledQueue is null)
        {
            Write(Path.Combine(Path.GetFullPath(outputRoot), "held-donation-failure.json"), heldResult);
            Write(Path.Combine(Path.GetFullPath(outputRoot), "held-availability-failure.json"),
                new StardewAI.Core.OptionRegistry.CandidateOptionAvailabilityEvaluator().Evaluate(held.Snapshot,
                    new[] { "museum.donate_items" }, commitmentLedger: held.Ledger));
        }
        Require(heldResult.RollingStepReady && heldResult.CompiledQueue is not null &&
            heldResult.CompiledQueue.Items.Any(item => item.OptionId == "executor.donate_museum_item") &&
            heldResult.SelectedAcquisitionProposal is null && contextCalls == 0 && heldResult.CompleteMuseumItemDenominator.Length == 95,
            "Safe held inventory must directly compile the existing donation chain without unrelated acquisition prerequisites.");
        passed.Add("held_inventory_uses_real_compiler_without_forecast_chain");
        foreach (var reserved in new[] { 1, 2 })
        {
            var fixture = new RustyKeyPortfolioFixture();
            fixture.EnableHeldDonation();
            fixture.Ledger.MaterialReservations = new[] { new MaterialReservation
            {
                ReservationId = "held-other", SourceDecisionId = "other", GoalId = "other.goal", Revision = 1,
                OwnerPlayerId = 1, SourceStateHash = fixture.Snapshot.StateHash, NodeId = "player:1",
                QualifiedItemId = "(O)fixture_59", Quantity = reserved, Purpose = "other target"
            } };
            var result = fixture.RunRolling();
            Require(result.RollingStepReady && (result.CompiledQueue is not null) == (reserved == 1) &&
                fixture.Ledger.MaterialReservations.Single().Quantity == reserved,
                "Only the unreserved unit may use the existing donation compiler; no reservation can be cancelled.");
            passed.Add("held_stack_two_reserved_" + reserved);
        }
        foreach (var donated in new[] { 60, 61, 95 })
        {
            var result = new RustyKeyPortfolioFixture(donated).RunRolling(() => throw new InvalidOperationException("threshold satisfied"));
            Require(result.ThresholdSatisfied && !result.RollingStepReady && result.CompiledQueue is null &&
                result.SelectedAcquisitionProposal is null && result.BlockingReasons.Length == 0,
                "Satisfied threshold must hand off, never schedule extra acquisition or infer possession of the key.");
            passed.Add("native_handoff_not_more_supply_" + donated);
        }
        Reject("future_supply_not_dispatch_ready", fixture => fixture.Opportunity.TargetTotalDay = 1,
            "rusty_key_rolling_acquisition_target_date_is_not_current_day");
        Reject("stale_snapshot", fixture => fixture.Snapshot.StateHash = "stale", "rusty_key_snapshot_identity_or_hash_invalid");
        Reject("foreign_ledger", fixture => fixture.Ledger.PlayerId = "2", "rusty_key_rolling_ledger_identity_invalid");
        Reject("unreadable_museum", fixture => { fixture.Snapshot.State.Remove("world_progress"); fixture.Rehash(); },
            "museum_transparent_state_unavailable");
        Reject("incomplete_opportunity_inventory", fixture => fixture.Opportunity.RouteOccurrenceInventoryComplete = false,
            "rusty_key_no_safe_current_rolling_supply_step");
        var drifted = new RustyKeyPortfolioFixture();
        Require(drifted.RunRolling(() => drifted.Context with { SnapshotSha256 = "changed-source" }).BlockingReasons
            .Contains("rusty_key_rolling_sources_changed_during_acquisition_rebuild"), "Rebuilt acquisition cannot use a different source identity.");
        passed.Add("lazy_rebuild_binds_same_sources");
        var missing = new RustyKeyPortfolioFixture().RunRolling(() => null);
        Require(!missing.RollingStepReady && missing.BlockingReasons.Contains("rusty_key_rolling_acquisition_source_chain_required"),
            "No held item and no source chain must block without fabricated acquisition.");
        passed.Add("missing_acquisition_sources_fail_closed");
        var late = new RustyKeyPortfolioFixture();
        var time = JsonSerializer.SerializeToNode(late.Snapshot.State["time"])!;
        time["total_days"]!["value"] = 223;
        late.Snapshot.State["time"] = JsonSerializer.SerializeToElement(time);
        late.Rehash();
        Require(late.RunRolling().BlockingReasons.Contains("rusty_key_supply_date_leaves_no_pre_year3_key_handoff"),
            "A late supply step cannot be presented as a pre-Year-3 key solution.");
        passed.Add("next_morning_deadline_bound");
        var root = Path.GetFullPath(outputRoot);
        Write(Path.Combine(root, "held-fixture-inventory.json"), held.Inventory);
        Write(Path.Combine(root, "held-fixture-snapshot.json"), held.Snapshot);
        Write(Path.Combine(root, "held-fixture-ledger.json"), held.Ledger);
        var publicResult = RustyKeyRollingSupplyProposalBuilder.Build(new()
        {
            RequirementInventoryPath = Path.Combine(root, "held-fixture-inventory.json"),
            SnapshotPath = Path.Combine(root, "held-fixture-snapshot.json"),
            StrategyLedgerPath = Path.Combine(root, "held-fixture-ledger.json")
        });
        Require(publicResult.CompiledQueue is not null && publicResult.RollingStepReady,
            "The public source-file entrypoint must support held donation without unrelated forecasts.");
        passed.Add("public_held_file_entrypoint");
        var noHeld = new RustyKeyPortfolioFixture();
        Write(Path.Combine(root, "no-held-fixture-snapshot.json"), noHeld.Snapshot);
        Write(Path.Combine(root, "no-held-fixture-ledger.json"), noHeld.Ledger);
        var publicBlocked = RustyKeyRollingSupplyProposalBuilder.Build(new()
        {
            RequirementInventoryPath = Path.Combine(root, "held-fixture-inventory.json"),
            SnapshotPath = Path.Combine(root, "no-held-fixture-snapshot.json"),
            StrategyLedgerPath = Path.Combine(root, "no-held-fixture-ledger.json")
        });
        Require(!publicBlocked.RollingStepReady && publicBlocked.SelectedAcquisitionProposal is null &&
            publicBlocked.BlockingReasons.Contains("rusty_key_rolling_acquisition_source_chain_required"),
            "The public no-stock path cannot invent an acquisition proposal without retained source axes.");
        passed.Add("public_no_stock_without_axes_blocks");
        var custodyService = new StardewAI.Core.Strategy.ReservationPortfolioLedgerService();
        var custodyCommit = custodyService.Commit(held.Ledger, held.Snapshot, new()
        {
            StateHash = held.Snapshot.StateHash, ExpectedLedgerRevision = held.Ledger.Revision,
            PortfolioId = "rolling-custody-fixture", SourceDecisionId = "rolling-custody-fixture", GoalId = held.Inventory.GoalId
        }, "synthetic-commit");
        var custody = custodyService.SettleCompletedRoute(custodyCommit.Ledger, held.Snapshot, new()
        {
            StateHash = held.Snapshot.StateHash, ExpectedLedgerRevision = custodyCommit.Ledger!.Revision,
            PortfolioId = "rolling-custody-fixture", RouteSourceDecisionId = "synthetic-native-route", GoalId = held.Inventory.GoalId,
            FreshTerminalReceiptSha256 = new string('a', 64), Reason = "synthetic native acquisition",
            OutputCustodyClaims = new[] { new ReservationPortfolioOutputCustodyClaim
            {
                NodeId = "player:1", SlotIndex = 0, QualifiedItemId = "(O)fixture_59", Quantity = 1,
                ConsumerOptionId = StardewAI.Core.Strategy.MaterialOutputCustodyContract.MuseumConsumer
            } }
        }, "synthetic-custody");
        Require(custody.Accepted, "Shared output custody could not be created for rolling integration: " + string.Join(",", custody.Errors));
        var heldCustody = RustyKeyRollingSupplyProposalBuilder.Build(held.Inventory, held.Snapshot, custody.Ledger!,
            "fixture-inventory-sha", "fixture-snapshot-sha", "fixture-custody-sha", () => throw new InvalidOperationException("held custody must not acquire again"));
        Require(heldCustody.RollingStepReady && heldCustody.CompiledQueue!.Items.Any(item => item.NormalizedCommand.Parameters
            .Any(parameter => parameter.Name == "museum_output_custody_reservation_id")), "Rolling producer did not reuse its own dedicated output custody.");
        passed.Add("rolling_producer_compiles_its_dedicated_custody");
        held.ChangeMuseum("museum_mutex_locked", JsonValue.Create(true));
        custody.Ledger!.MaterialReservations = custody.Ledger.MaterialReservations.Append(new MaterialReservation
        {
            ReservationId = "spare-unit-protected", SourceDecisionId = "other", GoalId = "other.goal", Revision = 1,
            OwnerPlayerId = 1, SourceStateHash = held.Snapshot.StateHash, NodeId = "player:1", SlotIndex = 0,
            QualifiedItemId = "(O)fixture_59", Quantity = 1, Purpose = "protect remaining unit"
        }).ToArray();
        var pendingCustody = RustyKeyRollingSupplyProposalBuilder.Build(held.Inventory, held.Snapshot, custody.Ledger!,
            "fixture-inventory-sha", "fixture-snapshot-sha", "fixture-custody-sha",
            () => throw new InvalidOperationException("Pending custody must not acquire again."));
        Require(!pendingCustody.RollingStepReady && !pendingCustody.ThresholdSatisfied &&
            pendingCustody.PendingDonationRequirementIds is { Length: 1 }, "Authenticated custody remains pending supply, not completed progress.");
        passed.Add("pending_dedicated_custody_not_reacquired");
        custody.Ledger.MaterialReservations.Single(row => row.OutputCustody is not null).OutputCustody!.FreshTerminalReceiptSha256 = new string('b', 64);
        var missingHistoryContextCalls = 0;
        var forgedCustody = RustyKeyRollingSupplyProposalBuilder.Build(held.Inventory, held.Snapshot, custody.Ledger,
            "fixture-inventory-sha", "fixture-snapshot-sha", "fixture-custody-sha", () => { missingHistoryContextCalls++; return null; });
        Require(forgedCustody.PendingDonationRequirementIds is null && missingHistoryContextCalls == 1,
            "An unauthenticated custody marker must not count another goal's reserved unit as pending supply.");
        passed.Add("pending_custody_requires_exact_creation_history");
        VerifyRollingPendingInventory(passed);
        VerifyRollingDonationTimeBudget(passed);
        Write(Path.Combine(root, "rusty-key-rolling-self-test.json"), new
        {
            status = "passed", passed_count = passed.Count, cases = passed,
            evidence_scope = "synthetic_rolling_producer_existing_compiler_and_shared_atomic_preflight",
            native_game_run = false, criterion_promoted = false, formal_training_authorized = false
        });

        void Reject(string name, Action<RustyKeyPortfolioFixture> mutate, string expected)
        {
            var fixture = new RustyKeyPortfolioFixture();
            mutate(fixture);
            var result = fixture.RunRolling();
            Require(!result.RollingStepReady && result.BlockingReasons.Contains(expected),
                "Rolling rejection missing: " + name + ": " + string.Join(",", result.BlockingReasons));
            passed.Add(name);
        }
    }
}
