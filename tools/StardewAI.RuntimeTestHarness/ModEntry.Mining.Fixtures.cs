using HarmonyLib;
using Microsoft.Xna.Framework;
using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Text.Json;
using StardewAI.Contracts.Training;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.GameData.Crops;
using StardewValley.Locations;
using StardewValley.Menus;
using StardewValley.Monsters;
using StardewValley.Objects;
using StardewValley.TerrainFeatures;
using StardewValley.Tools;
using XnaRectangle = Microsoft.Xna.Framework.Rectangle;
using TileLocation = xTile.Dimensions.Location;
using TileRectangle = xTile.Dimensions.Rectangle;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry : Mod
{
    private static readonly MethodInfo? AddLevelChestsMethod = typeof(MineShaft)
        .GetMethod("addLevelChests", BindingFlags.Instance | BindingFlags.NonPublic);

    private void StartSetupMiningFloor(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.MineLevel.HasValue || request.MineLevel.Value < 1 || request.MineLevel.Value > 120)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(request, "debug_setup_mining_floor", "current_location.mine_level=requested", "mine_level=" + request.MineLevel, "mining_fixture_level_out_of_range"));
            return;
        }

        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        ResetMineRewardChestFixtureIfEnabled(
            request.MineLevel.Value);
        if (Environment.GetEnvironmentVariable("STARDEWAI_RESET_SKULL_KEY_FIXTURE") == "1")
        {
            Game1.player.hasSkullKey = false;
        }
        var calibrationLoadout = Environment.GetEnvironmentVariable("STARDEWAI_MINING_CALIBRATION_LOADOUT") == "1"
            ? EnsureMiningCalibrationLoadout()
            : MiningCalibrationLoadoutFacts.Disabled;
        activeMineSetup = new ActiveMineSetup(
            pending,
            request.MineLevel.Value,
            "ordinary_mines",
            beforeLocation,
            calibrationLoadout,
            createForcedShaft: false);
        Game1.enterMine(request.MineLevel.Value);
    }

    private void StartSetupSkullCavernShaft(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.MineLevel.HasValue ||
            request.MineLevel.Value <= MineShaft.bottomOfMineLevel ||
            request.MineLevel.Value == MineShaft.quarryMineShaft)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "debug_setup_skull_cavern_shaft",
                "current_location.mine_kind=skull_cavern;native_shaft_tile=174",
                "mine_level=" + request.MineLevel,
                "skull_cavern_fixture_level_out_of_range"));
            return;
        }

        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        ResetMineRewardChestFixtureIfEnabled(
            request.MineLevel.Value);
        var calibrationLoadout = Environment.GetEnvironmentVariable("STARDEWAI_MINING_CALIBRATION_LOADOUT") == "1"
            ? EnsureMiningCalibrationLoadout()
            : MiningCalibrationLoadoutFacts.Disabled;
        activeMineSetup = new ActiveMineSetup(
            pending,
            request.MineLevel.Value,
            "skull_cavern",
            beforeLocation,
            calibrationLoadout,
            createForcedShaft: !string.Equals(
                Environment.GetEnvironmentVariable(
                    "STARDEWAI_SKIP_SKULL_CAVERN_SHAFT_FIXTURE"),
                "1",
                StringComparison.Ordinal));
        Game1.enterMine(request.MineLevel.Value);
    }

    private static void ResetMineRewardChestFixtureIfEnabled(
        int mineLevel)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    "STARDEWAI_RESET_MINE_REWARD_CHEST_FIXTURE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        EnsureFixtureInventoryCapacity(Game1.player);
        Game1.player.chestConsumedMineLevels.Remove(mineLevel);
        if (mineLevel == 100 &&
            Game1.player.mailReceived.Remove("CF_Mines"))
        {
            Game1.player.maxStamina.Value = Math.Max(
                0,
                Game1.player.maxStamina.Value - 34);
        }
    }

    private void StartSetupQuarryMine(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var calibrationLoadout = Environment.GetEnvironmentVariable("STARDEWAI_MINING_CALIBRATION_LOADOUT") == "1"
            ? EnsureMiningCalibrationLoadout()
            : MiningCalibrationLoadoutFacts.Disabled;
        var fixture = Environment.GetEnvironmentVariable("STARDEWAI_QUARRY_RESET_GOLDEN_SCYTHE") == "1"
            ? ResetGoldenScytheFixture()
            : ReadGoldenScytheFixture(resetEnabled: false, claimedBefore: false, countBefore: 0);
        activeQuarrySetup = new ActiveQuarrySetup(
            pending,
            beforeLocation,
            calibrationLoadout,
            fixture);
        Game1.enterMine(MineShaft.quarryMineShaft);
    }

    private static GoldenScytheFixtureFacts ResetGoldenScytheFixture()
    {
        var player = Game1.player;
        EnsureFixtureInventoryCapacity(player);
        var claimedBefore = player.mailReceived.Contains("gotGoldenScythe");
        var countBefore = CountInventoryItems("(W)53");
        for (var index = 0; index < player.MaxItems && index < player.Items.Count; index++)
        {
            if (player.Items[index]?.QualifiedItemId == "(W)53")
            {
                player.Items[index] = null;
            }
        }
        player.mailReceived.Remove("gotGoldenScythe");
        return ReadGoldenScytheFixture(resetEnabled: true, claimedBefore, countBefore);
    }

    private static GoldenScytheFixtureFacts ReadGoldenScytheFixture(bool resetEnabled, bool claimedBefore, int countBefore)
    {
        var player = Game1.player;
        EnsureFixtureInventoryCapacity(player);
        return new GoldenScytheFixtureFacts(
            resetEnabled,
            claimedBefore,
            countBefore,
            player.mailReceived.Contains("gotGoldenScythe"),
            CountInventoryItems("(W)53"),
            player.Items.Take(player.MaxItems).Count(item => item is null));
    }

    private void StartSetupVolcanoFloor(PendingExecution pending)
    {
        var request = pending.Request;
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            pending.Completion.SetResult(Blocked(request, reasons.ToArray()));
            return;
        }

        if (!request.MineLevel.HasValue || request.MineLevel.Value < 0 || request.MineLevel.Value > 9)
        {
            pending.Completion.SetResult(BlockedWithPrimitive(
                request,
                "debug_setup_volcano_floor",
                "current_location.runtime_type=VolcanoDungeon;volcano.level=requested",
                "volcano_level=" + request.MineLevel,
                "volcano_fixture_level_out_of_range"));
            return;
        }

        var beforeLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var calibrationLoadout = Environment.GetEnvironmentVariable("STARDEWAI_VOLCANO_CALIBRATION_LOADOUT") == "1"
            ? EnsureVolcanoCalibrationLoadout()
            : VolcanoCalibrationLoadoutFacts.Disabled;
        activeVolcanoSetup = new ActiveVolcanoSetup(
            pending,
            request.MineLevel.Value,
            beforeLocation,
            calibrationLoadout);
        Game1.warpFarmer(VolcanoDungeon.GetLevelName(request.MineLevel.Value), 0, 1, 2);
    }

    private static VolcanoCalibrationLoadoutFacts EnsureVolcanoCalibrationLoadout()
    {
        var player = Game1.player;
        EnsureFixtureInventoryCapacity(player);
        var selectedSlot = player.CurrentToolIndex;
        EnsureMiningCalibrationLoadout();

        var pickaxe = player.Items.OfType<Pickaxe>()
            .OrderByDescending(tool => tool.UpgradeLevel)
            .ThenByDescending(tool => tool.additionalPower.Value)
            .FirstOrDefault();
        if (pickaxe is null)
        {
            pickaxe = new Pickaxe { UpgradeLevel = 4 };
            InstallFixtureItem(player, pickaxe);
        }

        var wateringCan = player.Items.OfType<WateringCan>()
            .OrderByDescending(tool => tool.UpgradeLevel)
            .FirstOrDefault();
        if (wateringCan is null)
        {
            wateringCan = new WateringCan { UpgradeLevel = 4 };
            InstallFixtureItem(player, wateringCan);
        }
        wateringCan.WaterLeft = wateringCan.waterCanMax;

        var weapon = player.Items.OfType<MeleeWeapon>()
            .Where(item => !item.isScythe())
            .OrderByDescending(item => item.maxDamage.Value)
            .ThenByDescending(item => item.speed.Value)
            .FirstOrDefault();
        var food = player.Items.OfType<StardewValley.Object>()
            .Where(item => item.Edibility > 0 && item.healthRecoveredOnConsumption() > 0)
            .OrderByDescending(item => item.healthRecoveredOnConsumption())
            .FirstOrDefault();

        player.health = player.maxHealth;
        player.Stamina = player.MaxStamina;
        if (selectedSlot >= 0 && selectedSlot < player.Items.Count)
        {
            player.CurrentToolIndex = selectedSlot;
        }

        return new VolcanoCalibrationLoadoutFacts(
            true,
            player.Items.IndexOf(pickaxe),
            pickaxe.QualifiedItemId,
            pickaxe.UpgradeLevel,
            player.Items.IndexOf(wateringCan),
            wateringCan.QualifiedItemId,
            wateringCan.WaterLeft,
            weapon is null ? -1 : player.Items.IndexOf(weapon),
            weapon?.QualifiedItemId ?? string.Empty,
            weapon?.maxDamage.Value ?? 0,
            food is null ? -1 : player.Items.IndexOf(food),
            food?.QualifiedItemId ?? string.Empty,
            food?.Stack ?? 0);
    }

    private static MiningCalibrationLoadoutFacts EnsureMiningCalibrationLoadout()
    {
        var player = Game1.player;
        EnsureFixtureInventoryCapacity(player);
        var selectedSlot = player.CurrentToolIndex;
        var existingBestDamage = player.Items.OfType<MeleeWeapon>()
            .Where(weapon => !weapon.isScythe())
            .Select(weapon => weapon.maxDamage.Value)
            .DefaultIfEmpty(0)
            .Max();
        MeleeWeapon? bestRuntimeWeapon = null;
        for (var itemId = 0; itemId <= 256; itemId++)
        {
            try
            {
                var candidate = new MeleeWeapon(itemId.ToString());
                if (candidate.isScythe() || candidate.maxDamage.Value <= 0 || string.IsNullOrWhiteSpace(candidate.Name))
                {
                    continue;
                }
                if (bestRuntimeWeapon is null ||
                    candidate.maxDamage.Value > bestRuntimeWeapon.maxDamage.Value ||
                    candidate.maxDamage.Value == bestRuntimeWeapon.maxDamage.Value && candidate.speed.Value > bestRuntimeWeapon.speed.Value)
                {
                    bestRuntimeWeapon = candidate;
                }
            }
            catch
            {
            }
        }

        var weaponSlot = -1;
        if (bestRuntimeWeapon is not null && bestRuntimeWeapon.maxDamage.Value > existingBestDamage)
        {
            weaponSlot = InstallFixtureItem(player, bestRuntimeWeapon);
        }

        StardewValley.Object? bestRuntimeFood = null;
        foreach (var itemId in Game1.objectData.Keys)
        {
            try
            {
                var candidate = ItemRegistry.Create<StardewValley.Object>("(O)" + itemId);
                if (candidate.Edibility <= 0 || candidate.healthRecoveredOnConsumption() <= 0)
                {
                    continue;
                }
                if (bestRuntimeFood is null ||
                    candidate.healthRecoveredOnConsumption() > bestRuntimeFood.healthRecoveredOnConsumption())
                {
                    bestRuntimeFood = candidate;
                }
            }
            catch
            {
            }
        }

        var foodSlot = -1;
        if (bestRuntimeFood is not null)
        {
            bestRuntimeFood.Stack = 50;
            foodSlot = InstallFixtureItem(player, bestRuntimeFood);
        }
        player.CurrentToolIndex = selectedSlot;

        return new MiningCalibrationLoadoutFacts(
            true,
            weaponSlot,
            bestRuntimeWeapon?.QualifiedItemId ?? string.Empty,
            bestRuntimeWeapon?.maxDamage.Value ?? existingBestDamage,
            foodSlot,
            bestRuntimeFood?.QualifiedItemId ?? string.Empty,
            bestRuntimeFood?.healthRecoveredOnConsumption() ?? 0,
            bestRuntimeFood?.Stack ?? 0);
    }

    private static MineFishingFixtureFacts EnsureMineFishingFixtureEquipment()
    {
        var player = Game1.player;
        var before = ReadMineFishingFixtureSnapshot(player);
        if (player.MaxItems < 36)
        {
            player.increaseBackpackSize(36 - player.MaxItems);
        }
        while (player.Items.Count < player.MaxItems)
        {
            player.Items.Add(null);
        }

        var rod = player.Items.OfType<FishingRod>().FirstOrDefault(item => item.UpgradeLevel == 4 && item.AttachmentSlotsCount >= 3);
        if (rod is null)
        {
            rod = new FishingRod(4);
            var slot = FirstEmptyInventorySlot(player);
            if (slot < 0)
            {
                for (var index = 0; index < player.Items.Count; index++)
                {
                    if (player.Items[index] is FishingRod)
                    {
                        slot = index;
                        break;
                    }
                }
            }
            if (slot >= 0)
            {
                player.Items[slot] = rod;
            }
        }

        if (rod is null)
        {
            return new MineFishingFixtureFacts(before, ReadMineFishingFixtureSnapshot(player));
        }

        rod.AttachmentSlotsCount = Math.Max(rod.AttachmentSlotsCount, 3);
        while (rod.attach(null) is not null)
        {
        }

        var bait = ItemRegistry.GetObjectTypeDefinition().CreateFlavoredBait(ItemRegistry.Create<StardewValley.Object>("(O)162"));
        bait.Stack = 999;
        rod.attach(bait);
        rod.attach(ItemRegistry.Create<StardewValley.Object>("(O)856"));
        rod.attach(ItemRegistry.Create<StardewValley.Object>("(O)695"));
        player.CurrentToolIndex = player.Items.IndexOf(rod);
        player.Stamina = Math.Max(player.Stamina, 200f);
        return new MineFishingFixtureFacts(before, ReadMineFishingFixtureSnapshot(player));
    }

    private static MineFishingFixtureSnapshot ReadMineFishingFixtureSnapshot(Farmer player)
    {
        var selectedRod = player.CurrentTool as FishingRod;
        var selectedBait = selectedRod?.GetBait();
        var lavaEelInternalName = ItemRegistry.GetData("(O)162")?.InternalName ?? string.Empty;
        var baitInternalName = selectedBait?.Name ?? string.Empty;
        var emptySlots = player.Items.Take(player.MaxItems).Count(item => item is null);
        return new MineFishingFixtureSnapshot(
            player.MaxItems,
            emptySlots,
            player.CurrentToolIndex,
            selectedRod?.QualifiedItemId ?? string.Empty,
            selectedRod?.UpgradeLevel ?? -1,
            selectedRod?.AttachmentSlotsCount ?? 0,
            selectedBait?.preservedParentSheetIndex.Value ?? string.Empty,
            baitInternalName,
            !string.IsNullOrWhiteSpace(lavaEelInternalName) && baitInternalName.Contains(lavaEelInternalName, StringComparison.Ordinal),
            selectedRod?.HasCuriosityLure() == true,
            selectedRod?.GetTackle().Any(item => item?.QualifiedItemId == "(O)695") == true,
            player.Stamina);
    }

    private static int FirstEmptyInventorySlot(Farmer player)
    {
        for (var i = 0; i < player.MaxItems && i < player.Items.Count; i++)
        {
            if (player.Items[i] is null)
            {
                return i;
            }
        }

        return -1;
    }

    private void TickMineFishingSetup()
    {
        if (activeMineFishingSetup is null)
        {
            return;
        }

        var active = activeMineFishingSetup;
        active.ElapsedTicks++;
        var mine = Game1.currentLocation as MineShaft;
        var fishableTileCount = CountFishableTiles(mine);
        var verified = mine is not null &&
            mine.mineLevel == active.MineLevel &&
            mine.getMineArea() == MineShaft.lavaArea &&
            mine.canFishHere() &&
            fishableTileCount > 0;
        if (verified)
        {
            CompleteMineFishingSetup(active, mine!, fishableTileCount, verified: true);
            return;
        }

        if (active.ElapsedTicks >= active.MaxTicks)
        {
            CompleteMineFishingSetup(active, mine, fishableTileCount, verified: false);
        }
    }

    private void TickMineSetup()
    {
        if (activeMineSetup is null)
        {
            return;
        }

        var active = activeMineSetup;
        active.ElapsedTicks++;
        var mine = Game1.currentLocation as MineShaft;
        var loaded = mine is not null &&
            mine.mineLevel == active.MineLevel &&
            mine.map is not null &&
            string.Equals(RuntimeMineKind(mine), active.ExpectedMineKind, StringComparison.Ordinal);
        if (loaded)
        {
            EnsureMineRewardChestFixtureIfEnabled(mine!);
        }
        if (loaded && active.CreateForcedShaft && !active.ShaftCreationIssued)
        {
            var target = FindSkullCavernShaftFixtureTile(mine!);
            if (!target.HasValue)
            {
                active.FailureReason = "skull_cavern_fixture_no_reachable_shaft_tile";
                CompleteMineSetup(active, mine, verified: false);
                return;
            }

            foreach (var monster in mine!.characters.OfType<Monster>().ToArray())
            {
                mine.characters.Remove(monster);
            }
            ClearMiningFixtureArea(mine, Game1.player.TilePoint, radius: 4);
            mine.createLadderDown(target.Value.X, target.Value.Y, forceShaft: true);
            active.ShaftTile = target;
            active.ShaftCreationIssued = true;
        }

        var shaftVerified = !active.CreateForcedShaft ||
            active.ShaftTile.HasValue &&
            mine?.getTileIndexAt(active.ShaftTile.Value.X, active.ShaftTile.Value.Y, "Buildings", "mine") == 174;
        var verified = loaded && shaftVerified;
        if (verified || active.ElapsedTicks >= active.MaxTicks)
        {
            CompleteMineSetup(active, mine, verified);
        }
    }

    private static void EnsureMineRewardChestFixtureIfEnabled(MineShaft mine)
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable(
                    "STARDEWAI_RESET_MINE_REWARD_CHEST_FIXTURE"),
                "1",
                StringComparison.Ordinal) ||
            mine.overlayObjects.Values.Any(value =>
                value is Chest chest && chest.GetType() == typeof(Chest)))
        {
            return;
        }

        AddLevelChestsMethod?.Invoke(mine, null);
    }

    private void TickQuarrySetup()
    {
        if (activeQuarrySetup is null)
        {
            return;
        }

        var active = activeQuarrySetup;
        active.ElapsedTicks++;
        var mine = Game1.currentLocation as MineShaft;
        var loaded = mine is not null &&
            mine.mineLevel == MineShaft.quarryMineShaft &&
            mine.getMineArea() == MineShaft.quarryMineShaft &&
            mine.isSideBranch() &&
            mine.map is not null;
        if (!loaded)
        {
            if (active.ElapsedTicks >= active.MaxTicks)
            {
                CompleteQuarrySetup(active, mine, 0, verified: false, "quarry_fixture_state_mismatch");
            }
            return;
        }

        var altarCount = CountMapActionTiles(mine!, "GoldenScythe");
        var claimed = Game1.player.mailReceived.Contains("gotGoldenScythe");
        var scytheCount = CountInventoryItems("(W)53");
        var emptySlots = Game1.player.Items.Take(Game1.player.MaxItems).Count(item => item is null);
        var verified = altarCount > 0 && !claimed && scytheCount == 0 && emptySlots > 0;
        var failureReason = altarCount <= 0
            ? "quarry_fixture_golden_scythe_altar_missing"
            : claimed
                ? "quarry_fixture_golden_scythe_claim_still_present"
                : scytheCount > 0
                    ? "quarry_fixture_golden_scythe_item_still_present"
                    : emptySlots <= 0
                        ? "quarry_fixture_inventory_full"
                        : string.Empty;
        CompleteQuarrySetup(active, mine, altarCount, verified, failureReason);
    }

    private void CompleteQuarrySetup(
        ActiveQuarrySetup active,
        MineShaft? mine,
        int altarCount,
        bool verified,
        string failureReason)
    {
        activeQuarrySetup = null;
        var request = active.Pending.Request;
        var afterLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        var claimedAfter = Game1.player.mailReceived.Contains("gotGoldenScythe");
        var scytheCountAfter = CountInventoryItems("(W)53");
        var emptySlotsAfter = Game1.player.Items.Take(Game1.player.MaxItems).Count(item => item is null);
        var reasons = verified
            ? new[]
            {
                "native_enter_quarry_mine_completed",
                "quarry_mine_sentinel_verified",
                "quarry_side_branch_verified",
                "golden_scythe_altar_action_present",
                "golden_scythe_unclaimed_fixture_verified",
                "golden_scythe_inventory_slot_available",
                active.CalibrationLoadout.Enabled ? "runtime_data_calibration_loadout_installed" : "calibration_loadout_disabled"
            }
            : new[] { string.IsNullOrWhiteSpace(failureReason) ? "quarry_fixture_state_mismatch" : failureReason };
        var changedFacts = verified
            ? new List<SimulatedFactChange>
            {
                new() { Path = "player.location_id", Before = active.BeforeLocation, After = afterLocation },
                new() { Path = "current_location.mine_level", Before = string.Empty, After = MineShaft.quarryMineShaft.ToString() },
                new() { Path = "current_location.mine_kind", Before = string.Empty, After = "quarry_mine" }
            }
            : new List<SimulatedFactChange>();
        if (verified && active.Fixture.ResetEnabled)
        {
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.mail_received.gotGoldenScythe",
                Before = active.Fixture.ClaimedBefore.ToString().ToLowerInvariant(),
                After = claimedAfter.ToString().ToLowerInvariant()
            });
            changedFacts.Add(new SimulatedFactChange
            {
                Path = "player.inventory.(W)53.count",
                Before = active.Fixture.CountBefore.ToString(),
                After = scytheCountAfter.ToString()
            });
        }

        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = afterLocation,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_quarry_mine",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = reasons,
            RequestedEffect = "current_location.mine_level=" + MineShaft.quarryMineShaft +
                ";mine_kind=quarry_mine;golden_scythe_claimed=false;golden_scythe_count=0;empty_slots>0",
            ObservedEffect = "location=" + afterLocation +
                ";mine_level=" + (mine?.mineLevel.ToString() ?? "unavailable") +
                ";mine_area=" + (mine?.getMineArea().ToString() ?? "unavailable") +
                ";is_side_branch=" + (mine?.isSideBranch().ToString().ToLowerInvariant() ?? "unavailable") +
                ";loaded_map=" + (mine?.map is not null) +
                ";golden_scythe_altar_count=" + altarCount +
                ";gotGoldenScythe=" + claimedAfter.ToString().ToLowerInvariant() +
                ";golden_scythe_count=" + scytheCountAfter +
                ";empty_slots=" + emptySlotsAfter +
                ";fixture=" + active.Fixture.ToAuditString() +
                ";calibration_loadout=" + active.CalibrationLoadout.Enabled.ToString().ToLowerInvariant(),
            BlockReasons = verified ? Array.Empty<string>() : reasons,
            ChangedFacts = changedFacts.ToArray()
        });
    }

    private static int CountMapActionTiles(GameLocation location, string expectedActionType)
    {
        var layer = location.map?.GetLayer("Buildings");
        if (layer is null)
        {
            return 0;
        }

        var count = 0;
        for (var x = 0; x < layer.LayerWidth; x++)
        {
            for (var y = 0; y < layer.LayerHeight; y++)
            {
                var rawAction = location.doesTileHaveProperty(x, y, "Action", "Buildings");
                var actionType = rawAction?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (string.Equals(actionType, expectedActionType, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }
        }
        return count;
    }

    private void TickVolcanoSetup()
    {
        if (activeVolcanoSetup is null)
        {
            return;
        }

        var active = activeVolcanoSetup;
        active.ElapsedTicks++;
        var volcano = Game1.currentLocation as VolcanoDungeon;
        var loaded = volcano is not null &&
            volcano.level.Value == active.Level &&
            volcano.map is not null &&
            volcano.startPosition.HasValue &&
            volcano.endPosition.HasValue;
        if (loaded || active.ElapsedTicks >= active.MaxTicks)
        {
            CompleteVolcanoSetup(active, volcano, loaded);
        }
    }

    private void CompleteVolcanoSetup(ActiveVolcanoSetup active, VolcanoDungeon? volcano, bool verified)
    {
        activeVolcanoSetup = null;
        var request = active.Pending.Request;
        var afterLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = afterLocation,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_volcano_floor",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_generated_volcano_location_loaded",
                    "volcano_level_verified",
                    "volcano_start_and_end_positions_present",
                    active.CalibrationLoadout.Enabled ? "runtime_data_volcano_calibration_loadout_installed" : "calibration_loadout_disabled"
                }
                : new[] { "volcano_fixture_state_mismatch" },
            RequestedEffect = "current_location.runtime_type=VolcanoDungeon;volcano.level=" + active.Level,
            ObservedEffect = "location=" + afterLocation +
                ";runtime_type=" + (Game1.currentLocation?.GetType().FullName ?? "none") +
                ";volcano_level=" + (volcano?.level.Value.ToString() ?? "unavailable") +
                ";loaded_map=" + (volcano?.map is not null) +
                ";start_position=" + (volcano?.startPosition.HasValue == true ? volcano.startPosition.Value.X + "," + volcano.startPosition.Value.Y : "none") +
                ";end_position=" + (volcano?.endPosition.HasValue == true ? volcano.endPosition.Value.X + "," + volcano.endPosition.Value.Y : "none") +
                ";loadout=" + active.CalibrationLoadout.ToAuditString(),
            BlockReasons = verified ? Array.Empty<string>() : new[] { "volcano_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.location_id", Before = active.BeforeLocation, After = afterLocation },
                    new SimulatedFactChange { Path = "volcano.current_level.level", Before = "unavailable", After = active.Level.ToString() }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private void CompleteMineSetup(ActiveMineSetup active, MineShaft? mine, bool verified)
    {
        activeMineSetup = null;
        var request = active.Pending.Request;
        var afterLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = active.CreateForcedShaft ? "debug_setup_skull_cavern_shaft" : "debug_setup_mining_floor",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "native_enter_mine_completed",
                    "mine_level_verified",
                    "mine_kind_verified:" + active.ExpectedMineKind,
                    "loaded_mine_map_present",
                    active.CreateForcedShaft ? "native_forced_skull_cavern_shaft_created" : "shaft_fixture_not_requested",
                    active.CalibrationLoadout.Enabled ? "runtime_data_calibration_loadout_installed" : "calibration_loadout_disabled"
                }
                : new[] { string.IsNullOrWhiteSpace(active.FailureReason) ? "mining_fixture_state_mismatch" : active.FailureReason },
            RequestedEffect = "current_location.mine_level=" + active.MineLevel +
                ";mine_kind=" + active.ExpectedMineKind +
                ";forced_native_shaft=" + active.CreateForcedShaft.ToString().ToLowerInvariant(),
            ObservedEffect = "location=" + afterLocation +
                ";mine_level=" + (mine?.mineLevel.ToString() ?? "unavailable") +
                ";mine_kind=" + (mine is null ? "unavailable" : RuntimeMineKind(mine)) +
                ";loaded_map=" + (mine?.map is not null) +
                ";shaft_tile=" + (active.ShaftTile.HasValue ? active.ShaftTile.Value.X + "," + active.ShaftTile.Value.Y : "none") +
                ";shaft_tile_index=" + (active.ShaftTile.HasValue && mine is not null ? mine.getTileIndexAt(active.ShaftTile.Value.X, active.ShaftTile.Value.Y, "Buildings", "mine") : -1) +
                ";calibration_loadout=" + active.CalibrationLoadout.Enabled.ToString().ToLowerInvariant() +
                ";weapon=" + active.CalibrationLoadout.WeaponQualifiedItemId +
                ";weapon_max_damage=" + active.CalibrationLoadout.WeaponMaxDamage +
                ";food=" + active.CalibrationLoadout.FoodQualifiedItemId +
                ";food_health_recovery=" + active.CalibrationLoadout.FoodHealthRecovery +
                ";food_stack=" + active.CalibrationLoadout.FoodStack,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { string.IsNullOrWhiteSpace(active.FailureReason) ? "mining_fixture_state_mismatch" : active.FailureReason },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.location_id", Before = active.BeforeLocation, After = afterLocation },
                    new SimulatedFactChange { Path = "current_location.mine_level", Before = string.Empty, After = mine!.mineLevel.ToString() },
                    new SimulatedFactChange { Path = "current_location.mine_kind", Before = string.Empty, After = active.ExpectedMineKind }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }

    private static Point? FindSkullCavernShaftFixtureTile(MineShaft mine)
    {
        if (mine.map?.Layers.FirstOrDefault() is not { } layer)
        {
            return null;
        }

        var start = Game1.player.TilePoint;
        return Enumerable.Range(0, layer.LayerWidth)
            .SelectMany(x => Enumerable.Range(0, layer.LayerHeight).Select(y => new Point(x, y)))
            .Where(tile => tile != start)
            .Where(tile => ManhattanDistance(start, tile) >= 2)
            .Where(tile => mine.getTileIndexAt(tile.X, tile.Y, "Buildings", "mine") < 0)
            .Where(tile => mine.isTileClearForMineObjects(new Vector2(tile.X, tile.Y)))
            .Where(tile => BuildCompilerAdjacentPath(mine, tile, null, 64, out _) is not null)
            .OrderBy(tile => ManhattanDistance(start, tile))
            .ThenBy(tile => tile.Y)
            .ThenBy(tile => tile.X)
            .Cast<Point?>()
            .FirstOrDefault();
    }

    private static string RuntimeMineKind(MineShaft mine)
    {
        var area = mine.getMineArea();
        if (area == MineShaft.quarryMineShaft || mine.mineLevel == MineShaft.quarryMineShaft)
        {
            return "quarry_mine";
        }
        if (area == MineShaft.desertArea && mine.mineLevel > MineShaft.bottomOfMineLevel)
        {
            return "skull_cavern";
        }
        return "ordinary_mines";
    }

    private static void ClearMiningFixtureArea(MineShaft mine, Point center, int radius)
    {
        foreach (var pair in mine.objects.Pairs.Where(pair =>
            Math.Abs((int)pair.Key.X - center.X) <= radius &&
            Math.Abs((int)pair.Key.Y - center.Y) <= radius).ToArray())
        {
            mine.objects.Remove(pair.Key);
        }
        foreach (var pair in mine.terrainFeatures.Pairs.Where(pair =>
            Math.Abs((int)pair.Key.X - center.X) <= radius &&
            Math.Abs((int)pair.Key.Y - center.Y) <= radius).ToArray())
        {
            mine.terrainFeatures.Remove(pair.Key);
        }
    }

    private static int CountFishableTiles(MineShaft? mine)
    {
        if (mine?.map?.Layers.FirstOrDefault() is not { } layer)
        {
            return 0;
        }

        var count = 0;
        for (var y = 0; y < layer.LayerHeight; y++)
        {
            for (var x = 0; x < layer.LayerWidth; x++)
            {
                if (mine.isTileFishable(x, y))
                {
                    count++;
                }
            }
        }

        return count;
    }

    private void CompleteMineFishingSetup(ActiveMineFishingSetup active, MineShaft? mine, int fishableTileCount, bool verified)
    {
        activeMineFishingSetup = null;
        var request = active.Pending.Request;
        var afterLocation = Game1.currentLocation?.NameOrUniqueName ?? string.Empty;
        active.Pending.Completion.SetResult(new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            StartedAt = active.StartedAt,
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_mine_fishing_floor",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[] { "native_enter_mine_completed", "mine_area_80_verified", "mine_fishable_tiles_present" }
                : new[] { "mine_fishing_fixture_state_mismatch" },
            RequestedEffect = "current_location.mine_level=" + active.MineLevel + ";mine_area=80;can_fish_here=true",
            ObservedEffect = "location=" + afterLocation + ";mine_level=" + (mine?.mineLevel.ToString() ?? "unavailable") + ";mine_area=" + (mine?.getMineArea().ToString() ?? "unavailable") + ";fishable_tile_count=" + fishableTileCount,
            BlockReasons = verified ? Array.Empty<string>() : new[] { "mine_fishing_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange { Path = "player.location_id", Before = active.BeforeLocation, After = afterLocation },
                    new SimulatedFactChange { Path = "current_location.mine_level", Before = string.Empty, After = mine!.mineLevel.ToString() },
                    new SimulatedFactChange { Path = "current_location.mine_area", Before = string.Empty, After = mine.getMineArea().ToString() },
                    new SimulatedFactChange { Path = "current_location.fishable_tile_count", Before = string.Empty, After = fishableTileCount.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.backpack_max_items", Before = active.PrerequisiteFacts.Before.BackpackMaxItems.ToString(), After = active.PrerequisiteFacts.After.BackpackMaxItems.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.backpack_empty_slots", Before = active.PrerequisiteFacts.Before.BackpackEmptySlots.ToString(), After = active.PrerequisiteFacts.After.BackpackEmptySlots.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_qualified_item_id", Before = active.PrerequisiteFacts.Before.SelectedRodQualifiedItemId, After = active.PrerequisiteFacts.After.SelectedRodQualifiedItemId },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_slot", Before = active.PrerequisiteFacts.Before.SelectedRodSlot.ToString(), After = active.PrerequisiteFacts.After.SelectedRodSlot.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_upgrade_level", Before = active.PrerequisiteFacts.Before.SelectedRodUpgradeLevel.ToString(), After = active.PrerequisiteFacts.After.SelectedRodUpgradeLevel.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.selected_rod_attachment_slots", Before = active.PrerequisiteFacts.Before.SelectedRodAttachmentSlots.ToString(), After = active.PrerequisiteFacts.After.SelectedRodAttachmentSlots.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.specific_bait_target_item_id", Before = active.PrerequisiteFacts.Before.SpecificBaitTargetItemId, After = active.PrerequisiteFacts.After.SpecificBaitTargetItemId },
                    new SimulatedFactChange { Path = "fishing.fixture.bait_internal_name", Before = active.PrerequisiteFacts.Before.BaitInternalName, After = active.PrerequisiteFacts.After.BaitInternalName },
                    new SimulatedFactChange { Path = "fishing.fixture.lava_eel_native_name_condition", Before = active.PrerequisiteFacts.Before.LavaEelNativeNameCondition.ToString(), After = active.PrerequisiteFacts.After.LavaEelNativeNameCondition.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.curiosity_lure_equipped", Before = active.PrerequisiteFacts.Before.CuriosityLureEquipped.ToString(), After = active.PrerequisiteFacts.After.CuriosityLureEquipped.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.cork_bobber_equipped", Before = active.PrerequisiteFacts.Before.CorkBobberEquipped.ToString(), After = active.PrerequisiteFacts.After.CorkBobberEquipped.ToString() },
                    new SimulatedFactChange { Path = "fishing.fixture.stamina", Before = active.PrerequisiteFacts.Before.Stamina.ToString("R"), After = active.PrerequisiteFacts.After.Stamina.ToString("R") }
                }
                : Array.Empty<SimulatedFactChange>()
        });
    }


}
