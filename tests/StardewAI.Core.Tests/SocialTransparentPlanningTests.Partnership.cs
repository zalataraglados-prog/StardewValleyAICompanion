using System.Text.Json;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    private const string AbigailPartnershipNpc = "[{\"name\":\"Abigail\",\"display_name\":\"Abigail\",\"master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":10,\"is_villager\":true,\"is_datably_flagged\":true,\"is_married_or_engaged\":false,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"current_route_window_complete\":true}]";

    [Fact]
    public void BouquetUsesNative458BranchAndCompilesThroughSharedSocialExecutor()
    {
        var inventory = PartnershipItem("458", "(O)458", "Bouquet", "[]");
        var friendship = "[{\"npc_name\":\"Abigail\",\"points\":2000,\"heart_level\":8,\"status\":\"Friendly\",\"is_dating\":false,\"is_divorced\":false}]";
        var snapshot = CompleteSocialSnapshot(AbigailPartnershipNpc, friendship, inventory);

        var option = Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "social.advance_partnership" }).Options);

        Assert.Equal("confirmation_required", option.Status);
        var candidate = Assert.Single(option.SocialCandidates);
        Assert.True(candidate.Available);
        Assert.Equal("partnership_bouquet_current", candidate.Kind);
        Assert.Equal("(O)458", candidate.QualifiedItemId);

        var ranked = new StardewAI.Contracts.Training.PolicyEventCandidatePrediction
        {
            CandidateId = candidate.CandidateId,
            OptionId = "social.advance_partnership",
            Kind = candidate.Kind,
            Available = true,
            LocationId = candidate.LocationId,
            TileX = candidate.TileX,
            TileY = candidate.TileY,
            EstimatedTicks = candidate.EstimatedTicks,
            Parameters = candidate.Parameters,
            ExpectedEffect = candidate.ExpectedEffect
        };
        var plan = new DailyPlanCompiler().Compile(new[] { ranked }, snapshot.StateHash);
        Assert.Equal(2, plan.Steps.Length);
        Assert.Equal("move_to_tile", plan.Steps[0].Kind);
        Assert.Equal("social_interact", plan.Steps[1].Kind);
        Assert.Contains(plan.Steps[1].Parameters, p => p.Name == "social_action_kind" && p.Value == "bouquet");
    }

    [Fact]
    public void MermaidPendantUsesNative460BranchAndRequiresHouseAndTenHearts()
    {
        var inventory = PartnershipItem("460", "(O)460", "Mermaid's Pendant", "[]");
        var lowFriendship = "[{\"npc_name\":\"Abigail\",\"points\":2499,\"heart_level\":9,\"status\":\"Dating\",\"is_dating\":true,\"is_divorced\":false}]";
        var blocked = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(CompleteSocialSnapshot(AbigailPartnershipNpc, lowFriendship, inventory), new[] { "social.advance_partnership" }).Options).SocialCandidates);
        Assert.Contains("partnership_marriage_proposal_requires_2500_points", blocked.BlockReasons);
        Assert.Contains("partnership_proposal_requires_house_upgrade", blocked.BlockReasons);

        var readyFriendship = "[{\"npc_name\":\"Abigail\",\"points\":2500,\"heart_level\":10,\"status\":\"Dating\",\"is_dating\":true,\"is_divorced\":false}]";
        var ready = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(CompleteSocialSnapshot(AbigailPartnershipNpc, readyFriendship, inventory, farmhouseUpgradeLevel: 1), new[] { "social.advance_partnership" }).Options).SocialCandidates);
        Assert.True(ready.Available);
        Assert.Equal("partnership_propose_marriage_current", ready.Kind);
        Assert.Equal("(O)460", ready.QualifiedItemId);
    }

    [Fact]
    public void KrobusRoommateProposalRequiresExactContextTag()
    {
        var npc = "[{\"name\":\"Krobus\",\"display_name\":\"Krobus\",\"master_data_present\":true,\"current_instance_loaded\":true,\"location_id\":\"Town\",\"tile_x\":10,\"tile_y\":10,\"is_villager\":true,\"is_datably_flagged\":false,\"is_married_or_engaged\":false,\"simple_non_villager_npc\":false,\"is_invisible\":false,\"is_sleeping\":false,\"schedule_loaded\":true,\"can_socialize\":true,\"can_socialize_complete\":true,\"current_route_window_complete\":true}]";
        var friendship = "[{\"npc_name\":\"Krobus\",\"points\":2500,\"heart_level\":10,\"status\":\"Friendly\",\"is_dating\":false,\"is_divorced\":false}]";
        var inventory = PartnershipItem("808", "(O)808", "Void Ghost Pendant", "[\"propose_roommate_Krobus\"]");

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(CompleteSocialSnapshot(npc, friendship, inventory, farmhouseUpgradeLevel: 1), new[] { "social.advance_partnership" }).Options).SocialCandidates);

        Assert.True(candidate.Available);
        Assert.Equal("partnership_propose_roommate_current", candidate.Kind);
        Assert.Contains(candidate.Parameters, p => p.Name == "expected_roommate_marriage_after" && p.Value == "true");
    }

    [Fact]
    public void OrdinaryGiftOptionStillRejectsPartnershipItems()
    {
        var inventory = PartnershipItem("458", "(O)458", "Bouquet", "[]");
        var snapshot = CompleteSocialSnapshot(AbigailPartnershipNpc, inventoryValue: inventory);

        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(snapshot, new[] { "social.gift_npc" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("social_gift_special_switch_item_branch_unsupported", candidate.BlockReasons);
    }

    [Fact]
    public void PartnershipFailsClosedWhenNpcCommitmentFactIsMissing()
    {
        var npc = AbigailPartnershipNpc.Replace(",\"is_married_or_engaged\":false", string.Empty);
        var friendship = "[{\"npc_name\":\"Abigail\",\"points\":2000,\"heart_level\":8,\"status\":\"Friendly\",\"is_dating\":false,\"is_divorced\":false}]";
        var candidate = Assert.Single(Assert.Single(new CandidateOptionAvailabilityEvaluator()
            .Evaluate(CompleteSocialSnapshot(npc, friendship, PartnershipItem("458", "(O)458", "Bouquet", "[]")), new[] { "social.advance_partnership" }).Options).SocialCandidates);

        Assert.False(candidate.Available);
        Assert.Contains("partnership_target_commitment_fact_incomplete", candidate.BlockReasons);
    }

    [Fact]
    public void ProposalFailsClosedWhenPlayerCommitmentFactIsMissing()
    {
        var friendship = "[{\"npc_name\":\"Abigail\",\"points\":2500,\"heart_level\":10,\"status\":\"Dating\",\"is_dating\":true,\"is_divorced\":false}]";
        var snapshot = CompleteSocialSnapshot(
            AbigailPartnershipNpc,
            friendship,
            PartnershipItem("460", "(O)460", "Mermaid's Pendant", "[]"),
            farmhouseUpgradeLevel: 1);
        var state = snapshot.State.ToDictionary(pair => pair.Key, pair => pair.Value);
        var player = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(state["player"].GetRawText())!;
        player.Remove("engaged");
        state["player"] = JsonSerializer.SerializeToElement(player);

        var candidate = Assert.Single(SocialCandidateBuilder.Build(
            Snapshot(JsonSerializer.Serialize(state)),
            "social.advance_partnership"));

        Assert.False(candidate.Available);
        Assert.Contains("partnership_player_commitment_fact_incomplete", candidate.BlockReasons);
    }

    private static string PartnershipItem(string itemId, string qualifiedId, string name, string tags) =>
        "[{\"slot_index\":0,\"item_id\":\"" + itemId + "\",\"qualified_item_id\":\"" + qualifiedId + "\",\"display_name\":\"" + name + "\",\"stack\":1,\"quality\":0,\"is_object\":true,\"can_be_given_as_gift\":true,\"base_tag_not_giftable\":false,\"context_tags\":" + tags + ",\"is_empty\":false}]";
}
