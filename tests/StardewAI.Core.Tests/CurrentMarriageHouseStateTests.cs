using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CurrentMarriageHouseStateTests
{
    [Theory]
    [InlineData(false, 0, false, false, -1, false, 2, false)]
    [InlineData(true, 1, false, false, -1, false, 1, false)]
    [InlineData(true, 2, false, false, -1, true, 0, false)]
    [InlineData(true, 3, false, true, -1, true, 0, false)]
    [InlineData(false, 2, true, true, -1, false, 0, true)]
    [InlineData(true, 1, false, false, 0, false, 1, true)]
    [InlineData(false, 0, false, false, 3, false, 2, true)]
    public void JointConditionAndPendingSettlementDoNotBecomeAcquisitionEvidence(bool married, int level,
        bool engaged, bool roommate, int days, bool satisfied, int missing, bool pending)
    {
        var snapshot = Snapshot(married, level, engaged, roommate, days: days);
        var original = JsonSerializer.Serialize(snapshot);
        var result = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.Equal(snapshot.StateHash, result.SourceStateHash);
        Assert.Equal(snapshot.GameTick, result.GameTick);
        Assert.Equal(satisfied, result.CurrentCriterionSatisfied);
        Assert.Equal(missing, result.HouseLevelsMissing);
        Assert.Equal(!married, result.PartnershipStillRequired);
        Assert.Equal(pending, result.NativeSettlementPending);
        Assert.Equal(roommate, result.HasCurrentOrPendingRoommate);
        Assert.False(result.TrainingLabelEligible);
        Assert.False(result.NativeOutcomeVerified);
        Assert.False(result.PriorAcquisitionVerified);
        Assert.Equal(original, JsonSerializer.Serialize(snapshot));
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public void NativeTeamMarriageDoesNotRequireAnNpcSpouseNameOrFriendshipRow()
    {
        var result = new CurrentMarriageHouseStateBuilder().Build(Snapshot(true, 2, spouse: ""));
        Assert.Equal("ready_current_state_only", result.Status);
        Assert.True(result.CurrentCriterionSatisfied);
        Assert.Equal("", result.Spouse);
    }

    [Fact]
    public void RawCurrentQuoteKeepsUnmodeledNativeFieldsWithoutClaimingLegalPurchase()
    {
        var snapshot = Snapshot();
        Mutate(snapshot, "world_progress", node => node["marriage_house"]!["value"]!["house_upgrade"]!["future_native_detail"] = "retained");
        var result = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        Assert.Equal(2, result.Year);
        Assert.Equal("winter", result.Season);
        Assert.Equal(17, result.Day);
        Assert.Equal(1110, result.CurrentTime);
        Assert.Equal("retained", result.HouseUpgradeQuote!.Value.GetProperty("future_native_detail").GetString());
        Assert.Equal("science_house_not_current_location", result.HouseUpgradeQuote.Value.GetProperty("action_status").GetString());
        Assert.False(result.NativeOutcomeVerified);
    }

    [Theory]
    [InlineData("married_or_roommate", "false")]
    [InlineData("engaged", "true")]
    [InlineData("pending_roommate", "true")]
    [InlineData("spouse", "\"Other\"")]
    [InlineData("farmhouse_upgrade_level", "0")]
    [InlineData("days_until_farmhouse_upgrade", "3")]
    [InlineData("money", "0")]
    public void ConflictingNativeProjectionsNeverDefaultToAUsableZero(string field, string json)
    {
        var snapshot = Snapshot(true, 1);
        Mutate(snapshot, "world_progress", node => node["marriage_house"]!["value"]![field] = JsonNode.Parse(json));
        AssertBlocked(snapshot);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("unknown")]
    [InlineData("null")]
    [InlineData("wrong_type")]
    [InlineData("duplicate")]
    [InlineData("stale_hash")]
    [InlineData("version")]
    [InlineData("tick")]
    public void MissingMalformedOrUnauthenticatedFactsStayUnknown(string mutation)
    {
        var snapshot = Snapshot();
        if (mutation == "version") snapshot.GameVersion = "1.6.14";
        else if (mutation == "tick") snapshot.GameTick = -1;
        else if (mutation == "stale_hash") snapshot.StateHash = "wrong";
        else if (mutation == "duplicate")
        {
            snapshot.State["player"] = JsonDocument.Parse(snapshot.State["player"].GetRawText()
                .Replace("{\"married_or_roommate\":", "{\"married_or_roommate\":{},\"married_or_roommate\":" )).RootElement.Clone();
            snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        }
        else Mutate(snapshot, "player", node =>
        {
            if (mutation == "missing") node.AsObject().Remove("married_or_roommate");
            if (mutation == "unknown") node["married_or_roommate"]!["status"] = "unknown";
            if (mutation == "null") node["married_or_roommate"]!["value"] = null;
            if (mutation == "wrong_type") node["married_or_roommate"]!["value"] = "false";
        });
        AssertBlocked(snapshot);
    }

    [Theory]
    [InlineData("grandpa_factor_satisfied", "true")]
    [InlineData("house_upgrade", "null")]
    [InlineData("location_accessible", "null")]
    [InlineData("robin_present_at_counter", "\"true\"")]
    public void JointConditionAndServiceProjectionMustBeExplicit(string field, string json)
    {
        var snapshot = Snapshot();
        Mutate(snapshot, "world_progress", node => node["marriage_house"]!["value"]![field] = JsonNode.Parse(json));
        AssertBlocked(snapshot);
    }

    [Theory]
    [InlineData("level_after", "3")]
    [InlineData("inventory_item_count", "-1")]
    [InlineData("construction_days", "0")]
    [InlineData("price", "null")]
    public void IncompleteQuotesCannotBecomeCurrentBudgets(string field, string json)
    {
        var snapshot = Snapshot();
        Mutate(snapshot, "world_progress", node => node["marriage_house"]!["value"]!["house_upgrade"]![field] = JsonNode.Parse(json));
        AssertBlocked(snapshot);
    }

    [Theory]
    [InlineData("year", "0")]
    [InlineData("year", "null")]
    [InlineData("season", "\"unknown\"")]
    [InlineData("day", "29")]
    [InlineData("time", "1260")]
    [InlineData("time_of_day", "1120")]
    public void CalendarAndClockCannotBecomeGuessedConstructionOrWeddingBudgets(string field, string json)
    {
        var snapshot = Snapshot();
        Mutate(snapshot, "time", node => node[field] = JsonNode.Parse(Field(JsonSerializer.Deserialize<JsonElement>(json)).GetRawText()));
        AssertBlocked(snapshot);
    }

    internal static SnapshotEnvelope Snapshot(bool married = false, int level = 1, bool engaged = false,
        bool roommate = false, string spouse = "Abigail", int days = -1)
    {
        var facts = new Dictionary<string, object?>
        {
            ["married_or_roommate"] = married, ["engaged"] = engaged, ["has_pending_roommate"] = roommate,
            ["spouse"] = spouse, ["farmhouse_upgrade_level"] = level, ["days_until_farmhouse_upgrade"] = days, ["money"] = 80000
        };
        var progress = facts.ToDictionary(pair => pair.Key == "has_pending_roommate" ? "pending_roommate" : pair.Key, pair => pair.Value);
        progress["grandpa_factor_satisfied"] = married && level >= 2;
        progress["location_accessible"] = true;
        progress["is_current_location"] = false;
        progress["building_under_construction"] = false;
        progress["robin_present_at_counter"] = false;
        progress["house_upgrade"] = level == 3 ? null : new
        {
            level_before = level, level_after = level + 1, price = level == 0 ? 10000 : level == 1 ? 65000 : 100000,
            required_item_id = level == 0 ? "(O)388" : level == 1 ? "(O)709" : "",
            required_item_count = level == 0 ? 450 : level == 1 ? 100 : 0, inventory_item_count = 100,
            construction_days = 3, action_status = "science_house_not_current_location"
        };
        var snapshot = new SnapshotEnvelope { GameVersion = "1.6.15", GameTick = 37, State = new()
        {
            ["player"] = JsonSerializer.SerializeToElement(facts.ToDictionary(pair => pair.Key, pair => Field(pair.Value))),
            ["world_progress"] = JsonSerializer.SerializeToElement(new { marriage_house = Field(progress) }),
            ["time"] = JsonSerializer.SerializeToElement(new
                { year = Field(2), season = Field("winter"), day = Field(17), time = Field(1110) })
        } };
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }

    internal static JsonElement Field(object? value) => JsonSerializer.SerializeToElement(new
        { status = "available", value, confidence = 1, adapter = "test", read_at_tick = 1, source = new { kind = "test", path = "test" } });
    internal static void Mutate(SnapshotEnvelope snapshot, string section, Action<JsonNode> mutate)
    {
        var node = JsonNode.Parse(snapshot.State[section].GetRawText())!;
        mutate(node);
        snapshot.State[section] = JsonSerializer.SerializeToElement(node);
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
    }

    private static void AssertBlocked(SnapshotEnvelope snapshot)
    {
        var result = new CurrentMarriageHouseStateBuilder().Build(snapshot);
        Assert.Equal("blocked", result.Status);
        Assert.NotEmpty(result.BlockingReasons);
        Assert.Null(result.CurrentCriterionSatisfied);
        Assert.Null(result.Money);
        Assert.Null(result.HouseUpgradeQuote);
        Assert.False(result.TrainingLabelEligible);
    }
}
