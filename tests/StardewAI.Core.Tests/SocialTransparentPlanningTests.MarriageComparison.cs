using System.Text.Json;
using System.Text.Json.Nodes;
using StardewAI.GoalConditionedBootstrap;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void MarriageComparisonReusesTheOriginalGiftCompilerWithoutThe1975FriendshipGoalGate()
    {
        var f = MarriageComparisonFiles();
        var report = CurrentMarriageTeacherPreferenceBuilder.Build(f.Intake, f.Calibration, f.Ledger);
        Assert.True(report.IndependentPairwiseLabelEligible, MarriageComparisonSummary(report));
        Assert.False(report.CompletePreferenceLabelEligible);
        var pair = Assert.Single(report.PairwisePreferences);
        var chosen = report.Candidates.Single(row => row.CandidateId == pair.PreferredCandidateId);
        Assert.Equal(2420, chosen.FriendshipPointsBefore);
        Assert.Equal(2500, chosen.FriendshipPointsAfter);
        Assert.Equal(100, chosen.GiftSaleValue);
        Assert.Contains("liquidation_cost", pair.FirstDifferingAuthoritativeCriterion);
        Assert.Equal(5, report.Intake.ConsideredOptionIds.Length);
        Assert.NotEmpty(report.UncomparedCandidateIds);
        Assert.Contains(report.Candidates, row => row.Reasons.Contains("original_other_family_retained_not_a_negative_label"));
        Assert.All(report.DecisionCandidates, row => { Assert.Equal(0, row.Score); Assert.Null(row.ModelScore); Assert.Equal(1, row.Rank); });
        File.WriteAllText(f.Preference, JsonSerializer.Serialize(report, JsonOptions));
        var replay = CurrentMarriageTeacherPreferenceBuilder.Verify(f.Preference);
        Assert.Equal(JsonSerializer.Serialize(report), JsonSerializer.Serialize(replay));
        Assert.Throws<InvalidDataException>(() => CurrentMarriageTeacherPreferenceBuilder.Compile(f.Preference));
        var dispatch = CurrentMarriageTeacherPreferenceBuilder.Compile(f.Preference, independentPairs: true);
        Assert.Equal("goal.social.marriage_and_house_upgrade", dispatch.Plan.GoalId);
        Assert.Equal("pending", dispatch.Queue.Status);
        Assert.All(dispatch.Queue.Items, item => Assert.Equal("pending", item.Status));
        Assert.Contains(dispatch.Plan.Steps, step => step.Kind == "social_interact");
        Assert.False(report.NativeOutcomeVerified);
        Assert.False(report.FullCriterionVerified);
        Assert.False(report.FormalProductTrainingAuthorized);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void RegisteredGiftOrProposalReservationsAreNotSilentlyBorrowed(int reservedSlot)
    {
        var f = MarriageComparisonFiles();
        ReserveMarriageFixture(f, reservedSlot);
        var report = CurrentMarriageTeacherPreferenceBuilder.Build(f.Intake, f.Calibration, f.Ledger);
        Assert.False(report.IndependentPairwiseLabelEligible);
        Assert.Null(report.SelectedCandidate);
        Assert.Empty(report.PairwisePreferences);
        Assert.NotEmpty(report.UncomparedCandidateIds);
        Assert.Contains(report.Candidates, row => row.Reasons.Any(reason => reason.Contains("reservation", StringComparison.Ordinal)));
    }

    [Fact]
    public void PendingHouseConstructionDoesNotPreventParallelPartnerPreparationOrCountAsCompleted()
    {
        var f = MarriageComparisonFiles(houseLevel: 1, constructionDays: 2);
        var report = CurrentMarriageTeacherPreferenceBuilder.Build(f.Intake, f.Calibration, f.Ledger);
        Assert.True(report.IndependentPairwiseLabelEligible, MarriageComparisonSummary(report));
        Assert.True(report.Intake.CurrentState.NativeSettlementPending);
        Assert.False(report.Intake.CurrentState.CurrentCriterionSatisfied);
        Assert.Equal(214, report.Settlement.ExistingHouseSettlementTotalDay);
        Assert.All(report.Candidates.Where(row => row.Status == "admitted_current_partner_preparation"),
            row => Assert.Equal(215, row.ConditionalJointSettlementTotalDay));
    }

    [Theory]
    [InlineData("missing_source")]
    [InlineData("foreign")]
    [InlineData("missing_arrays")]
    [InlineData("duplicate_arrays")]
    [InlineData("production")]
    public void MissingOrUnbudgetedCommitmentsCannotBecomeImplicitlyEmpty(string change)
    {
        var f = MarriageComparisonFiles();
        var ledger = JsonNode.Parse(File.ReadAllText(f.Ledger))!;
        if (change == "missing_source") ledger["source_state_hash"] = "";
        if (change == "foreign") ledger["player_id"] = "2";
        if (change == "missing_arrays") ledger.AsObject().Remove("material_reservations");
        if (change == "production") ledger["crop_planting_commitments"]!.AsArray().Add(new JsonObject { ["status"] = "active" });
        File.WriteAllText(f.Ledger, ledger.ToJsonString());
        if (change == "duplicate_arrays")
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                using var document = JsonDocument.Parse(ledger.ToJsonString());
                foreach (var property in document.RootElement.EnumerateObject()) property.WriteTo(writer);
                writer.WriteStartArray("MATERIAL_RESERVATIONS"); writer.WriteEndArray();
                writer.WriteEndObject();
            }
            File.WriteAllText(f.Ledger, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
        }
        Assert.Throws<InvalidDataException>(() => CurrentMarriageTeacherPreferenceBuilder.Build(f.Intake, f.Calibration, f.Ledger));
        Assert.False(File.Exists(f.Preference));
    }

    [Fact]
    public void HistoricalLedgerProvenanceRemainsValidWhenCurrentInventoryStillCoversItsReservations()
    {
        var f = MarriageComparisonFiles();
        var ledger = JsonNode.Parse(File.ReadAllText(f.Ledger))!;
        ledger["source_state_hash"] = "historical-last-mutation";
        ledger["revision"] = 4;
        File.WriteAllText(f.Ledger, ledger.ToJsonString());
        var original = File.ReadAllBytes(f.Ledger);
        var report = CurrentMarriageTeacherPreferenceBuilder.Build(f.Intake, f.Calibration, f.Ledger);
        Assert.True(report.IndependentPairwiseLabelEligible, MarriageComparisonSummary(report));
        Assert.Equal(original, File.ReadAllBytes(f.Ledger));
        Assert.All(report.Candidates, row => Assert.Equal(report.Intake.CurrentState.SourceStateHash, row.SourceStateHash));
    }

    [Theory]
    [InlineData("native_outcome")]
    [InlineData("pair")]
    [InlineData("cost")]
    [InlineData("denominator")]
    [InlineData("dependency")]
    public void StoredClaimsCostsPairsAndOriginalSourcesAreRebuiltRatherThanTrusted(string mutation)
    {
        var f = MarriageComparisonFiles();
        var result = CurrentMarriageTeacherPreferenceBuilder.Build(f.Intake, f.Calibration, f.Ledger);
        Assert.True(result.IndependentPairwiseLabelEligible, MarriageComparisonSummary(result));
        var raw = JsonSerializer.SerializeToNode(result, JsonOptions)!;
        if (mutation == "native_outcome") raw["nativeOutcomeVerified"] = true;
        if (mutation == "pair") raw["pairwisePreferences"]!.AsArray().Clear();
        if (mutation == "cost") raw["candidates"]![0]!["giftSaleValue"] = -1;
        if (mutation == "denominator") raw["intake"]!["availability"]!["options"]!.AsArray().RemoveAt(0);
        if (mutation == "dependency") raw["bindingRole"] = "invented-direct-gift-owner";
        File.WriteAllText(f.Preference, raw.ToJsonString());
        Assert.Throws<InvalidDataException>(() => CurrentMarriageTeacherPreferenceBuilder.Verify(f.Preference));
        Assert.Throws<InvalidDataException>(() => CurrentMarriageTeacherPreferenceBuilder.Compile(f.Preference, independentPairs: true));
    }
}
