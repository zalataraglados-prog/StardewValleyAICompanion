using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.Contracts.State;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed class CurrentMarriageHouseSettlementTests
{
    [Fact]
    public void ProposalSkipsNativeForbiddenDatesAndIsNotAnAuthorizationOrCompletedMarriage()
    {
        var result = new CurrentMarriageHouseSettlementProjector().Build(Snapshot(blocked: new[] { "winter20", "winter21" }));
        Assert.Equal("ready_conditional_settlement_projection", result.Status);
        Assert.Equal(212, result.CurrentTotalDay);
        Assert.Equal(217, result.ConditionalProposalWeddingTotalDay);
        Assert.Equal(new[] { 215, 216 }, result.SkippedProposalWeddingDays);
        Assert.True(result.ConditionalProposalBeforeEvaluation);
        Assert.Null(result.ExistingPartnershipSettlementTotalDay);
        Assert.Null(result.ExistingJointSettlementTotalDay);
        Assert.Null(result.ExistingJointBeforeEvaluation);
        Assert.False(result.ProposalLegalityVerified);
        Assert.False(result.PurchaseLegalityVerified);
        Assert.False(result.TrainingLabelEligible);
        Assert.False(result.NativeOutcomeVerified);
    }

    [Fact]
    public void ExistingBranchesJoinByActualSettlementNotByThePurchaseOrProposalDate()
    {
        var snapshot = Snapshot(engaged: true, days: 2);
        var result = new CurrentMarriageHouseSettlementProjector().Build(snapshot);
        Assert.Equal(214, result.ExistingHouseSettlementTotalDay);
        Assert.Equal(215, result.ExistingPartnershipSettlementTotalDay);
        Assert.Equal(215, result.ExistingJointSettlementTotalDay);
        Assert.True(result.ExistingJointBeforeEvaluation);
        Assert.Null(result.ConditionalNextHousePurchaseSettlementTotalDay);
    }

    [Fact]
    public void KnownLateDatesAreRetainedButCannotMeetTheExclusiveEvaluationDeadline()
    {
        var snapshot = Snapshot(married: true);
        CurrentMarriageHouseStateTests.Mutate(snapshot, "time", node => node["day"] = JsonNode.Parse(CurrentMarriageHouseStateTests.Field(28).GetRawText()));
        CurrentMarriageHouseStateTests.Mutate(snapshot, "world_progress", node => node["game_state_query_calendar_state"]!["value"]!["current_total_day"] = 223);
        var result = new CurrentMarriageHouseSettlementProjector().Build(snapshot);
        Assert.Equal(226, result.ConditionalProposalWeddingTotalDay);
        Assert.False(result.ConditionalProposalBeforeEvaluation);
        Assert.Equal(226, result.ConditionalNextHousePurchaseSettlementTotalDay);
        Assert.Null(result.ExistingJointSettlementTotalDay);
    }

    [Theory]
    [InlineData("missing_calendar")]
    [InlineData("schema")]
    [InlineData("year")]
    [InlineData("seed")]
    [InlineData("source_day")]
    [InlineData("date_count")]
    [InlineData("duplicate_date")]
    [InlineData("unknown_date")]
    [InlineData("festival_omitted")]
    [InlineData("festival_set")]
    [InlineData("string_day")]
    public void MissingOrForeignCalendarEvidenceCannotBecomeAFeasibleWindow(string change)
    {
        var snapshot = Snapshot();
        CurrentMarriageHouseStateTests.Mutate(snapshot, "world_progress", node =>
        {
            var parent = node["game_state_query_calendar_state"]!["value"]!;
            var calendar = parent["wedding_calendar"]!;
            switch (change)
            {
                case "missing_calendar": parent.AsObject().Remove("wedding_calendar"); break;
                case "schema": calendar["schema_version"] = "future"; break;
                case "year": calendar["source_year"] = 1; break;
                case "seed": calendar["save_unique_id"] = 99; break;
                case "source_day": parent["current_total_day"] = 211; break;
                case "date_count": calendar["evaluated_date_count"] = 111; break;
                case "duplicate_date": calendar["blocked_date_keys"]!.AsArray().Add("winter25"); break;
                case "unknown_date": calendar["blocked_date_keys"]!.AsArray().Add("winter29"); break;
                case "festival_omitted": calendar["blocked_date_keys"] = new JsonArray(); break;
                case "festival_set": calendar["festival_date_keys"] = new JsonArray("spring13"); break;
                case "string_day": parent["current_total_day"] = "212"; break;
            }
        });
        var result = new CurrentMarriageHouseSettlementProjector().Build(snapshot);
        Assert.Equal("blocked", result.Status);
        Assert.NotEmpty(result.BlockingReasons);
        Assert.Null(result.ConditionalProposalWeddingTotalDay);
        Assert.False(result.TrainingLabelEligible);
    }

    [Fact]
    public void DueTodayEngagementCannotPretendThatTheUnobservedNativeDayPhaseAlreadyRan()
    {
        var snapshot = Snapshot(engaged: true);
        CurrentMarriageHouseStateTests.Mutate(snapshot, "npcs", node => node["friendships"]!["value"]![0]!["wedding_date_total_days"] = 212);
        var result = new CurrentMarriageHouseSettlementProjector().Build(snapshot);
        Assert.Equal("blocked", result.Status);
        Assert.Contains("marriage_house_due_wedding_requires_native_day_phase_evidence", result.BlockingReasons);
    }

    internal static SnapshotEnvelope Snapshot(bool married = false, bool engaged = false, int days = -1, string[]? blocked = null)
    {
        var snapshot = CurrentMarriageHouseStateTests.Snapshot(married, 1, engaged, days: days);
        var keys = new[] { "winter25" };
        var projection = new NativeWeddingCalendarProjection(442159967, 2, keys,
            keys.Concat(blocked ?? Array.Empty<string>()).Distinct().ToArray());
        CurrentMarriageHouseStateTests.Mutate(snapshot, "world_progress", node => node["game_state_query_calendar_state"] =
            JsonNode.Parse(CurrentMarriageHouseStateTests.Field(new { current_total_day = 212, save_unique_id = 442159967UL,
                festival_date_keys = keys, wedding_calendar = projection }).GetRawText()));
        snapshot.State["npcs"] = JsonSerializer.SerializeToElement(new
        {
            friendships = CurrentMarriageHouseStateTests.Field(new[] { new { npc_name = "Abigail", status = engaged ? "Engaged" : "Dating",
                is_engaged = engaged, roommate_marriage = false, wedding_date_total_days = 215 } })
        });
        snapshot.StateHash = SnapshotHash.ComputeStateHash(snapshot.State);
        return snapshot;
    }
}
