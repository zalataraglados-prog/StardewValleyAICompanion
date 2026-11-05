using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class MuseumDonationMainlineTests
{
    [Fact]
    public void RustyKeyTeacherBuildsOnlyFromPrestateAndOriginalCompiler()
    {
        var f = CreateDonationTeacherFixture();
        var receipt = RustyKeyDonationTeacherComparisonBuilder.Build(f.RequestPath);
        Assert.True(receipt.TeacherComparisonVerified, string.Join(";", receipt.CandidateEvaluations
            .Select(row => row.CandidateId + ":" + string.Join(",", row.BlockingReasons))));
        Assert.False(receipt.NativeTerminalOutcomeVerified);
        Assert.False(receipt.FullCriterionVerified);
        Assert.False(receipt.FormalTrainingAuthorized);
        Assert.False(receipt.UsesControlSelectionOrNativeOutcome);
        Assert.Equal("MuseumFarm:123:6", receipt.SplitKey);
        Assert.Equal(PolicyTrajectoryDatasetBuilder.PartitionFor(receipt.SplitKey), receipt.DatasetPartition);
        var pair = Assert.Single(receipt.PairwisePreferences);
        Assert.Equal("museum-donate:0:(O)96", pair.PreferredCandidateId);
        Assert.All(receipt.CandidateEvaluations, row => Assert.True(row.Admitted, string.Join(";", row.BlockingReasons)));
        Assert.Equal(new[] { "executor.donate_museum_item", "executor.wait_ticks" },
            receipt.CandidateEvaluations.Select(row => Assert.Single(row.CompiledQueue!.Items).OptionId));
        var path = Path.Combine(f.Root, "comparison.json");
        WriteTeacherJson(path, receipt);
        Assert.Equal(JsonSerializer.Serialize(receipt, JsonOptions),
            JsonSerializer.Serialize(RustyKeyDonationTeacherComparisonBuilder.Verify(path), JsonOptions));
    }

    [Fact]
    public void RustyKeyTeacherPreservesProtectedUnavailableDonationWithoutANegativeLabel()
    {
        var f = CreateDonationTeacherFixture(reserved: 2);
        var result = RustyKeyDonationTeacherComparisonBuilder.Build(f.RequestPath);
        Assert.False(result.TeacherComparisonVerified);
        Assert.Empty(result.PairwisePreferences);
        var donor = Assert.Single(result.CandidateEvaluations.Where(row => row.Kind == "donate_museum_item"));
        Assert.False(donor.Admitted);
        Assert.NotEmpty(donor.BlockingReasons);
        Assert.Null(donor.CompiledQueue);
        Assert.Equal(0, donor.ProjectedThresholdCredit);
    }

    [Fact]
    public void RustyKeyTeacherDoesNotInventAnOrderBetweenEquivalentDonations()
    {
        var f = CreateDonationTeacherFixture();
        EditTeacherSnapshot(f, state =>
        {
            var copy = state["world_progress"]!["museum"]!["value"]!["donation_candidates"]![0]!.DeepClone();
            copy["slot_index"] = 1;
            state["world_progress"]!["museum"]!["value"]!["donation_candidates"]!.AsArray().Add(copy);
            var inventory = state["player"]!["inventory"]!["value"]!.AsArray();
            var slot = inventory[0]!.DeepClone();
            slot["slot_index"] = 1;
            inventory.Add(slot);
            var slots = state["farm"]!["material_inventory_graph"]!["value"]!["inventory_nodes"]![0]!["slots"]!.AsArray();
            var graphSlot = slots[0]!.DeepClone();
            graphSlot["slot_index"] = 1;
            slots.Add(graphSlot);
        });
        var result = RustyKeyDonationTeacherComparisonBuilder.Build(f.RequestPath);
        Assert.Equal(3, result.CandidateEvaluations.Length);
        Assert.Equal(2, result.PairwisePreferences.Length);
        Assert.All(result.PairwisePreferences, pair => Assert.Equal("rusty_key:bounded_no_donation:600", pair.AlternativeCandidateId));
    }

    [Fact]
    public void RustyKeyTeacherDoesNotLabelAValidDonationWhenDeferFailsOriginalProvenanceGate()
    {
        var f = CreateDonationTeacherFixture();
        EditTeacherSnapshot(f, state => state["time"]!["time"]!.AsObject().Remove("source"));
        var result = RustyKeyDonationTeacherComparisonBuilder.Build(f.RequestPath);
        Assert.True(result.CandidateEvaluations[0].Admitted);
        Assert.False(result.CandidateEvaluations[1].Admitted);
        Assert.False(result.TeacherComparisonVerified);
        Assert.Empty(result.PairwisePreferences);
    }

    [Theory]
    [InlineData("wrong_schema")]
    [InlineData("missing_tick")]
    [InlineData("wrong_version")]
    [InlineData("broken_hash")]
    [InlineData("day_missing")]
    [InlineData("day_mistyped")]
    [InlineData("day_late")]
    [InlineData("day_negative")]
    [InlineData("no_clock")]
    [InlineData("insufficient_time")]
    [InlineData("threshold_already_reached")]
    [InlineData("threshold_missing")]
    [InlineData("reward_already_granted")]
    [InlineData("prerequisite_already_seen")]
    [InlineData("key_already_granted")]
    [InlineData("key_flag_missing")]
    [InlineData("active_event")]
    [InlineData("event_flag_missing")]
    [InlineData("open_menu")]
    [InlineData("remote_museum")]
    [InlineData("foreign_ledger")]
    public void RustyKeyTeacherRejectsMissingOrOutOfScopePrestate(string kind)
    {
        var f = CreateDonationTeacherFixture();
        var raw = JsonNode.Parse(File.ReadAllText(f.SnapshotPath))!.AsObject();
        var state = raw["state"]!;
        var museum = state["world_progress"]!["museum"]!["value"]!;
        switch (kind)
        {
            case "wrong_schema": raw["schema_version"] = "snapshot.v0"; break;
            case "missing_tick": raw.Remove("game_tick"); break;
            case "wrong_version": raw["game_version"] = "1.6.14"; break;
            case "broken_hash": raw["state_hash"] = "wrong"; break;
            case "day_missing": state["time"]!.AsObject().Remove("total_days"); break;
            case "day_mistyped": state["time"]!["total_days"]!["value"] = true; break;
            case "day_late": state["time"]!["total_days"]!["value"] = 224; break;
            case "day_negative": state["time"]!["total_days"]!["value"] = -1; break;
            case "no_clock": state["time"]!.AsObject().Remove("time"); break;
            case "insufficient_time": state["time"]!["time"]!["value"] = 1855; break;
            case "threshold_already_reached": museum["donated_count"] = 60; break;
            case "threshold_missing": museum.AsObject().Remove("rusty_key_donation_threshold"); break;
            case "reward_already_granted": museum["rusty_key_reward_claimed"] = true; break;
            case "prerequisite_already_seen": museum["rusty_key_prerequisite_event_seen"] = true; break;
            case "key_already_granted": museum["has_rusty_key"] = true; break;
            case "key_flag_missing": museum.AsObject().Remove("has_rusty_key"); break;
            case "active_event": state["player"]!["story_event"]!["value"]!["active"] = true; break;
            case "event_flag_missing": state["player"]!["story_event"]!["value"]!.AsObject().Remove("event_up"); break;
            case "open_menu": state["menus"]!["active_menu"]!["value"]!["is_open"] = true; break;
            case "remote_museum": museum["museum_is_current_location"] = false; break;
            case "foreign_ledger":
                var ledger = JsonNode.Parse(File.ReadAllText(f.LedgerPath))!;
                ledger["player_id"] = "other-player";
                WriteTeacherJson(f.LedgerPath, ledger);
                break;
        }
        if (kind != "broken_hash") raw["state_hash"] = SnapshotHash.ComputeStateHash(
            state.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!);
        WriteTeacherJson(f.SnapshotPath, raw);
        Assert.Throws<InvalidDataException>(() => RustyKeyDonationTeacherComparisonBuilder.Build(f.RequestPath));
    }

    [Theory]
    [InlineData("partition")]
    [InlineData("preference")]
    [InlineData("native_claim")]
    [InlineData("denominator")]
    [InlineData("source_change")]
    public void RustyKeyTeacherReplayRejectsChangedClaimsAndSources(string kind)
    {
        var f = CreateDonationTeacherFixture();
        var result = JsonSerializer.SerializeToNode(RustyKeyDonationTeacherComparisonBuilder.Build(f.RequestPath), JsonOptions)!;
        if (kind == "partition") result["dataset_partition"] = "made_up";
        if (kind == "preference") result["pairwise_preferences"]![0]!["preferred_candidate_id"] = "control_selected";
        if (kind == "native_claim") result["native_terminal_outcome_verified"] = true;
        if (kind == "denominator") result["candidate_evaluations"]!.AsArray().RemoveAt(1);
        if (kind == "source_change") EditTeacherSnapshot(f, state => state["time"]!["total_days"]!["value"] = 7);
        var path = Path.Combine(f.Root, "comparison.json");
        WriteTeacherJson(path, result);
        Assert.Throws<InvalidDataException>(() => RustyKeyDonationTeacherComparisonBuilder.Verify(path));
    }

    private static DonationTeacherFixture CreateDonationTeacherFixture(int reserved = 0)
    {
        var root = Path.Combine(Path.GetTempPath(), "stardewai-rusty-key-teacher", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var snapshot = ReservationSnapshot();
        snapshot.GameVersion = "1.6.15";
        snapshot.BridgeVersion = "test.v1";
        var state = JsonSerializer.SerializeToNode(snapshot.State, JsonOptions)!;
        state["world_progress"]!["museum"]!["value"]!["rusty_key_prerequisite_event_seen"] = false;
        state["player"]!["story_event"] = JsonSerializer.SerializeToNode(new
            { status = "available", value = new { active = false, event_up = false } });
        state["time"] = JsonSerializer.SerializeToNode(new
        {
            time = new { status = "available", value = 930, confidence = 1, read_at_tick = 1,
                source = new { kind = "game_object", path = "Game1.timeOfDay" }, adapter = "test" },
            total_days = new { status = "available", value = 6, confidence = 1, read_at_tick = 1,
                source = new { kind = "game_object", path = "Game1.Date.TotalDays" }, adapter = "test" }
        });
        snapshot.State = state.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!;
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        var f = new DonationTeacherFixture(root, Path.Combine(root, "snapshot.json"),
            Path.Combine(root, "ledger.json"), Path.Combine(root, "request.json"));
        WriteTeacherJson(f.SnapshotPath, snapshot);
        WriteTeacherJson(f.LedgerPath, DonationLedger(snapshot, reserved));
        WriteTeacherJson(f.RequestPath, new RustyKeyDonationTeacherComparisonRequest
            { SnapshotPath = f.SnapshotPath, StrategyLedgerPath = f.LedgerPath });
        return f;
    }

    private static void EditTeacherSnapshot(DonationTeacherFixture f, Action<JsonNode> edit)
    {
        var raw = JsonNode.Parse(File.ReadAllText(f.SnapshotPath))!;
        edit(raw["state"]!);
        raw["state_hash"] = SnapshotHash.ComputeStateHash(raw["state"]!.Deserialize<Dictionary<string, JsonElement>>(JsonOptions)!);
        WriteTeacherJson(f.SnapshotPath, raw);
    }

    private static void WriteTeacherJson(string path, object value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, JsonOptions));

    private sealed record DonationTeacherFixture(string Root, string SnapshotPath, string LedgerPath, string RequestPath);
}
