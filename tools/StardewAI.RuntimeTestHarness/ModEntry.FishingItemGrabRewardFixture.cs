using StardewModdingAPI;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Menus;
using StardewValley.Tools;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupFishingItemGrabReward(
        TrainingExecutionRequest request)
    {
        var started = DateTimeOffset.UtcNow.ToString("O");
        var reasons = ValidateExecutionRequest(request);
        var source = request.FishingRewardMenuSource ?? 0;
        var rewardId = string.IsNullOrWhiteSpace(
            request.FishingRewardQualifiedItemId)
                ? "(O)145"
                : request.FishingRewardQualifiedItemId;
        var rewardStack = request.FishingRewardStackBefore ?? 1;
        if (!Context.IsWorldReady)
            reasons.Add("fishing_item_grab_fixture_world_not_ready");
        if (source is not 0 and not ItemGrabMenu.source_fishingChest)
            reasons.Add("fishing_item_grab_fixture_source_unsupported");
        if (rewardStack <= 0)
            reasons.Add("fishing_item_grab_fixture_reward_stack_invalid");
        if (reasons.Count > 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_fishing_item_grab_reward",
                "fishing_item_grab.fixture=ready",
                FishingItemGrabRewardObservedEffect(),
                reasons.ToArray());
        }

        Game1.exitActiveMenu();
        StopAllMovement();
        var maxItems = Math.Min(
            Game1.player.maxItems.Value,
            Game1.player.Items.Count);
        if (maxItems <= 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_fishing_item_grab_reward",
                "fishing_item_grab.fixture=ready",
                FishingItemGrabRewardObservedEffect(),
                "fishing_item_grab_fixture_inventory_unavailable");
        }

        if (request.DebugFillInventory)
        {
            FillInventoryWithBlockingItems(
                ItemRegistry.GetDataOrErrorItem(rewardId).ItemId);
        }
        else
        {
            Game1.player.Items[maxItems - 1] = null;
        }

        var rod = Game1.player.Items
            .OfType<FishingRod>()
            .FirstOrDefault() ?? new FishingRod(4);
        var reward = ItemRegistry.Create(rewardId, rewardStack);
        var menu = new ItemGrabMenu(
            new List<Item> { reward },
            rod).setEssential(essential: true);
        menu.source = source;
        Game1.activeClickableMenu = menu;

        var verified = ReferenceEquals(Game1.activeClickableMenu, menu) &&
            menu.context is FishingRod &&
            menu.essential &&
            menu.source == source &&
            FishingItemGrabPendingCount(menu) == 1 &&
            FishingRewardMatchesRequest(
                reward,
                new TrainingExecutionRequest
                {
                    FishingRewardQualifiedItemId = rewardId,
                    FishingRewardStackBefore = rewardStack,
                    FishingRewardQuality = reward.Quality
                }) &&
            CanAcceptFishingRewardWithoutSideEffects(reward) ==
                !request.DebugFillInventory;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = started,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_fishing_item_grab_reward",
            PrimitiveVerificationStatus = verified
                ? "verified"
                : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_fixture_used_vanilla_FishingRod_ItemGrabMenu_constructor",
                    request.DebugFillInventory
                        ? "isolated_fixture_inventory_full"
                        : "isolated_fixture_inventory_capacity_available"
                }
                : new[] { "fishing_item_grab_fixture_postcondition_failed" },
            RequestedEffect =
                "fishing_item_grab.fixture=ready;source=" + source +
                ";reward=" + rewardId + "x" + rewardStack +
                ";inventory_full=" +
                request.DebugFillInventory.ToString().ToLowerInvariant(),
            ObservedEffect = FishingItemGrabRewardObservedEffect(),
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "fishing_item_grab_fixture_postcondition_failed" },
            ChangedFacts = new[]
            {
                new SimulatedFactChange
                {
                    Path = "menus.fishing_item_grab.fixture",
                    Before = "absent",
                    After = "ready"
                }
            }
        };
    }
}
