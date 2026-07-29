using StardewAI.Contracts.Training;
using StardewValley;
using StardewValley.Locations;
using StardewValley.Monsters;

namespace StardewAI.RuntimeTestHarness;

public sealed partial class ModEntry
{
    private TrainingExecutionResult ExecuteSetupQuestMonsterDropFixture(
        TrainingExecutionRequest request)
    {
        var reasons = ValidateExecutionRequest(request);
        if (reasons.Count > 0)
        {
            return Blocked(request, reasons.ToArray());
        }
        if (Game1.currentLocation is not MineShaft mine)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_monster_drop_fixture",
                "quest_monster_drop_fixture=ready",
                "location=not_mineshaft",
                "quest_monster_drop_fixture_requires_mineshaft");
        }
        if (string.IsNullOrWhiteSpace(request.QuestId) ||
            string.IsNullOrWhiteSpace(request.QualifiedItemId) ||
            !request.QuestExpectedTargetCount.HasValue ||
            request.QuestExpectedTargetCount.Value <= 0)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_monster_drop_fixture",
                "quest_monster_drop_fixture=ready",
                "quest_or_item=missing",
                "quest_monster_drop_fixture_parameters_required");
        }

        var target = FindMiningCombatFixtureTarget(
            mine,
            requireClearProjectilePath: false,
            requireBombEscape: false);
        if (!target.HasValue)
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_monster_drop_fixture",
                "quest_monster_drop_fixture=ready",
                "target_tile=missing",
                "quest_monster_drop_fixture_no_reachable_tile");
        }

        foreach (var monster in mine.characters.OfType<Monster>().ToArray())
        {
            mine.characters.Remove(monster);
        }
        ClearMiningFixtureArea(mine, target.Value, radius: 4);
        EnsureFixtureInventoryCapacity(Game1.player);

        var item = ItemRegistry.Create(request.QualifiedItemId);
        if (!TryInstallCollectionTaskFixture(
                request,
                item,
                out var taskState,
                out var taskReason))
        {
            return BlockedWithPrimitive(
                request,
                "debug_setup_quest_monster_drop_fixture",
                "quest_monster_drop_fixture=ready",
                "collection_task_fixture=not_installed",
                taskReason);
        }

        var monsterTarget = new GreenSlime(
            target.Value.ToVector2() * Game1.tileSize,
            mine.mineLevel);
        monsterTarget.objectsToDrop.Clear();
        monsterTarget.objectsToDrop.Add(item.QualifiedItemId);
        monsterTarget.Speed = 0;
        monsterTarget.moveTowardPlayerThreshold.Value = -1;
        mine.characters.Add(monsterTarget);

        var weaponSlot = InstallFixtureItem(
            Game1.player,
            new StardewValley.Tools.MeleeWeapon("9"));
        Game1.player.CurrentToolIndex = weaponSlot;
        Game1.player.health = Game1.player.maxHealth;

        var runtimeIdentity = System.Runtime.CompilerServices.RuntimeHelpers
            .GetHashCode(monsterTarget)
            .ToString("X8");
        var verified = mine.characters.Contains(monsterTarget) &&
            taskState.Present &&
            weaponSlot >= 0 &&
            taskState.CurrentCount == 0 &&
            taskState.TargetCount == request.QuestExpectedTargetCount.Value;
        return new TrainingExecutionResult
        {
            RunId = request.RunId,
            QueueId = request.QueueId,
            QueueItemId = request.QueueItemId,
            BeforeStateHash = request.BeforeStateHash,
            OptionId = request.OptionId,
            Status = verified ? "applied" : "blocked",
            FeedbackAvailable = true,
            TargetLocation = mine.NameOrUniqueName,
            TargetTileX = target.Value.X,
            TargetTileY = target.Value.Y,
            StartedAt = DateTimeOffset.UtcNow.ToString("O"),
            CompletedAt = DateTimeOffset.UtcNow.ToString("O"),
            PrimitiveKind = "debug_setup_quest_monster_drop_fixture",
            PrimitiveVerificationStatus = verified ? "verified" : "observed_mismatch",
            PrimitiveVerificationReasons = verified
                ? new[]
                {
                    "isolated_native_resource_collection_quest_present",
                    "deterministic_monster_drop_present",
                    "native_melee_weapon_present"
                }
                : new[] { "quest_monster_drop_fixture_state_mismatch" },
            RequestedEffect = "quest_family=" +
                taskState.Family +
                ";quest_id=" + request.QuestId +
                ";monster_drop=" + item.QualifiedItemId,
            ObservedEffect = "quest_count=" + taskState.CurrentCount +
                "/" + taskState.TargetCount +
                ";target_identity=" + runtimeIdentity +
                ";target_type=" + (monsterTarget.GetType().FullName ?? monsterTarget.GetType().Name) +
                ";target_tile=" + target.Value.X + "," + target.Value.Y +
                ";weapon_slot=" + weaponSlot,
            CombatTargetRuntimeType = monsterTarget.GetType().FullName ??
                monsterTarget.GetType().Name,
            CombatTargetRuntimeIdentity = runtimeIdentity,
            CombatTargetName = monsterTarget.Name,
            QuestCandidateId = "runtime_fixture:" + request.QuestId,
            QuestFamily = taskState.Family,
            QuestId = request.QuestId,
            QuestKey = taskState.QuestKey,
            QuestObjectiveIndex = taskState.ObjectiveIndex,
            QuestProgressBefore = 0,
            QuestProgressAfter = taskState.CurrentCount,
            QuestTargetCount = taskState.TargetCount,
            QuestPresentBefore = false,
            QuestPresentAfter = true,
            QuestCompletedBefore = false,
            QuestCompletedAfter = taskState.Complete,
            BlockReasons = verified
                ? Array.Empty<string>()
                : new[] { "quest_monster_drop_fixture_state_mismatch" },
            ChangedFacts = verified
                ? new[]
                {
                    new SimulatedFactChange
                    {
                        Path = "quests.runtime_fixture:" + request.QuestId,
                        Before = "absent",
                        After = "present"
                    },
                    new SimulatedFactChange
                    {
                        Path = "mining.monsters[" + runtimeIdentity + "].present",
                        Before = "false",
                        After = "true"
                    }
                }
                : Array.Empty<SimulatedFactChange>()
        };
    }
}
