using System.Text.Json;
using Microsoft.Xna.Framework;
using StardewAI.Contracts.State;
using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Constants;
using StardewValley.Menus;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private const string MasteryClaimRuntimeNativeContract =
        "Forest.MasteryRoom(all_five_base_skills_10)->MasteryCave;MasteryCave_skill_action->MasteryTrackerMenu(skill)->mainButton->claimReward(recipes,direct_inventory_else_debris,mastery_stat,masteryLevelsSpent,combat_trinket_slot,all_plaque_finale)";

    private void StartMasteryClaim(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (!request.MasterySkillId.HasValue || request.MasterySkillId is < 0 or > 4 ||
            request.MasteryProjectionFingerprint.Length != 64 || request.MasteryOptionFingerprint.Length != 64 ||
            !request.MasteryExperienceBefore.HasValue || !request.MasteryLevelBefore.HasValue ||
            !request.MasteryLevelsSpentBefore.HasValue || !request.MasterySkillStatBefore.HasValue ||
            !request.MasteryGrantsTrinketSlot.HasValue || !request.MasteryTrinketSlotsBefore.HasValue ||
            request.NativeContract != MasteryClaimRuntimeNativeContract)
            reasons.Add("mastery_claim_complete_typed_request_required");
        var recipesParsed = TryParseMasteryRecipeRewards(request.MasteryRecipeRewardsJson, out var expectedRecipes);
        var directRewardsParsed = TryParseMasteryDirectRewards(request.MasteryDirectRewardsJson, out var expectedDirectRewards);
        if (!recipesParsed || !directRewardsParsed)
            reasons.Add("mastery_claim_reward_projection_invalid");
        var live = ReadLiveMasteryClaimProjection();
        var option = live?.ClaimableOptions.FirstOrDefault(row => row.SkillId == request.MasterySkillId);
        if (live is null)
            reasons.Add("mastery_claim_live_projection_unavailable");
        else if (live.ProjectionFingerprint != request.MasteryProjectionFingerprint)
            reasons.Add("mastery_claim_projection_fingerprint_drifted");
        if (option is null)
            reasons.Add("mastery_claim_selected_option_unavailable");
        else if (option.OptionFingerprint != request.MasteryOptionFingerprint)
            reasons.Add("mastery_claim_option_fingerprint_drifted");
        if (live is not null && option is not null &&
            !MasteryClaimRequestMatches(request, live, option, expectedRecipes, expectedDirectRewards))
            reasons.Add("mastery_claim_typed_state_drifted");

        var location = Game1.currentLocation;
        var target = new Point(request.TargetTileX ?? -1, request.TargetTileY ?? -1);
        var stand = new Point(request.StandTileX ?? -1, request.StandTileY ?? -1);
        if (location is null || !MasteryClaimEndpointMatches(location, target, stand, request.MasteryActionRaw, request.MasterySkillId ?? -1))
            reasons.Add("mastery_claim_native_endpoint_drifted");
        if (Game1.activeClickableMenu is not null || Game1.dialogueUp)
            reasons.Add("mastery_claim_menu_conflict");
        if (reasons.Count > 0 || live is null || option is null || location is null)
        {
            pending.Completion.SetResult(MasteryClaimBlocked(request, reasons.ToArray()));
            return;
        }

        var maxMovementTiles = Math.Clamp(request.MaxMovementTiles ?? 512, 1, 512);
        var path = TryBuildTilePath(location, Game1.player.TilePoint, stand, maxMovementTiles, out var pathReason,
            avoidSoftObstacles: true, allowRemovableObstacles: false);
        if (path is null)
        {
            pending.Completion.SetResult(MasteryClaimBlocked(request, "mastery_claim_path_unavailable:" + pathReason));
            return;
        }
        activeMasteryClaim = new ActiveMasteryClaim(
            pending, location, target, stand, path, maxMovementTiles,
            MasteryStatValues(), expectedRecipes, expectedDirectRewards,
            CountMasteryDirectRewards(location, expectedDirectRewards));
    }

    private void TickMasteryClaimSafely()
    {
        var active = activeMasteryClaim;
        if (active is null) return;
        try
        {
            TickMasteryClaim(active);
        }
        catch (Exception ex)
        {
            Monitor.Log($"Mastery claim execution failed and was blocked: {ex}", StardewModdingAPI.LogLevel.Error);
            CompleteMasteryClaim(active, false, "mastery_claim_executor_exception:" + ex.GetType().Name);
        }
    }

    private void TickMasteryClaim(ActiveMasteryClaim active)
    {
        if (active.Stage != MasteryClaimRuntimeStage.Move) active.ElapsedTicks++;
        if (active.ElapsedTicks > active.MaxTicks)
        {
            CompleteMasteryClaim(active, false, "mastery_claim_timeout");
            return;
        }
        if (active.Stage == MasteryClaimRuntimeStage.Move)
        {
            var movement = AdvanceNativeObjectInteractionMovement(active, "mastery_claim", out var failure);
            if (movement == NativeObjectMovementStatus.Failed)
            {
                CompleteMasteryClaim(active, false, failure);
                return;
            }
            if (movement == NativeObjectMovementStatus.Moving) return;
            var request = active.Pending.Request;
            var live = ReadLiveMasteryClaimProjection();
            var option = live?.ClaimableOptions.FirstOrDefault(row => row.SkillId == request.MasterySkillId);
            if (live is null || option is null || live.ProjectionFingerprint != request.MasteryProjectionFingerprint ||
                option.OptionFingerprint != request.MasteryOptionFingerprint ||
                !MasteryClaimEndpointMatches(active.Location, active.Target, active.Stand, request.MasteryActionRaw, request.MasterySkillId ?? -1))
            {
                CompleteMasteryClaim(active, false, "mastery_claim_state_drifted_while_moving");
                return;
            }
            Game1.player.faceDirection(DirectionTo(Game1.player.TilePoint, active.Target));
            active.NativeHandled = active.Location.checkAction(
                new TileLocation(active.Target.X, active.Target.Y),
                new TileRectangle(Game1.viewport.X, Game1.viewport.Y, Game1.viewport.Width, Game1.viewport.Height),
                Game1.player);
            if (Game1.activeClickableMenu is not MasteryTrackerMenu menu || menu.mainButton is null)
            {
                CompleteMasteryClaim(active, false, "mastery_claim_native_menu_or_claim_button_missing");
                return;
            }
            active.Menu = menu;
            menu.receiveLeftClick(menu.mainButton.bounds.Center.X, menu.mainButton.bounds.Center.Y, playSound: true);
            if (Game1.player.stats.Get(StatKeys.Mastery(request.MasterySkillId!.Value)) !=
                (uint)(request.MasterySkillStatBefore!.Value + 1))
            {
                CompleteMasteryClaim(active, false, "mastery_claim_native_button_did_not_apply_claim");
                return;
            }
            active.Stage = MasteryClaimRuntimeStage.WaitForSettlement;
            return;
        }

        if (active.Menu is not null && ReferenceEquals(Game1.activeClickableMenu, active.Menu)) return;
        var verified = MasteryClaimReceipt(active);
        CompleteMasteryClaim(active, verified,
            verified ? Array.Empty<string>() : new[] { "mastery_claim_native_receipt_mismatch" });
    }

    private void CompleteMasteryClaim(ActiveMasteryClaim active, bool verified, params string[] reasons)
    {
        StopAllMovement();
        if (active.Menu is not null && ReferenceEquals(Game1.activeClickableMenu, active.Menu) && active.Menu.readyToClose())
            Game1.exitActiveMenu();
        activeMasteryClaim = null;
        var request = active.Pending.Request;
        var skillId = request.MasterySkillId ?? -1;
        var statsAfter = MasteryStatValues();
        var spentAfter = checked((int)Game1.stats.Get("masteryLevelsSpent"));
        var trinketAfter = checked((int)Game1.player.stats.Get("trinketSlots"));
        var directAfter = CountMasteryDirectRewards(active.Location, active.DirectRewards);
        var allComplete = statsAfter.All(value => value != 0);
        var verification = verified
            ? new[]
            {
                "shared_BFS_reached_exact_MasteryCave_skill_plaque",
                "native_MasteryCave_checkAction_opened_MasteryTrackerMenu",
                "native_mainButton_claimed_exact_selected_skill_once",
                "masteryLevelsSpent_incremented_once_and_other_skill_stats_unchanged",
                "all_exact_recipe_and_direct_inventory_else_debris_rewards_observed",
                skillId == 4 ? "combat_trinket_slot_native_effect_observed" : "trinket_slot_unchanged",
                "final_plaque_completion_state_observed"
            }
            : reasons.Length == 0 ? new[] { "mastery_claim_post_state_mismatch" } : reasons;
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
            TrainingImpactScope = "strategy_value_and_executor_calibration",
            PrimitiveKind = "claim_mastery",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verification,
            RequestedEffect = MasteryClaimRequestedEffect(request),
            ObservedEffect = "skill=" + skillId + ";skill_stats=" + string.Join(",", statsAfter) +
                ";spent=" + spentAfter + ";trinket_slots=" + trinketAfter +
                ";direct_reward_delta=" + (directAfter - active.DirectRewardTotalBefore) +
                ";all_complete=" + allComplete.ToString().ToLowerInvariant() +
                ";native_handled=" + active.NativeHandled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : verification,
            MasterySkillId = skillId,
            MasteryLevelsSpentAfter = spentAfter,
            MasterySkillStatAfter = skillId is >= 0 and <= 4 ? statsAfter[skillId] : null,
            MasteryTrinketSlotsAfter = trinketAfter,
            MasteryAllPlaquesCompletedAfter = allComplete,
            MasteryDirectRewardTotalDelta = directAfter - active.DirectRewardTotalBefore,
            ChangedFacts = new[]
            {
                new SimulatedFactChange { Path = "player.mastery_claim.skills[" + skillId + "].mastery_stat_value", Before = request.MasterySkillStatBefore?.ToString() ?? "unknown", After = skillId is >= 0 and <= 4 ? statsAfter[skillId].ToString() : "unknown" },
                new SimulatedFactChange { Path = "player.mastery_claim.mastery_levels_spent", Before = request.MasteryLevelsSpentBefore?.ToString() ?? "unknown", After = spentAfter.ToString() },
                new SimulatedFactChange { Path = "player.mastery_claim.trinket_slots", Before = request.MasteryTrinketSlotsBefore?.ToString() ?? "unknown", After = trinketAfter.ToString() },
                new SimulatedFactChange { Path = "player.mastery_claim.all_plaques_completed", Before = active.SkillStatsBefore.All(value => value != 0).ToString().ToLowerInvariant(), After = allComplete.ToString().ToLowerInvariant() }
            }
        });
    }

    private static bool MasteryClaimReceipt(ActiveMasteryClaim active)
    {
        var request = active.Pending.Request;
        var skillId = request.MasterySkillId!.Value;
        var after = MasteryStatValues();
        var expected = active.SkillStatsBefore.ToArray();
        expected[skillId]++;
        var recipesPresent = active.RecipeRewards.All(reward => Game1.player.craftingRecipes.ContainsKey(reward.RecipeName));
        var directAfter = CountMasteryDirectRewards(active.Location, active.DirectRewards);
        var directExpectedDelta = active.DirectRewards.Sum(reward => reward.Stack);
        var trinketAfter = checked((int)Game1.player.stats.Get("trinketSlots"));
        var expectedTrinket = request.MasteryGrantsTrinketSlot == true ? 1 : request.MasteryTrinketSlotsBefore;
        return after.SequenceEqual(expected) &&
            checked((int)Game1.stats.Get("masteryLevelsSpent")) == request.MasteryLevelsSpentBefore + 1 &&
            checked((int)Game1.stats.Get("MasteryExp")) == request.MasteryExperienceBefore &&
            MasteryTrackerMenu.getCurrentMasteryLevel() == request.MasteryLevelBefore &&
            trinketAfter == expectedTrinket && recipesPresent &&
            directAfter == active.DirectRewardTotalBefore + directExpectedDelta;
    }

    private static bool MasteryClaimRequestMatches(
        TrainingExecutionRequest request,
        MasteryClaimProjectionRef live,
        MasteryClaimOptionRef option,
        MasteryClaimRecipeRewardRef[] expectedRecipes,
        MasteryClaimDirectRewardRef[] expectedDirectRewards)
    {
        var statsCsv = string.Join(",", live.Skills.OrderBy(row => row.SkillId).Select(row => row.MasteryStatValue));
        return live.ServiceStatus == "ready" && live.CurrentLocationMatches && live.MenuClear &&
            live.AllBaseSkillsLevelTen && live.UnspentMasteryLevels > 0 && !option.Claimed && option.Claimable &&
            option.SkillKey == request.MasterySkillKey && live.MasteryExperience == request.MasteryExperienceBefore &&
            live.CurrentMasteryLevel == request.MasteryLevelBefore && live.MasteryLevelsSpent == request.MasteryLevelsSpentBefore &&
            option.MasteryStatValue == request.MasterySkillStatBefore && statsCsv == request.MasteryAllSkillStatsBeforeCsv &&
            live.TrinketSlots == request.MasteryTrinketSlotsBefore && option.GrantsTrinketSlot == request.MasteryGrantsTrinketSlot &&
            JsonSerializer.Serialize(option.RecipeRewards) == JsonSerializer.Serialize(expectedRecipes) &&
            JsonSerializer.Serialize(option.DirectRewards) == JsonSerializer.Serialize(expectedDirectRewards);
    }

    private static bool MasteryClaimEndpointMatches(
        GameLocation location,
        Point action,
        Point stand,
        string actionRaw,
        int skillId) =>
        string.Equals(location.NameOrUniqueName, "MasteryCave", StringComparison.OrdinalIgnoreCase) &&
        AreAdjacent(action, stand) && IsTileOnMap(location, stand) && IsTileWalkable(location, stand) &&
        !IsTileOccupiedByCharacter(location, stand) && actionRaw == MasteryActionToken(skillId) &&
        location.doesTileHaveProperty(action.X, action.Y, "Action", "Buildings") == actionRaw;

    private static bool TryParseMasteryRecipeRewards(string json, out MasteryClaimRecipeRewardRef[] rewards)
    {
        try
        {
            rewards = JsonSerializer.Deserialize<MasteryClaimRecipeRewardRef[]>(json) ?? Array.Empty<MasteryClaimRecipeRewardRef>();
            return rewards.All(reward => !string.IsNullOrWhiteSpace(reward.RecipeName));
        }
        catch (JsonException)
        {
            rewards = Array.Empty<MasteryClaimRecipeRewardRef>();
            return false;
        }
    }

    private static bool TryParseMasteryDirectRewards(string json, out MasteryClaimDirectRewardRef[] rewards)
    {
        try
        {
            rewards = JsonSerializer.Deserialize<MasteryClaimDirectRewardRef[]>(json) ?? Array.Empty<MasteryClaimDirectRewardRef>();
            return rewards.All(reward => reward.Stack > 0 && !string.IsNullOrWhiteSpace(reward.QualifiedItemId));
        }
        catch (JsonException)
        {
            rewards = Array.Empty<MasteryClaimDirectRewardRef>();
            return false;
        }
    }

    private static MasteryClaimProjectionRef? ReadLiveMasteryClaimProjection()
    {
        var player = Game1.player;
        if (player is null) return null;
        var skillLevels = new[] { player.farmingLevel.Value, player.fishingLevel.Value, player.foragingLevel.Value, player.miningLevel.Value, player.combatLevel.Value };
        var allSkillsLevelTen = skillLevels.Sum(level => level / 10) >= 5;
        var masteryExperience = checked((int)Game1.stats.Get("MasteryExp"));
        var currentLevel = MasteryTrackerMenu.getCurrentMasteryLevel();
        var spent = checked((int)Game1.stats.Get("masteryLevelsSpent"));
        var unspent = Math.Max(0, currentLevel - spent);
        var options = Enumerable.Range(0, 5).Select(skillId => RuntimeMasteryOption(skillId, skillLevels[skillId], unspent, allSkillsLevelTen)).ToArray();
        var claimable = options.Where(option => option.Claimable).ToArray();
        var currentMatches = string.Equals(Game1.currentLocation?.NameOrUniqueName, "MasteryCave", StringComparison.OrdinalIgnoreCase);
        var menuClear = Game1.activeClickableMenu is null && !Game1.dialogueUp;
        var blocked = !allSkillsLevelTen || unspent <= 0 || claimable.Length == 0 || claimable.Any(option => option.ActionTile is null) || !menuClear;
        var projection = new MasteryClaimProjectionRef
        {
            ProjectionStatus = "complete_locked_base_1.6.15",
            NativeContract = MasteryClaimRuntimeNativeContract,
            CurrentLocationMatches = currentMatches,
            MenuClear = menuClear,
            AllBaseSkillsLevelTen = allSkillsLevelTen,
            MasteryExperience = masteryExperience,
            CurrentMasteryLevel = currentLevel,
            MasteryLevelsSpent = spent,
            UnspentMasteryLevels = unspent,
            AllPlaquesCompleted = options.All(option => option.Claimed),
            TrinketSlots = checked((int)player.stats.Get("trinketSlots")),
            Skills = options,
            ClaimableOptions = claimable,
            GameId = Game1.uniqueIDForThisGame,
            PlayerId = player.UniqueMultiplayerID,
            ServiceStatus = blocked ? "blocked" : currentMatches ? "ready" : "route_required"
        };
        projection.ProjectionFingerprint = MasteryClaimIdentity.ComputeProjectionFingerprint(projection);
        return projection;
    }

    private static MasteryClaimOptionRef RuntimeMasteryOption(int skillId, int skillLevel, int unspent, bool allSkillsLevelTen)
    {
        var masteryStatKey = StatKeys.Mastery(skillId);
        var stat = checked((int)Game1.player.stats.Get(masteryStatKey));
        var cave = Game1.getLocationFromName("MasteryCave");
        var direct = MasteryDirectRewardIds(skillId).Select(id =>
        {
            var item = ItemRegistry.Create(id);
            return new MasteryClaimDirectRewardRef
            {
                QualifiedItemId = item.QualifiedItemId,
                ItemId = item.ItemId,
                DisplayName = item.DisplayName,
                Stack = item.Stack,
                RuntimeType = item.GetType().FullName ?? string.Empty,
                InventoryCountBefore = Game1.player.Items.CountId(item.QualifiedItemId),
                MasteryCaveDebrisCountBefore = CountMasteryRewardDebris(cave, item.QualifiedItemId)
            };
        }).ToArray();
        var recipes = MasteryRecipeRewardNames(skillId).Select(name => new MasteryClaimRecipeRewardRef
        {
            RecipeName = name,
            KnownBefore = Game1.player.craftingRecipes.ContainsKey(name)
        }).ToArray();
        var option = new MasteryClaimOptionRef
        {
            SkillId = skillId,
            SkillKey = MasterySkillKey(skillId),
            SkillLevel = skillLevel,
            MasteryStatKey = masteryStatKey,
            MasteryStatValue = stat,
            Claimed = stat != 0,
            Claimable = allSkillsLevelTen && unspent > 0 && stat == 0,
            ActionTile = RuntimeMasteryActionTile(MasteryActionToken(skillId)),
            DirectRewards = direct,
            RecipeRewards = recipes,
            GrantsTrinketSlot = skillId == 4
        };
        option.OptionFingerprint = MasteryClaimIdentity.ComputeOptionFingerprint(option);
        return option;
    }

    private static MasteryClaimActionTileRef? RuntimeMasteryActionTile(string token)
    {
        var cave = Game1.getLocationFromName("MasteryCave");
        var layer = cave?.Map?.GetLayer("Buildings");
        if (cave is null || layer is null) return null;
        for (var y = 0; y < layer.LayerHeight; y++)
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            var action = cave.doesTileHaveProperty(x, y, "Action", "Buildings");
            if (action != token) continue;
            return new MasteryClaimActionTileRef { LocationId = cave.NameOrUniqueName, TileX = x, TileY = y, ActionRaw = action };
        }
        return null;
    }

    private static string MasterySkillKey(int skillId) => skillId switch
    {
        0 => "farming", 1 => "fishing", 2 => "foraging", 3 => "mining", 4 => "combat", _ => "unknown"
    };

    private static string MasteryActionToken(int skillId) => "MasteryCave_" + (skillId switch
    {
        0 => "Farming", 1 => "Fishing", 2 => "Foraging", 3 => "Mining", 4 => "Combat", _ => "Unknown"
    });

    private static string[] MasteryDirectRewardIds(int skillId) => skillId switch
    {
        0 => new[] { "(W)66" }, 1 => new[] { "(T)AdvancedIridiumRod" }, _ => Array.Empty<string>()
    };

    private static string[] MasteryRecipeRewardNames(int skillId) => skillId switch
    {
        0 => new[] { "Statue Of Blessings" },
        1 => new[] { "Challenge Bait" },
        2 => new[] { "Mystic Tree Seed", "Treasure Totem" },
        3 => new[] { "Statue Of The Dwarf King", "Heavy Furnace" },
        4 => new[] { "Anvil", "Mini-Forge" },
        _ => Array.Empty<string>()
    };

    private static int[] MasteryStatValues() => Enumerable.Range(0, 5)
        .Select(skillId => checked((int)Game1.player.stats.Get(StatKeys.Mastery(skillId)))).ToArray();

    private static int CountMasteryRewardDebris(GameLocation? location, string qualifiedItemId) => location?.debris
        .Where(debris => string.Equals(DebrisQualifiedItemId(debris), qualifiedItemId, StringComparison.Ordinal))
        .Sum(debris => Math.Max(1, debris.item?.Stack ?? debris.Chunks.Count)) ?? 0;

    private static int CountMasteryDirectRewards(GameLocation location, MasteryClaimDirectRewardRef[] rewards) =>
        rewards.Sum(reward => Game1.player.Items.CountId(reward.QualifiedItemId) + CountMasteryRewardDebris(location, reward.QualifiedItemId));

    private static TrainingExecutionResult MasteryClaimBlocked(TrainingExecutionRequest request, params string[] reasons)
    {
        var result = BlockedWithPrimitive(request, "claim_mastery", MasteryClaimRequestedEffect(request),
            "skill=" + request.MasterySkillId + ";status=not_started_or_incomplete", reasons.Distinct(StringComparer.Ordinal).ToArray());
        result.MasterySkillId = request.MasterySkillId;
        return result;
    }

    private static string MasteryClaimRequestedEffect(TrainingExecutionRequest request) =>
        "mastery_" + request.MasterySkillId + "+=1;masteryLevelsSpent+=1;recipes=" + request.MasteryRecipeRewardsJson +
        ";direct_rewards=" + request.MasteryDirectRewardsJson + ";combat_trinket_slot=" + request.MasteryGrantsTrinketSlot;

    private sealed class ActiveMasteryClaim : INativeObjectInteractionMovement
    {
        public ActiveMasteryClaim(PendingExecution pending, GameLocation location, Point target, Point stand,
            List<Point> path, int maxMovementTiles, int[] skillStatsBefore,
            MasteryClaimRecipeRewardRef[] recipeRewards, MasteryClaimDirectRewardRef[] directRewards, int directRewardTotalBefore)
        {
            Pending = pending;
            Location = location;
            Target = target;
            Stand = stand;
            Path = path;
            MaxMovementTiles = maxMovementTiles;
            SkillStatsBefore = skillStatsBefore;
            RecipeRewards = recipeRewards;
            DirectRewards = directRewards;
            DirectRewardTotalBefore = directRewardTotalBefore;
            LastPosition = Game1.player.Position;
            LastObservedTile = Game1.player.TilePoint;
        }

        public PendingExecution Pending { get; }
        public GameLocation Location { get; }
        public Point Target { get; }
        public Point Stand { get; }
        public List<Point> Path { get; }
        public int MaxMovementTiles { get; }
        public int[] SkillStatsBefore { get; }
        public MasteryClaimRecipeRewardRef[] RecipeRewards { get; }
        public MasteryClaimDirectRewardRef[] DirectRewards { get; }
        public int DirectRewardTotalBefore { get; }
        public string StartedAt { get; } = DateTimeOffset.UtcNow.ToString("O");
        public int MaxTicks { get; } = 1200;
        public int ElapsedTicks { get; set; }
        public int PathIndex { get; set; }
        public int StuckTicks { get; set; }
        public int MovementTiles { get; set; }
        public Vector2 LastPosition { get; set; }
        public Point LastObservedTile { get; set; }
        public bool NativeHandled { get; set; }
        public MasteryTrackerMenu? Menu { get; set; }
        public MasteryClaimRuntimeStage Stage { get; set; }
    }

    private enum MasteryClaimRuntimeStage { Move, WaitForSettlement }
}
