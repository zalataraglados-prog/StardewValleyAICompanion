using System.Text.Json;
using StardewAI.Contracts.Execution;
using StardewAI.Contracts.Options;
using StardewAI.Contracts.State;

namespace StardewAI.GoalConditionedBootstrap;

internal static partial class BootstrapSelfTest
{
    private static void VerifyMoneyRevenueRecoveryPrerequisite()
    {
        var snapshot = new SnapshotEnvelope
        {
            State = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>("""
                {"menus":{"active_menu":{"status":"available","value":{"is_open":true,"type":"ShopMenu"}}},
                 "player":{"location_id":{"status":"available","value":"Blacksmith"}},
                 "time":{"total_days":{"status":"available","value":6}}}
                """, JsonDefaults.Options)!
        };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var node = new MoneyRevenueCandidateDenominatorBuilder.RevenueActionNodeDefinition(
            "dependency.earn_money.native_shipping_day_settlement", "deterministic_transition",
            "recovery.stabilize_day", false, true, new[] { "recovery_sleep_immediately" },
            new[] { "time.total_days" });
        var direction = new MoneyRevenueCandidateDenominatorBuilder.RevenueDirectionDefinition(
            "in_progress", "complete", new[] { "fresh_save_schedule_pending" }, new[] { node },
            Array.Empty<MoneyRevenueCandidateDenominatorBuilder.RevenueDependencyNodeDefinition>(),
            Array.Empty<MoneyRevenueCandidateDenominatorBuilder.RevenueDependencyEdgeDefinition>());
        EventCandidate Candidate() => new()
        {
            CandidateId = "recovery:close_blocking_menu", Kind = "recovery_close_menu",
            Available = true, LocationId = "Blacksmith", EstimatedTicks = 10,
            ExpectedEffect = "menu_not_blocking_execution",
            Parameters = new[] { new SmallModelActionParameter
                { Name = "execution_option_id", Value = "executor.close_menu" } }
        };
        MoneyRevenueCandidateDenominatorCatalog Build(SnapshotEnvelope source, EventCandidate candidate,
            MoneyRevenueCandidateDenominatorBuilder.RevenueActionNodeDefinition? definition = null) =>
            MoneyRevenueCandidateDenominatorBuilder.BuildCatalog(source, "snapshot.json", new string('a', 64),
                "dependencies.json", new string('b', 64),
                direction with { Nodes = new[] { definition ?? node } },
                new OptionAvailabilityEnvelope
                {
                    StateHash = source.StateHash,
                    Options = new[] { ReadyRevenueOption((definition ?? node).OptionId,
                        eventCandidates: new[] { candidate }) }
                });
        var original = Candidate();
        var catalog = Build(snapshot, original);
        var entry = catalog.Nodes.Single().Candidates.Single();
        Require(catalog.NodeCoverageComplete && catalog.CurrentSnapshotDenominatorAuthoritative &&
                !catalog.CurrentComparisonDenominatorReady && !catalog.LongHorizonDenominatorAuthoritative &&
                !catalog.ProductionTeacherPreferenceLabelEligible && !catalog.FormalProductTrainingAuthorized &&
                catalog.EnumeratedCandidateCount == 1 && catalog.ComparisonCandidateCount == 0 &&
                entry.SemanticKindBinding == "deterministic_recovery_prerequisite" &&
                !entry.ComparisonEligible && entry.CandidateKind == original.Kind &&
                entry.CandidateId == original.CandidateId && entry.ExpectedEffect == original.ExpectedEffect &&
                entry.EstimatedTicks == 10 && entry.TotalValue == 0 && entry.CandidateSha256.Length == 64 &&
                catalog.DependencyExpansionStatus == "in_progress" &&
                catalog.DependencyExpansionBlockers.SequenceEqual(direction.Blockers),
            "Native shop recovery was dropped, relabeled as settlement, or granted comparison/global authority.");

        void Reject(EventCandidate candidate,
            MoneyRevenueCandidateDenominatorBuilder.RevenueActionNodeDefinition? definition = null,
            SnapshotEnvelope? source = null)
        {
            var rejected = Build(source ?? snapshot, candidate, definition);
            Require(!rejected.NodeCoverageComplete &&
                    rejected.Nodes.Single().Candidates.Single().SemanticKindBinding == "unbound",
                "Money recovery prerequisite admitted an unrelated, forged, or context-free control.");
        }
        var changed = Candidate(); changed.Kind = "unknown_recovery"; Reject(changed);
        changed = Candidate(); changed.CandidateId = "another-menu"; Reject(changed);
        changed = Candidate(); changed.LocationId = "Farm"; Reject(changed);
        changed = Candidate(); changed.ExpectedEffect = "day_settled"; Reject(changed);
        changed = Candidate(); changed.Available = false; Reject(changed);
        changed = Candidate(); changed.TotalValue = 1; Reject(changed);
        changed = Candidate(); changed.BlockReasons = new[] { "unsafe_menu" }; Reject(changed);
        changed = Candidate(); changed.Parameters[0].Value = "executor.sleep"; Reject(changed);
        changed = Candidate(); changed.Parameters = changed.Parameters.Concat(changed.Parameters).ToArray(); Reject(changed);
        Reject(Candidate(), node with { PolicyChoice = true, DeterministicSupport = false });
        Reject(Candidate(), node with { OptionId = "economy.sell_items" });
        Reject(Candidate(), node with { CandidateKinds = new[] { "unrelated_transition" } });
        var noMenu = CloneSnapshot(snapshot);
        noMenu.State.Remove("menus");
        noMenu.StateHash = SnapshotHash.ComputeStateHash(noMenu.State);
        Reject(Candidate(), source: noMenu);
        var closedMenu = CloneSnapshot(snapshot);
        closedMenu.State["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = new { is_open = false, type = "ShopMenu" } }
        });
        closedMenu.StateHash = SnapshotHash.ComputeStateHash(closedMenu.State);
        Reject(Candidate(), source: closedMenu);
        var otherMenu = CloneSnapshot(snapshot);
        otherMenu.State["menus"] = JsonSerializer.SerializeToElement(new
        {
            active_menu = new { status = "available", value = new { is_open = true, type = "DialogueBox" } }
        });
        otherMenu.StateHash = SnapshotHash.ComputeStateHash(otherMenu.State);
        Reject(Candidate(), source: otherMenu);
    }
}
