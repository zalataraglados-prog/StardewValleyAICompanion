using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewObject = StardewValley.Object;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string AdventureGuildRewardRuntimeNativeContract =
        "AdventureGuild.checkAction_gil_tile->gil_all_complete_unclaimed_goals->DialogueBox_optional->ItemGrabMenu->receiveLeftClick_each_reward->OnRewardCollected_Gil_goalId";

    private void StartAdventureGuildRewardClaim(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (!request.TargetTileX.HasValue || !request.TargetTileY.HasValue ||
            !request.StandTileX.HasValue || !request.StandTileY.HasValue ||
            !request.AdventureGuildRewardActionTileIndex.HasValue ||
            !request.AdventureGuildRewardPendingGoalCount.HasValue ||
            !request.AdventureGuildRewardItemCount.HasValue ||
            !request.AdventureGuildRewardDialogueCount.HasValue ||
            !request.AdventureGuildRewardInventoryMaxItems.HasValue ||
            !request.AdventureGuildRewardInventoryOccupiedSlots.HasValue ||
            request.AdventureGuildRewardInventoryCapacitySufficient != true)
            reasons.Add("adventure_guild_reward_typed_fields_required");
        if (!TryParseAdventureGuildRewardGoals(request.AdventureGuildRewardGoalsJson, out var expectedGoals))
            reasons.Add("adventure_guild_reward_goals_json_invalid");
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp || Game1.player.UsingTool || !Game1.player.CanMove)
            reasons.Add("adventure_guild_reward_player_or_menu_not_ready");
        var location = Game1.currentLocation as AdventureGuild;
        var target = new Point(request.TargetTileX ?? -1, request.TargetTileY ?? -1);
        var stand = new Point(request.StandTileX ?? -1, request.StandTileY ?? -1);
        var liveGoals = location is null ? Array.Empty<AdventureGuildRewardGoalRef>() : ReadLiveAdventureGuildRewardGoals(Game1.player);
        if (location?.GetType() != typeof(AdventureGuild) || request.LocationId != "AdventureGuild")
            reasons.Add("adventure_guild_reward_exact_current_location_required");
        if (!AdventureGuildRewardEndpointMatches(location, target, stand, request.AdventureGuildRewardActionTileIndex ?? -1))
            reasons.Add("adventure_guild_reward_endpoint_drifted");
        if (expectedGoals.Length == 0 || expectedGoals.Length != request.AdventureGuildRewardPendingGoalCount ||
            expectedGoals.Length != request.AdventureGuildRewardItemCount ||
            expectedGoals.Any(goal => string.IsNullOrWhiteSpace(goal.RewardItemId)))
            reasons.Add("adventure_guild_reward_complete_item_backed_batch_required");
        if (!AdventureGuildRewardGoalsEqual(liveGoals, expectedGoals) ||
            AdventureGuildRewardIdentity.Compute(liveGoals) != request.AdventureGuildRewardBatchFingerprint ||
            liveGoals.Count(goal => goal.RewardDialogueShouldShow) != request.AdventureGuildRewardDialogueCount)
            reasons.Add("adventure_guild_reward_batch_projection_drifted");
        if (Game1.player.MaxItems != request.AdventureGuildRewardInventoryMaxItems ||
            Game1.player.Items.Take(Game1.player.MaxItems).Count(item => item is not null) != request.AdventureGuildRewardInventoryOccupiedSlots ||
            !AdventureGuildRewardBatchFits(Game1.player, liveGoals))
            reasons.Add("adventure_guild_reward_inventory_capacity_drifted");
        if (request.NativeContract != AdventureGuildRewardRuntimeNativeContract)
            reasons.Add("adventure_guild_reward_native_contract_mismatch");
        if (reasons.Count > 0 || location is null)
        {
            pending.Completion.SetResult(AdventureGuildRewardBlocked(request, reasons.ToArray()));
            return;
        }
        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(AdventureGuildRewardBlocked(request,
                "adventure_guild_reward_path_unavailable:" + pathReason));
            return;
        }
        activeAdventureGuildReward = new ActiveAdventureGuildReward(
            pending, location, target, stand, path, maxMovementTiles, liveGoals,
            liveGoals.ToDictionary(goal => goal.GoalId, goal => CountAdventureGuildReward(goal.RewardItemId), StringComparer.Ordinal));
    }

    private void TickAdventureGuildRewardClaimSafely()
    {
        var active = activeAdventureGuildReward;
        if (active is null) return;
        try
        {
            TickAdventureGuildRewardClaim(active);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Adventure Guild reward claim failed and was blocked: {ex}", StardewModdingAPI.LogLevel.Error);
            CompleteAdventureGuildReward(active, false, "adventure_guild_reward_executor_exception:" + ex.GetType().Name);
        }
    }

    private void TickAdventureGuildRewardClaim(ActiveAdventureGuildReward active)
    {
        if (active.Stage != AdventureGuildRewardStage.Move) active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteAdventureGuildReward(active, false, "adventure_guild_reward_timeout");
            return;
        }
        if (active.Stage == AdventureGuildRewardStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "adventure_guild_reward", out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompleteAdventureGuildReward(active, false, failure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving) return;
            if (!AdventureGuildRewardEndpointMatches(active.Location, active.Target, active.Stand,
                    active.Pending.Request.AdventureGuildRewardActionTileIndex ?? -1) ||
                !AdventureGuildRewardGoalsEqual(ReadLiveAdventureGuildRewardGoals(Game1.player), active.Goals) ||
                !AdventureGuildRewardBatchFits(Game1.player, active.Goals))
            {
                CompleteAdventureGuildReward(active, false, "adventure_guild_reward_state_drifted_while_moving");
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            active.NativeHandled = active.Location.checkAction(
                new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            if (!active.NativeHandled)
            {
                CompleteAdventureGuildReward(active, false, "adventure_guild_reward_native_check_action_rejected");
                return;
            }
            active.Stage = AdventureGuildRewardStage.DialogueOrMenu;
            return;
        }

        if (active.Stage == AdventureGuildRewardStage.DialogueOrMenu)
        {
            if (Game1.activeClickableMenu is DialogueBox dialogue)
            {
                if (!dialogue.transitioning && dialogue.safetyTimer <= 0 && active.ElapsedTicks % 12 == 0)
                {
                    dialogue.receiveLeftClick(dialogue.xPositionOnScreen + dialogue.width / 2,
                        dialogue.yPositionOnScreen + dialogue.height / 2);
                    active.DialogueClicks++;
                }
                return;
            }
            if (Game1.activeClickableMenu is ItemGrabMenu menu && ReferenceEquals(menu.context, active.Location))
            {
                active.Stage = AdventureGuildRewardStage.Transfer;
                return;
            }
            CompleteAdventureGuildReward(active, false, "adventure_guild_reward_native_dialogue_or_menu_missing");
            return;
        }

        if (active.Stage == AdventureGuildRewardStage.Transfer)
        {
            if (Game1.activeClickableMenu is not ItemGrabMenu menu || !ReferenceEquals(menu.context, active.Location))
            {
                CompleteAdventureGuildReward(active, false, "adventure_guild_reward_owned_item_menu_missing");
                return;
            }
            var slot = menu.ItemsToGrabMenu.actualInventory.ToList().FindIndex(item => item is not null);
            if (slot < 0)
            {
                if (!menu.readyToClose())
                {
                    CompleteAdventureGuildReward(active, false, "adventure_guild_reward_empty_menu_not_ready_to_close");
                    return;
                }
                Game1.exitActiveMenu();
                active.Stage = AdventureGuildRewardStage.Verify;
                return;
            }
            var item = menu.ItemsToGrabMenu.actualInventory[slot];
            if (item is null || item.SpecialVariable < 0 || item.SpecialVariable >= active.Goals.Length)
            {
                CompleteAdventureGuildReward(active, false, "adventure_guild_reward_menu_item_identity_invalid");
                return;
            }
            var goal = active.Goals[item.SpecialVariable];
            if (item.QualifiedItemId != goal.RewardItemId || !Game1.player.couldInventoryAcceptThisItem(item))
            {
                CompleteAdventureGuildReward(active, false, "adventure_guild_reward_menu_item_or_capacity_drifted");
                return;
            }
            var beforeCount = CountAdventureGuildReward(goal.RewardItemId);
            var position = InventorySlotScreenPosition(menu.ItemsToGrabMenu, slot);
            if (!position.HasValue)
            {
                CompleteAdventureGuildReward(active, false, "adventure_guild_reward_menu_slot_position_unavailable");
                return;
            }
            menu.receiveLeftClick(position.Value.X, position.Value.Y, playSound: true);
            if (!Game1.player.mailReceived.Contains(goal.GilMailFlag) ||
                CountAdventureGuildReward(goal.RewardItemId) != beforeCount + goal.RewardItemStack)
            {
                CompleteAdventureGuildReward(active, false, "adventure_guild_reward_native_item_click_receipt_mismatch");
                return;
            }
            active.CollectedItems++;
            return;
        }

        if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
        {
            CompleteAdventureGuildReward(active, false, "adventure_guild_reward_owned_menu_did_not_close");
            return;
        }
        var verified = active.CollectedItems == active.Goals.Length &&
            active.Goals.All(goal => Game1.player.mailReceived.Contains(goal.GilMailFlag) &&
                CountAdventureGuildReward(goal.RewardItemId) == active.ItemCountsBefore[goal.GoalId] + goal.RewardItemStack) &&
            AdventureGuildRewardNativeSideEffectsPresent(active.Goals);
        CompleteAdventureGuildReward(active, verified,
            verified ? Array.Empty<string>() : new[] { "adventure_guild_reward_final_receipt_mismatch" });
    }

    private void CompleteAdventureGuildReward(ActiveAdventureGuildReward active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        if (Game1.activeClickableMenu is ItemGrabMenu menu && ReferenceEquals(menu.context, active.Location) && menu.readyToClose())
            Game1.exitActiveMenu();
        activeAdventureGuildReward = null;
        var request = active.Pending.Request;
        var verification = verified
            ? new[]
            {
                "shared_BFS_reached_exact_Gil_adjacent_stand",
                "native_AdventureGuild_checkAction_gil_branch_handled",
                "native_optional_dialogue_chain_completed",
                "native_ItemGrabMenu_clicked_every_projected_reward",
                "native_OnRewardCollected_set_every_Gil_goal_flag",
                "native_reward_mail_and_flag_side_effects_present",
                "entire_batch_item_deltas_verified"
            }
            : reasons.Length == 0 ? new[] { "adventure_guild_reward_post_state_mismatch" } : reasons;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            ActualTicks = active.ElapsedTicks,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            TrainingImpactScope = "policy_training",
            PrimitiveKind = "claim_adventure_guild_reward",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verification,
            RequestedEffect = AdventureGuildRewardRequestedEffect(request),
            ObservedEffect = "claimed_goal_flags=" + active.Goals.Count(goal => Game1.player.mailReceived.Contains(goal.GilMailFlag)) +
                ";collected_items=" + active.CollectedItems + ";dialogue_clicks=" + active.DialogueClicks +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verification,
            AdventureGuildRewardBatchFingerprint = request.AdventureGuildRewardBatchFingerprint,
            AdventureGuildRewardClaimedGoalCount = active.Goals.Count(goal => Game1.player.mailReceived.Contains(goal.GilMailFlag)),
            AdventureGuildRewardCollectedItemCount = active.CollectedItems,
            AdventureGuildRewardDialogueClickCount = active.DialogueClicks,
            ChangedFacts = active.Goals.Select(goal => new SimulatedFactChange
            {
                Path = "quests.adventure_guild_reward.goals[" + goal.GoalId + "].collected",
                Before = "false",
                After = Game1.player.mailReceived.Contains(goal.GilMailFlag).ToString().ToLowerInvariant()
            }).ToArray()
        });
    }

    private static TrainingExecutionResult AdventureGuildRewardBlocked(TrainingExecutionRequest request, params string[] reasons)
    {
        var result = BlockedWithPrimitive(request, "claim_adventure_guild_reward",
            AdventureGuildRewardRequestedEffect(request),
            "adventure_guild_reward_batch=" + request.AdventureGuildRewardBatchFingerprint + ";status=not_started_or_incomplete",
            reasons.Distinct(StringComparer.Ordinal).ToArray());
        result.AdventureGuildRewardBatchFingerprint = request.AdventureGuildRewardBatchFingerprint;
        return result;
    }

    private static string AdventureGuildRewardRequestedEffect(TrainingExecutionRequest request) =>
        "batch_fingerprint=" + request.AdventureGuildRewardBatchFingerprint +
        ";Gil_goal_flags+=" + request.AdventureGuildRewardPendingGoalCount +
        ";inventory_reward_items+=" + request.AdventureGuildRewardItemCount +
        ";native_reward_mail_and_flags=true";

    private static bool TryParseAdventureGuildRewardGoals(string json, out AdventureGuildRewardGoalRef[] goals)
    {
        try
        {
            goals = JsonSerializer.Deserialize<AdventureGuildRewardGoalRef[]>(json) ?? Array.Empty<AdventureGuildRewardGoalRef>();
            return goals.All(goal => !string.IsNullOrWhiteSpace(goal.GoalId) && goal.Complete && !goal.Collected &&
                goal.RequiredKills >= 0 && goal.CurrentKills >= goal.RequiredKills &&
                goal.RewardItemStack > 0 && goal.RewardItemSpecialVariable >= 0);
        }
        catch (JsonException)
        {
            goals = Array.Empty<AdventureGuildRewardGoalRef>();
            return false;
        }
    }

    private static AdventureGuildRewardGoalRef[] ReadLiveAdventureGuildRewardGoals(Farmer player)
    {
        var rows = new List<AdventureGuildRewardGoalRef>();
        foreach (var pair in DataLoader.MonsterSlayerQuests(Game1.content))
        {
            var data = pair.Value;
            if (AdventureGuild.HasCollectedReward(player, pair.Key) || !AdventureGuild.IsComplete(data)) continue;
            Item? reward = null;
            if (!string.IsNullOrWhiteSpace(data.RewardItemId))
            {
                reward = ItemRegistry.Create(data.RewardItemId);
                reward.SpecialVariable = rows.Count;
                if (reward is StardewObject obj) obj.specialItem = true;
            }
            var targets = data.Targets?.ToArray() ?? Array.Empty<string>();
            rows.Add(new AdventureGuildRewardGoalRef
            {
                GoalId = pair.Key,
                DisplayName = data.DisplayName ?? string.Empty,
                Targets = targets,
                RequiredKills = data.Count,
                CurrentKills = targets.Sum(player.stats.getMonstersKilled),
                Complete = true,
                Collected = false,
                GilMailFlag = "Gil_" + pair.Key,
                RewardItemId = reward?.QualifiedItemId ?? data.RewardItemId ?? string.Empty,
                RewardItemRuntimeType = reward?.GetType().FullName ?? string.Empty,
                RewardItemStack = reward?.Stack ?? 0,
                RewardItemQuality = reward?.Quality ?? 0,
                RewardItemSpecialVariable = reward?.SpecialVariable ?? -1,
                RewardItemSpecialItem = reward is StardewObject { specialItem: true },
                RewardDialogue = data.RewardDialogue ?? string.Empty,
                RewardDialogueFlag = data.RewardDialogueFlag ?? string.Empty,
                RewardDialogueShouldShow = !string.IsNullOrWhiteSpace(data.RewardDialogue) &&
                    (string.IsNullOrWhiteSpace(data.RewardDialogueFlag) || !player.mailReceived.Contains(data.RewardDialogueFlag)),
                RewardMail = data.RewardMail ?? string.Empty,
                RewardMailAll = data.RewardMailAll ?? string.Empty,
                RewardFlag = data.RewardFlag ?? string.Empty,
                RewardFlagAll = data.RewardFlagAll ?? string.Empty
            });
        }
        return rows.ToArray();
    }

    private static bool AdventureGuildRewardGoalsEqual(AdventureGuildRewardGoalRef[] left, AdventureGuildRewardGoalRef[] right) =>
        JsonSerializer.Serialize(left) == JsonSerializer.Serialize(right);

    private static bool AdventureGuildRewardBatchFits(Farmer player, AdventureGuildRewardGoalRef[] goals)
    {
        var inventory = player.Items.Select(CloneAdventureGuildRewardItem).ToList();
        foreach (var goal in goals)
        {
            if (string.IsNullOrWhiteSpace(goal.RewardItemId)) return false;
            var item = ItemRegistry.Create(goal.RewardItemId);
            item.Stack = goal.RewardItemStack;
            item.SpecialVariable = goal.RewardItemSpecialVariable;
            if (item is StardewObject obj) obj.specialItem = goal.RewardItemSpecialItem;
            if (Utility.addItemToThisInventoryList(item, inventory, player.MaxItems) is not null) return false;
        }
        return true;
    }

    private static Item? CloneAdventureGuildRewardItem(Item? item)
    {
        if (item is null) return null;
        var clone = item.getOne();
        clone.Stack = item.Stack;
        return clone;
    }

    private static bool AdventureGuildRewardEndpointMatches(AdventureGuild? guild, Point action, Point stand, int tileIndex)
    {
        if (guild is null || !AreAdjacent(action, stand) || !IsTileOnMap(guild, stand) ||
            !IsTileWalkable(guild, stand) || IsTileOccupiedByCharacter(guild, stand)) return false;
        var buildings = guild.Map?.GetLayer("Buildings");
        if (buildings is null || action.X < 0 || action.Y < 0 ||
            action.X >= buildings.LayerWidth || action.Y >= buildings.LayerHeight) return false;
        var sourceIndex = buildings.Tiles[action.X, action.Y]?.TileIndex ?? -1;
        var animatedIndex = guild.getTileIndexAt(action.X, action.Y, "Buildings");
        return tileIndex is 1291 or 1292 or 1355 or 1356 or 1357 or 1358 &&
            sourceIndex is 1291 or 1292 or 1355 or 1356 or 1357 or 1358 &&
            animatedIndex is 1291 or 1292 or 1355 or 1356 or 1357 or 1358;
    }

    private static bool AdventureGuildRewardNativeSideEffectsPresent(AdventureGuildRewardGoalRef[] goals)
    {
        var farmers = Game1.getAllFarmers().ToArray();
        foreach (var goal in goals)
        {
            if (!string.IsNullOrWhiteSpace(goal.RewardMail) &&
                !Game1.player.mailForTomorrow.Contains(goal.RewardMail)) return false;
            if (!string.IsNullOrWhiteSpace(goal.RewardFlag) &&
                !Game1.player.mailReceived.Contains(goal.RewardFlag)) return false;
            if (!string.IsNullOrWhiteSpace(goal.RewardMailAll) &&
                farmers.Any(farmer => !farmer.mailForTomorrow.Contains(goal.RewardMailAll))) return false;
            if (!string.IsNullOrWhiteSpace(goal.RewardFlagAll) &&
                farmers.Any(farmer => !farmer.mailReceived.Contains(goal.RewardFlagAll))) return false;
        }
        return true;
    }

    private static int CountAdventureGuildReward(string qualifiedItemId) =>
        Game1.player.Items.Where(item => item?.QualifiedItemId == qualifiedItemId).Sum(item => item!.Stack);

    private sealed class ActiveAdventureGuildReward : INativeObjectInteractionMovement
    {
        public ActiveAdventureGuildReward(PendingExecution pending, AdventureGuild location, Point target, Point stand,
            List<Point> path, int maxMovementTiles, AdventureGuildRewardGoalRef[] goals,
            Dictionary<string, int> itemCountsBefore)
        {
            Pending = pending;
            Location = location;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            Goals = goals;
            ItemCountsBefore = itemCountsBefore;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public AdventureGuild Location { get; }
        GameLocation INativeObjectInteractionMovement.Location => Location;
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public AdventureGuildRewardGoalRef[] Goals { get; }
        public Dictionary<string, int> ItemCountsBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 3600;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool NativeHandled { get; set; }
        public int DialogueClicks { get; set; }
        public int CollectedItems { get; set; }
        public AdventureGuildRewardStage Stage { get; set; }
    }

    private enum AdventureGuildRewardStage { Move, DialogueOrMenu, Transfer, Verify }
}
