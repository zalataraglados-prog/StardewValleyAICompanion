using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.Strategy;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class CurrentMuseumCollectionIntakeTests
{
    [Fact]
    public void TerminalComparisonUsesIndependentPrestateAndBothOriginalCompiledAlternatives()
    {
        var (f, request, _) = TerminalFixture();
        var comparison = MuseumTerminalTeacherComparisonBuilder.Build(request);
        Assert.True(comparison.IndependentPairwiseLabelEligible,
            string.Join(";", comparison.CandidateEvaluations.Select(row => row.CandidateId + ":" + string.Join(",", row.BlockingReasons))));
        Assert.False(comparison.CompletePreferenceLabelEligible);
        Assert.False(comparison.NativeTerminalOutcomeVerified);
        Assert.False(comparison.PriorAcquisitionVerified);
        Assert.False(comparison.FullRootVerified);
        Assert.False(comparison.FormalTrainingAuthorized);
        Assert.False(comparison.UsesLearnerRankOrNativeOutcome);
        Assert.Equal(1, comparison.OriginalDonationCandidateCount);
        Assert.Equal("MuseumFarm:123:6", comparison.SplitKey);
        Assert.Equal(PolicyTrajectoryDatasetBuilder.PartitionFor(comparison.SplitKey), comparison.DatasetPartition);
        var pair = Assert.Single(comparison.PairwisePreferences);
        var donor = comparison.CandidateEvaluations.Single(row => row.CandidateId == pair.PreferredCandidateId);
        Assert.Equal(1, donor.ProjectedNewAchievementCredit);
        Assert.Equal("pending", donor.CompiledQueue!.Status);
        Assert.Equal("executor.donate_museum_item", Assert.Single(donor.CompiledQueue.Items).OptionId);
        Assert.Contains(donor.CompiledPlan!.Steps[0].Parameters,
            row => row.Name == "expected_complete_collection_achievement_after" && row.Value == "true");
        var defer = comparison.CandidateEvaluations.Single(row => row.CandidateId == pair.AlternativeCandidateId);
        Assert.True(defer.Admitted);
        Assert.Equal(0, defer.ProjectedNewAchievementCredit);
        Assert.Equal("executor.wait_ticks", Assert.Single(defer.CompiledQueue!.Items).OptionId);
        var path = Path.Combine(Path.GetDirectoryName(f.Snapshot)!, "comparison.json");
        Write(path, comparison);
        Assert.Equal(JsonSerializer.Serialize(comparison, Json), JsonSerializer.Serialize(MuseumTerminalTeacherComparisonBuilder.Verify(path), Json));
        var selected = MuseumTerminalTeacherComparisonBuilder.Compile(path, pair.PreferredCandidateId);
        Assert.Equal(donor.CompiledQueue.QueueId, selected.Queue.QueueId);
        Assert.Equal(donor.CompiledPlan.PlanId, selected.Plan.PlanId);
        Assert.Throws<InvalidDataException>(() => MuseumTerminalTeacherComparisonBuilder.Compile(path, pair.AlternativeCandidateId));
    }

    [Fact]
    public void TerminalComparisonKeepsEveryEquivalentCopyAndDoesNotInventItsOrdering()
    {
        var (f, request, _) = TerminalFixture();
        Edit(f.Snapshot, root =>
        {
            var state = root["state"]!;
            var donor = state["world_progress"]!["museum"]!["value"]!["donation_candidates"]![0]!.DeepClone();
            donor["slot_index"] = 1;
            state["world_progress"]!["museum"]!["value"]!["donation_candidates"]!.AsArray().Add(donor);
            var inventory = state["player"]!["inventory"]!["value"]!.AsArray();
            var item = inventory[0]!.DeepClone();
            item["slot_index"] = 1;
            inventory.Add(item);
            var slots = state["farm"]!["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]!.AsArray();
            var slot = slots[0]!.DeepClone();
            slot["slot_index"] = 1;
            slots.Add(slot);
        }, snapshot: true);
        RefreshIntake(f);
        var comparison = MuseumTerminalTeacherComparisonBuilder.Build(request);
        Assert.Equal(2, comparison.OriginalDonationCandidateCount);
        Assert.Equal(2, comparison.PairwisePreferences.Length);
        Assert.All(comparison.PairwisePreferences, row => Assert.Equal("museum:bounded_no_donation:600", row.AlternativeCandidateId));
        Assert.False(comparison.CompletePreferenceLabelEligible);
    }

    [Fact]
    public void TerminalComparisonCannotLabelDonationWhenBoundedWaitFailsOriginalProvenance()
    {
        var (f, request, _) = TerminalFixture();
        Edit(f.Snapshot, root => root["state"]!["time"]!["time"]!.AsObject().Remove("source"), snapshot: true);
        RefreshIntake(f);
        var comparison = MuseumTerminalTeacherComparisonBuilder.Build(request);
        Assert.False(comparison.IndependentPairwiseLabelEligible);
        Assert.True(comparison.CandidateEvaluations.Single(row => row.Kind == "donate_museum_item").Admitted);
        Assert.False(comparison.CandidateEvaluations.Single(row => row.Kind == "bounded_no_donation_counterfactual").Admitted);
    }

    [Theory]
    [InlineData("missing_native_projection")]
    [InlineData("wrong_native_total")]
    [InlineData("reserved_copy")]
    public void TerminalComparisonRetainsRejectedDonationWithoutNegativeLabels(string mutation)
    {
        var (f, request, ledger) = TerminalFixture();
        if (mutation == "reserved_copy") Edit(ledger, root => root["material_reservations"] = JsonSerializer.SerializeToNode(new[]
        {
            new MaterialReservation { ReservationId = "protected-quest-item", Revision = 1, OwnerPlayerId = 123,
                SourceStateHash = root["source_state_hash"]!.GetValue<string>(), SourceDecisionId = "quest-protection",
                GoalId = "quest.collect", NodeId = "player:123", SlotIndex = 0, QualifiedItemId = "(O)96", Quantity = 2, Purpose = "quest" }
        }, Json));
        else
        {
            Edit(f.Snapshot, root =>
            {
                var museum = root["state"]!["world_progress"]!["museum"]!["value"]!;
                if (mutation == "missing_native_projection") museum["donation_candidates"]![0]!["expected_complete_collection_achievement_after"] = false;
                if (mutation == "wrong_native_total") museum["donation_candidates"]![0]!["donated_count_after"] = 94;
            }, snapshot: true);
            RefreshIntake(f);
        }
        var comparison = MuseumTerminalTeacherComparisonBuilder.Build(request);
        Assert.False(comparison.IndependentPairwiseLabelEligible);
        Assert.Empty(comparison.PairwisePreferences);
        var donor = Assert.Single(comparison.CandidateEvaluations.Where(row => row.Kind == "donate_museum_item"));
        Assert.False(donor.Admitted);
        Assert.Equal(0, donor.ProjectedNewAchievementCredit);
        Assert.NotEmpty(donor.BlockingReasons);
    }

    [Theory]
    [InlineData("already_earned")]
    [InlineData("late")]
    [InlineData("active_event")]
    [InlineData("open_menu")]
    [InlineData("outside_museum")]
    [InlineData("not_terminal")]
    [InlineData("wrong_goal")]
    [InlineData("foreign_ledger")]
    public void TerminalComparisonRejectsOutOfScopeOrForeignPrestate(string mutation)
    {
        var (f, request, ledger) = TerminalFixture();
        if (mutation == "wrong_goal") Edit(request, root => root["goal_id"] = "other.goal");
        else if (mutation == "foreign_ledger") Edit(ledger, root => root["player_id"] = "other-player");
        else
        {
            Edit(f.Snapshot, root =>
            {
                var state = root["state"]!;
                switch (mutation)
                {
                    case "already_earned":
                        state["world_progress"]!["museum"]!["value"]!["complete_collection_achievement_received"] = true;
                        state["world_progress"]!["achievements"]!["value"] = new JsonArray(5, 28); break;
                    case "late": state["time"]!["total_days"]!["value"] = 224; break;
                    case "active_event": state["player"]!["story_event"]!["value"]!["active"] = true; break;
                    case "open_menu": state["menus"]!["active_menu"]!["value"]!["is_open"] = true; break;
                    case "outside_museum": state["player"]!["location_id"]!["value"] = "Town"; break;
                    case "not_terminal":
                        var museum = state["world_progress"]!["museum"]!["value"]!;
                        museum["donated_count"] = 93;
                        museum["missing_item_count"] = 2;
                        museum["donatable_items"]![93]!["donated"] = false;
                        museum["missing_item_ids"]!.AsArray().Add("94"); break;
                }
            }, snapshot: true);
            RefreshIntake(f);
        }
        Assert.Throws<InvalidDataException>(() => MuseumTerminalTeacherComparisonBuilder.Build(request));
    }

    [Theory]
    [InlineData("forged_pair")]
    [InlineData("forged_native")]
    [InlineData("changed_ledger")]
    public void TerminalComparisonVerifyRebuildsAllSources(string mutation)
    {
        var (f, request, ledger) = TerminalFixture();
        var path = Path.Combine(Path.GetDirectoryName(f.Snapshot)!, "comparison.json");
        Write(path, MuseumTerminalTeacherComparisonBuilder.Build(request));
        if (mutation == "changed_ledger") Edit(ledger, root => root["revision"] = 2);
        else Edit(path, root =>
        {
            if (mutation == "forged_pair") root["pairwise_preferences"]![0]!["alternative_candidate_id"] = "unknown";
            if (mutation == "forged_native") root["native_terminal_outcome_verified"] = true;
        });
        Assert.Throws<InvalidDataException>(() => MuseumTerminalTeacherComparisonBuilder.Verify(path));
    }
}
