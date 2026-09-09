using System.Text.Json;
using StardewAI.Core.OptionRegistry;
using StardewAI.Core.Training;

namespace StardewAI.Core.Tests;

public sealed partial class SocialTransparentPlanningTests
{
    [Fact]
    public void SharedGiftBindingReturnsExactPositiveOwnedItem()
    {
        var result = new SocialGiftInventoryBindingResolver().Resolve(
            CompleteSocialSnapshot(),
            "Abigail");

        Assert.Equal("exact", result.Status);
        var binding = Assert.Single(result.Bindings);
        Assert.True(binding.Available);
        Assert.True(binding.EvidenceComplete);
        Assert.Equal(0, binding.SlotIndex);
        Assert.Equal("(O)66", binding.QualifiedItemId);
        Assert.Equal("love", binding.GiftTaste);
        Assert.Equal(80, binding.ExpectedFriendshipDelta);
        Assert.Equal(330, binding.ExpectedFriendshipPointsAfter);
    }

    [Fact]
    public void SharedGiftBindingExactlyExcludesNonObjectWithoutTasteLookup()
    {
        const string inventory =
            "[{\"slot_index\":0,\"item_id\":\"GoldPickaxe\"," +
            "\"qualified_item_id\":\"(T)GoldPickaxe\",\"stack\":1," +
            "\"quality\":0,\"is_object\":false,\"is_empty\":false}]";
        var result = new SocialGiftInventoryBindingResolver().Resolve(
            CompleteSocialSnapshot(
                inventoryValue: inventory,
                giftTasteField:
                    "{\"value\":[],\"status\":\"available\"}"),
            "Abigail");

        Assert.Equal("exact", result.Status);
        var binding = Assert.Single(result.Bindings);
        Assert.True(binding.EvidenceComplete);
        Assert.False(binding.Available);
        Assert.Equal(
            new[] { "social_gift_item_not_object" },
            binding.BlockReasons);
    }

    [Fact]
    public void SharedGiftBindingBlocksGiftableItemWithMissingNativeTaste()
    {
        var result = new SocialGiftInventoryBindingResolver().Resolve(
            CompleteSocialSnapshot(
                giftTasteField:
                    "{\"value\":[],\"status\":\"available\"}"),
            "Abigail");

        Assert.Equal("blocked", result.Status);
        var binding = Assert.Single(result.Bindings);
        Assert.False(binding.EvidenceComplete);
        Assert.Contains(
            "social_gift_taste_incomplete",
            binding.BlockReasons);
    }

    [Fact]
    public void DynamicTrackingIntentReusesExistingSocialRouteCandidate()
    {
        var snapshot = CompleteSocialSnapshot(currentTime: 600);
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(snapshot, JsonOptions));
        var directive = new CurrentSocialDynamicTrackingDirective
        {
            NpcName = "Abigail",
            ObservedLocationName = "Town",
            ObservedTileX = 10,
            ObservedTileY = 10,
            ObservedAtTime = 600,
            CandidateFamilies = new[] { "social.talk_npc" }
        };

        var result = new CurrentSocialDynamicTrackingIntentCompiler().Compile(
            document.RootElement,
            directive);

        Assert.Equal("pass", result.Status);
        var intent = Assert.Single(result.Intents);
        Assert.Equal("social.talk_npc", intent.OptionId);
        Assert.Equal("social:talk:Abigail", intent.CandidateId);
        Assert.Equal(
            "social_candidate_builder_to_daily_plan_compiler_to_existing_native_executor",
            intent.CompileChain);
    }

    [Fact]
    public void DynamicTrackingIntentAcceptsCurrentRollingSnapshotTime()
    {
        var snapshot = CompleteSocialSnapshot(currentTime: 910);
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(snapshot, JsonOptions));
        var directive = new CurrentSocialDynamicTrackingDirective
        {
            NpcName = "Abigail",
            ObservedLocationName = "Town",
            ObservedTileX = 10,
            ObservedTileY = 10,
            ObservedAtTime = 910,
            CandidateFamilies = new[] { "social.talk_npc" }
        };

        var result = new CurrentSocialDynamicTrackingIntentCompiler().Compile(
            document.RootElement,
            directive);

        Assert.Equal("pass", result.Status);
        Assert.Single(result.Intents);
    }

    [Fact]
    public void DynamicTrackingIntentBlocksStaleObservedTime()
    {
        var snapshot = CompleteSocialSnapshot(currentTime: 920);
        using var document = JsonDocument.Parse(
            JsonSerializer.Serialize(snapshot, JsonOptions));
        var directive = new CurrentSocialDynamicTrackingDirective
        {
            NpcName = "Abigail",
            ObservedLocationName = "Town",
            ObservedTileX = 10,
            ObservedTileY = 10,
            ObservedAtTime = 910,
            CandidateFamilies = new[] { "social.talk_npc" }
        };

        var result = new CurrentSocialDynamicTrackingIntentCompiler().Compile(
            document.RootElement,
            directive);

        Assert.Equal("blocked", result.Status);
        Assert.Equal(
            new[] { "current_social_dynamic_tracking_observed_time_mismatch" },
            result.BlockingReasons);
    }
}
